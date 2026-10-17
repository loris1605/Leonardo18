using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Models.Context;
using Models.Contabilita;
using Models.Tables;

namespace Leonardo18.DatabaseHost.Cassa;

public sealed partial class CassaDatabaseRepository
{
    private static Guid ChargeKey(int sourceId,string action,int sequence=0) => new(SHA256.HashData(
        Encoding.UTF8.GetBytes(FormattableString.Invariant($"Cassa:SchedaConto:{sourceId}:{action}:{sequence}")))[..16]);

    private static async Task<ContoContabile> ChargeAccount(AppDbContext db,string code,NaturaConto nature,CancellationToken ct)
    {
        var account=await db.ContabilitaPianoConti.AsNoTracking().SingleOrDefaultAsync(x=>x.Codice==code,ct);
        if(account is null||!account.Attivo||!account.Movimentabile||account.Natura!=nature)
            throw new InvalidOperationException($"Per contabilizzare la voce di spesa serve il conto {code} attivo, movimentabile e con natura corretta.");
        return account;
    }

    private static async Task RequireChargeAccount(AppDbContext db,int id,NaturaConto nature,CancellationToken ct)
    {
        if(!await db.ContabilitaPianoConti.AnyAsync(x=>x.Id==id&&x.Attivo&&x.Movimentabile&&x.Natura==nature,ct))
            throw new InvalidOperationException("Il conto della scrittura originale non è disponibile per la rettifica contabile.");
    }

    private static RigaContabile ChargeRow(RigaContabile template,int number,int account,decimal debit,decimal credit) => new()
    {
        NumeroRiga=number, ContoId=account, Dare=debit, Avere=credit, Descrizione=template.Descrizione,
        PersonId=template.PersonId, Nominativo=template.Nominativo, NumeroTessera=template.NumeroTessera,
        SettoreId=template.SettoreId, NomeSettore=template.NomeSettore,
        TariffaId=template.TariffaId, EtichettaTariffa=template.EtichettaTariffa
    };

    private static async Task<MovimentoContabile> ChargeHeader(AppDbContext db,int operatorId,Postazione station,
        SchedaConto source,string action,string description,Guid key,MovimentoContabile? basis,CancellationToken ct)
    {
        var sheet=await db.Schede.AsNoTracking().SingleAsync(x=>x.Id==source.SchedaId,ct);
        var day=await db.Giornate.AsNoTracking().SingleAsync(x=>x.Aperta,ct);
        return new()
        {
            IdOperazione=key, DataOra=action=="VoceSpesa"?source.DataOra:DateTime.Now,
            DataCompetenza=day.DataInizio.Date, Stato=StatoMovimentoContabile.Confermato,
            Valuta=basis?.Valuta??"EUR", Causale=action switch
            {
                "VoceSpesa"=>"VOCE_SPESA", "PagamentoVoceSpesa"=>"INCASSO_VOCE_SPESA",
                "AnnullamentoPagamentoVoceSpesa"=>"ANNULLAMENTO_INCASSO", _=>"STORNO_VOCE_SPESA"
            },
            Descrizione=description, Note=$"Posizione: {sheet.Posizione}. {source.Note}".Trim(),
            OrigineModulo="Cassa", OrigineEntita="SchedaConto", OrigineId=source.Id.ToString(CultureInfo.InvariantCulture),
            OrigineEvento=action, PersonId=basis?.PersonId??sheet.PersonId,
            Nominativo=basis?.Nominativo??$"{sheet.Cognome} {sheet.Nome}".Trim(),
            NumeroTessera=basis?.NumeroTessera??sheet.NumeroTessera,
            NumeroSocio=basis is not null?basis.NumeroSocio:await db.Tessere.Where(x=>x.NumeroTessera==sheet.NumeroTessera).Select(x=>x.Socio!.NumeroSocio).SingleOrDefaultAsync(ct),
            OperatoreId=operatorId, NomeOperatore=await db.Operatori.Where(x=>x.Id==operatorId).Select(x=>x.Nome).SingleAsync(ct),
            PostazioneId=station.Id, NomePostazione=station.Nome, GiornataId=day.Id
        };
    }

    private static async Task PostCharge(AppDbContext db,int operatorId,Postazione station,SchedaConto source,Listino item,CancellationToken ct)
    {
        if(source.VoicePrice==0)return;
        if(source.VoicePrice<0||decimal.Round(source.VoicePrice,2)!=source.VoicePrice)
            throw new InvalidOperationException("Importo della voce di spesa non valido per la contabilità.");
        var credit=await ChargeAccount(db,"120",NaturaConto.Attivita,ct);
        var revenue=await ChargeAccount(db,"400",NaturaConto.Ricavo,ct);
        var movement=await ChargeHeader(db,operatorId,station,source,"VoceSpesa","Voce di spesa - "+source.VoiceDesc,ChargeKey(source.Id,"VoceSpesa"),null,ct);
        var details=new RigaContabile
        {
            Descrizione=source.VoiceDesc,PersonId=movement.PersonId,Nominativo=movement.Nominativo,
            NumeroTessera=movement.NumeroTessera,SettoreId=item.SettoreId,NomeSettore=item.Settore!.Nome,
            TariffaId=item.TariffaId,EtichettaTariffa=item.Tariffa!.Label
        };
        movement.Righe=new(){ChargeRow(details,1,credit.Id,source.VoicePrice,0),ChargeRow(details,2,revenue.Id,0,source.VoicePrice)};
        movement.VerificaQuadratura();db.ContabilitaMovimenti.Add(movement);
        db.ContabilitaPartite.Add(new()
        {
            RigaApertura=movement.Righe[0],Tipo=TipoPartitaContabile.Credito,PersonId=movement.PersonId,
            Controparte=movement.Nominativo??"",Descrizione=source.VoiceDesc,
            DataApertura=movement.DataOra,ImportoOriginale=source.VoicePrice
        });
        await db.SaveChangesAsync(ct);
    }

    private sealed record ChargeState(MovimentoContabile Original,PartitaContabile Partita,
        List<AbbinamentoPartitaContabile> Allocations)
    {
        public decimal Residuo=>Partita.ImportoOriginale-Allocations.Sum(x=>x.Importo);
        public AbbinamentoPartitaContabile? ActivePayment=>Allocations.SingleOrDefault(x=>x.Importo>0&&!Allocations.Any(y=>y.StornoDiId==x.Id));
        public RigaContabile CreditRow=>Original.Righe.Single(x=>x.Dare>0);
        public RigaContabile RevenueRow=>Original.Righe.Single(x=>x.Avere>0);
    }

    private static async Task<ChargeState?> FindChargeState(AppDbContext db,SchedaConto source,CancellationToken ct)
    {
        var key=ChargeKey(source.Id,"VoceSpesa");
        var original=await db.ContabilitaMovimenti.AsNoTracking().Include(x=>x.Righe).SingleOrDefaultAsync(x=>x.IdOperazione==key,ct);
        if(original is null)
        {
            var sourceId=source.Id.ToString(CultureInfo.InvariantCulture);
            if(await db.ContabilitaMovimenti.AnyAsync(x=>x.OrigineModulo=="Cassa"&&x.OrigineEntita=="SchedaConto"&&x.OrigineId==sourceId&&x.OrigineEvento=="StornoVoceSpesa",ct))
                throw new InvalidOperationException("La voce è uno storno già contabilizzato: il pagamento non è modificabile.");
            return null; // Existing operational rows are not retroactively posted.
        }
        if(await db.ContabilitaMovimenti.AnyAsync(x=>x.StornoDiId==original.Id,ct))
            throw new InvalidOperationException("La voce di spesa è già stata stornata.");
        var opening=original.Righe.Single(x=>x.Dare>0);
        var partita=await db.ContabilitaPartite.AsNoTracking().SingleAsync(x=>x.RigaAperturaId==opening.Id,ct);
        if(source.VoicePrice<=0||partita.ImportoOriginale!=source.VoicePrice||opening.Dare!=source.VoicePrice)
            throw new InvalidOperationException("La voce di spesa non coincide con la registrazione contabile originale.");
        var allocations=await db.ContabilitaAbbinamentiPartite.AsNoTrackingWithIdentityResolution().Where(x=>x.PartitaId==partita.Id)
            .Include(x=>x.RigaRegolamento).ThenInclude(x=>x.Movimento).ThenInclude(x=>x.Righe).ToListAsync(ct);
        var state=new ChargeState(original,partita,allocations);
        if(source.Pagato?(state.Residuo!=0||state.ActivePayment is null):(state.Residuo!=source.VoicePrice||state.ActivePayment is not null))
            throw new InvalidOperationException("La partita ha regolamenti diversi dal pagamento completo previsto dalla scheda.");
        return state;
    }

    private static async Task PostChargePayment(AppDbContext db,int operatorId,Postazione station,SchedaConto source,bool paid,CancellationToken ct)
    {
        var state=await FindChargeState(db,source,ct);if(state is null)return;
        await RequireChargeAccount(db,state.CreditRow.ContoId,NaturaConto.Attivita,ct);
        var sequence=await db.ContabilitaMovimenti.CountAsync(x=>x.OrigineModulo=="Cassa"&&x.OrigineEntita=="SchedaConto"&&x.OrigineId==source.Id.ToString(),ct);
        var action=paid?"PagamentoVoceSpesa":"AnnullamentoPagamentoVoceSpesa";
        var movement=await ChargeHeader(db,operatorId,station,source,action,(paid?"Incasso - ":"Annullamento incasso - ")+state.RevenueRow.Descrizione,ChargeKey(source.Id,action,sequence),state.Original,ct);
        var allocation=new AbbinamentoPartitaContabile
        {
            IdOperazione=movement.IdOperazione,PartitaId=state.Partita.Id,DataOra=movement.DataOra,
            Importo=paid?source.VoicePrice:-source.VoicePrice,Note=movement.Descrizione
        };
        if(paid)
        {
            var cash=await ChargeAccount(db,"100",NaturaConto.Attivita,ct);
            movement.Righe=new(){ChargeRow(state.CreditRow,1,cash.Id,source.VoicePrice,0),ChargeRow(state.CreditRow,2,state.CreditRow.ContoId,0,source.VoicePrice)};
            allocation.RigaRegolamento=movement.Righe[1];
        }
        else
        {
            var payment=state.ActivePayment!;
            var cash=payment.RigaRegolamento.Movimento.Righe.Single(x=>x.Dare>0).ContoId;
            await RequireChargeAccount(db,cash,NaturaConto.Attivita,ct);
            movement.StornoDiId=payment.RigaRegolamento.MovimentoId;
            movement.Righe=new(){ChargeRow(state.CreditRow,1,state.CreditRow.ContoId,source.VoicePrice,0),ChargeRow(state.CreditRow,2,cash,0,source.VoicePrice)};
            allocation.StornoDiId=payment.Id;allocation.RigaRegolamento=movement.Righe[0];
        }
        movement.VerificaQuadratura();db.ContabilitaMovimenti.Add(movement);
        db.ContabilitaAbbinamentiPartite.Add(allocation);
        await db.SaveChangesAsync(ct);
    }

    private static async Task PostChargeReversal(AppDbContext db,int operatorId,Postazione station,SchedaConto source,SchedaConto reversal,CancellationToken ct)
    {
        var state=await FindChargeState(db,source,ct);if(state is null)return;
        await RequireChargeAccount(db,state.RevenueRow.ContoId,NaturaConto.Ricavo,ct);
        var contra=state.CreditRow.ContoId;
        if(source.Pagato)contra=state.ActivePayment!.RigaRegolamento.Movimento.Righe.Single(x=>x.Dare>0).ContoId;
        await RequireChargeAccount(db,contra,NaturaConto.Attivita,ct);
        var movement=await ChargeHeader(db,operatorId,station,reversal,"StornoVoceSpesa",reversal.VoiceDesc,ChargeKey(reversal.Id,"StornoVoceSpesa"),state.Original,ct);
        movement.StornoDiId=state.Original.Id;
        movement.Righe=new(){ChargeRow(state.RevenueRow,1,state.RevenueRow.ContoId,source.VoicePrice,0),ChargeRow(state.CreditRow,2,contra,0,source.VoicePrice)};
        movement.VerificaQuadratura();db.ContabilitaMovimenti.Add(movement);
        if(!source.Pagato)
            db.ContabilitaAbbinamentiPartite.Add(new()
            {
                IdOperazione=movement.IdOperazione,PartitaId=state.Partita.Id,RigaRegolamento=movement.Righe[1],
                DataOra=movement.DataOra,Importo=source.VoicePrice,Note=movement.Descrizione
            });
        await db.SaveChangesAsync(ct);
    }
}
