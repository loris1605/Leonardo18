using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Models.Contabilita;
using Models.Context;

internal static class ContabilitaChecks
{
    public static int Count;
    private static void Check(bool value, string message)
    { if (!value) throw new Exception(message); Count++; }

    private static MovimentoContabile Movimento(decimal importo, int dare, int avere, int? personId = null) => new()
    {
        IdOperazione=Guid.NewGuid(), DataOra=DateTime.Now, DataCompetenza=DateTime.Today,
        Causale="TEST", Descrizione="Operazione isolata", PersonId=personId, Nominativo=personId.HasValue?"Socio storico":null,
        Righe=new()
        {
            new() { NumeroRiga=1, ContoId=dare, Dare=importo },
            new() { NumeroRiga=2, ContoId=avere, Avere=importo }
        }
    };

    private static async Task Rifiuta(AppDbContext db, object entity, string message)
    {
        db.Add(entity);
        try { await db.SaveChangesAsync(); throw new Exception(message); }
        catch(DbUpdateException) { Count++; db.ChangeTracker.Clear(); }
    }

    public static async Task Database()
    {
        using(var sql=new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=localhost;Database=SchemaTest;Integrated Security=true;TrustServerCertificate=true").Options))
        {
            Check(!sql.Database.HasPendingModelChanges(),"Snapshot contabile coerente");
            var accounting=sql.Model.GetEntityTypes().Where(x=>x.ClrType.Namespace=="Models.Contabilita").ToArray();
            Check(accounting.Length==5,"Cinque entità contabili separate");
            foreach(var entity in accounting)
            {
                Check(entity.GetTableName()!.StartsWith("Contabilita"),"Prefisso tabelle contabili");
                Check(entity.GetForeignKeys().All(x=>x.PrincipalEntityType.ClrType.Namespace=="Models.Contabilita"),"Nessuna FK verso tabelle operative");
                Check(entity.GetForeignKeys().All(x=>x.DeleteBehavior==DeleteBehavior.NoAction),"Nessuna cancellazione contabile in cascata");
            }
            var assembly=sql.GetService<IMigrationsAssembly>();
            var last=assembly.Migrations.Single(x=>x.Key.EndsWith("_Start"));
            var migration=assembly.CreateMigration(last.Value,sql.Database.ProviderName!);
            Check(migration.UpOperations.OfType<CreateTableOperation>().Count(x=>x.Name.StartsWith("Contabilita"))==5,"Start crea le cinque tabelle contabili");
            Check(migration.UpOperations.All(x=>x is CreateTableOperation or CreateIndexOperation or InsertDataOperation),"Migrazione solo additiva");
            Check(migration.UpOperations.OfType<CreateTableOperation>().Any(x=>x.Name=="SchedeConto"),"Start include anche lo schema operativo");
            var script=sql.GetService<IMigrator>().GenerateScript(Migration.InitialDatabase,last.Key);
            Check(script.Contains("decimal(18,2)")&&script.Contains("CK_ContabilitaRiga_Importi"),"Precisione e vincoli presenti nello script SQL Server");
            Check(!script.Contains("DROP TABLE")&&!script.Contains("ALTER TABLE [SchedeConto]"),"Script SQL conserva le tabelle operative");
        }

        await using var connection=new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        var options=new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using var db=new AppDbContext(options); await db.Database.EnsureCreatedAsync();
        Check(await db.ContabilitaPianoConti.CountAsync()==7,"Piano conti iniziale");
        var vendita=Movimento(30,-3,-6,987654);
        vendita.OrigineModulo="Cassa"; vendita.OrigineEntita="SchedaConto"; vendita.OrigineId="42"; vendita.OrigineEvento="Addebito";
        vendita.Righe[0].PersonId=987654;
        vendita.VerificaQuadratura(); Count++;
        db.ContabilitaMovimenti.Add(vendita); await db.SaveChangesAsync();
        Check(vendita.Id>0&&vendita.Righe.All(x=>x.Id>0),"Movimento e righe salvati senza dipendenza dall'anagrafica operativa");
        var partita=new PartitaContabile { RigaAperturaId=vendita.Righe[0].Id, Tipo=TipoPartitaContabile.Credito,
            PersonId=987654, Controparte="Socio storico", Descrizione="Spesa 30 euro", ImportoOriginale=30, DataApertura=DateTime.Today };
        db.ContabilitaPartite.Add(partita); await db.SaveChangesAsync();
        var pagamento=Movimento(20,-1,-3,987654); pagamento.VerificaQuadratura();
        db.ContabilitaMovimenti.Add(pagamento); await db.SaveChangesAsync();
        var abbinamento=new AbbinamentoPartitaContabile { IdOperazione=Guid.NewGuid(), PartitaId=partita.Id,
            RigaRegolamentoId=pagamento.Righe[1].Id, DataOra=DateTime.Now, Importo=20 };
        db.ContabilitaAbbinamentiPartite.Add(abbinamento); await db.SaveChangesAsync();
        async Task<decimal> Residuo() => partita.ImportoOriginale-(await db.ContabilitaAbbinamentiPartite
            .Where(x=>x.PartitaId==partita.Id).Select(x=>x.Importo).ToListAsync()).Sum();
        Check(await Residuo()==10,"Pagamento parziale: residuo 10 euro");
        var saldo=Movimento(10,-1,-3,987654); db.ContabilitaMovimenti.Add(saldo); await db.SaveChangesAsync();
        db.ContabilitaAbbinamentiPartite.Add(new() { IdOperazione=Guid.NewGuid(), PartitaId=partita.Id,
            RigaRegolamentoId=saldo.Righe[1].Id, DataOra=DateTime.Now, Importo=10 }); await db.SaveChangesAsync();
        Check(await Residuo()==0,"Pagamento successivo chiude la partita senza un nuovo ricavo");
        var storno=Movimento(20,-3,-1,987654); storno.StornoDiId=pagamento.Id;
        db.ContabilitaMovimenti.Add(storno); await db.SaveChangesAsync();
        db.ContabilitaAbbinamentiPartite.Add(new() { IdOperazione=Guid.NewGuid(), PartitaId=partita.Id,
            RigaRegolamentoId=storno.Righe[0].Id, DataOra=DateTime.Now, Importo=-20, StornoDiId=abbinamento.Id });
        await db.SaveChangesAsync(); Check(await Residuo()==20,"Storno del pagamento riapre il credito conservando lo storico");
        var costo=Movimento(50,-7,-1); db.ContabilitaMovimenti.Add(costo); await db.SaveChangesAsync();
        Check(costo.PersonId is null,"Costo manuale senza socio");
        var ricavo=Movimento(15,-1,-6); db.ContabilitaMovimenti.Add(ricavo); await db.SaveChangesAsync();
        Check(ricavo.PersonId is null,"Ricavo manuale senza socio");
        var debito=Movimento(40,-7,-4); db.ContabilitaMovimenti.Add(debito); await db.SaveChangesAsync();
        db.ContabilitaPartite.Add(new() { RigaAperturaId=debito.Righe[1].Id, Tipo=TipoPartitaContabile.Debito,
            Controparte="Fornitore esterno", ImportoOriginale=40, DataApertura=DateTime.Today, DataScadenza=DateTime.Today.AddDays(30) });
        await db.SaveChangesAsync(); Check(await db.ContabilitaPartite.AnyAsync(x=>x.Tipo==TipoPartitaContabile.Debito&&x.PersonId==null),"Debito verso controparte senza socio");
        var invalido=Movimento(1,-1,-6); invalido.Righe[1].Avere=2;
        try { invalido.VerificaQuadratura(); throw new Exception("Accettato movimento sbilanciato"); }
        catch(InvalidOperationException) { Count++; }
        invalido.Righe[1].Avere=1; invalido.Righe[0].Dare=1.001m;
        try { invalido.VerificaQuadratura(); throw new Exception("Accettate frazioni di centesimo"); }
        catch(InvalidOperationException) { Count++; }
        var duplicato=Movimento(1,-1,-6); duplicato.IdOperazione=vendita.IdOperazione;
        await Rifiuta(db,duplicato,"Evento contabile duplicato accettato");
        await Rifiuta(db,new RigaContabile { MovimentoId=vendita.Id, ContoId=-1, NumeroRiga=9, Dare=1, Avere=1 },"Accettati Dare e Avere contemporanei");
        await Rifiuta(db,new RigaContabile { MovimentoId=vendita.Id, ContoId=-1, NumeroRiga=9, Dare=-1 },"Accettato importo negativo sulla riga");
        await Rifiuta(db,new RigaContabile { MovimentoId=vendita.Id, ContoId=-1, NumeroRiga=9 },"Accettata riga senza importo");
        await Rifiuta(db,new RigaContabile { MovimentoId=vendita.Id, ContoId=-1, NumeroRiga=1, Dare=1 },"Accettato numero riga duplicato");
        await Rifiuta(db,new RigaContabile { MovimentoId=vendita.Id, ContoId=999999, NumeroRiga=9, Dare=1 },"Accettato conto inesistente");
        await Rifiuta(db,new MovimentoContabile { IdOperazione=Guid.Empty },"Accettata chiave operazione vuota");
        await Rifiuta(db,new AbbinamentoPartitaContabile { IdOperazione=Guid.NewGuid(), PartitaId=partita.Id, RigaRegolamentoId=pagamento.Righe[1].Id, Importo=-1 },"Accettato abbinamento negativo senza storno");
        await Rifiuta(db,new AbbinamentoPartitaContabile { IdOperazione=Guid.NewGuid(), PartitaId=partita.Id, RigaRegolamentoId=storno.Righe[0].Id, Importo=-20, StornoDiId=abbinamento.Id },"Accettato doppio storno dello stesso abbinamento");
        var contoUsato=await db.ContabilitaPianoConti.SingleAsync(x=>x.Id==-1); db.Remove(contoUsato);
        try { await db.SaveChangesAsync(); throw new Exception("Eliminato conto utilizzato"); }
        catch(DbUpdateException) { Count++; db.ChangeTracker.Clear(); }
        Check(await db.ContabilitaMovimenti.CountAsync()==7,"Operazioni valide conservate dopo tentativi non validi");
    }
}
