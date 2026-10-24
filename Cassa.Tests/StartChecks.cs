using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Models.Context;

internal static class StartChecks
{
    public static int Count;
    private static void Check(bool condition,string message)
    { if(!condition)throw new Exception(message);Count++; }

    public static async Task Database()
    {
        using(var sql=new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=localhost;Database=StartSchemaTest;Integrated Security=true;TrustServerCertificate=true").Options))
        {
            var assembly=sql.GetService<IMigrationsAssembly>();
            Check(assembly.Migrations.Count==9&&assembly.Migrations.First().Key.EndsWith("_Start")&&assembly.Migrations.Last().Key.EndsWith("_CassaInizialeGiornata"),"Start conservata e aggiornamento Sospesi aggiuntivo");
            Check(!sql.Database.HasPendingModelChanges(),"Snapshot iniziale allineato al modello completo");
            var all=assembly.Migrations.Select(x=>assembly.CreateMigration(x.Value,sql.Database.ProviderName!)).ToArray();
            var initial=all[0];
            var tables=all.SelectMany(x=>x.UpOperations).OfType<CreateTableOperation>().ToArray();
            var expected=sql.Model.GetEntityTypes().Select(x=>x.GetTableName()!).ToHashSet();
            Check(expected.SetEquals(tables.Select(x=>x.Name)),"Start crea tutte e sole le tabelle del modello attuale");
            Check(initial.UpOperations.All(x=>x is CreateTableOperation or CreateIndexOperation or InsertDataOperation),"Start contiene solo creazione schema, indici e dati iniziali");
            Check(tables.All(x=>x.Name is not "CausaliContabili" and not "AbbonamentiAcquistoConto"),"Tabelle abbandonate escluse");
            var script=sql.GetService<IMigrator>().GenerateScript();
            Check(script.Contains("CREATE TABLE [ContabilitaMovimenti]")&&script.Contains("CREATE TABLE [SchedeConto]"),"Script SQL Server completo");
            Check(script.Contains("IDENTITY_INSERT [ContabilitaPianoConti] ON")&&script.Contains("INSERT INTO [Settings]"),"Script SQL Server include i dati iniziali");
            var down=sql.GetService<IMigrator>().GenerateScript(assembly.Migrations.Last().Key,Migration.InitialDatabase);
            Check(tables.All(x=>down.Contains($"DROP TABLE [{x.Name}]")),"Rollback della Start copre tutte le tabelle");
        }

        // Execute the actual Start operations on a blank isolated SQLite database.
        // Adapt provider-specific column types and identity annotations for SQLite only.
        await using var connection=new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        await using var db=new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        var migrations=db.GetService<IMigrationsAssembly>();
        var allMigrations=migrations.Migrations.Select(x=>migrations.CreateMigration(x.Value,db.Database.ProviderName!)).ToArray();
        foreach(var migration in allMigrations)
        {
            foreach(var column in migration.UpOperations.OfType<CreateTableOperation>().SelectMany(x=>x.Columns).Cast<ColumnOperation>()
                .Concat(migration.UpOperations.OfType<AddColumnOperation>()))
            {
                column.ColumnType=null;
                if(column.FindAnnotation("SqlServer:Identity") is not null)column.AddAnnotation("Sqlite:Autoincrement",true);
            }
            // SQLite has no ALTER TABLE ADD CHECK; the full-model fixtures test that constraint.
            var operations=migration.UpOperations.Where(x=>x is not AddCheckConstraintOperation).ToArray();
            foreach(var command in db.GetService<IMigrationsSqlGenerator>().Generate(operations,db.Model))
                await db.Database.ExecuteSqlRawAsync(command.CommandText);
            if(migration==allMigrations[0])
            {
                await db.Database.ExecuteSqlRawAsync("INSERT INTO TipiAbbonamento (Id,Nome,NumeroIngressi,DurataGiorni,Prezzo,Attivo) VALUES (-100,'Preesistente',1,1,1,1);");
                await db.Database.ExecuteSqlRawAsync("INSERT INTO Abbonamenti (Id,Nome,TipoAbbonamentoId,PersonId1,DataInizio,DataFine,Attivo,Note) VALUES (-100,'Preesistente',-100,-1,'2026-01-01','2026-12-31',1,'Da conservare');");
            }
        }
        Check(await db.Abbonamenti.AnyAsync(x=>x.Id==-100&&x.Nome=="Preesistente"&&x.AcquistoId==null&&x.PrezzoAcquisto==null),"Aggiornamento Sospesi conserva gli abbonamenti esistenti");
        await using(var command=connection.CreateCommand())
        {
            command.CommandText="SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'";
            await using var reader=await command.ExecuteReaderAsync();var actual=new HashSet<string>();
            while(await reader.ReadAsync())actual.Add(reader.GetString(0));
            Check(actual.SetEquals(db.Model.GetEntityTypes().Select(x=>x.GetTableName()!)),"Ricostruzione completa da database vuoto");
        }
        Check(await db.Settings.AnyAsync(x=>x.Id==-1&&x.Version==1),"Impostazioni iniziali presenti");
        Check(await db.People.AnyAsync(x=>x.Id==-2)&&await db.Operatori.AnyAsync(x=>x.Id==-1&&x.Abilitato),"Anagrafica e operatore iniziali presenti");
        Check(await db.ContabilitaPianoConti.CountAsync()==7,"Piano conti iniziale ricostruito");
        Check(await db.SchedeConto.CountAsync()==0&&await db.ContabilitaMovimenti.CountAsync()==0,"Nessuna operazione di esempio nel database nuovo");
        var person=new Models.Tables.Person { FirstName="Nuovo",SurName="Socio",UniqueParam="TESTSTART",Natoil=19900101 };
        db.People.Add(person); await db.SaveChangesAsync();
        Check(person.Id>0,"Identità e scritture utilizzabili dopo la ricostruzione");
                // Columns are dropped with Abbonamenti below; omit SQLite-specific table rebuilds on rollback to zero.
        foreach(var command in db.GetService<IMigrationsSqlGenerator>().Generate(allMigrations.Reverse().SelectMany(x=>x.DownOperations).Where(x=>x is not DropColumnOperation and not DropCheckConstraintOperation).ToArray(),db.Model))
            await db.Database.ExecuteSqlRawAsync(command.CommandText);
        await using(var command=connection.CreateCommand())
        {
            command.CommandText="SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'";
            Check(Convert.ToInt32(await command.ExecuteScalarAsync())==0,"Rollback completo nel database isolato");
        }
    }
}
