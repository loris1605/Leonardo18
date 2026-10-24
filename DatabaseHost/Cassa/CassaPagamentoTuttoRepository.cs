using System.Globalization;
using AppServices.Database;
using Microsoft.EntityFrameworkCore;
using Models.Context;
using Models.Tables;
using Models.Contabilita;
namespace Leonardo18.DatabaseHost.Cassa;
public sealed partial class CassaDatabaseRepository
{
    private static async Task PayAllAccount(AppDbContext db,int operatorId,Postazione station,CassaRequest request,CassaScheda sheet,CancellationToken ct)
    {
        if(request.SchedaId!=sheet.Id)throw new InvalidOperationException("La scheda della posizione è cambiata. Riapri la scheda.");
        if(sheet.Blocco||sheet.CheckoutTime!=DateTime.MaxValue)throw new InvalidOperationException("La scheda è bloccata o chiusa: pagamento non consentito.");
        var rows=await db.SchedeConto.Where(x=>x.SchedaId==sheet.Id).OrderBy(x=>x.Id).ToArrayAsync(ct);
        if(request.ContoIds.Distinct().Count()!=request.ContoIds.Length||!rows.Select(x=>x.Id).ToHashSet().SetEquals(request.ContoIds))
            throw new InvalidOperationException("Il conto è cambiato. Riapri la scheda prima di pagare tutte le voci.");
        var unpaid=rows.Where(x=>!x.Pagato).ToArray();
        if(unpaid.Length==0)return;
        var due=unpaid.Sum(x=>x.VoicePrice);
        if(due!=request.PrezzoConfermato)throw new InvalidOperationException("Il totale da pagare è cambiato. Riapri la scheda prima di confermare.");
        if(due<0||unpaid.Any(x=>decimal.Round(x.VoicePrice,2)!=x.VoicePrice))
            throw new InvalidOperationException("Il conto prevede un rimborso o importi non validi. Regolarizza le voci prima di pagare tutto.");
        var ids=rows.Select(x=>x.Id.ToString(CultureInfo.InvariantCulture)).ToArray();
        var posted=await db.ContabilitaMovimenti.AsNoTracking().Where(x=>x.OrigineModulo=="Cassa"&&x.OrigineEntita=="SchedaConto"&&ids.Contains(x.OrigineId!)).ToArrayAsync(ct);
        var voided=new HashSet<int>();
        foreach(var reversal in posted.Where(x=>x.StornoDiId!=null&&x.OrigineEvento is "StornoVoceSpesa" or "StornoIngresso"))
        {
            var original=posted.SingleOrDefault(x=>x.Id==reversal.StornoDiId);
            if(original is null)continue;
            var source=rows.Single(x=>x.Id.ToString(CultureInfo.InvariantCulture)==original.OrigineId);
            var reversed=rows.Single(x=>x.Id.ToString(CultureInfo.InvariantCulture)==reversal.OrigineId);
            if(source.Pagato!=reversed.Pagato)throw new InvalidOperationException("Pagamento e storno non coincidono. Regolarizza il conto.");
            voided.Add(source.Id);voided.Add(reversed.Id);
        }
        // Historical cancellations have no journal link: match their exact operational description and amount.
        var recorded=posted.Select(x=>x.OrigineId).ToHashSet();
        foreach(var negative in unpaid.Where(x=>x.VoicePrice<0&&!voided.Contains(x.Id)))
        {
            if(recorded.Contains(negative.Id.ToString(CultureInfo.InvariantCulture)))throw new InvalidOperationException("Storno non riconciliato: regolarizza il conto.");
            var original=unpaid.FirstOrDefault(x=>x.VoicePrice==-negative.VoicePrice&&!voided.Contains(x.Id)
                &&!recorded.Contains(x.Id.ToString(CultureInfo.InvariantCulture))&&x.DescSettore==negative.DescSettore&&x.DescPostazione==negative.DescPostazione
                &&(negative.VoiceDesc=="STORNO - "+x.VoiceDesc||negative.VoiceDesc==("STORNO - NON ENTRA - "+x.VoiceDesc)[..Math.Min(50,("STORNO - NON ENTRA - "+x.VoiceDesc).Length)]));
            if(original is null)throw new InvalidOperationException("Il conto contiene uno storno da regolarizzare prima del pagamento.");
            await PostLegacyCharge(db,operatorId,station,original,ct);
            await PostChargeReversal(db,operatorId,station,original,negative,ct);
            voided.Add(original.Id);voided.Add(negative.Id);
        }
        foreach(var row in unpaid.Where(x=>!voided.Contains(x.Id)))
        {
            await ProtectPostedEntry(db,row.Id,ct);
            if(row.VoicePrice>0)
            {
                if(!posted.Any(x=>x.IdOperazione==ChargeKey(row.Id,"VoceSpesa")))
                    await PostLegacyCharge(db,operatorId,station,row,ct);
                await PostChargePayment(db,operatorId,station,row,true,ct);
            }
        }
        foreach(var row in unpaid)row.Pagato=true;
        await db.SaveChangesAsync(ct);
    }
    private static async Task PostLegacyCharge(AppDbContext db,int operatorId,Postazione station,SchedaConto source,CancellationToken ct)
    {
        var credit=await ChargeAccount(db,"120",NaturaConto.Attivita,ct);
        var revenue=await ChargeAccount(db,"400",NaturaConto.Ricavo,ct);
        var movement=await ChargeHeader(db,operatorId,station,source,"VoceSpesa","Voce pregressa - "+source.VoiceDesc,ChargeKey(source.Id,"VoceSpesa"),null,ct);
        var details=new RigaContabile{Descrizione=source.VoiceDesc,PersonId=movement.PersonId,Nominativo=movement.Nominativo,NumeroTessera=movement.NumeroTessera,NomeSettore=source.DescSettore,EtichettaTariffa=source.VoiceDesc};
        movement.Righe=[ChargeRow(details,1,credit.Id,source.VoicePrice,0),ChargeRow(details,2,revenue.Id,0,source.VoicePrice)];
        movement.VerificaQuadratura();db.ContabilitaMovimenti.Add(movement);
        db.ContabilitaPartite.Add(new(){RigaApertura=movement.Righe[0],Tipo=TipoPartitaContabile.Credito,PersonId=movement.PersonId,Controparte=movement.Nominativo??"",Descrizione=source.VoiceDesc,DataApertura=movement.DataOra,ImportoOriginale=source.VoicePrice});
        await db.SaveChangesAsync(ct);
    }
}
