using Microsoft.EntityFrameworkCore;
using Models.Tables;
namespace Models.Context;
public partial class AppDbContext
{
    public DbSet<SchedaUscita> SchedeUscite=>Set<SchedaUscita>();
    public DbSet<SchedaUscitaConto> SchedeUsciteConto=>Set<SchedaUscitaConto>();
    private static void UsciteConfig(ModelBuilder builder)
    {
        var sheet=builder.Entity<SchedaUscita>();sheet.ToTable("SchedeUscite");sheet.HasKey(x=>x.Id);sheet.Property(x=>x.Id).ValueGeneratedNever();
        sheet.HasIndex(x=>x.IdOperazione).IsUnique();sheet.HasIndex(x=>new{x.PersonId,x.CheckoutTime});sheet.HasIndex(x=>x.CheckinTime);sheet.HasIndex(x=>new{x.PersonId,x.IsRientro,x.CheckinTime});sheet.HasIndex(x=>new{x.GiornataId,x.CheckoutTime});
        sheet.Property(x=>x.VersioneConto).HasMaxLength(64);sheet.Property(x=>x.Posizione).HasMaxLength(30);sheet.Property(x=>x.NumeroTessera).HasMaxLength(30);
        sheet.Property(x=>x.Cognome).HasMaxLength(30);sheet.Property(x=>x.Nome).HasMaxLength(30);sheet.Property(x=>x.Note).HasMaxLength(50);
        sheet.Property(x=>x.NomeOperatore).HasMaxLength(100);sheet.Property(x=>x.NomePostazione).HasMaxLength(100);
        sheet.Property(x=>x.Consumazione).HasPrecision(18,2).HasColumnType("decimal(18,2)");
        sheet.Property(x=>x.ImportoIncassato).HasPrecision(18,2).HasColumnType("decimal(18,2)");sheet.Property(x=>x.ImportoSospeso).HasPrecision(18,2).HasColumnType("decimal(18,2)");
        var row=builder.Entity<SchedaUscitaConto>();row.ToTable("SchedeUsciteConto");row.HasKey(x=>x.Id);row.Property(x=>x.Id).ValueGeneratedNever();
        row.HasOne(x=>x.Scheda).WithMany(x=>x.Conto).HasForeignKey(x=>x.SchedaId).OnDelete(DeleteBehavior.NoAction);
        row.Property(x=>x.DescSettore).HasMaxLength(30);row.Property(x=>x.DescPostazione).HasMaxLength(30);row.Property(x=>x.VoiceDesc).HasMaxLength(50);row.Property(x=>x.Note).HasMaxLength(50);
        row.Property(x=>x.VoicePrice).HasPrecision(7,2).HasColumnType("decimal(7,2)");row.HasIndex(x=>x.DataOra);
    }
}
