using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Models.Migrations
{
    /// <inheritdoc />
    public partial class AddSospesiAbbonamenti : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AcquistoId",
                table: "Abbonamenti",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DataAcquisto",
                table: "Abbonamenti",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OperatoreAcquistoId",
                table: "Abbonamenti",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PagatoAllAcquisto",
                table: "Abbonamenti",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PostazioneAcquistoId",
                table: "Abbonamenti",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PrezzoAcquisto",
                table: "Abbonamenti",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Sospesi",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdOperazione = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    Origine = table.Column<int>(type: "int", nullable: false),
                    Stato = table.Column<int>(type: "int", nullable: false),
                    DataOra = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DataCompetenza = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DataScadenza = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Importo = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Valuta = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Descrizione = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    PersonId = table.Column<int>(type: "int", nullable: true),
                    Nominativo = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    NumeroSocio = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    NumeroTessera = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    PersonId2 = table.Column<int>(type: "int", nullable: true),
                    Nominativo2 = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    NumeroSocio2 = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    NumeroTessera2 = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    AbbonamentoId = table.Column<int>(type: "int", nullable: true),
                    SchedaId = table.Column<int>(type: "int", nullable: true),
                    SchedaContoId = table.Column<int>(type: "int", nullable: true),
                    PartitaContabileId = table.Column<long>(type: "bigint", nullable: true),
                    OrigineModulo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    OrigineEntita = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    OrigineId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    OperatoreId = table.Column<int>(type: "int", nullable: true),
                    NomeOperatore = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PostazioneId = table.Column<int>(type: "int", nullable: true),
                    NomePostazione = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    GiornataId = table.Column<int>(type: "int", nullable: true),
                    DataSaldo = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MovimentoSaldoId = table.Column<long>(type: "bigint", nullable: true),
                    OperatoreSaldoId = table.Column<int>(type: "int", nullable: true),
                    PostazioneSaldoId = table.Column<int>(type: "int", nullable: true),
                    GiornataSaldoId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sospesi", x => x.Id);
                    table.CheckConstraint("CK_Sospesi_Importo", "[Importo] > 0");
                    table.CheckConstraint("CK_Sospesi_Operazione", "[IdOperazione] <> '00000000-0000-0000-0000-000000000000'");
                    table.CheckConstraint("CK_Sospesi_Origine", "[Origine] IN (1,2,3)");
                    table.CheckConstraint("CK_Sospesi_SociDistinti", "[PersonId2] IS NULL OR [PersonId] IS NULL OR [PersonId2] <> [PersonId]");
                    table.CheckConstraint("CK_Sospesi_Stato", "([Stato] = 0 AND [DataSaldo] IS NULL AND [MovimentoSaldoId] IS NULL) OR ([Stato] = 1 AND [DataSaldo] IS NOT NULL AND [MovimentoSaldoId] IS NOT NULL)");
                    table.CheckConstraint("CK_Sospesi_Tipo", "[Tipo] IN (1,2)");
                    table.ForeignKey(
                        name: "FK_Sospesi_ContabilitaMovimenti_MovimentoSaldoId",
                        column: x => x.MovimentoSaldoId,
                        principalTable: "ContabilitaMovimenti",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Sospesi_ContabilitaPartite_PartitaContabileId",
                        column: x => x.PartitaContabileId,
                        principalTable: "ContabilitaPartite",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Abbonamenti_AcquistoId",
                table: "Abbonamenti",
                column: "AcquistoId",
                unique: true,
                filter: "[AcquistoId] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Abbonamenti_PrezzoAcquisto",
                table: "Abbonamenti",
                sql: "[PrezzoAcquisto] IS NULL OR [PrezzoAcquisto] >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_Sospesi_AbbonamentoId",
                table: "Sospesi",
                column: "AbbonamentoId",
                unique: true,
                filter: "[AbbonamentoId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Sospesi_IdOperazione",
                table: "Sospesi",
                column: "IdOperazione",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sospesi_MovimentoSaldoId",
                table: "Sospesi",
                column: "MovimentoSaldoId");

            migrationBuilder.CreateIndex(
                name: "IX_Sospesi_PartitaContabileId",
                table: "Sospesi",
                column: "PartitaContabileId",
                unique: true,
                filter: "[PartitaContabileId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Sospesi_PersonId_Stato_DataOra",
                table: "Sospesi",
                columns: new[] { "PersonId", "Stato", "DataOra" });

            migrationBuilder.CreateIndex(
                name: "IX_Sospesi_PersonId2_Stato_DataOra",
                table: "Sospesi",
                columns: new[] { "PersonId2", "Stato", "DataOra" });

            migrationBuilder.CreateIndex(
                name: "IX_Sospesi_Stato_Tipo_DataOra",
                table: "Sospesi",
                columns: new[] { "Stato", "Tipo", "DataOra" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Sospesi");

            migrationBuilder.DropIndex(
                name: "IX_Abbonamenti_AcquistoId",
                table: "Abbonamenti");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Abbonamenti_PrezzoAcquisto",
                table: "Abbonamenti");

            migrationBuilder.DropColumn(
                name: "AcquistoId",
                table: "Abbonamenti");

            migrationBuilder.DropColumn(
                name: "DataAcquisto",
                table: "Abbonamenti");

            migrationBuilder.DropColumn(
                name: "OperatoreAcquistoId",
                table: "Abbonamenti");

            migrationBuilder.DropColumn(
                name: "PagatoAllAcquisto",
                table: "Abbonamenti");

            migrationBuilder.DropColumn(
                name: "PostazioneAcquistoId",
                table: "Abbonamenti");

            migrationBuilder.DropColumn(
                name: "PrezzoAcquisto",
                table: "Abbonamenti");
        }
    }
}
