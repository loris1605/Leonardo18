using Microsoft.EntityFrameworkCore.Migrations;
#nullable disable
namespace Models.Migrations;
public partial class BlocchiSoci:Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.AddColumn<bool>(name:"BloccoSocio",table:"People",type:"bit",nullable:false,defaultValue:false);
        m.AddColumn<DateTime>(name:"BloccoSocioFino",table:"People",type:"datetime2",nullable:true);
        m.AddColumn<string>(name:"BloccoSocioMotivo",table:"People",type:"nvarchar(1000)",maxLength:1000,nullable:false,defaultValue:"");
    }
    protected override void Down(MigrationBuilder m)
    {
        m.DropColumn(name:"BloccoSocio",table:"People");
        m.DropColumn(name:"BloccoSocioFino",table:"People");
        m.DropColumn(name:"BloccoSocioMotivo",table:"People");
    }
}
