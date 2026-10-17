using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Models.Migrations
{
    /// <inheritdoc />
    public partial class AddSchedeUscite : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SchedeUscite",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    IdOperazione = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersioneConto = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    PersonId = table.Column<int>(type: "int", nullable: false),
                    Posizione = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    NumeroTessera = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Cognome = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Natoil = table.Column<int>(type: "int", nullable: false),
                    CheckinTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CheckoutTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Grb1 = table.Column<int>(type: "int", nullable: false),
                    Grb2 = table.Column<int>(type: "int", nullable: false),
                    Grb3 = table.Column<int>(type: "int", nullable: false),
                    Grb4 = table.Column<int>(type: "int", nullable: false),
                    Consumazione = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Blocco = table.Column<bool>(type: "bit", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    OperatoreId = table.Column<int>(type: "int", nullable: false),
                    NomeOperatore = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PostazioneId = table.Column<int>(type: "int", nullable: false),
                    NomePostazione = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    GiornataId = table.Column<int>(type: "int", nullable: false),
                    Incassa = table.Column<bool>(type: "bit", nullable: false),
                    ImportoIncassato = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ImportoSospeso = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchedeUscite", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SchedeUsciteConto",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_SchedeUsciteConto", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SchedeUsciteConto_SchedeUscite_SchedaId",
                        column: x => x.SchedaId,
                        principalTable: "SchedeUscite",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_SchedeUscite_CheckinTime",
                table: "SchedeUscite",
                column: "CheckinTime");

            migrationBuilder.CreateIndex(
                name: "IX_SchedeUscite_GiornataId_CheckoutTime",
                table: "SchedeUscite",
                columns: new[] { "GiornataId", "CheckoutTime" });

            migrationBuilder.CreateIndex(
                name: "IX_SchedeUscite_IdOperazione",
                table: "SchedeUscite",
                column: "IdOperazione",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SchedeUscite_PersonId_CheckoutTime",
                table: "SchedeUscite",
                columns: new[] { "PersonId", "CheckoutTime" });

            migrationBuilder.CreateIndex(
                name: "IX_SchedeUsciteConto_DataOra",
                table: "SchedeUsciteConto",
                column: "DataOra");

            migrationBuilder.CreateIndex(
                name: "IX_SchedeUsciteConto_SchedaId",
                table: "SchedeUsciteConto",
                column: "SchedaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SchedeUsciteConto");

            migrationBuilder.DropTable(
                name: "SchedeUscite");
        }
    }
}
