using AppServices.Database;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Models.Context;
using Models.Tables;
using Soci.ViewModels;

internal static class PersonMaiuscoloChecks
{
    public static int Count;
    private static void Check(bool value,string text){if(!value)throw new Exception(text);Count++;}
    public static async Task Database()
    {
        var person=new Person{Id=100,FirstName="Anna marìa",SurName="D'angelo",Natoil=19900101,UniqueParam="TEST100"};
        Check(person.FirstName=="ANNA MARÌA"&&person.SurName=="D'ANGELO","Normalizzazione nomi con accenti e apostrofi");
        var data=new SociRecord{Nome=" mario ",Cognome=" rossi ",Natoil=19900101};
        SociValidation.Person(data);
        Check(data.Nome=="MARIO"&&data.Cognome=="ROSSI"&&data.CodiceUnivoco=="ROSMAR19900101","Validazione normalizza prima del codice univoco");
        var editor=new PersonUpdViewModel(data);editor.Nome="Luca";editor.Cognome="de luca";
        Check(editor.Nome=="LUCA"&&editor.Cognome=="DE LUCA"&&editor.ToDto().Nome=="LUCA","Editor mantiene maiuscolo durante la digitazione");
        await using var connection=new SqliteConnection("Data Source=:memory:");await connection.OpenAsync();
        var options=new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using var db=new AppDbContext(options);await db.Database.EnsureCreatedAsync();
        db.People.Add(person);await db.SaveChangesAsync();
        await using(var cmd=connection.CreateCommand())
        {
            cmd.CommandText="SELECT FirstName || '|' || SurName FROM People WHERE Id=100;";
            Check((string?)await cmd.ExecuteScalarAsync()=="ANNA MARÌA|D'ANGELO","Maiuscole persistite nel database");
        }
        await db.Database.ExecuteSqlRawAsync("UPDATE People SET FirstName='mario',SurName='Rossi' WHERE Id=100;");
        var assembly=db.GetService<IMigrationsAssembly>();
        var definition=assembly.Migrations.Single(x=>x.Key.EndsWith("_PersonMaiuscolo"));
        var migration=assembly.CreateMigration(definition.Value,db.Database.ProviderName!);
        foreach(var cmd in db.GetService<IMigrationsSqlGenerator>().Generate(migration.UpOperations,db.Model))await db.Database.ExecuteSqlRawAsync(cmd.CommandText);
        await using(var cmd=connection.CreateCommand())
        {
            cmd.CommandText="SELECT FirstName || '|' || SurName FROM People WHERE Id=100;";
            Check((string?)await cmd.ExecuteScalarAsync()=="MARIO|ROSSI","Migrazione normalizza i record precedenti");
        }
        foreach(var cmd in db.GetService<IMigrationsSqlGenerator>().Generate(migration.UpOperations,db.Model))await db.Database.ExecuteSqlRawAsync(cmd.CommandText);
        Check(await db.People.CountAsync()==5,"Aggiornamento ripetuto conserva tutte le anagrafiche");
    }
}