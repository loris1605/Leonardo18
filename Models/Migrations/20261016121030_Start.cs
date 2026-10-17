using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Models.Migrations
{
    /// <inheritdoc />
    public partial class Start : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ContabilitaMovimenti",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdOperazione = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DataOra = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DataCompetenza = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Stato = table.Column<int>(type: "int", nullable: false),
                    Valuta = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Causale = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Descrizione = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    OrigineModulo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    OrigineEntita = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    OrigineId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    OrigineEvento = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PersonId = table.Column<int>(type: "int", nullable: true),
                    Nominativo = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    NumeroSocio = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    NumeroTessera = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    OperatoreId = table.Column<int>(type: "int", nullable: true),
                    NomeOperatore = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PostazioneId = table.Column<int>(type: "int", nullable: true),
                    NomePostazione = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    GiornataId = table.Column<int>(type: "int", nullable: true),
                    StornoDiId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContabilitaMovimenti", x => x.Id);
                    table.CheckConstraint("CK_ContabilitaMovimento_Operazione", "[IdOperazione] <> '00000000-0000-0000-0000-000000000000'");
                    table.CheckConstraint("CK_ContabilitaMovimento_Stato", "[Stato] IN (0,1)");
                    table.CheckConstraint("CK_ContabilitaMovimento_Storno", "[StornoDiId] IS NULL OR [StornoDiId] <> [Id]");
                    table.ForeignKey(
                        name: "FK_ContabilitaMovimenti_ContabilitaMovimenti_StornoDiId",
                        column: x => x.StornoDiId,
                        principalTable: "ContabilitaMovimenti",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ContabilitaPianoConti",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Codice = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Natura = table.Column<int>(type: "int", nullable: false),
                    ContoPadreId = table.Column<int>(type: "int", nullable: true),
                    Movimentabile = table.Column<bool>(type: "bit", nullable: false),
                    Attivo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContabilitaPianoConti", x => x.Id);
                    table.CheckConstraint("CK_ContabilitaConto_Natura", "[Natura] IN (1,2,3,4,5)");
                    table.CheckConstraint("CK_ContabilitaConto_Padre", "[ContoPadreId] IS NULL OR [ContoPadreId] <> [Id]");
                    table.ForeignKey(
                        name: "FK_ContabilitaPianoConti_ContabilitaPianoConti_ContoPadreId",
                        column: x => x.ContoPadreId,
                        principalTable: "ContabilitaPianoConti",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Giornate",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DataInizio = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DataFine = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Aperta = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Giornate", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Operatori",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nome = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Password = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Abilitato = table.Column<bool>(type: "bit", nullable: false),
                    Pass = table.Column<int>(type: "int", nullable: false),
                    PersonId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Operatori", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "People",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FirstName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SurName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Natoil = table.Column<int>(type: "int", nullable: false),
                    UniqueParam = table.Column<string>(type: "nvarchar(14)", maxLength: 14, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_People", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Settings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Version = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Settings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Strisciate",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CodiceSocio = table.Column<string>(type: "nvarchar(25)", maxLength: 25, nullable: false),
                    DataOra = table.Column<int>(type: "int", nullable: false),
                    NumeroTessera = table.Column<string>(type: "nvarchar(25)", maxLength: 25, nullable: false),
                    Cognome = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Natoil = table.Column<int>(type: "int", nullable: false),
                    Scadenza = table.Column<int>(type: "int", nullable: false),
                    CodMotivo = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Strisciate", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tariffe",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nome = table.Column<string>(type: "nvarchar(25)", maxLength: 25, nullable: false),
                    Label = table.Column<string>(type: "nvarchar(25)", maxLength: 25, nullable: false),
                    IsFreeDrink = table.Column<bool>(type: "bit", nullable: false),
                    Prezzo = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tariffe", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TipiAbbonamento",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nome = table.Column<string>(type: "nvarchar(25)", maxLength: 25, nullable: false),
                    NumeroIngressi = table.Column<int>(type: "int", nullable: false),
                    DurataGiorni = table.Column<int>(type: "int", nullable: false),
                    Prezzo = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Attivo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TipiAbbonamento", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TipiFidelityInput",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TipiFidelityInput", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TipiFidelityOutput",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TipiFidelityOutput", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TipiPostazione",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(25)", maxLength: 25, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TipiPostazione", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TipiRientro",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nome = table.Column<string>(type: "nvarchar(25)", maxLength: 25, nullable: false),
                    DurataOre = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TipiRientro", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TipiSettore",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nome = table.Column<string>(type: "nvarchar(25)", maxLength: 25, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TipiSettore", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ContabilitaRighe",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MovimentoId = table.Column<long>(type: "bigint", nullable: false),
                    NumeroRiga = table.Column<int>(type: "int", nullable: false),
                    ContoId = table.Column<int>(type: "int", nullable: false),
                    Dare = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Avere = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Descrizione = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    PersonId = table.Column<int>(type: "int", nullable: true),
                    Nominativo = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    SettoreId = table.Column<int>(type: "int", nullable: true),
                    NomeSettore = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TariffaId = table.Column<int>(type: "int", nullable: true),
                    EtichettaTariffa = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    NumeroTessera = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    AbbonamentoId = table.Column<int>(type: "int", nullable: true),
                    NomeAbbonamento = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContabilitaRighe", x => x.Id);
                    table.CheckConstraint("CK_ContabilitaRiga_Importi", "([Dare] > 0 AND [Avere] = 0) OR ([Avere] > 0 AND [Dare] = 0)");
                    table.CheckConstraint("CK_ContabilitaRiga_Numero", "[NumeroRiga] > 0");
                    table.ForeignKey(
                        name: "FK_ContabilitaRighe_ContabilitaMovimenti_MovimentoId",
                        column: x => x.MovimentoId,
                        principalTable: "ContabilitaMovimenti",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ContabilitaRighe_ContabilitaPianoConti_ContoId",
                        column: x => x.ContoId,
                        principalTable: "ContabilitaPianoConti",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Schede",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Posizione = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    NumeroTessera = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    PersonId = table.Column<int>(type: "int", nullable: false),
                    Cognome = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Natoil = table.Column<int>(type: "int", nullable: false),
                    CheckinTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CheckoutTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Grb1 = table.Column<int>(type: "int", nullable: false),
                    Grb2 = table.Column<int>(type: "int", nullable: false),
                    Grb3 = table.Column<int>(type: "int", nullable: false),
                    Grb4 = table.Column<int>(type: "int", nullable: false),
                    Consumazione = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Blocco = table.Column<bool>(type: "bit", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Schede", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Schede_People_PersonId",
                        column: x => x.PersonId,
                        principalTable: "People",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Soci",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NumeroSocio = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PersonId = table.Column<int>(type: "int", nullable: false),
                    Abilitato = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Soci", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Soci_People_PersonId",
                        column: x => x.PersonId,
                        principalTable: "People",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TipiFidelity",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nome = table.Column<string>(type: "nvarchar(25)", maxLength: 25, nullable: false),
                    DurataGG = table.Column<int>(type: "int", nullable: false),
                    TipoFidelityInputId = table.Column<int>(type: "int", nullable: false),
                    TipoFidelityOutputId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TipiFidelity", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TipiFidelity_TipiFidelityInput_TipoFidelityInputId",
                        column: x => x.TipoFidelityInputId,
                        principalTable: "TipiFidelityInput",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TipiFidelity_TipiFidelityOutput_TipoFidelityOutputId",
                        column: x => x.TipoFidelityOutputId,
                        principalTable: "TipiFidelityOutput",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Postazioni",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nome = table.Column<string>(type: "nvarchar(25)", maxLength: 25, nullable: false),
                    TipoPostazioneId = table.Column<int>(type: "int", nullable: false),
                    TipoRientroId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Postazioni", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Postazioni_TipiPostazione_TipoPostazioneId",
                        column: x => x.TipoPostazioneId,
                        principalTable: "TipiPostazione",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Postazioni_TipiRientro_TipoRientroId",
                        column: x => x.TipoRientroId,
                        principalTable: "TipiRientro",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Settori",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nome = table.Column<string>(type: "nvarchar(25)", maxLength: 25, nullable: false),
                    Label = table.Column<string>(type: "nvarchar(25)", maxLength: 25, nullable: false),
                    TipoSettoreId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Settori", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Settori_TipiSettore_TipoSettoreId",
                        column: x => x.TipoSettoreId,
                        principalTable: "TipiSettore",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ContabilitaPartite",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RigaAperturaId = table.Column<long>(type: "bigint", nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    PersonId = table.Column<int>(type: "int", nullable: true),
                    Controparte = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Descrizione = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    DataApertura = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DataScadenza = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ImportoOriginale = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContabilitaPartite", x => x.Id);
                    table.CheckConstraint("CK_ContabilitaPartita_Importo", "[ImportoOriginale] > 0");
                    table.CheckConstraint("CK_ContabilitaPartita_Tipo", "[Tipo] IN (1,2)");
                    table.ForeignKey(
                        name: "FK_ContabilitaPartite_ContabilitaRighe_RigaAperturaId",
                        column: x => x.RigaAperturaId,
                        principalTable: "ContabilitaRighe",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "SchedeConto",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchedaId = table.Column<int>(type: "int", nullable: false),
                    DescSettore = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    DescPostazione = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    VoiceDesc = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    VoicePrice = table.Column<decimal>(type: "decimal(7,2)", precision: 7, scale: 2, nullable: false),
                    Pagato = table.Column<bool>(type: "bit", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DataOra = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchedeConto", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SchedeConto_Schede_SchedaId",
                        column: x => x.SchedaId,
                        principalTable: "Schede",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Tessere",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NumeroTessera = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Scadenza = table.Column<int>(type: "int", nullable: false),
                    SocioId = table.Column<int>(type: "int", nullable: false),
                    Abilitato = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tessere", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tessere_Soci_SocioId",
                        column: x => x.SocioId,
                        principalTable: "Soci",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Fidelities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TipoFidelityId = table.Column<int>(type: "int", nullable: false),
                    PersonId = table.Column<int>(type: "int", nullable: false),
                    DataAttivazione = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Fidelities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Fidelities_People_PersonId",
                        column: x => x.PersonId,
                        principalTable: "People",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Fidelities_TipiFidelity_TipoFidelityId",
                        column: x => x.TipoFidelityId,
                        principalTable: "TipiFidelity",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Permessi",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OperatoreId = table.Column<int>(type: "int", nullable: false),
                    PostazioneId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Permessi", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Permessi_Operatori_OperatoreId",
                        column: x => x.OperatoreId,
                        principalTable: "Operatori",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Permessi_Postazioni_PostazioneId",
                        column: x => x.PostazioneId,
                        principalTable: "Postazioni",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Listini",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SettoreId = table.Column<int>(type: "int", nullable: false),
                    TariffaId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Listini", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Listini_Settori_SettoreId",
                        column: x => x.SettoreId,
                        principalTable: "Settori",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Listini_Tariffe_TariffaId",
                        column: x => x.TariffaId,
                        principalTable: "Tariffe",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Reparti",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SettoreId = table.Column<int>(type: "int", nullable: false),
                    PostazioneId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reparti", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Reparti_Postazioni_PostazioneId",
                        column: x => x.PostazioneId,
                        principalTable: "Postazioni",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Reparti_Settori_SettoreId",
                        column: x => x.SettoreId,
                        principalTable: "Settori",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ContabilitaAbbinamentiPartite",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdOperazione = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PartitaId = table.Column<long>(type: "bigint", nullable: false),
                    RigaRegolamentoId = table.Column<long>(type: "bigint", nullable: false),
                    DataOra = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Importo = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    StornoDiId = table.Column<long>(type: "bigint", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContabilitaAbbinamentiPartite", x => x.Id);
                    table.CheckConstraint("CK_ContabilitaAbbinamento_Importo", "([Importo] > 0 AND [StornoDiId] IS NULL) OR ([Importo] < 0 AND [StornoDiId] IS NOT NULL)");
                    table.CheckConstraint("CK_ContabilitaAbbinamento_Operazione", "[IdOperazione] <> '00000000-0000-0000-0000-000000000000'");
                    table.CheckConstraint("CK_ContabilitaAbbinamento_Storno", "[StornoDiId] IS NULL OR [StornoDiId] <> [Id]");
                    table.ForeignKey(
                        name: "FK_ContabilitaAbbinamentiPartite_ContabilitaAbbinamentiPartite_StornoDiId",
                        column: x => x.StornoDiId,
                        principalTable: "ContabilitaAbbinamentiPartite",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ContabilitaAbbinamentiPartite_ContabilitaPartite_PartitaId",
                        column: x => x.PartitaId,
                        principalTable: "ContabilitaPartite",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ContabilitaAbbinamentiPartite_ContabilitaRighe_RigaRegolamentoId",
                        column: x => x.RigaRegolamentoId,
                        principalTable: "ContabilitaRighe",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Abbonamenti",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nome = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TipoAbbonamentoId = table.Column<int>(type: "int", nullable: false),
                    TesseraId1 = table.Column<int>(type: "int", nullable: true),
                    TesseraId2 = table.Column<int>(type: "int", nullable: true),
                    IngressiAcquistati = table.Column<int>(type: "int", nullable: true),
                    PersonId1 = table.Column<int>(type: "int", nullable: false),
                    PersonId2 = table.Column<int>(type: "int", nullable: true),
                    DataInizio = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DataFine = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Attivo = table.Column<bool>(type: "bit", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Abbonamenti", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Abbonamenti_People_PersonId1",
                        column: x => x.PersonId1,
                        principalTable: "People",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Abbonamenti_People_PersonId2",
                        column: x => x.PersonId2,
                        principalTable: "People",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Abbonamenti_Tessere_TesseraId1",
                        column: x => x.TesseraId1,
                        principalTable: "Tessere",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Abbonamenti_Tessere_TesseraId2",
                        column: x => x.TesseraId2,
                        principalTable: "Tessere",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Abbonamenti_TipiAbbonamento_TipoAbbonamentoId",
                        column: x => x.TipoAbbonamentoId,
                        principalTable: "TipiAbbonamento",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "FidelityEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FidelityId = table.Column<int>(type: "int", nullable: false),
                    Value = table.Column<int>(type: "int", nullable: false),
                    DataOra = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FidelityEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FidelityEntries_Fidelities_FidelityId",
                        column: x => x.FidelityId,
                        principalTable: "Fidelities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AbbonamentoConti",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nome = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AbbonamentoId = table.Column<int>(type: "int", nullable: false),
                    NumeroIngresso = table.Column<int>(type: "int", nullable: false),
                    DataIngresso = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Entrato = table.Column<bool>(type: "bit", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AbbonamentoConti", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AbbonamentoConti_Abbonamenti_AbbonamentoId",
                        column: x => x.AbbonamentoId,
                        principalTable: "Abbonamenti",
                        principalColumn: "Id");
                });

            migrationBuilder.InsertData(
                table: "ContabilitaPianoConti",
                columns: new[] { "Id", "Attivo", "Codice", "ContoPadreId", "Movimentabile", "Natura", "Nome" },
                values: new object[,]
                {
                    { -7, true, "500", null, true, 4, "Costi diversi" },
                    { -6, true, "400", null, true, 5, "Ricavi diversi" },
                    { -5, true, "300", null, true, 3, "Patrimonio netto" },
                    { -4, true, "200", null, true, 2, "Debiti diversi" },
                    { -3, true, "120", null, true, 1, "Crediti verso soci" },
                    { -2, true, "110", null, true, 1, "Banca" },
                    { -1, true, "100", null, true, 1, "Cassa" }
                });

            migrationBuilder.InsertData(
                table: "Operatori",
                columns: new[] { "Id", "Abilitato", "Nome", "Pass", "Password", "PersonId" },
                values: new object[] { -1, true, "ADMIN", 0, "ADMIN", -2 });

            migrationBuilder.InsertData(
                table: "People",
                columns: new[] { "Id", "FirstName", "Natoil", "SurName", "UniqueParam" },
                values: new object[,]
                {
                    { -999, "Di Servizio", 21000101, "Tessera", "TESSER21000101" },
                    { -3, "Non Importato", 21000101, "Socio", "SCOSOC21000101" },
                    { -2, "SOCIO", 21000101, "AMMINISTRATORE", "AMMSOC21000101" },
                    { -1, "SOCIO", 21000101, "VIRTUALE", "VIRSOC21000101" }
                });

            migrationBuilder.InsertData(
                table: "Settings",
                columns: new[] { "Id", "UpdatedAt", "Version" },
                values: new object[] { -1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 1 });

            migrationBuilder.InsertData(
                table: "TipiFidelityInput",
                columns: new[] { "Id", "Nome" },
                values: new object[,]
                {
                    { 1, "Ingressi" },
                    { 2, "Incassi" }
                });

            migrationBuilder.InsertData(
                table: "TipiFidelityOutput",
                columns: new[] { "Id", "Nome" },
                values: new object[,]
                {
                    { 1, "Ingressi" },
                    { 2, "Sconto" },
                    { 3, "Premio" }
                });

            migrationBuilder.InsertData(
                table: "TipiPostazione",
                columns: new[] { "Id", "Nome" },
                values: new object[,]
                {
                    { 1, "Amministratore" },
                    { 2, "Cassa" },
                    { 3, "Bar" },
                    { 4, "Guardaroba" },
                    { 5, "Pulizie" }
                });

            migrationBuilder.InsertData(
                table: "TipiRientro",
                columns: new[] { "Id", "DurataOre", "Nome" },
                values: new object[,]
                {
                    { -2, 0, "Giornata" },
                    { -1, 0, "Nessuno" }
                });

            migrationBuilder.InsertData(
                table: "TipiSettore",
                columns: new[] { "Id", "Nome" },
                values: new object[,]
                {
                    { -2, "Standard" },
                    { -1, "Ingressi" }
                });

            migrationBuilder.InsertData(
                table: "Postazioni",
                columns: new[] { "Id", "Nome", "TipoPostazioneId", "TipoRientroId" },
                values: new object[] { -1, "Amministratore base", 1, -1 });

            migrationBuilder.InsertData(
                table: "Permessi",
                columns: new[] { "Id", "OperatoreId", "PostazioneId" },
                values: new object[] { -1, -1, -1 });

            migrationBuilder.CreateIndex(
                name: "IX_Abbonamenti_PersonId1",
                table: "Abbonamenti",
                column: "PersonId1");

            migrationBuilder.CreateIndex(
                name: "IX_Abbonamenti_PersonId2",
                table: "Abbonamenti",
                column: "PersonId2");

            migrationBuilder.CreateIndex(
                name: "IX_Abbonamenti_TesseraId1",
                table: "Abbonamenti",
                column: "TesseraId1");

            migrationBuilder.CreateIndex(
                name: "IX_Abbonamenti_TesseraId2",
                table: "Abbonamenti",
                column: "TesseraId2");

            migrationBuilder.CreateIndex(
                name: "IX_Abbonamenti_TipoAbbonamentoId",
                table: "Abbonamenti",
                column: "TipoAbbonamentoId");

            migrationBuilder.CreateIndex(
                name: "IX_AbbonamentoConti_AbbonamentoId",
                table: "AbbonamentoConti",
                column: "AbbonamentoId");

            migrationBuilder.CreateIndex(
                name: "IX_ContabilitaAbbinamentiPartite_IdOperazione",
                table: "ContabilitaAbbinamentiPartite",
                column: "IdOperazione",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContabilitaAbbinamentiPartite_PartitaId",
                table: "ContabilitaAbbinamentiPartite",
                column: "PartitaId");

            migrationBuilder.CreateIndex(
                name: "IX_ContabilitaAbbinamentiPartite_RigaRegolamentoId",
                table: "ContabilitaAbbinamentiPartite",
                column: "RigaRegolamentoId");

            migrationBuilder.CreateIndex(
                name: "IX_ContabilitaAbbinamentiPartite_StornoDiId",
                table: "ContabilitaAbbinamentiPartite",
                column: "StornoDiId",
                unique: true,
                filter: "[StornoDiId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ContabilitaMovimenti_DataCompetenza",
                table: "ContabilitaMovimenti",
                column: "DataCompetenza");

            migrationBuilder.CreateIndex(
                name: "IX_ContabilitaMovimenti_GiornataId_PostazioneId_DataOra",
                table: "ContabilitaMovimenti",
                columns: new[] { "GiornataId", "PostazioneId", "DataOra" });

            migrationBuilder.CreateIndex(
                name: "IX_ContabilitaMovimenti_IdOperazione",
                table: "ContabilitaMovimenti",
                column: "IdOperazione",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContabilitaMovimenti_OrigineModulo_OrigineEntita_OrigineId",
                table: "ContabilitaMovimenti",
                columns: new[] { "OrigineModulo", "OrigineEntita", "OrigineId" });

            migrationBuilder.CreateIndex(
                name: "IX_ContabilitaMovimenti_PersonId_DataOra",
                table: "ContabilitaMovimenti",
                columns: new[] { "PersonId", "DataOra" });

            migrationBuilder.CreateIndex(
                name: "IX_ContabilitaMovimenti_StornoDiId",
                table: "ContabilitaMovimenti",
                column: "StornoDiId");

            migrationBuilder.CreateIndex(
                name: "IX_ContabilitaPartite_PersonId_Tipo_DataScadenza",
                table: "ContabilitaPartite",
                columns: new[] { "PersonId", "Tipo", "DataScadenza" });

            migrationBuilder.CreateIndex(
                name: "IX_ContabilitaPartite_RigaAperturaId",
                table: "ContabilitaPartite",
                column: "RigaAperturaId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContabilitaPianoConti_Codice",
                table: "ContabilitaPianoConti",
                column: "Codice",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContabilitaPianoConti_ContoPadreId",
                table: "ContabilitaPianoConti",
                column: "ContoPadreId");

            migrationBuilder.CreateIndex(
                name: "IX_ContabilitaRighe_ContoId_PersonId",
                table: "ContabilitaRighe",
                columns: new[] { "ContoId", "PersonId" });

            migrationBuilder.CreateIndex(
                name: "IX_ContabilitaRighe_MovimentoId_NumeroRiga",
                table: "ContabilitaRighe",
                columns: new[] { "MovimentoId", "NumeroRiga" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContabilitaRighe_SettoreId",
                table: "ContabilitaRighe",
                column: "SettoreId");

            migrationBuilder.CreateIndex(
                name: "IX_Fidelities_PersonId",
                table: "Fidelities",
                column: "PersonId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Fidelities_TipoFidelityId",
                table: "Fidelities",
                column: "TipoFidelityId");

            migrationBuilder.CreateIndex(
                name: "IX_FidelityEntries_FidelityId",
                table: "FidelityEntries",
                column: "FidelityId");

            migrationBuilder.CreateIndex(
                name: "IX_Listini_SettoreId",
                table: "Listini",
                column: "SettoreId");

            migrationBuilder.CreateIndex(
                name: "IX_Listini_TariffaId",
                table: "Listini",
                column: "TariffaId");

            migrationBuilder.CreateIndex(
                name: "IX_People_UniqueParam",
                table: "People",
                column: "UniqueParam",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Person_FirstName",
                table: "People",
                column: "FirstName");

            migrationBuilder.CreateIndex(
                name: "IX_Person_SurName",
                table: "People",
                column: "SurName");

            migrationBuilder.CreateIndex(
                name: "IX_Permessi_OperatoreId",
                table: "Permessi",
                column: "OperatoreId");

            migrationBuilder.CreateIndex(
                name: "IX_Permessi_PostazioneId",
                table: "Permessi",
                column: "PostazioneId");

            migrationBuilder.CreateIndex(
                name: "IX_Postazioni_TipoPostazioneId",
                table: "Postazioni",
                column: "TipoPostazioneId");

            migrationBuilder.CreateIndex(
                name: "IX_Postazioni_TipoRientroId",
                table: "Postazioni",
                column: "TipoRientroId");

            migrationBuilder.CreateIndex(
                name: "IX_Reparti_PostazioneId",
                table: "Reparti",
                column: "PostazioneId");

            migrationBuilder.CreateIndex(
                name: "IX_Reparti_SettoreId",
                table: "Reparti",
                column: "SettoreId");

            migrationBuilder.CreateIndex(
                name: "IX_Schede_PersonId",
                table: "Schede",
                column: "PersonId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Schede_Posizione",
                table: "Schede",
                column: "Posizione",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SchedeConto_SchedaId",
                table: "SchedeConto",
                column: "SchedaId");

            migrationBuilder.CreateIndex(
                name: "IX_Settori_TipoSettoreId",
                table: "Settori",
                column: "TipoSettoreId");

            migrationBuilder.CreateIndex(
                name: "IX_Soci_NumeroSocio",
                table: "Soci",
                column: "NumeroSocio",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Soci_PersonId",
                table: "Soci",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_Tessere_NumeroTessera",
                table: "Tessere",
                column: "NumeroTessera",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tessere_SocioId",
                table: "Tessere",
                column: "SocioId");

            migrationBuilder.CreateIndex(
                name: "IX_TipiFidelity_TipoFidelityInputId",
                table: "TipiFidelity",
                column: "TipoFidelityInputId");

            migrationBuilder.CreateIndex(
                name: "IX_TipiFidelity_TipoFidelityOutputId",
                table: "TipiFidelity",
                column: "TipoFidelityOutputId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AbbonamentoConti");

            migrationBuilder.DropTable(
                name: "ContabilitaAbbinamentiPartite");

            migrationBuilder.DropTable(
                name: "FidelityEntries");

            migrationBuilder.DropTable(
                name: "Giornate");

            migrationBuilder.DropTable(
                name: "Listini");

            migrationBuilder.DropTable(
                name: "Permessi");

            migrationBuilder.DropTable(
                name: "Reparti");

            migrationBuilder.DropTable(
                name: "SchedeConto");

            migrationBuilder.DropTable(
                name: "Settings");

            migrationBuilder.DropTable(
                name: "Strisciate");

            migrationBuilder.DropTable(
                name: "Abbonamenti");

            migrationBuilder.DropTable(
                name: "ContabilitaPartite");

            migrationBuilder.DropTable(
                name: "Fidelities");

            migrationBuilder.DropTable(
                name: "Tariffe");

            migrationBuilder.DropTable(
                name: "Operatori");

            migrationBuilder.DropTable(
                name: "Postazioni");

            migrationBuilder.DropTable(
                name: "Settori");

            migrationBuilder.DropTable(
                name: "Schede");

            migrationBuilder.DropTable(
                name: "Tessere");

            migrationBuilder.DropTable(
                name: "TipiAbbonamento");

            migrationBuilder.DropTable(
                name: "ContabilitaRighe");

            migrationBuilder.DropTable(
                name: "TipiFidelity");

            migrationBuilder.DropTable(
                name: "TipiPostazione");

            migrationBuilder.DropTable(
                name: "TipiRientro");

            migrationBuilder.DropTable(
                name: "TipiSettore");

            migrationBuilder.DropTable(
                name: "Soci");

            migrationBuilder.DropTable(
                name: "ContabilitaMovimenti");

            migrationBuilder.DropTable(
                name: "ContabilitaPianoConti");

            migrationBuilder.DropTable(
                name: "TipiFidelityInput");

            migrationBuilder.DropTable(
                name: "TipiFidelityOutput");

            migrationBuilder.DropTable(
                name: "People");
        }
    }
}
