using AppServices.Database;
using Microsoft.EntityFrameworkCore;
using Models.Context;
using Models.Tables;
namespace Leonardo18.DatabaseHost.Cassa;

public sealed partial class CassaDatabaseRepository
{
    private const int TimbroFid=1,IngressoFid=2,PremioDenaroFid=3,ScontoFid=4,StornoScontoFid=5,ScontoIngressoFid=6;
    private static bool FidelityActive(Fidelity f,DateTime now)=>f.Attiva&&f.AttivataIl is {} start&&start<=now
        &&(f.ScadeIl is null||f.ScadeIl.Value.Date>=now.Date)&&f.Traguardo>0&&f.TipoPremioId is 1 or 2;
    private static async Task<CassaFidelity?> ReadFidelity(AppDbContext db,int person,CancellationToken ct)
    {
        var f=await db.Fidelities.AsNoTracking().Include(x=>x.TipoFidelity).Include(x=>x.FidelityConti).SingleOrDefaultAsync(x=>x.PersonId==person,ct);
        if(f is null)return null;
        return new(){Id=f.Id,Nome=f.TipoFidelity?.Nome??"Fidelity",Attiva=FidelityActive(f,DateTime.Now),AttivataIl=f.AttivataIl,ScadeIl=f.ScadeIl,
            Traguardo=f.Traguardo,TipoPremioId=f.TipoPremioId,
            Punti=(int)Math.Clamp(f.FidelityConti.Where(x=>x.Ciclo==f.Ciclo).Sum(x=>(long)x.Value),0,int.MaxValue),
            CreditoSconto=f.FidelityConti.Sum(x=>x.Importo),
            Timbri=f.FidelityConti.Where(x=>x.TipoMovimento==TimbroFid&&x.Value>0&&!f.FidelityConti.Any(a=>a.TipoMovimento==7&&a.SchedaId==x.SchedaId)).OrderBy(x=>x.RegistratoIl).ThenBy(x=>x.Id)
                .Select(x=>new CassaTimbroFidelity{DataOra=x.RegistratoIl,DataStorica=x.DataOra}).ToArray()};
    }
    private static async Task LoadFidelityOptions(AppDbContext db,CassaResult result,CancellationToken ct)
    {
        if(result.Socio is null)return;
        result.Fidelity=await ReadFidelity(db,result.Socio.PersonId,ct);
        result.TipiFidelity=await db.TipiFidelity.AsNoTracking().Where(x=>x.Attivo&&x.TipoFidelityInputId==1&&x.Traguardo>0&&x.DurataGG>=0
            &&(x.TipoFidelityOutputId==1||x.TipoFidelityOutputId==2&&x.ValorePremio>0)).OrderBy(x=>x.Nome)
            .Select(x=>new CassaTipoFidelity{Id=x.Id,Nome=x.Nome,Traguardo=x.Traguardo,TipoPremioId=x.TipoFidelityOutputId,ValorePremio=x.ValorePremio,DurataGiorni=x.DurataGG}).ToArrayAsync(ct);
    }
    private static async Task ActivateFidelity(AppDbContext db,CassaRequest r,CassaResult result,CancellationToken ct)
    {
        var member=await FindCard(db,r.NumeroTessera,ct);
        if(member?.SocioBloccato==true)throw new InvalidOperationException(member.AvvisoBlocco);
        if(!ValidSubscriptionCard(member)||member!.PersonId!=r.PersonId1)throw new InvalidOperationException("Identifica il socio con una tessera valida prima di attivare la Fidelity.");
        var type=await db.TipiFidelity.SingleOrDefaultAsync(x=>x.Id==r.TipoFidelityId&&x.Attivo&&x.TipoFidelityInputId==1,ct)
            ??throw new InvalidOperationException("Tipo Fidelity non disponibile.");
        if(type.Traguardo<1||type.DurataGG<0||type.TipoFidelityOutputId is not (1 or 2)||type.TipoFidelityOutputId==2&&type.ValorePremio<=0)
            throw new InvalidOperationException("Completa la configurazione Fidelity in Servizi.");
        var f=await db.Fidelities.SingleOrDefaultAsync(x=>x.PersonId==member.PersonId,ct);
        if(f is not null&&FidelityActive(f,DateTime.Now))
        {
            if(f.TipoFidelityId!=type.Id)throw new InvalidOperationException("Il socio ha già una Fidelity attiva.");
            result.Socio=member;return;
        }
        DateTime? end=null;
        if(type.DurataGG>0){try{end=DateTime.Today.AddDays(type.DurataGG);}catch(ArgumentOutOfRangeException){throw new InvalidOperationException("Durata Fidelity non valida.");}}
        if(f is null){f=new(){PersonId=member.PersonId};db.Fidelities.Add(f);}
        f.TipoFidelityId=type.Id;f.Attiva=true;f.AttivataIl=DateTime.Now;f.ScadeIl=end;f.Traguardo=type.Traguardo;
        f.TipoPremioId=type.TipoFidelityOutputId;f.ValorePremio=type.TipoFidelityOutputId==2?type.ValorePremio:0;f.Ciclo++;
        f.DataAttivazione=DateTime.Today.Year*10000+DateTime.Today.Month*100+DateTime.Today.Day;
        await db.SaveChangesAsync(ct);result.Socio=member;
    }
    private static async Task ConsumeFidelityEntry(AppDbContext db,CassaSocio member,int fidelityId,Scheda sheet,CancellationToken ct)
    {
        var f=await db.Fidelities.Include(x=>x.FidelityConti).SingleOrDefaultAsync(x=>x.Id==fidelityId&&x.PersonId==member.PersonId,ct);
        if(f is null||!FidelityActive(f,DateTime.Now)||f.TipoPremioId!=1||f.FidelityConti.Where(x=>x.Ciclo==f.Ciclo).Sum(x=>(long)x.Value)<f.Traguardo)
            throw new InvalidOperationException("Ingresso Fidelity non disponibile. Identifica nuovamente il socio.");
        db.FidelityEntries.Add(FidelityRow(f,IngressoFid,sheet.Id,-f.Traguardo));
    }
    private static FidelityConto FidelityRow(Fidelity f,int kind,int? sheet,int value=0,decimal amount=0,int? conto=null)=>new()
    {
        FidelityId=f.Id,Ciclo=f.Ciclo,TipoMovimento=kind,SchedaId=sheet,ContoId=conto,Value=value,Importo=amount,
        RegistratoIl=DateTime.Now,DataOra=DateTime.Today.Year*10000+DateTime.Today.Month*100+DateTime.Today.Day
    };
    private static async Task StampFidelity(AppDbContext db,Scheda sheet,CancellationToken ct)
    {
        if(sheet.IsRientro)return;
        var f=await db.Fidelities.Include(x=>x.FidelityConti).SingleOrDefaultAsync(x=>x.PersonId==sheet.PersonId,ct);
        if(f is null||!FidelityActive(f,sheet.CheckinTime))return;
        if(f.FidelityConti.Any(x=>x.SchedaId==sheet.Id&&x.TipoMovimento==TimbroFid))return;
        var balance=f.FidelityConti.Where(x=>x.Ciclo==f.Ciclo).Sum(x=>(long)x.Value)+1;
        var stamp=FidelityRow(f,TimbroFid,sheet.Id,1);stamp.RegistratoIl=sheet.CheckinTime;db.FidelityEntries.Add(stamp);
        if(f.TipoPremioId==2&&balance>=f.Traguardo)
            db.FidelityEntries.Add(FidelityRow(f,PremioDenaroFid,sheet.Id,-f.Traguardo,f.ValorePremio));
    }
    private static async Task<(Fidelity? Fidelity,decimal Discount)> PrepareFidelityDiscount(AppDbContext db,int person,decimal price,bool use,CancellationToken ct)
    {
        if(!use||price<=0)return(null,0);
        var f=await db.Fidelities.Include(x=>x.FidelityConti).SingleOrDefaultAsync(x=>x.PersonId==person,ct);
        var credit=f?.FidelityConti.Sum(x=>x.Importo)??0;
        if(f is null||credit<=0)throw new InvalidOperationException("Credito sconto Fidelity non disponibile. Aggiorna i dati del socio.");
        return(f,Math.Min(price,credit));
    }
    private static void RecordFidelityDiscount(AppDbContext db,(Fidelity? Fidelity,decimal Discount) discount,SchedaConto row,bool ingresso=false)
    {
        if(discount.Fidelity is {} f&&discount.Discount>0)
            db.FidelityEntries.Add(FidelityRow(f,ingresso?ScontoIngressoFid:ScontoFid,row.SchedaId,amount:-discount.Discount,conto:row.Id));
    }
    private static async Task<bool> HasUnrefundedFidelityDiscount(AppDbContext db,int conto,CancellationToken ct)=>
        await db.FidelityEntries.AnyAsync(x=>x.ContoId==conto&&x.TipoMovimento==ScontoFid,ct)
        &&!await db.FidelityEntries.AnyAsync(x=>x.ContoId==conto&&x.TipoMovimento==StornoScontoFid,ct);
    private static async Task RefundFidelityDiscount(AppDbContext db,int conto,CancellationToken ct)
    {
        if(!await HasUnrefundedFidelityDiscount(db,conto,ct))return;
        var debit=await db.FidelityEntries.SingleAsync(x=>x.ContoId==conto&&x.TipoMovimento==ScontoFid,ct);
        var f=await db.Fidelities.SingleAsync(x=>x.Id==debit.FidelityId,ct);
        db.FidelityEntries.Add(FidelityRow(f,StornoScontoFid,debit.SchedaId,amount:-debit.Importo,conto:conto));
        await db.SaveChangesAsync(ct);
    }
}
