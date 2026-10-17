using System.Data;
using AppServices.Database;
using Microsoft.EntityFrameworkCore;
using Models.Context;
using Models.Tables;

namespace Leonardo18.DatabaseHost.Cassa;

/// <summary>Cassa queries and entry writes migrated from Leonardo17. EF runs only in the managed host.</summary>
public sealed partial class CassaDatabaseRepository(Func<AppDbContext> contextFactory)
{
    public async Task<CassaResult> Execute(int operatorId, CassaRequest request, CancellationToken ct = default)
    {
        await using var db = contextFactory();
        // Hold station/day checks and entry collision checks together with the write.
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (!await db.Operatori.AnyAsync(x => x.Id == operatorId && x.Abilitato, ct)
            || !await db.Permessi.AnyAsync(x => x.OperatoreId == operatorId && x.PostazioneId == request.PostazioneId
                && x.Postazione!.TipoPostazioneId == 2, ct))
            throw new InvalidOperationException("Non hai il permesso di accedere a questa postazione di cassa.");
        if (await db.Giornate.CountAsync(x => x.Aperta, ct) != 1)
            throw new InvalidOperationException("La cassa richiede una sola giornata contabile aperta. Torna al menu.");
        var station = await db.Postazioni.AsNoTracking().SingleAsync(x => x.Id == request.PostazioneId, ct);
        var result = new CassaResult { PostazioneName = station.Nome };
        switch (request.Action)
        {
            case CassaAction.Load:
                result.AbbonamentiDisponibili=(await SellableSubscriptions(db,ct)).Length>0;
                break;
            case CassaAction.LoadFidelityHistory:
                if(string.IsNullOrWhiteSpace(request.NumeroTessera))throw new InvalidOperationException("Inserisci la tessera del socio.");
                result.Socio=await FindCard(db,request.NumeroTessera.Trim(),ct) ?? throw new InvalidOperationException("Socio non trovato.");
                result.Fidelity=await ReadFidelity(db,result.Socio.PersonId,ct);
                if(result.Fidelity is {} fidelity)
                {
                    var entries=await db.FidelityEntries.AsNoTracking().Where(x=>x.FidelityId==fidelity.Id).ToArrayAsync(ct);
                    result.MovimentiFidelity=entries.OrderByDescending(x=>x.RegistratoIl?.ToString("yyyyMMddHHmmss")??x.DataOra.ToString("D8")+"000000")
                        .ThenByDescending(x=>x.Id).Select(x=>new CassaMovimentoFidelity{Id=x.Id,Ciclo=x.Ciclo,TipoMovimento=x.TipoMovimento,DataOra=x.RegistratoIl,DataStorica=x.DataOra,Punti=x.Value,Importo=x.Importo}).ToArray();
                }
                break;
            case CassaAction.ActivateFidelity:
                await ActivateFidelity(db,request,result,ct);await LoadMemberEntryOptions(db,station,result,ct);
                break;
            case CassaAction.PrepareAbbonamento:
                await ImportSwipes(db, ct);
                result.TipiAbbonamento=await SellableSubscriptions(db,ct);
                result.AbbonamentiDisponibili=result.TipiAbbonamento.Length>0;
                break;
            case CassaAction.PrepareUscita:
                await PrepareExit(db,request,result,ct);
                break;
            case CassaAction.RegisterUscita:
                await RegisterExit(db,operatorId,station,request,result,ct);
                break;
            case CassaAction.LoadSospesi:
                await LoadPending(db, request, result, ct);
                break;
            case CassaAction.SaldaSospeso:
                var settled=await SettlePending(db, operatorId, station, request, ct);
                var summaries=new List<CassaSocio>();
                foreach(var card in new[]{settled.NumeroTessera,settled.NumeroTessera2}.Where(x=>!string.IsNullOrEmpty(x)).Distinct())
                    if(await FindCard(db,card!,ct) is {} summary)summaries.Add(summary);
                result.SociConSospesi=summaries.ToArray();
                result.SospesoSaldatoId=request.SospesoId;
                await LoadPending(db, request, result, ct);
                await LoadMemberEntryOptions(db,station,result,ct);
                break;
            case CassaAction.BuyAbbonamento:
                result.AcquistoAbbonamento=await BuyAbbonamento(db,operatorId,station,request,ct);
                break;
            case CassaAction.OpenSheet:
            case CassaAction.AddCharge:
            case CassaAction.SaveGuardaroba:
            case CassaAction.SetPagamento:
            case CassaAction.StornaConto:
                if (string.IsNullOrWhiteSpace(request.Posizione)) throw new InvalidOperationException("Inserisci la posizione.");
                result.Scheda = await db.Schede.AsNoTracking().Where(x => x.Posizione == request.Posizione.Trim())
                    .Select(x => ToSheet(x)).SingleOrDefaultAsync(ct);
                if (result.Scheda is null) throw new InvalidOperationException("Nessuna scheda trovata per la posizione specificata.");
                if (request.Action == CassaAction.StornaConto)
                {
                    if (request.SchedaId != result.Scheda.Id)
                        throw new InvalidOperationException("La scheda della posizione è cambiata. Riapri la scheda.");
                    if (result.Scheda.Blocco || result.Scheda.CheckoutTime != DateTime.MaxValue)
                        throw new InvalidOperationException("La scheda è bloccata o chiusa: storno non consentito.");
                    var source = await db.SchedeConto.AsNoTracking().SingleOrDefaultAsync(
                        x => x.Id == request.ContoId && x.SchedaId == request.SchedaId, ct)
                        ?? throw new InvalidOperationException("La voce di spesa non appartiene alla scheda aperta o non è più presente.");
                    if(source.IsIngresso&&await db.Schede.AnyAsync(x=>x.Id==source.SchedaId&&x.IngressoAnnullato,ct))throw new InvalidOperationException("Ingresso già annullato: NON ENTRA.");
                    if(source.IsIngresso&&await CompletePreviousEntryReversal(db,source,ct))result.Scheda.IngressoAnnullato=true;
                    else if (source.IsIngresso||source.VoicePrice > 0||source.VoicePrice==0&&await HasUnrefundedFidelityDiscount(db,source.Id,ct))
                    {
                        await ProtectPostedEntry(db, source.Id, ct, allowEntryReversal:true);
                        var description=(source.IsIngresso?"STORNO - NON ENTRA - ":"STORNO - ") + source.VoiceDesc;
                        if(source.IsIngresso&&description.Length>50)description=description[..50];
                        if (description.Length > 50) throw new InvalidOperationException("Descrizione dello storno troppo lunga.");
                        var reversalLine = new SchedaConto {
                            SchedaId=source.SchedaId, DescSettore=source.DescSettore, DescPostazione=source.DescPostazione,
                            VoiceDesc=description, VoicePrice=-source.VoicePrice, Pagato=source.Pagato,
                            Note=source.Note, DataOra=DateTime.Now
                        };
                        db.SchedeConto.Add(reversalLine);
                        await db.SaveChangesAsync(ct);
                        if(source.IsIngresso){await AnnullaIngresso(db,source,ct);result.Scheda.IngressoAnnullato=true;}
                        await PostPaidEntryReversal(db,operatorId,station,source,reversalLine,ct);
                        await PostChargeReversal(db, operatorId, station, source, reversalLine, ct);
                        await RefundFidelityDiscount(db,source.Id,ct);
                    }
                }
                if (request.Action == CassaAction.SetPagamento)
                {
                    if (request.SchedaId != result.Scheda.Id)
                        throw new InvalidOperationException("La scheda della posizione è cambiata. Riapri la scheda.");
                    if (result.Scheda.Blocco || result.Scheda.CheckoutTime != DateTime.MaxValue)
                        throw new InvalidOperationException("La scheda è bloccata o chiusa: pagamento non modificabile.");
                    var accountLine = await db.SchedeConto.SingleOrDefaultAsync(
                        x => x.Id == request.ContoId && x.SchedaId == request.SchedaId, ct)
                        ?? throw new InvalidOperationException("La voce di spesa non appartiene alla scheda aperta o non è più presente.");
                    if(accountLine.Pagato != request.Pagato)
                    {
                        await ProtectPostedEntry(db, accountLine.Id, ct);
                        await PostChargePayment(db, operatorId, station, accountLine, request.Pagato, ct);
                    }
                    accountLine.Pagato=request.Pagato;
                    await db.SaveChangesAsync(ct);
                }
                if (request.Action == CassaAction.SaveGuardaroba)
                {
                    if (request.SchedaId != result.Scheda.Id)
                        throw new InvalidOperationException("La scheda della posizione è cambiata. Riapri la scheda.");
                    if (result.Scheda.Blocco || result.Scheda.CheckoutTime != DateTime.MaxValue)
                        throw new InvalidOperationException("La scheda è bloccata o chiusa: guardaroba non modificabile.");
                    if (request.Grb1 < 0 || request.Grb2 < 0 || request.Grb3 < 0 || request.Grb4 < 0)
                        throw new InvalidOperationException("I numeri guardaroba non possono essere negativi.");
                    var wardrobeSheet = await db.Schede.SingleAsync(s => s.Id == request.SchedaId, ct);
                    wardrobeSheet.Grb1=request.Grb1; wardrobeSheet.Grb2=request.Grb2;
                    wardrobeSheet.Grb3=request.Grb3; wardrobeSheet.Grb4=request.Grb4;
                    await db.SaveChangesAsync(ct);
                    result.Scheda=ToSheet(wardrobeSheet);
                    // The wardrobe control needs only these saved fields, not account rows or tariffs.
                    break;
                }
                if (request.Action == CassaAction.AddCharge)
                {
                    if (request.SchedaId != result.Scheda.Id)
                        throw new InvalidOperationException("La scheda della posizione è cambiata. Riapri la scheda.");
                    if (result.Scheda.Blocco || result.Scheda.CheckoutTime != DateTime.MaxValue)
                        throw new InvalidOperationException("La scheda è bloccata o chiusa: addebito non consentito.");
                    var item = await db.Listini.AsNoTracking().Include(l => l.Settore).Include(l => l.Tariffa)
                        .SingleOrDefaultAsync(l => l.Id == request.ListinoId && l.Settore!.TipoSettoreId == -2
                            && l.Settore.Reparti.Any(r => r.PostazioneId == station.Id), ct);
                    if (item?.Tariffa is null || item.Settore is null)
                        throw new InvalidOperationException("Voce di spesa non disponibile per questa postazione.");
                    if (item.Tariffa.Prezzo < 0 || decimal.Round(item.Tariffa.Prezzo,2) != item.Tariffa.Prezzo)
                        throw new InvalidOperationException("La voce di spesa ha un prezzo non valido.");
                    var discount=await PrepareFidelityDiscount(db,result.Scheda.PersonId,item.Tariffa.Prezzo,request.UsaCreditoFidelity,ct);
                    var chargeLine = new SchedaConto {
                        SchedaId=result.Scheda.Id, DescPostazione=station.Nome, DescSettore=item.Settore.Nome,
                        VoiceDesc=item.Tariffa.Label, VoicePrice=item.Tariffa.Prezzo-discount.Discount, Note=discount.Discount>0?$"Sconto Fidelity {discount.Discount:N2} €":"", Pagato=false, DataOra=DateTime.Now
                    };
                    db.SchedeConto.Add(chargeLine);
                    await db.SaveChangesAsync(ct);
                    await PostCharge(db, operatorId, station, chargeLine, item, ct);
                    RecordFidelityDiscount(db,discount,chargeLine);await db.SaveChangesAsync(ct);
                }
                if (request.Action is not (CassaAction.SetPagamento or CassaAction.StornaConto))
                result.VociSpesa = await db.Listini.AsNoTracking()
                    .Where(l => l.Settore!.TipoSettoreId == -2 && l.Tariffa!.Prezzo >= 0
                        && l.Settore.Reparti.Any(r => r.PostazioneId == station.Id))
                    .OrderBy(l => l.Settore!.Nome).ThenBy(l => l.Tariffa!.Nome)
                    .Select(l => new CassaVoceSpesa {
                        ListinoId=l.Id, Settore=l.Settore!.Nome,
                        Descrizione=l.Tariffa!.Label == "" ? l.Tariffa.Nome : l.Tariffa.Label, Prezzo=l.Tariffa.Prezzo
                    }).ToArrayAsync(ct);
                result.Conto = await db.SchedeConto.AsNoTracking().Where(x => x.SchedaId == result.Scheda.Id)
                    .OrderBy(x => x.DataOra).ThenBy(x => x.Id).Select(x => new CassaConto {
                        Id=x.Id, IsIngresso=x.IsIngresso,IsStornata=x.IsIngresso&&x.Scheda!.IngressoAnnullato, DescPostazione=x.DescPostazione, DescSettore=x.DescSettore,
                        VoiceDesc=x.VoiceDesc, VoicePrice=x.VoicePrice, Pagato=x.Pagato, Note=x.Note, DataOra=x.DataOra,HasScontoFidelity=db.FidelityEntries.Any(f=>f.ContoId==x.Id&&f.TipoMovimento==ScontoFid)&&!db.FidelityEntries.Any(f=>f.ContoId==x.Id&&f.TipoMovimento==StornoScontoFid)
                    }).ToArrayAsync(ct);
                if (request.Action != CassaAction.SetPagamento)
                    await ApplyTariffLabels(db, result.Conto, ct);
                break;
            case CassaAction.ListInside:
                result.Schede = await db.Schede.AsNoTracking().Where(x=>!x.IngressoAnnullato).OrderBy(x => x.Posizione).Select(x => ToSheet(x)).ToArrayAsync(ct);
                break;
            case CassaAction.PrepareEntry:
                await ImportSwipes(db, ct);
                result.Ingressi = await EntryRates(db, station.Id).ToArrayAsync(ct);
                result.PosizioniOccupate = await db.Schede.AsNoTracking().Select(x => x.Posizione).ToArrayAsync(ct);
                break;
            case CassaAction.FindCard:
                result.Socio = await FindCard(db, request.NumeroTessera, ct);
                if (result.Socio is { HasSospesi:true }) await LoadPending(db, request, result, ct);
                await LoadMemberEntryOptions(db,station,result,ct);
                result.PosizioniOccupate = await db.Schede.AsNoTracking().Select(x => x.Posizione).ToArrayAsync(ct);
                break;
            case CassaAction.RegisterEntry:
                if (request.Note.Length > 50) throw new InvalidOperationException("Le note possono contenere al massimo 50 caratteri.");
                var socio = await FindCard(db, request.NumeroTessera, ct);
                var tariffa = await EntryRates(db, station.Id).SingleOrDefaultAsync(x => x.Id == request.TariffaId, ct);
                var reentry=await FindReentry(db,station,socio,ct);
                if(request.Rientro)
                {
                    if(reentry is null)throw new InvalidOperationException("Rientro non più disponibile. Identifica nuovamente il socio.");
                    if(request.TariffaId!=0||request.AbbonamentoIngressoId!=0||request.NumeroIngressoAbbonamento!=0)
                        throw new InvalidOperationException("Per questo socio è disponibile solo il rientro.");
                    tariffa=new CassaIngresso {Nome="Rientro",Etichetta="RIENTRO GRATUITO",Prezzo=0};
                }
                else if(reentry is not null)throw new InvalidOperationException("Il socio ha diritto al rientro gratuito. Identifica nuovamente il socio.");
                if(request.IngressoFidelityId!=0)
                {
                    if(request.Rientro||request.TariffaId!=0||request.AbbonamentoIngressoId!=0||request.NumeroIngressoAbbonamento!=0)
                        throw new InvalidOperationException("Seleziona solo l'ingresso Fidelity.");
                    tariffa=new CassaIngresso {Etichetta="INGRESSO FIDELITY",Prezzo=0};
                }
                AbbonamentoConto? subscriptionEntry=null;
                if(request.AbbonamentoIngressoId!=0)
                    tariffa=new CassaIngresso {Prezzo=0,Etichetta=$"Abbonamento n. {request.AbbonamentoIngressoId} - ingresso {request.NumeroIngressoAbbonamento}"};
                else if(request.NumeroIngressoAbbonamento!=0)throw new InvalidOperationException("Seleziona un abbonamento valido.");
                var occupied = await db.Schede.Select(x => x.Posizione).ToArrayAsync(ct);
                var error = CassaEntryRules.Validate(socio, request.NumeroTessera, request.Posizione, tariffa, occupied, DateTime.Today);
                if (error.Length != 0) throw new InvalidOperationException(error);
                if(request.AbbonamentoIngressoId!=0)subscriptionEntry=await ConsumeEntrySubscription(db,socio,request,ct);
                var entryDiscount=await PrepareFidelityDiscount(db,socio!.PersonId,tariffa!.Prezzo,request.UsaCreditoFidelity,ct);
                tariffa.Prezzo-=entryDiscount.Discount;
                if(tariffa!.Prezzo < 0 || decimal.Round(tariffa.Prezzo,2) != tariffa.Prezzo)
                    throw new InvalidOperationException("Importo dell'ingresso non valido per la contabilità.");
                var now = DateTime.Now;
                var sheet = new Scheda {
                    PersonId=socio!.PersonId, NumeroTessera=socio.NumeroTessera, Cognome=socio.Cognome,
                    Nome=socio.Nome, Natoil=socio.Natoil, Posizione=request.Posizione.Trim(),
                    IsRientro=request.Rientro,GiornataIngressoId=(await db.Giornate.AsNoTracking().SingleAsync(x=>x.Aperta,ct)).Id,CheckinTime=now, CheckoutTime=DateTime.MaxValue, Consumazione=0, Blocco=socio.Blocco, Note=request.Note,
                    SchedeConto=[new SchedaConto {
                        IsIngresso=true,DescSettore="Ingresso", DescPostazione=station.Nome, VoiceDesc=tariffa!.Etichetta,
                        VoicePrice=tariffa.Prezzo, Pagato=true, Note=entryDiscount.Discount>0?$"Sconto Fidelity {entryDiscount.Discount:N2} €":"Ingresso registrato", DataOra=now
                    }]
                };
                db.Schede.Add(sheet);
                await db.SaveChangesAsync(ct);
                if(request.IngressoFidelityId!=0)await ConsumeFidelityEntry(db,socio!,request.IngressoFidelityId,sheet,ct);
                await PostPaidEntry(db, operatorId, station, sheet, socio!, tariffa!, ct);
                RecordFidelityDiscount(db,entryDiscount,sheet.SchedeConto.Single(),true);
                await StampFidelity(db,sheet,ct);await db.SaveChangesAsync(ct);
                if(subscriptionEntry is not null) { subscriptionEntry.SchedaIngressoId=sheet.Id;subscriptionEntry.Note+=$"; scheda {sheet.Id}; operatore {operatorId}"; await db.SaveChangesAsync(ct); }
                result.Scheda = ToSheet(sheet);
                break;
            default: throw new InvalidOperationException("Operazione cassa non supportata.");
        }
        if(result.Scheda is not null && request.Action is CassaAction.OpenSheet or CassaAction.RegisterEntry)
        {
            var pending=await MemberPending(db,result.Scheda.PersonId,ct);
            result.Scheda.SospesiAperti=pending.Count;result.Scheda.TotaleSospesi=pending.Total;
        }
        if(result.Scheda is not null)result.Fidelity=await ReadFidelity(db,result.Scheda.PersonId,ct);
        await transaction.CommitAsync(ct);
        return result;
    }

    private static async Task<CassaTipoAbbonamento[]> SellableSubscriptions(AppDbContext db,CancellationToken ct)
    {
        var types=await db.TipiAbbonamento.AsNoTracking().Where(x=>x.Id>0&&x.Attivo&&x.NumeroIngressi>0&&x.DurataGiorni>0)
            .OrderBy(x=>x.Nome).Select(x=>new CassaTipoAbbonamento{Id=x.Id,Nome=x.Nome,NumeroIngressi=x.NumeroIngressi,DurataGiorni=x.DurataGiorni,Prezzo=x.Prezzo}).ToArrayAsync(ct);
        var maxDays=(DateTime.MaxValue.Date-DateTime.Today).Days;
        return types.Where(x=>x.Prezzo>=0&&decimal.Round(x.Prezzo,2)==x.Prezzo&&x.DurataGiorni<=maxDays).ToArray();
    }

    private static async Task ApplyTariffLabels(AppDbContext db, CassaConto[] rows, CancellationToken ct)
    {
        if (rows.Length==0) return;
        const string stornoPrefix="STORNO - ";
        var tariffs=await db.Tariffe.AsNoTracking().Select(t=>new { t.Nome,t.Label }).ToArrayAsync(ct);
        var labels=tariffs.Where(t=>!string.IsNullOrWhiteSpace(t.Label))
            .Select(t=>t.Label).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var byName=tariffs.Where(t=>!string.IsNullOrWhiteSpace(t.Label))
            .GroupBy(t=>t.Nome,StringComparer.OrdinalIgnoreCase)
            .Select(g=>new { Nome=g.Key,Labels=g.Select(t=>t.Label).Distinct(StringComparer.OrdinalIgnoreCase).ToArray() })
            .Where(g=>g.Labels.Length==1).ToDictionary(g=>g.Nome,g=>g.Labels[0],StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            var reversal=row.VoicePrice<0 && row.VoiceDesc.StartsWith(stornoPrefix,StringComparison.Ordinal);
            var description=reversal ? row.VoiceDesc[stornoPrefix.Length..] : row.VoiceDesc;
            // Preserve already stored labels and never guess when a name maps to multiple labels.
            if (!labels.Contains(description) && byName.TryGetValue(description,out var label))
                row.VoiceDesc=(reversal ? stornoPrefix : "")+label;
        }
    }

    private static IQueryable<CassaIngresso> EntryRates(AppDbContext db, int stationId) => db.Tariffe.AsNoTracking()
        .Where(t => t.Listini.Any(l => l.Settore!.TipoSettoreId == -1
            && l.Settore.Reparti.Any(r => r.PostazioneId == stationId)))
        .OrderBy(t => t.Nome).Select(t => new CassaIngresso {
            Id=t.Id, Nome=t.Nome, Etichetta=t.Label, Prezzo=t.Prezzo, FreeDrink=t.IsFreeDrink
        });

    private static async Task<CassaSocio?> FindCard(AppDbContext db, string number, CancellationToken ct)
    {
        var card = await db.Tessere.AsNoTracking().Include(t => t.Socio).ThenInclude(s => s!.Person)
            .SingleOrDefaultAsync(t => t.NumeroTessera == number.Trim(), ct);
        if (card?.Socio?.Person is not { } person) return null;
        var position = await db.Schede.AsNoTracking().Where(x => x.PersonId == person.Id)
            .Select(x => x.Posizione).SingleOrDefaultAsync(ct);
        var member = new CassaSocio {
            PersonId=person.Id, NumeroTessera=card.NumeroTessera, NumeroSocio=card.Socio.NumeroSocio,
            Cognome=person.SurName, Nome=person.FirstName, Natoil=person.Natoil,
            ScadenzaTessera=card.Scadenza, Blocco=!card.Abilitato, PosizioneOccupata=position ?? ""
        };
        var pending=await MemberPending(db,person.Id,ct);
        member.SospesiAperti=pending.Count;member.TotaleSospesi=pending.Total;
        return member;
    }

    private static CassaScheda ToSheet(Scheda x) => new() {
        Id=x.Id, IngressoAnnullato=x.IngressoAnnullato, PersonId=x.PersonId, Posizione=x.Posizione, NumeroTessera=x.NumeroTessera,
        Cognome=x.Cognome, Nome=x.Nome, Natoil=x.Natoil, CheckinTime=x.CheckinTime,
        CheckoutTime=x.CheckoutTime, Grb1=x.Grb1, Grb2=x.Grb2, Grb3=x.Grb3, Grb4=x.Grb4,
        Consumazione=x.Consumazione, Blocco=x.Blocco, Note=x.Note
    };

    private static Task ImportSwipes(AppDbContext db, CancellationToken ct) =>
        Strisciate.StrisciateProcessor.ProcessPendingAsync(db, ct);
}
