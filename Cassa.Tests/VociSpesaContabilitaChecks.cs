using AppServices.Database;
using Leonardo18.DatabaseHost.Cassa;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Models.Context;
using Models.Contabilita;
using Models.Tables;

internal static class VociSpesaContabilitaChecks
{
    public static int Count;
    private static void Check(bool value,string message){if(!value)throw new Exception(message);Count++;}
    private static async Task Reject(Func<Task> action,string fragment)
    {
        try{await action();}catch(InvalidOperationException ex)when(ex.Message.Contains(fragment,StringComparison.OrdinalIgnoreCase)){Count++;return;}
        throw new Exception("Operazione non rifiutata: "+fragment);
    }
    private static CassaRequest Request(CassaAction action,int id=0,bool paid=false,int listino=110)=>new()
    {Action=action,PostazioneId=100,Posizione="A01",SchedaId=100,ContoId=id,Pagato=paid,ListinoId=listino};

    public static async Task Database()
    {
        await using var connection=new SqliteConnection("Data Source=:memory:");await connection.OpenAsync();
        var options=new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using(var db=new AppDbContext(options))
        {
            await db.Database.EnsureCreatedAsync();
            db.Postazioni.Add(new(){Id=100,Nome="Cassa test",TipoPostazioneId=2,TipoRientroId=-1});
            db.Operatori.Add(new(){Id=100,Nome="Cassiere test",PersonId=-1,Abilitato=true});
            db.Permessi.Add(new(){OperatoreId=100,PostazioneId=100});
            db.Giornate.Add(new(){Id=100,Aperta=true,DataInizio=DateTime.Today.AddDays(-1).AddHours(20),DataFine=DateTime.MaxValue});
            db.People.Add(new(){Id=100,FirstName="Mario",SurName="Rossi",UniqueParam="VOCE100",Natoil=19900101,
                Soci=new(){new(){NumeroSocio="S100",Tessere=new(){new(){NumeroTessera="T100",Scadenza=20991231,Abilitato=true}}}}});
            db.Schede.Add(new(){Id=100,PersonId=100,Posizione="A01",NumeroTessera="T100",Cognome="Rossi",Nome="Mario",CheckinTime=DateTime.Now,CheckoutTime=DateTime.MaxValue});
            db.Settori.AddRange(new Settore{Id=110,Nome="Bar",TipoSettoreId=-2},new Settore{Id=120,Nome="Servizi",TipoSettoreId=-2});
            db.Reparti.AddRange(new Reparto{SettoreId=110,PostazioneId=100},new Reparto{SettoreId=120,PostazioneId=100});
            db.Tariffe.AddRange(new Tariffa{Id=110,Nome="Nome interno acqua",Label="Acqua",Prezzo=2.50m},
                new Tariffa{Id=120,Nome="Nome interno servizio",Label="Servizio",Prezzo=10},
                new Tariffa{Id=111,Nome="Omaggio",Label="Omaggio",Prezzo=0},
                new Tariffa{Id=112,Nome="Negativa",Label="Negativa",Prezzo=-1},
                new Tariffa{Id=113,Nome="Frazioni",Label="Frazioni",Prezzo=1.001m});
            foreach(var i in new[]{110,111,112,113,120})db.Listini.Add(new(){Id=i,TariffaId=i,SettoreId=i==120?120:110});
            await db.SaveChangesAsync();
        }
        var repository=new CassaDatabaseRepository(()=>new AppDbContext(options));
        async Task<int> Add(int listino=110)
        {
            var result=await repository.Execute(100,Request(CassaAction.AddCharge,listino:listino));
            return result.Conto.Max(x=>x.Id);
        }
        async Task<decimal> Residuo(int sourceId)
        {
            await using var db=new AppDbContext(options);
            var origin=sourceId.ToString();
            var original=await db.ContabilitaMovimenti.Include(x=>x.Righe).SingleAsync(x=>x.OrigineId==origin&&x.OrigineEvento=="VoceSpesa");
            var rowId=original.Righe.Single(x=>x.Dare>0).Id;
            var partita=await db.ContabilitaPartite.SingleAsync(x=>x.RigaAperturaId==rowId);
            var allocations=await db.ContabilitaAbbinamentiPartite.Where(x=>x.PartitaId==partita.Id).Select(x=>x.Importo).ToListAsync();
            return partita.ImportoOriginale-allocations.Sum();
        }
        async Task<decimal> Net(string code)
        {
            await using var db=new AppDbContext(options);
            var id=await db.ContabilitaPianoConti.Where(x=>x.Codice==code).Select(x=>x.Id).SingleAsync();
            var rows=await db.ContabilitaRighe.Where(x=>x.ContoId==id).ToListAsync();
            return rows.Sum(x=>x.Dare-x.Avere);
        }
        var first=await Add();
        await using(var db=new AppDbContext(options))
        {
            var source=await db.SchedeConto.SingleAsync();
            var movement=await db.ContabilitaMovimenti.Include(x=>x.Righe).SingleAsync();
            var partita=await db.ContabilitaPartite.SingleAsync();
            Check(!source.Pagato&&source.VoiceDesc=="Acqua"&&source.VoicePrice==2.50m,"Addebito operativo non pagato");
            Check(movement.Stato==StatoMovimentoContabile.Confermato&&movement.Causale=="VOCE_SPESA"&&movement.OrigineId==source.Id.ToString(),"Ricavo confermato collegato alla riga operativa");
            Check(movement.PersonId==100&&movement.Nominativo=="Rossi Mario"&&movement.NumeroSocio=="S100"&&movement.NumeroTessera=="T100","Controparte e tessera conservate");
            Check(movement.OperatoreId==100&&movement.PostazioneId==100&&movement.GiornataId==100&&movement.DataCompetenza==DateTime.Today.AddDays(-1),"Operatore postazione e giornata contabile");
            Check(movement.Righe.All(x=>x.SettoreId==110&&x.NomeSettore=="Bar"&&x.TariffaId==110&&x.EtichettaTariffa=="Acqua"&&x.Descrizione=="Acqua"),"Settore e etichetta della tariffa effettivamente premuta");
            Check(partita.Tipo==TipoPartitaContabile.Credito&&partita.PersonId==100&&partita.ImportoOriginale==2.50m&&partita.RigaAperturaId==movement.Righe.Single(x=>x.Dare>0).Id,"Partita di credito verso il socio");
            movement.VerificaQuadratura();Count++;
        }
        Check(await Net("400")==-2.50m&&await Net("120")==2.50m&&await Net("100")==0,"Ricavo e credito senza incasso anticipato");
        var second=await Add();var third=await Add(120);
        await using(var db=new AppDbContext(options))
        {
            Check(await db.ContabilitaMovimenti.CountAsync()==3&&await db.ContabilitaPartite.CountAsync()==3,"Ogni click reale crea una propria voce e scrittura");
            var origin=third.ToString();var movement=await db.ContabilitaMovimenti.Include(x=>x.Righe).SingleAsync(x=>x.OrigineId==origin);
            Check(movement.Righe.All(x=>x.SettoreId==120&&x.NomeSettore=="Servizi"&&x.TariffaId==120),"Voci di altri settori contabilizzate correttamente");
            (await db.Tariffe.SingleAsync(x=>x.Id==110)).Label="Nome cambiato";
            (await db.Settori.SingleAsync(x=>x.Id==110)).Nome="Settore cambiato";
            await db.SaveChangesAsync();
        }
        var payment=Request(CassaAction.SetPagamento,first,true);
        await repository.Execute(100,payment);
        Check(await Residuo(first)==0&&await Net("400")==-15m&&await Net("100")==2.50m&&await Net("120")==12.50m,"Incasso chiude il credito senza duplicare il ricavo");
        await using(var db=new AppDbContext(options))
        {
            var movement=await db.ContabilitaMovimenti.Include(x=>x.Righe).SingleAsync(x=>x.OrigineEvento=="PagamentoVoceSpesa");
            Check(movement.Righe.All(x=>x.EtichettaTariffa=="Acqua"&&x.NomeSettore=="Bar"),"Incasso conserva i dettagli storici originali");
            Check(await db.ContabilitaAbbinamentiPartite.CountAsync()==1,"Incasso abbinato alla partita");
        }
        await repository.Execute(100,payment);
        await using(var db=new AppDbContext(options))Check(await db.ContabilitaMovimenti.CountAsync()==4&&await db.ContabilitaAbbinamentiPartite.CountAsync()==1,"Stesso flag pagato ripetuto non duplica incasso");
        payment.Pagato=false;await repository.Execute(100,payment);
        Check(await Residuo(first)==2.50m&&await Net("100")==0&&await Net("400")==-15m,"Annullamento pagamento riapre il credito senza modificare il ricavo");
        await using(var db=new AppDbContext(options))
        {
            var cancel=await db.ContabilitaMovimenti.SingleAsync(x=>x.OrigineEvento=="AnnullamentoPagamentoVoceSpesa");
            Check(cancel.StornoDiId.HasValue&&await db.ContabilitaAbbinamentiPartite.AnyAsync(x=>x.Importo==-2.50m&&x.StornoDiId!=null),"Annullamento tracciato come storno del precedente incasso e abbinamento");
        }
        await repository.Execute(100,payment);
        payment.Pagato=true;await repository.Execute(100,payment);payment.Pagato=false;await repository.Execute(100,payment);payment.Pagato=true;await repository.Execute(100,payment);
        Check(await Residuo(first)==0&&await Net("100")==2.50m&&await Net("400")==-15m,"Cicli pagato/non pagato mantengono storico e saldi corretti");
        var unpaidReversal=await repository.Execute(100,Request(CassaAction.StornaConto,second));
        var negativeUnpaid=unpaidReversal.Conto.Single(x=>x.VoicePrice<0).Id;
        Check(await Residuo(second)==0&&await Net("400")==-12.50m&&await Net("120")==10&&await Net("100")==2.50m,"Storno non pagato annulla ricavo e credito senza movimento di cassa");
        await Reject(()=>repository.Execute(100,Request(CassaAction.StornaConto,second)),"già stata stornata");
        await Reject(()=>repository.Execute(100,Request(CassaAction.SetPagamento,second,true)),"già stata stornata");
        await Reject(()=>repository.Execute(100,Request(CassaAction.SetPagamento,negativeUnpaid,true)),"storno già contabilizzato");
        await repository.Execute(100,Request(CassaAction.StornaConto,negativeUnpaid));
        var paidReversal=await repository.Execute(100,Request(CassaAction.StornaConto,first));
        Check(paidReversal.Conto.Any(x=>x.VoicePrice==-2.50m&&x.Pagato),"Storno pagato conserva lo stato operativo");
        Check(await Net("400")==-10&&await Net("120")==10&&await Net("100")==0&&await Residuo(first)==0,"Storno pagato annulla ricavo e restituisce cassa senza riaprire credito");
        await Reject(()=>repository.Execute(100,Request(CassaAction.StornaConto,first)),"già stata stornata");
        var zero=await Add(111);
        await using(var db=new AppDbContext(options))Check(!await db.ContabilitaMovimenti.AnyAsync(x=>x.OrigineId==zero.ToString()),"Voce gratuita non genera movimenti monetari");
        await Reject(()=>Add(112),"prezzo non valido");await Reject(()=>Add(113),"prezzo non valido");
        await using(var db=new AppDbContext(options))
        {
            (await db.ContabilitaPianoConti.SingleAsync(x=>x.Codice=="400")).Attivo=false;await db.SaveChangesAsync();
        }
        int operativeCount,journalCount;
        await using(var db=new AppDbContext(options)){operativeCount=await db.SchedeConto.CountAsync();journalCount=await db.ContabilitaMovimenti.CountAsync();}
        await Reject(()=>Add(120),"conto 400");
        await using(var db=new AppDbContext(options))
        {
            Check(await db.SchedeConto.CountAsync()==operativeCount&&await db.ContabilitaMovimenti.CountAsync()==journalCount,"Errore contabile annulla anche la nuova voce operativa");
            (await db.ContabilitaPianoConti.SingleAsync(x=>x.Codice=="400")).Attivo=true;
            (await db.ContabilitaPianoConti.SingleAsync(x=>x.Codice=="100")).Attivo=false;await db.SaveChangesAsync();
        }
        await Reject(()=>repository.Execute(100,Request(CassaAction.SetPagamento,third,true)),"conto 100");
        Check(await Residuo(third)==10,"Incasso rifiutato non chiude la partita");
        await using(var db=new AppDbContext(options))
        {
            Check(!(await db.SchedeConto.SingleAsync(x=>x.Id==third)).Pagato&&await db.ContabilitaMovimenti.CountAsync()==journalCount,"Incasso rifiutato non cambia flag o scritture");
            (await db.ContabilitaPianoConti.SingleAsync(x=>x.Codice=="100")).Attivo=true;await db.SaveChangesAsync();
        }
        var failure=new FailJournal();var failureOptions=new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).AddInterceptors(failure).Options;
        var failing=new CassaDatabaseRepository(()=>new AppDbContext(failureOptions));
        async Task CountsUnchanged(string message)
        {
            await using var db=new AppDbContext(options);Check(await db.SchedeConto.CountAsync()==operativeCount&&await db.ContabilitaMovimenti.CountAsync()==journalCount,message);
        }
        await Reject(()=>failing.Execute(100,Request(CassaAction.AddCharge,listino:120)),"errore simulato");await CountsUnchanged("Errore durante scrittura ricavo: rollback completo");
        await Reject(()=>failing.Execute(100,Request(CassaAction.SetPagamento,third,true)),"errore simulato");
        Check(await Residuo(third)==10,"Errore durante incasso non lascia abbinamenti");await CountsUnchanged("Errore durante incasso non lascia movimenti");
        await Reject(()=>failing.Execute(100,Request(CassaAction.StornaConto,third)),"errore simulato");await CountsUnchanged("Errore durante storno annulla anche la riga negativa");
        await repository.Execute(100,Request(CassaAction.SetPagamento,third,true));
        failure.FailOperational=true;
        await Reject(()=>failing.Execute(100,Request(CassaAction.SetPagamento,third,false)),"errore operativo simulato");
        Check(await Residuo(third)==0&&await Net("100")==10,"Errore dopo salvataggio contabile annulla anche rettifica e abbinamento");
        await using(var db=new AppDbContext(options))
        {
            Check((await db.SchedeConto.SingleAsync(x=>x.Id==third)).Pagato,"Errore finale conserva il flag originale");
            var movements=await db.ContabilitaMovimenti.Include(x=>x.Righe).ToListAsync();
            foreach(var movement in movements)movement.VerificaQuadratura();
            Check(movements.All(x=>x.Stato==StatoMovimentoContabile.Confermato),"Tutte le scritture confermate e quadrate");
            Check(movements.Select(x=>x.IdOperazione).Distinct().Count()==movements.Count,"Identità eventi distinte anche nei cicli di pagamento");
            Check(await db.ContabilitaPartite.CountAsync()==3,"Una sola partita per ogni voce a pagamento");
        }
    }

    private sealed class FailJournal:SaveChangesInterceptor
    {
        public bool FailOperational;
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data,InterceptionResult<int> result,CancellationToken ct=default)
        {
            if(!FailOperational&&data.Context!.ChangeTracker.Entries<MovimentoContabile>().Any(x=>x.State==EntityState.Added))throw new InvalidOperationException("Errore simulato");
            if(FailOperational&&data.Context!.ChangeTracker.Entries<SchedaConto>().Any(x=>x.State==EntityState.Modified))throw new InvalidOperationException("Errore operativo simulato");
            return ValueTask.FromResult(result);
        }
    }
}
