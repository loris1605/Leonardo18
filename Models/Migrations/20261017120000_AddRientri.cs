using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace Models.Migrations;

public partial class AddRientri : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(name:"IsRientro",table:"Schede",type:"bit",nullable:false,defaultValue:false);
        migrationBuilder.AddColumn<int>(name:"GiornataIngressoId",table:"Schede",type:"int",nullable:true);
        migrationBuilder.AddColumn<bool>(name:"IsRientro",table:"SchedeUscite",type:"bit",nullable:false,defaultValue:false);
        migrationBuilder.AddColumn<int>(name:"GiornataIngressoId",table:"SchedeUscite",type:"int",nullable:true);
        migrationBuilder.CreateIndex(name:"IX_SchedeUscite_PersonId_IsRientro_CheckinTime",table:"SchedeUscite",columns:new[]{"PersonId","IsRientro","CheckinTime"});
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name:"IX_SchedeUscite_PersonId_IsRientro_CheckinTime",table:"SchedeUscite");
        migrationBuilder.DropColumn(name:"IsRientro",table:"Schede");
        migrationBuilder.DropColumn(name:"GiornataIngressoId",table:"Schede");
        migrationBuilder.DropColumn(name:"IsRientro",table:"SchedeUscite");
        migrationBuilder.DropColumn(name:"GiornataIngressoId",table:"SchedeUscite");
    }
}
