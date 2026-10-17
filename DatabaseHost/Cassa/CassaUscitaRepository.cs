using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using AppServices.Database;
using Microsoft.EntityFrameworkCore;
using Models.Context;
using Models.Contabilita;
using Models.Sospesi;
using Models.Tables;
namespace Leonardo18.DatabaseHost.Cassa;

public sealed partial class CassaDatabaseRepository
{
    private static Guid ExitKey(string text)=>new(SHA256.HashData(Encoding.UTF8.GetBytes(text))[..16]);
    private static string ExitVersion(Scheda sheet)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
        FormattableString.Invariant($"{sheet.Id}|{sheet.PersonId}|{sheet.Posizione}|{sheet.Blocco}|{sheet.CheckinTime.Ticks}|{sheet.CheckoutTime.Ticks}|{sheet.Grb1}|{sheet.Grb2}|{sheet.Grb3}|{sheet.Grb4}|{sheet.Consumazione}|{sheet.Note.Length}:{sheet.Note}|")+
        string.Join(";",sheet.SchedeConto.OrderBy(x=>x.Id).Select(x=>FormattableString.Invariant($"{x.Id}|{x.VoicePrice}|{x.Pagato}|{x.DataOra.Ticks}|{x.VoiceDesc}|{x.DescSettore}|{x.DescPostazione}"))))));
    private static async Task PrepareExit(AppDbContext db,CassaRequest request,CassaResult result,CancellationToken ct)
    {
        IQueryable<Scheda> query=db.Schede.AsNoTracking().Include(x=>x.SchedeConto).Where(x=>x.CheckoutTime==DateTime.MaxValue);
        if(!string.IsNullOrWhiteSpace(request.Posizione))query=query.Where(x=>x.Posizione==request.Posizione.Trim());
        else if(!string.IsNullOrWhiteSpace(request.NumeroTessera))
        {
            var card=request.NumeroTessera.Trim();
            var person=await db.Tessere.Where(x=>x.NumeroTessera==card).Select(x=>(int?)x.Socio!.PersonId).SingleOrDefaultAsync(ct);
            query=query.Where(x=>x.NumeroTessera==card||(person!=null&&x.PersonId==person));
        }
        else throw new InvalidOperationException("Inserisci la posizione oppure la tessera del socio in uscita.");
        var sheet=await query.SingleOrDefaultAsync(ct)??throw new InvalidOperationException("Nessuna scheda aperta trovata per il socio in uscita.");
        result.Scheda=ToSheet(sheet);result.Conto=ExitRows(sheet.SchedeConto);
        result.Uscita=new(){VersioneConto=ExitVersion(sheet),DaPagare=sheet.SchedeConto.Where(x=>!x.Pagato).Sum(x=>x.VoicePrice)};
        var pending=await MemberPending(db,sheet.PersonId,ct);result.Scheda.SospesiAperti=pending.Count;result.Scheda.TotaleSospesi=pending.Total;
        await ApplyTariffLabels(db,result.Conto,ct);
    }
    private static CassaConto[] ExitRows(IEnumerable<SchedaConto> rows)=>rows.OrderBy(x=>x.DataOra).ThenBy(x=>x.Id).Select(x=>new CassaConto{Id=x.Id,DescSettore=x.DescSettore,DescPostazione=x.DescPostazione,VoiceDesc=x.VoiceDesc,VoicePrice=x.VoicePrice,Pagato=x.Pagato,Note=x.Note,DataOra=x.DataOra}).ToArray();
    private static CassaScheda ExitSheet(SchedaUscita x)=>new(){Id=x.Id,PersonId=x.PersonId,Posizione=x.Posizione,NumeroTessera=x.NumeroTessera,Cognome=x.Cognome,Nome=x.Nome,Natoil=x.Natoil,CheckinTime=x.CheckinTime,CheckoutTime=x.CheckoutTime,Grb1=x.Grb1,Grb2=x.Grb2,Grb3=x.Grb3,Grb4=x.Grb4,Consumazione=x.Consumazione,Blocco=x.Blocco,Note=x.Note};
    private static void ExitResult(SchedaUscita sheet,CassaResult result)
    {
        result.Scheda=ExitSheet(sheet);result.Conto=sheet.Conto.OrderBy(x=>x.DataOra).ThenBy(x=>x.Id).Select(x=>new CassaConto{Id=x.Id,DescSettore=x.DescSettore,DescPostazione=x.DescPostazione,VoiceDesc=x.VoiceDesc,VoicePrice=x.VoicePrice,Pagato=x.Pagato,Note=x.Note,DataOra=x.DataOra}).ToArray();
        result.Uscita=new(){IdOperazione=sheet.IdOperazione,VersioneConto=sheet.VersioneConto,Confermata=true,DataUscita=sheet.CheckoutTime,DaPagare=sheet.Conto.Where(x=>!x.Pagato).Sum(x=>x.VoicePrice),Incassato=sheet.ImportoIncassato,Sospeso=sheet.ImportoSospeso};
    }
    private static async Task RegisterExit(AppDbContext db,int operatorId,Postazione station,CassaRequest request,CassaResult result,CancellationToken ct)
    {
        if(request.UscitaId==Guid.Empty||request.SchedaId<=0)throw new InvalidOperationException("Identificativo uscita mancante. Ricarica la scheda.");
        var previous=await db.SchedeUscite.Include(x=>x.Conto).SingleOrDefaultAsync(x=>x.IdOperazione==request.UscitaId,ct);
        if(previous!=null)
        {
            if(previous.Id!=request.SchedaId||previous.OperatoreId!=operatorId||previous.PostazioneId!=station.Id||previous.Incassa!=request.Pagato||previous.VersioneConto!=request.VersioneConto)
                throw new InvalidOperationException("Identificativo uscita già utilizzato con dati diversi.");
            ExitResult(previous,result);await ApplyTariffLabels(db,result.Conto,ct);return;
        }
        var active=await db.Schede.Include(x=>x.SchedeConto).SingleOrDefaultAsync(x=>x.Id==request.SchedaId,ct)
            ??throw new InvalidOperationException("La scheda è già uscita o non è più disponibile.");
        if(active.CheckoutTime!=DateTime.MaxValue||active.Blocco)throw new InvalidOperationException("La scheda è bloccata o già chiusa.");
        if(ExitVersion(active)!=request.VersioneConto)throw new InvalidOperationException("Il conto è cambiato. Aggiorna il riepilogo prima di confermare l'uscita.");
        var unpaid=active.SchedeConto.Where(x=>!x.Pagato).ToArray();
        if(unpaid.Any(x=>decimal.Round(x.VoicePrice,2)!=x.VoicePrice))throw new InvalidOperationException("Il conto contiene importi non validi.");
        var due=unpaid.Sum(x=>x.VoicePrice);
        if(due<0)throw new InvalidOperationException("Il conto prevede un rimborso: regolarizza le voci nella Scheda prima dell'uscita.");
        var day=await db.Giornate.AsNoTracking().SingleAsync(x=>x.Aperta,ct);
        var archive=new SchedaUscita{Id=active.Id,IdOperazione=request.UscitaId,VersioneConto=request.VersioneConto,PersonId=active.PersonId,Posizione=active.Posizione,NumeroTessera=active.NumeroTessera,
            Cognome=active.Cognome,Nome=active.Nome,Natoil=active.Natoil,CheckinTime=active.CheckinTime,CheckoutTime=DateTime.Now,IngressoAnnullato=active.IngressoAnnullato,IsRientro=active.IsRientro,GiornataIngressoId=active.GiornataIngressoId,
            Grb1=active.Grb1,Grb2=active.Grb2,Grb3=active.Grb3,Grb4=active.Grb4,Consumazione=active.Consumazione,Blocco=active.Blocco,Note=active.Note,
            OperatoreId=operatorId,NomeOperatore=await db.Operatori.Where(x=>x.Id==operatorId).Select(x=>x.Nome).SingleAsync(ct),PostazioneId=station.Id,NomePostazione=station.Nome,GiornataId=day.Id,
            Incassa=request.Pagato,ImportoIncassato=request.Pagato?due:0,ImportoSospeso=request.Pagato?0:due};
        var ids=active.SchedeConto.Select(x=>x.Id.ToString(CultureInfo.InvariantCulture)).ToArray();
        var posted=await db.ContabilitaMovimenti.AsNoTracking().Where(x=>x.OrigineModulo=="Cassa"&&x.OrigineEntita=="SchedaConto"&&ids.Contains(x.OrigineId!)
            &&(x.OrigineEvento=="VoceSpesa"||x.OrigineEvento=="StornoVoceSpesa"||x.OrigineEvento=="IngressoPagato")).Include(x=>x.Righe).ToArrayAsync(ct);
        var credits=new List<(PartitaContabile Credit,int Row,decimal Amount)>();
        foreach(var original in posted.Where(x=>x.OrigineEvento=="VoceSpesa"))
        {
            var sourceId=int.Parse(original.OrigineId!,CultureInfo.InvariantCulture);
            var opening=original.Righe.Single(x=>x.Dare>0);
            var credit=await db.ContabilitaPartite.AsNoTracking().Include(x=>x.RigaApertura).SingleAsync(x=>x.RigaAperturaId==opening.Id,ct);
            var settled=(await db.ContabilitaAbbinamentiPartite.Where(x=>x.PartitaId==credit.Id).Select(x=>x.Importo).ToArrayAsync(ct)).Sum();
            var amount=credit.ImportoOriginale-settled;
            if(amount<0||(amount>0&&active.SchedeConto.Single(x=>x.Id==sourceId).Pagato))throw new InvalidOperationException("Conto e credito contabile non coincidono. Verifica la Scheda.");
            if(amount>0)credits.Add((credit,sourceId,amount));
        }
        var postedIds=posted.Select(x=>x.OrigineId).ToHashSet();
        var legacy=unpaid.Where(x=>!postedIds.Contains(x.Id.ToString(CultureInfo.InvariantCulture))).ToArray();
        var legacyAmount=legacy.Sum(x=>x.VoicePrice);
        if(legacyAmount<0||credits.Sum(x=>x.Amount)+legacyAmount!=due)throw new InvalidOperationException("Conto e partite non sono riconciliati. Regolarizza gli storni prima dell'uscita.");
        foreach(var item in credits)
        {
            if(request.Pagato)await ReceiveExitCredit(db,operatorId,station,archive,item.Credit,item.Amount,ExitKey($"Uscita:{request.UscitaId}:Partita:{item.Credit.Id}"),"SchedaUscita",archive.Id.ToString(CultureInfo.InvariantCulture),ct);
            else db.Sospesi.Add(await ExitPending(db,archive,item.Amount,item.Credit.Id,item.Row,item.Credit.Descrizione,ct));
        }
        if(legacyAmount>0)
        {
            if(request.Pagato)await ReceiveLegacyExit(db,operatorId,station,archive,legacy.Select(x=>(x.DescSettore,x.VoicePrice)).ToArray(),legacyAmount,ExitKey($"Uscita:{request.UscitaId}:Pregresso"),"SchedaUscita",archive.Id.ToString(CultureInfo.InvariantCulture),ct);
            else db.Sospesi.Add(await ExitPending(db,archive,legacyAmount,null,null,"voci precedenti non saldate",ct));
        }
        archive.Conto=active.SchedeConto.Select(x=>new SchedaUscitaConto{Id=x.Id,IsIngresso=x.IsIngresso,SchedaId=archive.Id,DescSettore=x.DescSettore,DescPostazione=x.DescPostazione,VoiceDesc=x.VoiceDesc,VoicePrice=x.VoicePrice,
            Pagato=x.Pagato||request.Pagato,Note=x.Note,DataOra=x.DataOra}).ToList();
        db.SchedeUscite.Add(archive);db.Schede.Remove(active);
        await db.SaveChangesAsync(ct);ExitResult(archive,result);await ApplyTariffLabels(db,result.Conto,ct);
    }
    private static async Task<Sospeso> ExitPending(AppDbContext db,SchedaUscita sheet,decimal amount,long? credit,int? row,string description,CancellationToken ct)=>new()
    {
        IdOperazione=ExitKey($"Uscita:{sheet.IdOperazione}:Sospeso:{credit?.ToString(CultureInfo.InvariantCulture)??"Pregresso"}"),Tipo=TipoSospeso.Credito,Origine=OrigineSospeso.Uscita,
        DataOra=sheet.CheckoutTime,DataCompetenza=(await db.Giornate.AsNoTracking().SingleAsync(x=>x.Aperta,ct)).DataInizio.Date,Importo=amount,
        Descrizione=$"Uscita posizione {sheet.Posizione} - "+description,
        Note=$"Scheda {sheet.Id}; uscita {sheet.CheckoutTime:dd/MM/yyyy HH:mm}. {sheet.Note}",PersonId=sheet.PersonId,Nominativo=$"{sheet.Cognome} {sheet.Nome}".Trim(),NumeroTessera=sheet.NumeroTessera,
        NumeroSocio=await db.Tessere.Where(x=>x.NumeroTessera==sheet.NumeroTessera).Select(x=>x.Socio!.NumeroSocio).SingleOrDefaultAsync(ct),
        SchedaId=sheet.Id,SchedaContoId=row,PartitaContabileId=credit,OrigineModulo="Cassa",OrigineEntita="SchedaUscita",OrigineId=sheet.Id.ToString(CultureInfo.InvariantCulture),
        OperatoreId=sheet.OperatoreId,NomeOperatore=sheet.NomeOperatore,PostazioneId=sheet.PostazioneId,NomePostazione=sheet.NomePostazione,GiornataId=sheet.GiornataId
    };
    private static async Task<MovimentoContabile> ExitMovement(AppDbContext db,int operatorId,Postazione station,SchedaUscita sheet,Guid key,string entity,string origin,CancellationToken ct)
    {
        var day=await db.Giornate.AsNoTracking().SingleAsync(x=>x.Aperta,ct);
        return new(){IdOperazione=key,DataOra=DateTime.Now,DataCompetenza=day.DataInizio.Date,Stato=StatoMovimentoContabile.Confermato,
            Causale=entity=="Sospeso"?"SALDO_SOSPESO_USCITA":"INCASSO_USCITA",Descrizione=$"Incasso uscita posizione {sheet.Posizione}",
            OrigineModulo="Cassa",OrigineEntita=entity,OrigineId=origin,OrigineEvento=entity=="Sospeso"?"SaldoSospesoUscita":"IncassoUscita",
            PersonId=sheet.PersonId,Nominativo=$"{sheet.Cognome} {sheet.Nome}".Trim(),NumeroTessera=sheet.NumeroTessera,
            OperatoreId=operatorId,NomeOperatore=await db.Operatori.Where(x=>x.Id==operatorId).Select(x=>x.Nome).SingleAsync(ct),PostazioneId=station.Id,NomePostazione=station.Nome,GiornataId=day.Id};
    }
    private static async Task<MovimentoContabile> ReceiveExitCredit(AppDbContext db,int operatorId,Postazione station,SchedaUscita sheet,PartitaContabile credit,decimal amount,Guid key,string entity,string origin,CancellationToken ct)
    {
        var cash=await ChargeAccount(db,"100",NaturaConto.Attivita,ct);await RequireChargeAccount(db,credit.RigaApertura.ContoId,NaturaConto.Attivita,ct);
        var movement=await ExitMovement(db,operatorId,station,sheet,key,entity,origin,ct);
        movement.Righe=[ChargeRow(credit.RigaApertura,1,cash.Id,amount,0),ChargeRow(credit.RigaApertura,2,credit.RigaApertura.ContoId,0,amount)];
        movement.VerificaQuadratura();db.ContabilitaMovimenti.Add(movement);
        db.ContabilitaAbbinamentiPartite.Add(new(){IdOperazione=key,PartitaId=credit.Id,RigaRegolamento=movement.Righe[1],Importo=amount,DataOra=movement.DataOra,Note=movement.Descrizione});
        await db.SaveChangesAsync(ct);return movement;
    }
    private static async Task<MovimentoContabile> ReceiveLegacyExit(AppDbContext db,int operatorId,Postazione station,SchedaUscita sheet,(string Sector,decimal Amount)[] rows,decimal amount,Guid key,string entity,string origin,CancellationToken ct)
    {
        if(amount<=0||rows.Sum(x=>x.Amount)!=amount)throw new InvalidOperationException("Importo delle voci precedenti non coerente.");
        var cash=await ChargeAccount(db,"100",NaturaConto.Attivita,ct);var revenue=await ChargeAccount(db,"400",NaturaConto.Ricavo,ct);
        var movement=await ExitMovement(db,operatorId,station,sheet,key,entity,origin,ct);
        movement.Righe=[new(){NumeroRiga=1,ContoId=cash.Id,Dare=amount,Descrizione=movement.Descrizione,PersonId=sheet.PersonId,NumeroTessera=sheet.NumeroTessera}];
        foreach(var group in rows.GroupBy(x=>x.Sector))
        {
            var net=group.Sum(x=>x.Amount);if(net==0)continue;
            movement.Righe.Add(new(){NumeroRiga=movement.Righe.Count+1,ContoId=revenue.Id,Avere=net>0?net:0,Dare=net<0?-net:0,NomeSettore=group.Key,
                Descrizione="Saldo voci precedenti - "+group.Key,PersonId=sheet.PersonId,NumeroTessera=sheet.NumeroTessera});
        }
        movement.VerificaQuadratura();db.ContabilitaMovimenti.Add(movement);await db.SaveChangesAsync(ct);return movement;
    }
    private static async Task<Sospeso> SettleExitPending(AppDbContext db,int operatorId,Postazione station,Sospeso pending,CancellationToken ct)
    {
        if(pending.Stato==StatoSospeso.Saldato)return pending;
        if(pending.Tipo!=TipoSospeso.Credito||pending.SchedaId==null||pending.Valuta!="EUR")throw new InvalidOperationException("Sospeso di uscita non valido.");
        var sheet=await db.SchedeUscite.Include(x=>x.Conto).SingleOrDefaultAsync(x=>x.Id==pending.SchedaId,ct)??throw new InvalidOperationException("Storico uscita non trovato.");
        if(sheet.PersonId!=pending.PersonId)throw new InvalidOperationException("Il sospeso non corrisponde al socio dell'uscita.");
        var key=ExitKey($"SaldoSospesoUscita:{pending.IdOperazione}");MovimentoContabile movement;
        if(pending.PartitaContabileId is {} creditId)
        {
            var credit=await db.ContabilitaPartite.AsNoTracking().Include(x=>x.RigaApertura).SingleAsync(x=>x.Id==creditId,ct);
            var allocated=(await db.ContabilitaAbbinamentiPartite.Where(x=>x.PartitaId==credit.Id).Select(x=>x.Importo).ToArrayAsync(ct)).Sum();
            if(credit.PersonId!=pending.PersonId||credit.ImportoOriginale-allocated!=pending.Importo)throw new InvalidOperationException("Credito contabile non coerente con il sospeso.");
            var row=sheet.Conto.SingleOrDefault(x=>x.Id==pending.SchedaContoId)??throw new InvalidOperationException("Voce del sospeso non trovata nello storico.");
            movement=await ReceiveExitCredit(db,operatorId,station,sheet,credit,pending.Importo,key,"Sospeso",pending.Id.ToString(CultureInfo.InvariantCulture),ct);row.Pagato=true;
        }
        else
        {
            var rows=new List<SchedaUscitaConto>();
            foreach(var row in sheet.Conto.Where(x=>!x.Pagato))
                if(!await db.ContabilitaMovimenti.AnyAsync(x=>x.OrigineEntita=="SchedaConto"&&x.OrigineId==row.Id.ToString()&&(x.OrigineEvento=="VoceSpesa"||x.OrigineEvento=="StornoVoceSpesa"||x.OrigineEvento=="IngressoPagato"),ct))rows.Add(row);
            movement=await ReceiveLegacyExit(db,operatorId,station,sheet,rows.Select(x=>(x.DescSettore,x.VoicePrice)).ToArray(),pending.Importo,key,"Sospeso",pending.Id.ToString(CultureInfo.InvariantCulture),ct);
            foreach(var row in rows)row.Pagato=true;
        }
        pending.Stato=StatoSospeso.Saldato;pending.DataSaldo=movement.DataOra;pending.MovimentoSaldo=movement;pending.OperatoreSaldoId=operatorId;pending.PostazioneSaldoId=station.Id;pending.GiornataSaldoId=movement.GiornataId;
        await db.SaveChangesAsync(ct);return pending;
    }
}
