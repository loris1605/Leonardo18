using Microsoft.EntityFrameworkCore;
using Models.Contabilita;

namespace Models.Context;

public partial class AppDbContext
{
    public DbSet<ContoContabile> ContabilitaPianoConti => Set<ContoContabile>();
    public DbSet<MovimentoContabile> ContabilitaMovimenti => Set<MovimentoContabile>();
    public DbSet<RigaContabile> ContabilitaRighe => Set<RigaContabile>();
    public DbSet<PartitaContabile> ContabilitaPartite => Set<PartitaContabile>();
    public DbSet<AbbinamentoPartitaContabile> ContabilitaAbbinamentiPartite => Set<AbbinamentoPartitaContabile>();

    private static void ContabilitaConfig(ModelBuilder modelBuilder)
    {
        var conto = modelBuilder.Entity<ContoContabile>();
        conto.ToTable("ContabilitaPianoConti", t => {
            t.HasCheckConstraint("CK_ContabilitaConto_Natura", "[Natura] IN (1,2,3,4,5)");
            t.HasCheckConstraint("CK_ContabilitaConto_Padre", "[ContoPadreId] IS NULL OR [ContoPadreId] <> [Id]");
        });
        conto.HasKey(x => x.Id);
        conto.Property(x => x.Codice).HasMaxLength(32).IsRequired();
        conto.Property(x => x.Nome).HasMaxLength(150).IsRequired();
        conto.HasIndex(x => x.Codice).IsUnique();
        conto.HasOne(x => x.ContoPadre).WithMany().HasForeignKey(x => x.ContoPadreId).OnDelete(DeleteBehavior.NoAction);
        conto.HasData(
            new ContoContabile { Id=-1, Codice="100", Nome="Cassa", Natura=NaturaConto.Attivita },
            new ContoContabile { Id=-2, Codice="110", Nome="Banca", Natura=NaturaConto.Attivita },
            new ContoContabile { Id=-3, Codice="120", Nome="Crediti verso soci", Natura=NaturaConto.Attivita },
            new ContoContabile { Id=-4, Codice="200", Nome="Debiti diversi", Natura=NaturaConto.Passivita },
            new ContoContabile { Id=-5, Codice="300", Nome="Patrimonio netto", Natura=NaturaConto.PatrimonioNetto },
            new ContoContabile { Id=-6, Codice="400", Nome="Ricavi diversi", Natura=NaturaConto.Ricavo },
            new ContoContabile { Id=-7, Codice="500", Nome="Costi diversi", Natura=NaturaConto.Costo });

        var movimento = modelBuilder.Entity<MovimentoContabile>();
        movimento.ToTable("ContabilitaMovimenti", t => {
            t.HasCheckConstraint("CK_ContabilitaMovimento_Stato", "[Stato] IN (0,1)");
            t.HasCheckConstraint("CK_ContabilitaMovimento_Operazione", "[IdOperazione] <> '00000000-0000-0000-0000-000000000000'");
            t.HasCheckConstraint("CK_ContabilitaMovimento_Storno", "[StornoDiId] IS NULL OR [StornoDiId] <> [Id]");
        });
        movimento.HasKey(x => x.Id);
        movimento.HasIndex(x => x.IdOperazione).IsUnique();
        movimento.HasIndex(x => x.DataCompetenza);
        movimento.HasIndex(x => new { x.GiornataId, x.PostazioneId, x.DataOra });
        movimento.HasIndex(x => new { x.PersonId, x.DataOra });
        movimento.HasIndex(x => new { x.OrigineModulo, x.OrigineEntita, x.OrigineId });
        movimento.Property(x => x.Valuta).HasMaxLength(3).IsRequired();
        movimento.Property(x => x.Causale).HasMaxLength(100).IsRequired();
        movimento.Property(x => x.Descrizione).HasMaxLength(500).IsRequired();
        movimento.Property(x => x.Note).HasMaxLength(2000).IsRequired();
        movimento.Property(x => x.OrigineModulo).HasMaxLength(50).IsRequired();
        movimento.Property(x => x.OrigineEntita).HasMaxLength(100);
        movimento.Property(x => x.OrigineId).HasMaxLength(100);
        movimento.Property(x => x.OrigineEvento).HasMaxLength(100);
        movimento.Property(x => x.Nominativo).HasMaxLength(150);
        movimento.Property(x => x.NumeroSocio).HasMaxLength(30);
        movimento.Property(x => x.NumeroTessera).HasMaxLength(30);
        movimento.Property(x => x.NomeOperatore).HasMaxLength(100);
        movimento.Property(x => x.NomePostazione).HasMaxLength(100);
        movimento.HasOne(x => x.StornoDi).WithMany().HasForeignKey(x => x.StornoDiId).OnDelete(DeleteBehavior.NoAction);

        var riga = modelBuilder.Entity<RigaContabile>();
        riga.ToTable("ContabilitaRighe", t => {
            t.HasCheckConstraint("CK_ContabilitaRiga_Importi", "([Dare] > 0 AND [Avere] = 0) OR ([Avere] > 0 AND [Dare] = 0)");
            t.HasCheckConstraint("CK_ContabilitaRiga_Numero", "[NumeroRiga] > 0");
        });
        riga.HasKey(x => x.Id);
        riga.Ignore(x => x.ImportiValidi);
        riga.Property(x => x.Dare).HasPrecision(18,2).HasColumnType("decimal(18,2)");
        riga.Property(x => x.Avere).HasPrecision(18,2).HasColumnType("decimal(18,2)");
        riga.Property(x => x.Descrizione).HasMaxLength(500).IsRequired();
        riga.Property(x => x.Nominativo).HasMaxLength(150);
        riga.Property(x => x.NomeSettore).HasMaxLength(100);
        riga.Property(x => x.EtichettaTariffa).HasMaxLength(150);
        riga.Property(x => x.NumeroTessera).HasMaxLength(30);
        riga.Property(x => x.NomeAbbonamento).HasMaxLength(150);
        riga.HasIndex(x => new { x.MovimentoId, x.NumeroRiga }).IsUnique();
        riga.HasIndex(x => new { x.ContoId, x.PersonId });
        riga.HasIndex(x => x.SettoreId);
        riga.HasOne(x => x.Movimento).WithMany(x => x.Righe).HasForeignKey(x => x.MovimentoId).OnDelete(DeleteBehavior.NoAction);
        riga.HasOne(x => x.Conto).WithMany().HasForeignKey(x => x.ContoId).OnDelete(DeleteBehavior.NoAction);

        var partita = modelBuilder.Entity<PartitaContabile>();
        partita.ToTable("ContabilitaPartite", t => {
            t.HasCheckConstraint("CK_ContabilitaPartita_Tipo", "[Tipo] IN (1,2)");
            t.HasCheckConstraint("CK_ContabilitaPartita_Importo", "[ImportoOriginale] > 0");
        });
        partita.HasKey(x => x.Id);
        partita.Property(x => x.ImportoOriginale).HasPrecision(18,2).HasColumnType("decimal(18,2)");
        partita.Property(x => x.Controparte).HasMaxLength(150).IsRequired();
        partita.Property(x => x.Descrizione).HasMaxLength(500).IsRequired();
        partita.HasIndex(x => x.RigaAperturaId).IsUnique();
        partita.HasIndex(x => new { x.PersonId, x.Tipo, x.DataScadenza });
        partita.HasOne(x => x.RigaApertura).WithMany().HasForeignKey(x => x.RigaAperturaId).OnDelete(DeleteBehavior.NoAction);

        var abbinamento = modelBuilder.Entity<AbbinamentoPartitaContabile>();
        abbinamento.ToTable("ContabilitaAbbinamentiPartite", t => {
            t.HasCheckConstraint("CK_ContabilitaAbbinamento_Importo", "([Importo] > 0 AND [StornoDiId] IS NULL) OR ([Importo] < 0 AND [StornoDiId] IS NOT NULL)");
            t.HasCheckConstraint("CK_ContabilitaAbbinamento_Storno", "[StornoDiId] IS NULL OR [StornoDiId] <> [Id]");
            t.HasCheckConstraint("CK_ContabilitaAbbinamento_Operazione", "[IdOperazione] <> '00000000-0000-0000-0000-000000000000'");
        });
        abbinamento.HasKey(x => x.Id);
        abbinamento.Property(x => x.Importo).HasPrecision(18,2).HasColumnType("decimal(18,2)");
        abbinamento.Property(x => x.Note).HasMaxLength(1000).IsRequired();
        abbinamento.HasIndex(x => x.IdOperazione).IsUnique();
        abbinamento.HasIndex(x => x.StornoDiId).IsUnique().HasFilter("[StornoDiId] IS NOT NULL");
        abbinamento.HasOne(x => x.Partita).WithMany(x => x.Abbinamenti).HasForeignKey(x => x.PartitaId).OnDelete(DeleteBehavior.NoAction);
        abbinamento.HasOne(x => x.RigaRegolamento).WithMany().HasForeignKey(x => x.RigaRegolamentoId).OnDelete(DeleteBehavior.NoAction);
        abbinamento.HasOne(x => x.StornoDi).WithMany().HasForeignKey(x => x.StornoDiId).OnDelete(DeleteBehavior.NoAction);
    }
}
