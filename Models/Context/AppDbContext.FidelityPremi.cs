using Microsoft.EntityFrameworkCore;
using Models.Tables;
namespace Models.Context;
public partial class AppDbContext
{
    private static void FidelityPremiConfig(ModelBuilder b)
    {
        b.Entity<TipoFidelity>().Property(x=>x.ValorePremio).HasPrecision(18,2).HasColumnType("decimal(18,2)");
        b.Entity<Fidelity>().Property(x=>x.ValorePremio).HasPrecision(18,2).HasColumnType("decimal(18,2)");
        var entry=b.Entity<FidelityConto>();entry.HasIndex(x=>x.FidelityId);
        entry.Property(x=>x.Importo).HasPrecision(18,2).HasColumnType("decimal(18,2)");
        entry.HasIndex(x=>new{x.FidelityId,x.SchedaId,x.TipoMovimento}).IsUnique()
            .HasFilter("[SchedaId] IS NOT NULL AND [TipoMovimento] IN (1,2,3)");
        entry.HasIndex(x=>new{x.FidelityId,x.ContoId,x.TipoMovimento}).IsUnique().HasFilter("[ContoId] IS NOT NULL");
    }
}
