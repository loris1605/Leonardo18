using System.Data;
using AppServices.Database;
using Microsoft.EntityFrameworkCore;
using Models.Context;
using Models.Tables;
using Models.Contabilita;
using System.Text.Json;
namespace Leonardo18.DatabaseHost.Menu;

/// <summary>Queries and day writes from Leonardo17, isolated from the NativeAOT desktop.</summary>
public sealed class MenuDatabaseRepository(Func<AppDbContext> contextFactory)
{
#if DEBUG
    ~MenuDatabaseRepository() => global::Leonardo18.Diagnostics.GcDebugLog.Collected(this);
#endif

    public async Task<MenuState> Execute(int operatorId, string operation, CancellationToken ct = default, MenuAperturaRequest? apertura = null)
    {
        await using var db = contextFactory();
        // A serializable transaction prevents two clients from opening a day simultaneously.
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (!await db.Operatori.AnyAsync(x => x.Id == operatorId && x.Abilitato, ct))
            throw new InvalidOperationException("Operatore non abilitato.");
        var stations = await db.Permessi.AsNoTracking().Where(x => x.OperatoreId == operatorId)
            .Select(x => new LoginStation { Id = x.PostazioneId, Name = x.Postazione!.Nome,
                TypeId = x.Postazione.TipoPostazioneId }).Distinct().ToArrayAsync(ct);
        var days = await db.Giornate.Where(x => x.Aperta).Take(2).ToListAsync(ct);
        if (days.Count > 1) throw new InvalidOperationException("Sono presenti più giornate aperte. Verifica la sessione contabile.");
        var day = days.SingleOrDefault();
        if (operation != "menu-load" && !stations.Any(x => x.TypeId is 1 or 2))
            throw new InvalidOperationException("Non hai il permesso di gestire la giornata.");
        switch (operation)
        {
            case "menu-open":
                var tagli=CassaInizialeRules.Verifica(apertura);
                var conteggio=JsonSerializer.Serialize(tagli,DatabaseProtocolJsonContext.Default.TaglioEuroArray);
                if(day is not null)
                {
                    if(day.AperturaId!=apertura!.IdOperazione || day.ConteggioCassaIniziale!=conteggio)
                        throw new InvalidOperationException("La giornata è già aperta. Torna al menu e aggiorna la sessione.");
                    break;
                }
                if(await db.Giornate.AnyAsync(x=>x.AperturaId==apertura!.IdOperazione,ct))
                    throw new InvalidOperationException("Questa apertura appartiene a una giornata già chiusa. Aggiorna il menu.");
                var total=tagli.Sum(x=>x.Totale);
                day = new Giornata { Aperta = true, DataInizio = DateTime.Now, DataFine = DateTime.MaxValue,
                    AperturaId=apertura!.IdOperazione, CassaIniziale=total, ConteggioCassaIniziale=conteggio };
                db.Giornate.Add(day);
                await db.SaveChangesAsync(ct);
                if(total>0)
                {
                    var cash=await db.ContabilitaPianoConti.SingleOrDefaultAsync(x=>x.Codice=="100"&&x.Attivo&&x.Movimentabile&&x.Natura==NaturaConto.Attivita,ct)
                        ??throw new InvalidOperationException("Conto Cassa non disponibile.");
                    var opening=await db.ContabilitaPianoConti.SingleOrDefaultAsync(x=>x.Codice=="310",ct);
                    if(opening is null)
                    {
                        opening=new ContoContabile{Codice="310",Nome="Cassa Iniziale",Natura=NaturaConto.PatrimonioNetto};
                        db.ContabilitaPianoConti.Add(opening);
                    }
                    else if(!opening.Attivo||!opening.Movimentabile||opening.Natura!=NaturaConto.PatrimonioNetto||opening.Nome!="Cassa Iniziale")
                        throw new InvalidOperationException("Il conto patrimoniale 310 deve essere attivo, movimentabile e denominato Cassa Iniziale.");
                    var movement=new MovimentoContabile{
                        IdOperazione=apertura.IdOperazione, DataOra=day.DataInizio,DataCompetenza=day.DataInizio.Date,
                        Stato=StatoMovimentoContabile.Confermato,Causale="Cassa Iniziale",Descrizione="Fondo cassa all'apertura della giornata",
                        OrigineModulo="Menu",OrigineEntita="Giornata",OrigineId=day.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        OrigineEvento="CassaIniziale",GiornataId=day.Id,OperatoreId=operatorId,
                        NomeOperatore=await db.Operatori.Where(x=>x.Id==operatorId).Select(x=>x.Nome).SingleAsync(ct),
                        Righe=[new(){NumeroRiga=1,Conto=cash,Dare=total,Descrizione="Cassa Iniziale"},
                               new(){NumeroRiga=2,Conto=opening,Avere=total,Descrizione="Cassa Iniziale"}]
                    };
                    movement.VerificaQuadratura();db.ContabilitaMovimenti.Add(movement);await db.SaveChangesAsync(ct);
                }
                break;
            case "menu-close":
                if (day is not null)
                {
                    day.Aperta = false; day.DataFine = DateTime.Now;
                    await db.SaveChangesAsync(ct);
                    day = null;
                }
                break;
            case "menu-load": break;
            default: throw new InvalidOperationException("Operazione menu non supportata.");
        }
        await transaction.CommitAsync(ct);
        return new MenuState { Stations = stations,
            Day = day is null ? null : new LoginDay { Id = day.Id, Start = day.DataInizio, End = day.DataFine, Open = day.Aperta } };
    }
}