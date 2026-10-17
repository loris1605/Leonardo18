using Microsoft.EntityFrameworkCore.Migrations;
#nullable disable
namespace Models.Migrations;
public partial class AddFidelityPremi:Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(name:"Traguardo",table:"TipiFidelity",type:"int",nullable:false,defaultValue:0);
        migrationBuilder.AddColumn<decimal>(name:"ValorePremio",table:"TipiFidelity",type:"decimal(18,2)",nullable:false,defaultValue:0m);
        migrationBuilder.AddColumn<bool>(name:"Attivo",table:"TipiFidelity",type:"bit",nullable:false,defaultValue:true);
        migrationBuilder.AddColumn<bool>(name:"Attiva",table:"Fidelities",type:"bit",nullable:false,defaultValue:false);
        migrationBuilder.AddColumn<DateTime>(name:"AttivataIl",table:"Fidelities",type:"datetime2",nullable:true);
        migrationBuilder.AddColumn<DateTime>(name:"ScadeIl",table:"Fidelities",type:"datetime2",nullable:true);
        migrationBuilder.AddColumn<int>(name:"Traguardo",table:"Fidelities",type:"int",nullable:false,defaultValue:0);
        migrationBuilder.AddColumn<int>(name:"TipoPremioId",table:"Fidelities",type:"int",nullable:false,defaultValue:0);
        migrationBuilder.AddColumn<decimal>(name:"ValorePremio",table:"Fidelities",type:"decimal(18,2)",nullable:false,defaultValue:0m);
        migrationBuilder.AddColumn<int>(name:"Ciclo",table:"Fidelities",type:"int",nullable:false,defaultValue:0);
        migrationBuilder.AddColumn<DateTime>(name:"RegistratoIl",table:"FidelityEntries",type:"datetime2",nullable:true);
        migrationBuilder.AddColumn<int>(name:"TipoMovimento",table:"FidelityEntries",type:"int",nullable:false,defaultValue:1);
        migrationBuilder.AddColumn<int>(name:"Ciclo",table:"FidelityEntries",type:"int",nullable:false,defaultValue:0);
        migrationBuilder.AddColumn<int>(name:"SchedaId",table:"FidelityEntries",type:"int",nullable:true);
        migrationBuilder.AddColumn<int>(name:"ContoId",table:"FidelityEntries",type:"int",nullable:true);
        migrationBuilder.AddColumn<decimal>(name:"Importo",table:"FidelityEntries",type:"decimal(18,2)",nullable:false,defaultValue:0m);
        migrationBuilder.CreateIndex(name:"IX_FidelityEntries_FidelityId_SchedaId_TipoMovimento",table:"FidelityEntries",columns:new[]{"FidelityId","SchedaId","TipoMovimento"},unique:true,filter:"[SchedaId] IS NOT NULL AND [TipoMovimento] IN (1,2,3)");
        migrationBuilder.CreateIndex(name:"IX_FidelityEntries_FidelityId_ContoId_TipoMovimento",table:"FidelityEntries",columns:new[]{"FidelityId","ContoId","TipoMovimento"},unique:true,filter:"[ContoId] IS NOT NULL");
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name:"IX_FidelityEntries_FidelityId_SchedaId_TipoMovimento",table:"FidelityEntries");
        migrationBuilder.DropIndex(name:"IX_FidelityEntries_FidelityId_ContoId_TipoMovimento",table:"FidelityEntries");
        migrationBuilder.DropColumn(name:"Traguardo",table:"TipiFidelity");
        migrationBuilder.DropColumn(name:"ValorePremio",table:"TipiFidelity");
        migrationBuilder.DropColumn(name:"Attivo",table:"TipiFidelity");
        migrationBuilder.DropColumn(name:"Attiva",table:"Fidelities");
        migrationBuilder.DropColumn(name:"AttivataIl",table:"Fidelities");
        migrationBuilder.DropColumn(name:"ScadeIl",table:"Fidelities");
        migrationBuilder.DropColumn(name:"Traguardo",table:"Fidelities");
        migrationBuilder.DropColumn(name:"TipoPremioId",table:"Fidelities");
        migrationBuilder.DropColumn(name:"ValorePremio",table:"Fidelities");
        migrationBuilder.DropColumn(name:"Ciclo",table:"Fidelities");
        migrationBuilder.DropColumn(name:"RegistratoIl",table:"FidelityEntries");
        migrationBuilder.DropColumn(name:"TipoMovimento",table:"FidelityEntries");
        migrationBuilder.DropColumn(name:"Ciclo",table:"FidelityEntries");
        migrationBuilder.DropColumn(name:"SchedaId",table:"FidelityEntries");
        migrationBuilder.DropColumn(name:"ContoId",table:"FidelityEntries");
        migrationBuilder.DropColumn(name:"Importo",table:"FidelityEntries");
    }
}
