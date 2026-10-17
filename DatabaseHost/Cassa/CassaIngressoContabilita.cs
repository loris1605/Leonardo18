using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using AppServices.Database;
using Microsoft.EntityFrameworkCore;
using Models.Context;
using Models.Contabilita;
using Models.Tables;

namespace Leonardo18.DatabaseHost.Cassa;

public sealed partial class CassaDatabaseRepository
{
    private static Guid EntryAccountingKey(int accountLineId) => new(SHA256.HashData(
        Encoding.UTF8.GetBytes($"Cassa:SchedaConto:{accountLineId.ToString(CultureInfo.InvariantCulture)}:IngressoPagato"))[..16]);

    private static async Task PostPaidEntry(AppDbContext db, int operatorId, Postazione station,
        Scheda sheet, CassaSocio socio, CassaIngresso tariffa, CancellationToken ct)
    {
        var source=sheet.SchedeConto.Single();
        // Free entry remains a paid operational line, without a zero-value journal entry.
        if(source.VoicePrice==0) return;
        if(source.VoicePrice<0 || decimal.Round(source.VoicePrice,2)!=source.VoicePrice)
            throw new InvalidOperationException("Importo dell'ingresso non valido per la contabilità.");
        var accounts=await db.ContabilitaPianoConti.AsNoTracking()
            .Where(x=>x.Codice=="100"||x.Codice=="400").ToArrayAsync(ct);
        var cash=accounts.SingleOrDefault(x=>x.Codice=="100"&&x.Attivo&&x.Movimentabile&&x.Natura==NaturaConto.Attivita);
        var revenue=accounts.SingleOrDefault(x=>x.Codice=="400"&&x.Attivo&&x.Movimentabile&&x.Natura==NaturaConto.Ricavo);
        if(cash is null||revenue is null)
            throw new InvalidOperationException("Per contabilizzare l'ingresso servono i conti 100 Cassa e 400 Ricavi attivi, movimentabili e con natura corretta.");
        var day=await db.Giornate.AsNoTracking().SingleAsync(x=>x.Aperta,ct);
        // Entry selection is per tariff; use the first assigned entry sector if more than one offers it.
        var sector=await db.Listini.AsNoTracking().Where(x=>x.TariffaId==tariffa.Id
                &&x.Settore!.TipoSettoreId==-1&&x.Settore.Reparti.Any(r=>r.PostazioneId==station.Id))
            .OrderBy(x=>x.SettoreId).Select(x=>x.Settore!).FirstAsync(ct);
        var movement=new MovimentoContabile
        {
            IdOperazione=EntryAccountingKey(source.Id), DataOra=source.DataOra,
            DataCompetenza=day.DataInizio.Date, Stato=StatoMovimentoContabile.Confermato,
            Causale="INGRESSO_PAGATO", Descrizione="Ingresso pagato - "+source.VoiceDesc,
            Note=$"Posizione: {sheet.Posizione}. {sheet.Note}".Trim(),
            OrigineModulo="Cassa", OrigineEntita="SchedaConto", OrigineId=source.Id.ToString(CultureInfo.InvariantCulture),
            OrigineEvento="IngressoPagato", PersonId=socio.PersonId, Nominativo=socio.Anagrafica,
            NumeroSocio=socio.NumeroSocio, NumeroTessera=socio.NumeroTessera,
            OperatoreId=operatorId, NomeOperatore=await db.Operatori.Where(x=>x.Id==operatorId).Select(x=>x.Nome).SingleAsync(ct),
            PostazioneId=station.Id, NomePostazione=station.Nome, GiornataId=day.Id,
            Righe=new()
            {
                EntryLine(1,cash.Id,source.VoicePrice,0),
                EntryLine(2,revenue.Id,0,source.VoicePrice)
            }
        };
        movement.VerificaQuadratura();
        db.ContabilitaMovimenti.Add(movement);
        // Execute already holds the same serializable transaction as the saved sheet/account line.
        await db.SaveChangesAsync(ct);

        RigaContabile EntryLine(int number,int accountId,decimal debit,decimal credit) => new()
        {
            NumeroRiga=number, ContoId=accountId, Dare=debit, Avere=credit, Descrizione=source.VoiceDesc,
            PersonId=socio.PersonId, Nominativo=socio.Anagrafica, NumeroTessera=socio.NumeroTessera,
            SettoreId=sector.Id, NomeSettore=sector.Nome, TariffaId=tariffa.Id, EtichettaTariffa=tariffa.Etichetta
        };
    }

    private static async Task PostPaidEntryReversal(AppDbContext db,int operatorId,Postazione station,
        SchedaConto source,SchedaConto reversal,CancellationToken ct)
    {
        var key=EntryAccountingKey(source.Id);
        var original=await db.ContabilitaMovimenti.AsNoTracking().Include(x=>x.Righe).SingleOrDefaultAsync(x=>x.IdOperazione==key,ct);
        if(original is null)return;
        if(await db.ContabilitaMovimenti.AnyAsync(x=>x.StornoDiId==original.Id,ct))
            throw new InvalidOperationException("L'ingresso è già stato stornato.");
        if(original.Stato!=StatoMovimentoContabile.Confermato||!source.Pagato||source.VoicePrice<=0
            ||original.Righe.Count!=2||original.Righe.Sum(x=>x.Dare)!=source.VoicePrice||original.Righe.Sum(x=>x.Avere)!=source.VoicePrice)
            throw new InvalidOperationException("L'ingresso non coincide con la registrazione contabile originale.");
        var cash=original.Righe.Single(x=>x.Dare>0);
        var revenue=original.Righe.Single(x=>x.Avere>0);
        await RequireChargeAccount(db,cash.ContoId,NaturaConto.Attivita,ct);
        await RequireChargeAccount(db,revenue.ContoId,NaturaConto.Ricavo,ct);
        var movement=await ChargeHeader(db,operatorId,station,reversal,"StornoIngresso",reversal.VoiceDesc,
            ChargeKey(source.Id,"StornoIngresso"),original,ct);
        movement.Causale="STORNO_INGRESSO";movement.StornoDiId=original.Id;
        movement.Righe=original.Righe.OrderBy(x=>x.NumeroRiga)
            .Select((x,i)=>ChargeRow(x,i+1,x.ContoId,x.Avere,x.Dare)).ToList();
        movement.VerificaQuadratura();db.ContabilitaMovimenti.Add(movement);
        await db.SaveChangesAsync(ct);
    }

    private static async Task ProtectPostedEntry(AppDbContext db,int accountLineId,CancellationToken ct,bool allowEntryReversal=false)
    {
        if(await db.Sospesi.AnyAsync(x=>x.Origine==Models.Sospesi.OrigineSospeso.Abbonamento&&x.SchedaContoId==accountLineId,ct))
            throw new InvalidOperationException("Il saldo del sospeso è già registrato in contabilità. Occorre una rettifica contabile.");
        if(!allowEntryReversal&&await db.FidelityEntries.AnyAsync(x=>x.ContoId==accountLineId&&x.TipoMovimento==ScontoIngressoFid,ct))
            throw new InvalidOperationException("Lo storno di un ingresso scontato richiede una rettifica contabile.");
        var accountId=accountLineId.ToString(CultureInfo.InvariantCulture);
        if(await db.ContabilitaMovimenti.AnyAsync(x=>x.OrigineModulo=="Cassa"&&x.OrigineEntita=="SchedaConto"&&x.OrigineId==accountId&&x.OrigineEvento=="StornoIngresso",ct))
            throw new InvalidOperationException("Il pagamento di uno storno ingresso contabilizzato non è modificabile.");
        var key=EntryAccountingKey(accountLineId);
        if(!allowEntryReversal&&await db.ContabilitaMovimenti.AnyAsync(x=>x.IdOperazione==key,ct))
            throw new InvalidOperationException("L'ingresso è già registrato in contabilità. Il cambio pagamento e lo storno richiedono una rettifica contabile, non ancora disponibile.");
    }
}
