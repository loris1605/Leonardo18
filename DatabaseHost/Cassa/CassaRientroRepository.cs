using AppServices.Database;
using Microsoft.EntityFrameworkCore;
using Models.Context;
using Models.Tables;

namespace Leonardo18.DatabaseHost.Cassa;

public sealed partial class CassaDatabaseRepository
{
    private static async Task<CassaRientro?> FindReentry(AppDbContext db,Postazione station,CassaSocio? member,CancellationToken ct)
    {
        if(member is null||!string.IsNullOrEmpty(member.PosizioneOccupata))return null;
        var type=await db.TipiRientro.AsNoTracking().SingleAsync(x=>x.Id==station.TipoRientroId,ct);
        if(type.Id==-1||(type.Id!=-2&&type.DurataOre<=0))return null;
        var last=await db.SchedeUscite.AsNoTracking().Where(x=>x.PersonId==member.PersonId&&!x.IsRientro&&!x.IngressoAnnullato)
            .OrderByDescending(x=>x.CheckinTime).ThenByDescending(x=>x.Id)
            .Select(x=>new{x.CheckinTime,x.GiornataIngressoId}).FirstOrDefaultAsync(ct);
        if(last is null)return null;
        var day=await db.Giornate.AsNoTracking().SingleAsync(x=>x.Aperta,ct);
        if(!CassaRientroRules.Disponibile(type.Id,type.DurataOre,last.CheckinTime,last.GiornataIngressoId,day.Id,day.DataInizio,DateTime.Now))return null;
        DateTime? expiry=null;
        if(type.Id!=-2)expiry=type.DurataOre>(DateTime.MaxValue-last.CheckinTime).TotalHours?DateTime.MaxValue:last.CheckinTime.AddHours(type.DurataOre);
        return new(){Tipo=type.Nome,UltimoIngresso=last.CheckinTime,ValidoFino=expiry,GiornataId=day.Id};
    }
    private static async Task LoadMemberEntryOptions(AppDbContext db,Postazione station,CassaResult result,CancellationToken ct)
    {
        if(result.Socio is null || result.Socio.SocioBloccato)return;
        await LoadFidelityOptions(db,result,ct);
        result.Rientro=await FindReentry(db,station,result.Socio,ct);
        result.AbbonamentiIngresso=result.Rientro is null?await LoadEntrySubscriptions(db,result.Socio.PersonId,ct):[];
    }
}
