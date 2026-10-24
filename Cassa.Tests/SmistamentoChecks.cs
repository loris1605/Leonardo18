using System.Net;
using Microsoft.EntityFrameworkCore;
using Models.Context;
using Models.Tables;
using AppServices.Database;
using Leonardo18.DatabaseHost.Cassa;
using Leonardo18.DatabaseHost.Strisciate;

internal static class SmistamentoChecks
{
    public static async Task<int> Run(DbContextOptions<AppDbContext> options, CassaDatabaseRepository cassa, HttpClient client, string endpoint)
    {
        int checks=0;
        void Check(bool ok,string message) { if (!ok) throw new Exception(message); checks++; }
        var fields=new Dictionary<string,string> {
            ["codice_socio"]="000054",["numero_tessera"]="000077",["nome"]="ANNA MARIA",
            ["cognome"]="D'ANGELO'); DROP TABLE People;--",["born_date"]="19950302",["data_scadenza"]="20991231"
        };
        async Task<HttpStatusCode> Send(Dictionary<string,string> values)
        {
            var url=endpoint+"/update?"+string.Join("&",values.Select(v=>Uri.EscapeDataString(v.Key)+"="+Uri.EscapeDataString(v.Value)));
            using var response=await client.GetAsync(url);
            return response.StatusCode;
        }
        var key=StrisciateProcessor.UniqueParam(fields["cognome"],fields["nome"],19950302);
        int personId; int members; int cards; int people;
        await using (var db=new AppDbContext(options))
        {
            personId=await db.People.Where(p=>p.UniqueParam==key).Select(p=>p.Id).SingleAsync();
            people=await db.People.CountAsync(); members=await db.Soci.CountAsync(); cards=await db.Tessere.CountAsync();
            // Existing card must even preserve disabled status and member details when incoming data differs.
            var card=await db.Tessere.SingleAsync(t=>t.NumeroTessera=="000077"); card.Abilitato=false;
            await db.SaveChangesAsync();
        }
        var duplicate=new Dictionary<string,string>(fields) {
            ["codice_socio"]="UNUSED_MEMBER",["nome"]="PERSONA DIVERSA",["cognome"]="COGNOME DIVERSO",
            ["born_date"]="19800101",["data_scadenza"]="20300101"
        };
        Check(await Send(duplicate)==HttpStatusCode.OK,"Tessera esistente: risposta OK con aggiornamento nascita e scadenza.");
        await using (var db=new AppDbContext(options))
        {
            var person=await db.People.SingleAsync(p=>p.Id==personId);
            var card=await db.Tessere.SingleAsync(t=>t.NumeroTessera=="000077");
            Check(person.UniqueParam==key && person.Natoil==19800101 && person.SurName==fields["cognome"].ToUpperInvariant() && person.FirstName==fields["nome"],"Tessera esistente aggiorna la nascita della Person collegata preservando identità e nomi.");
            Check(card.Scadenza==20300101 && !card.Abilitato,"Tessera esistente aggiorna scadenza e conserva abilitazione.");
            Check(await db.People.CountAsync()==people && await db.Soci.CountAsync()==members && await db.Tessere.CountAsync()==cards,"Tessera esistente non aggiunge Person, NumeroSocio o NumeroTessera.");
            Check(await db.Strisciate.CountAsync()==0,"Tessera esistente non resta in coda.");
            person.Natoil=19940101; // Simulate a stored birth date inconsistent with the identifying UniqueParam.
            await db.SaveChangesAsync();
        }
        var newCard=new Dictionary<string,string>(fields) { ["numero_tessera"]="000078",["data_scadenza"]="20311231" };
        Check(await Send(newCard)==HttpStatusCode.OK,"Nuova tessera aggiunta alla Person trovata tramite UniqueParam.");
        await using (var db=new AppDbContext(options))
        {
            var person=await db.People.Include(p=>p.Soci).ThenInclude(s=>s.Tessere).SingleAsync(p=>p.Id==personId);
            Check(person.Natoil==19950302 && person.UniqueParam==key,"Data di nascita aggiornata sulla Person identificata.");
            var member=person.Soci.Single(s=>s.NumeroSocio=="000054");
            Check(member.Tessere.Count==2 && person.Soci.Count==1,"Stesso NumeroSocio riutilizzato senza duplicati.");
            Check(member.Tessere.Single(t=>t.NumeroTessera=="000078").Scadenza==20311231,"Nuova tessera conserva la scadenza ricevuta.");
            Check(member.Tessere.Single(t=>t.NumeroTessera=="000077").Scadenza==20300101,"Nuova tessera non altera la precedente.");
        }
        var newMember=new Dictionary<string,string>(fields) { ["codice_socio"]="000055",["numero_tessera"]="000079",["data_scadenza"]="20321231" };
        Check(await Send(newMember)==HttpStatusCode.OK,"Nuovo NumeroSocio aggiunto alla stessa Person.");
        await using (var db=new AppDbContext(options))
        {
            var person=await db.People.Include(p=>p.Soci).ThenInclude(s=>s.Tessere).SingleAsync(p=>p.Id==personId);
            Check(person.Soci.Count==2 && await db.People.CountAsync()==people,"Nuovo NumeroSocio non duplica la Person.");
            Check(person.Soci.Single(s=>s.NumeroSocio=="000055").Tessere.Single().NumeroTessera=="000079","Nuova tessera collegata al nuovo NumeroSocio.");
            Check(person.Soci.Single(s=>s.NumeroSocio=="000054").Tessere.Count==2,"Il NumeroSocio precedente e le sue tessere restano presenti.");
        }
        Check(await Send(newMember)==HttpStatusCode.OK,"Ripetizione di tessera già elaborata aggiorna la scadenza.");
        await using (var db=new AppDbContext(options)) Check(await db.Soci.CountAsync(s=>s.PersonId==personId)==2 && await db.Tessere.CountAsync(t=>t.Socio!.PersonId==personId)==3,"Ripetizioni non duplicano NumeroSocio o tessere.");

        // The member-number unique index must not cause reassignment to a different UniqueParam.
        var conflicting=new Dictionary<string,string>(fields) { ["codice_socio"]="S100",["numero_tessera"]="UNUSED_CARD" };
        Check(await Send(conflicting)==HttpStatusCode.Conflict,"NumeroSocio appartenente ad altra Person respinto.");
        await using (var db=new AppDbContext(options))
        {
            Check(!await db.Tessere.AnyAsync(t=>t.NumeroTessera=="UNUSED_CARD") && await db.Strisciate.CountAsync()==0,"Conflitto non lascia tessere o righe parziali.");
            Check((await db.Soci.SingleAsync(s=>s.NumeroSocio=="S100")).PersonId==100,"Conflitto non sposta il NumeroSocio dell'altra Person.");
        }
        var foreignDuplicate=new Dictionary<string,string>(conflicting) { ["numero_tessera"]="000123",["born_date"]="19850101" };
        Check(await Send(foreignDuplicate)==HttpStatusCode.OK,"Tessera già esistente aggiorna la scadenza prima di qualunque riconoscimento o conflitto.");
        await using (var db=new AppDbContext(options)) Check((await db.People.SingleAsync(p=>p.Id==100)).Natoil==19850101,"Tessera esistente corregge la nascita della propria Person anche con UniqueParam ricevuto diverso.");
        var longNumber=new Dictionary<string,string>(fields) { ["numero_tessera"]=new string('1',21) };
        Check(await Send(longNumber)==HttpStatusCode.BadRequest,"Lunghezza limitata a 20 caratteri come nelle tabelle Socio/Tessera.");

        // Queue fallback shares the same expiry-update and lookup rules, including duplicates within a single unsaved batch.
        await using (var db=new AppDbContext(options))
        {
            db.Strisciate.AddRange(new Strisciata { Cognome="Bianchi",Nome="Luca",Natoil=19980101,CodiceSocio="LEGACY_MEMBER",NumeroTessera="LEGACY_CARD",Scadenza=20310101 },
                new Strisciata { Cognome="Bianchi",Nome="Luca",Natoil=19990101,CodiceSocio="LEGACY_OTHER",NumeroTessera="LEGACY_CARD",Scadenza=20400101 });
            await db.SaveChangesAsync();
        }
        await cassa.Execute(100,new() { Action=CassaAction.PrepareEntry,PostazioneId=100 });
        await using (var db=new AppDbContext(options))
        {
            var person=await db.People.Include(p=>p.Soci).ThenInclude(s=>s.Tessere).SingleAsync(p=>p.UniqueParam=="BIALUC19980101");
            Check(person.Natoil==19990101 && person.Soci.Count==1 && person.Soci.Single().NumeroSocio=="LEGACY_MEMBER","Duplicato nello stesso batch aggiorna la nascita e conserva il NumeroSocio.");
            Check(person.Soci.Single().Tessere.Single().Scadenza==20400101,"Duplicato nello stesso batch aggiorna la scadenza.");
            Check(!await db.People.AnyAsync(p=>p.UniqueParam=="BIALUC19990101") && await db.Strisciate.CountAsync()==0,"Batch aggiorna la tessera duplicata e consuma la coda.");
        }
        await using (var db=new AppDbContext(options))
        {
            db.Strisciate.AddRange(new Strisciata { Cognome="Verifica",Nome="Transazione",Natoil=19970101,CodiceSocio="ROLLBACK_MEMBER",NumeroTessera="ROLLBACK_CARD",Scadenza=20310101 },
                new Strisciata { Cognome="Persona",Nome="Differente",Natoil=19990101,CodiceSocio="S100",NumeroTessera="CONFLICT_CARD",Scadenza=20310101 });
            await db.SaveChangesAsync();
        }
        try { await cassa.Execute(100,new() { Action=CassaAction.PrepareEntry,PostazioneId=100 }); throw new Exception("Conflitto nella coda non respinto."); }
        catch (StrisciateConflictException) { Check(true,"Conflitto batch segnalato."); }
        await using (var db=new AppDbContext(options))
        {
            Check(await db.Strisciate.CountAsync()==2,"Errore batch lascia i record in coda.");
            Check(!await db.Tessere.AnyAsync(t=>t.NumeroTessera=="ROLLBACK_CARD") && !await db.People.AnyAsync(p=>p.UniqueParam=="VERTRA19970101"),"Errore batch annulla anche le modifiche dei record precedenti.");
            db.Strisciate.RemoveRange(db.Strisciate); await db.SaveChangesAsync();
        }
        return checks;
    }
}
