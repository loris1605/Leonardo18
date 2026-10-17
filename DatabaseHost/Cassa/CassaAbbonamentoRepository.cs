using System.Globalization;
using AppServices.Database;
using Microsoft.EntityFrameworkCore;
using Models.Context;
using Models.Contabilita;
using Models.Sospesi;
using Models.Tables;
namespace Leonardo18.DatabaseHost.Cassa;

public sealed partial class CassaDatabaseRepository
{
    private sealed record SubscriptionHolder(int PersonId,string Nome,string? NumeroSocio,string NumeroTessera);

    private static bool ValidSubscriptionCard(CassaSocio? member) => member is { PersonId:>0, Blocco:false }
        && DateTime.TryParseExact(member.ScadenzaTessera.ToString("D8",CultureInfo.InvariantCulture),"yyyyMMdd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var expiry)
        && expiry>=DateTime.Today;

    private static async Task<CassaAcquistoAbbonamento> BuyAbbonamento(AppDbContext db,int operatorId,Postazione station,CassaRequest request,CancellationToken ct)
    {
        if(request.AcquistoId==Guid.Empty)throw new InvalidOperationException("Identificativo acquisto mancante: riapri il modulo.");
        if(request.Note.Length>50)throw new InvalidOperationException("Le note possono contenere al massimo 50 caratteri.");
        var card1=request.NumeroTessera.Trim();var card2=request.NumeroTessera2.Trim();
        var existing=await db.Abbonamenti.Include(x=>x.Person1).Include(x=>x.Person2).Include(x=>x.Tessera1).Include(x=>x.Tessera2)
            .SingleOrDefaultAsync(x=>x.AcquistoId==request.AcquistoId,ct);
        if(existing is not null)
        {
            if(existing.OperatoreAcquistoId!=operatorId||existing.PostazioneAcquistoId!=station.Id
                ||existing.TipoAbbonamentoId!=request.TipoAbbonamentoId||existing.PersonId1!=request.PersonId1||(existing.PersonId2??0)!=request.PersonId2
                ||existing.Tessera1?.NumeroTessera!=card1||(existing.Tessera2?.NumeroTessera??"")!=card2
                ||existing.PrezzoAcquisto!=request.PrezzoConfermato||existing.IngressiAcquistati!=request.IngressiConfermati
                ||(existing.DataFine-existing.DataInizio).Days!=request.DurataConfermata||existing.PagatoAllAcquisto!=request.Pagato||existing.Note!=request.Note)
                throw new InvalidOperationException("Questo identificativo acquisto è già stato usato con dati diversi.");
            return await SubscriptionResult(db,existing,ct);
        }
        var type=await db.TipiAbbonamento.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==request.TipoAbbonamentoId&&x.Id>0&&x.Attivo,ct)
            ??throw new InvalidOperationException("Tipo di abbonamento non disponibile.");
        if(type.Prezzo<0||decimal.Round(type.Prezzo,2)!=type.Prezzo||type.NumeroIngressi<=0||type.DurataGiorni<=0)
            throw new InvalidOperationException("Prezzo, ingressi o durata dell'abbonamento non validi.");
        if(type.Prezzo!=request.PrezzoConfermato||type.NumeroIngressi!=request.IngressiConfermati||type.DurataGiorni!=request.DurataConfermata)
            throw new InvalidOperationException("Le condizioni dell'abbonamento sono cambiate. Aggiorna i tipi e conferma i nuovi dati.");
        var first=await FindCard(db,card1,ct);
        if(!ValidSubscriptionCard(first)||first!.PersonId!=request.PersonId1)
            throw new InvalidOperationException("La tessera del primo socio non è valida o non corrisponde al socio identificato.");
        CassaSocio? second=null;
        if(card2.Length>0)
        {
            second=await FindCard(db,card2,ct);
            if(!ValidSubscriptionCard(second)||second!.PersonId!=request.PersonId2)
                throw new InvalidOperationException("La tessera del secondo socio non è valida o non corrisponde al socio identificato.");
            if(first.PersonId==second.PersonId)throw new InvalidOperationException("Le due tessere devono appartenere a soci distinti.");
        }
        else if(request.PersonId2!=0)throw new InvalidOperationException("Manca la tessera del secondo socio.");
        DateTime end;
        try{end=DateTime.Today.AddDays(type.DurataGiorni);}catch(ArgumentOutOfRangeException){throw new InvalidOperationException("Durata abbonamento non valida.");}
        var now=DateTime.Now;
        var subscription=new Abbonamento
        {
            AcquistoId=request.AcquistoId,PrezzoAcquisto=type.Prezzo,PagatoAllAcquisto=request.Pagato,DataAcquisto=now,
            OperatoreAcquistoId=operatorId,PostazioneAcquistoId=station.Id,
            Nome=type.Nome,TipoAbbonamentoId=type.Id,PersonId1=first.PersonId,PersonId2=second?.PersonId,
            TesseraId1=await db.Tessere.Where(x=>x.NumeroTessera==card1).Select(x=>x.Id).SingleAsync(ct),
            TesseraId2=second is null?null:await db.Tessere.Where(x=>x.NumeroTessera==card2).Select(x=>x.Id).SingleAsync(ct),
            IngressiAcquistati=type.NumeroIngressi,DataInizio=DateTime.Today,DataFine=end,Attivo=true,Note=request.Note
        };
        db.Abbonamenti.Add(subscription);await db.SaveChangesAsync(ct);
        var holder1=new SubscriptionHolder(first.PersonId,first.Anagrafica,first.NumeroSocio,first.NumeroTessera);
        var holder2=second is null?null:new SubscriptionHolder(second.PersonId,second.Anagrafica,second.NumeroSocio,second.NumeroTessera);
        if(type.Prezzo>0&&request.Pagato)
        {
            var movement=await SubscriptionMovement(db,operatorId,station,subscription,request.AcquistoId,"Abbonamento",subscription.Id.ToString(CultureInfo.InvariantCulture),"AcquistoAbbonamentoPagato",holder1,holder2,ct);
            db.ContabilitaMovimenti.Add(movement);await db.SaveChangesAsync(ct);
        }
        else if(type.Prezzo>0)
        {
            var day=await db.Giornate.AsNoTracking().SingleAsync(x=>x.Aperta,ct);
            db.Sospesi.Add(new()
            {
                IdOperazione=request.AcquistoId,Tipo=TipoSospeso.Credito,Origine=OrigineSospeso.Abbonamento,Stato=StatoSospeso.Aperto,
                DataOra=now,DataCompetenza=day.DataInizio.Date,Importo=type.Prezzo,Descrizione=$"Abbonamento {type.Nome} n. {subscription.Id}",Note=request.Note,
                PersonId=first.PersonId,Nominativo=first.Anagrafica,NumeroSocio=first.NumeroSocio,NumeroTessera=first.NumeroTessera,
                PersonId2=second?.PersonId,Nominativo2=second?.Anagrafica,NumeroSocio2=second?.NumeroSocio,NumeroTessera2=second?.NumeroTessera,
                AbbonamentoId=subscription.Id,OrigineEntita="Abbonamento",OrigineId=subscription.Id.ToString(CultureInfo.InvariantCulture),
                OperatoreId=operatorId,NomeOperatore=await db.Operatori.Where(x=>x.Id==operatorId).Select(x=>x.Nome).SingleAsync(ct),
                PostazioneId=station.Id,NomePostazione=station.Nome,GiornataId=day.Id
            });
            await db.SaveChangesAsync(ct);
        }
        return await SubscriptionResult(db,subscription,ct);
    }

    private static async Task<MovimentoContabile> SubscriptionMovement(AppDbContext db,int operatorId,Postazione station,Abbonamento subscription,
        Guid key,string entity,string originId,string action,SubscriptionHolder first,SubscriptionHolder? second,CancellationToken ct)
    {
        var accounts=await db.ContabilitaPianoConti.AsNoTracking().Where(x=>x.Codice=="100"||x.Codice=="400").ToArrayAsync(ct);
        var cash=accounts.SingleOrDefault(x=>x.Codice=="100"&&x.Attivo&&x.Movimentabile&&x.Natura==NaturaConto.Attivita);
        var revenue=accounts.SingleOrDefault(x=>x.Codice=="400"&&x.Attivo&&x.Movimentabile&&x.Natura==NaturaConto.Ricavo);
        if(cash is null||revenue is null)throw new InvalidOperationException("Per l'incasso abbonamento servono i conti 100 Cassa e 400 Ricavi attivi, movimentabili e con natura corretta.");
        var day=await db.Giornate.AsNoTracking().SingleAsync(x=>x.Aperta,ct);
        var amount=subscription.PrezzoAcquisto??throw new InvalidOperationException("Prezzo di acquisto non disponibile.");
        var movement=new MovimentoContabile
        {
            IdOperazione=key,DataOra=DateTime.Now,DataCompetenza=day.DataInizio.Date,Stato=StatoMovimentoContabile.Confermato,
            Causale=action=="SaldoSospesoAbbonamento"?"SALDO_SOSPESO_ABBONAMENTO":"ACQUISTO_ABBONAMENTO",
            Descrizione=$"Incasso abbonamento {subscription.Nome} n. {subscription.Id}",
            Note=subscription.Note+(second is null?"":$" · Secondo socio: {second.Nome}, tessera {second.NumeroTessera}"),
            OrigineModulo="Cassa",OrigineEntita=entity,OrigineId=originId,OrigineEvento=action,
            PersonId=first.PersonId,Nominativo=first.Nome,NumeroSocio=first.NumeroSocio,NumeroTessera=first.NumeroTessera,
            OperatoreId=operatorId,NomeOperatore=await db.Operatori.Where(x=>x.Id==operatorId).Select(x=>x.Nome).SingleAsync(ct),
            PostazioneId=station.Id,NomePostazione=station.Nome,GiornataId=day.Id,
            Righe=new(){Row(1,cash.Id,amount,0),Row(2,revenue.Id,0,amount)}
        };
        movement.VerificaQuadratura();return movement;
        RigaContabile Row(int number,int account,decimal debit,decimal credit)=>new()
        {
            NumeroRiga=number,ContoId=account,Dare=debit,Avere=credit,Descrizione=subscription.Nome,
            PersonId=first.PersonId,Nominativo=first.Nome,NumeroTessera=first.NumeroTessera,
            NomeSettore="Abbonamenti",AbbonamentoId=subscription.Id,NomeAbbonamento=subscription.Nome
        };
    }

    private static async Task<CassaAcquistoAbbonamento> SubscriptionResult(AppDbContext db,Abbonamento subscription,CancellationToken ct)
    {
        await db.Entry(subscription).Reference(x=>x.Person1).LoadAsync(ct);await db.Entry(subscription).Reference(x=>x.Person2).LoadAsync(ct);
        await db.Entry(subscription).Reference(x=>x.Tessera1).LoadAsync(ct);await db.Entry(subscription).Reference(x=>x.Tessera2).LoadAsync(ct);
        var pending=await db.Sospesi.AsNoTracking().SingleOrDefaultAsync(x=>x.AbbonamentoId==subscription.Id,ct);
        if(subscription.PrezzoAcquisto>0)
        {
            if(subscription.PagatoAllAcquisto==false&&pending is null)throw new InvalidOperationException("Registrazione acquisto incompleta: manca il sospeso.");
            if(subscription.PagatoAllAcquisto==true&&!await db.ContabilitaMovimenti.AnyAsync(x=>x.IdOperazione==subscription.AcquistoId,ct))
                throw new InvalidOperationException("Registrazione acquisto incompleta: manca l'incasso.");
        }
        return new()
        {
            AbbonamentoId=subscription.Id,Nome=subscription.Nome,
            Socio1=pending?.Nominativo??$"{subscription.Person1!.SurName} {subscription.Person1.FirstName}".Trim(),
            Socio2=pending?.Nominativo2??(subscription.Person2 is null?"":$"{subscription.Person2.SurName} {subscription.Person2.FirstName}".Trim()),
            NumeroTessera1=subscription.Tessera1?.NumeroTessera??"",NumeroTessera2=subscription.Tessera2?.NumeroTessera??"",
            Ingressi=subscription.IngressiAcquistati??0,DataInizio=subscription.DataInizio,DataFine=subscription.DataFine,
            Prezzo=subscription.PrezzoAcquisto??0,Pagato=subscription.PrezzoAcquisto==0||subscription.PagatoAllAcquisto==true||pending?.Stato==StatoSospeso.Saldato,
            SospesoId=pending?.Id
        };
    }
}
