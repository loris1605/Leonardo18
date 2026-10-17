using Microsoft.EntityFrameworkCore;
using Models.Sospesi;
using Models.Tables;
namespace Models.Context;

public partial class AppDbContext
{
    public DbSet<Sospeso> Sospesi => Set<Sospeso>();
    private static void SospesiConfig(ModelBuilder modelBuilder)
    {
        var entity=modelBuilder.Entity<Sospeso>();
        entity.ToTable("Sospesi",t=>{
            t.HasCheckConstraint("CK_Sospesi_Importo","[Importo] > 0");
            t.HasCheckConstraint("CK_Sospesi_Tipo","[Tipo] IN (1,2)");
            t.HasCheckConstraint("CK_Sospesi_Origine","[Origine] IN (1,2,3)");
            t.HasCheckConstraint("CK_Sospesi_Operazione","[IdOperazione] <> '00000000-0000-0000-0000-000000000000'");
            t.HasCheckConstraint("CK_Sospesi_Stato","([Stato] = 0 AND [DataSaldo] IS NULL AND [MovimentoSaldoId] IS NULL) OR ([Stato] = 1 AND [DataSaldo] IS NOT NULL AND [MovimentoSaldoId] IS NOT NULL)");
            t.HasCheckConstraint("CK_Sospesi_SociDistinti","[PersonId2] IS NULL OR [PersonId] IS NULL OR [PersonId2] <> [PersonId]");
        });
        entity.HasKey(x=>x.Id);
        entity.HasIndex(x=>x.IdOperazione).IsUnique();
        entity.HasIndex(x=>new{x.PersonId,x.Stato,x.DataOra});
        entity.HasIndex(x=>new{x.PersonId2,x.Stato,x.DataOra});
        entity.HasIndex(x=>new{x.Stato,x.Tipo,x.DataOra});
        entity.HasIndex(x=>x.AbbonamentoId).IsUnique().HasFilter("[AbbonamentoId] IS NOT NULL");
        entity.HasIndex(x=>x.PartitaContabileId).IsUnique().HasFilter("[PartitaContabileId] IS NOT NULL");
        entity.Property(x=>x.Importo).HasPrecision(18,2).HasColumnType("decimal(18,2)");
        entity.Property(x=>x.Valuta).HasMaxLength(3).IsRequired();
        entity.Property(x=>x.Descrizione).HasMaxLength(500).IsRequired();
        entity.Property(x=>x.Note).HasMaxLength(2000).IsRequired();
        entity.Property(x=>x.Nominativo).HasMaxLength(150).IsRequired();
        entity.Property(x=>x.Nominativo2).HasMaxLength(150);
        entity.Property(x=>x.NumeroSocio).HasMaxLength(30);entity.Property(x=>x.NumeroSocio2).HasMaxLength(30);
        entity.Property(x=>x.NumeroTessera).HasMaxLength(30);entity.Property(x=>x.NumeroTessera2).HasMaxLength(30);
        entity.Property(x=>x.NomeOperatore).HasMaxLength(100);entity.Property(x=>x.NomePostazione).HasMaxLength(100);
        entity.Property(x=>x.OrigineModulo).HasMaxLength(50).IsRequired();
        entity.Property(x=>x.OrigineEntita).HasMaxLength(100).IsRequired();entity.Property(x=>x.OrigineId).HasMaxLength(100).IsRequired();
        entity.HasOne(x=>x.PartitaContabile).WithMany().HasForeignKey(x=>x.PartitaContabileId).OnDelete(DeleteBehavior.NoAction);
        entity.HasOne(x=>x.MovimentoSaldo).WithMany().HasForeignKey(x=>x.MovimentoSaldoId).OnDelete(DeleteBehavior.NoAction);

        var purchase=modelBuilder.Entity<Abbonamento>();
        purchase.HasIndex(x=>x.AcquistoId).IsUnique().HasFilter("[AcquistoId] IS NOT NULL");
        purchase.Property(x=>x.PrezzoAcquisto).HasPrecision(18,2).HasColumnType("decimal(18,2)");
        purchase.ToTable("Abbonamenti",t=>t.HasCheckConstraint("CK_Abbonamenti_PrezzoAcquisto","[PrezzoAcquisto] IS NULL OR [PrezzoAcquisto] >= 0"));
    }
}
