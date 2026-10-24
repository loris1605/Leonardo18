using Microsoft.EntityFrameworkCore.Migrations;
#nullable disable
namespace Models.Migrations;
public partial class CassaInizialeGiornata:Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.AddColumn<Guid>(name:"AperturaId",table:"Giornate",type:"uniqueidentifier",nullable:true);
        m.AddColumn<decimal>(name:"CassaIniziale",table:"Giornate",type:"decimal(18,2)",precision:18,scale:2,nullable:false,defaultValue:0m);
        m.AddColumn<string>(name:"ConteggioCassaIniziale",table:"Giornate",type:"nvarchar(2000)",maxLength:2000,nullable:false,defaultValue:"");
        m.CreateIndex(name:"IX_Giornate_AperturaId",table:"Giornate",column:"AperturaId",unique:true,filter:"[AperturaId] IS NOT NULL");
    }
    protected override void Down(MigrationBuilder m)
    {
        m.DropIndex(name:"IX_Giornate_AperturaId",table:"Giornate");
        m.DropColumn(name:"AperturaId",table:"Giornate");
        m.DropColumn(name:"CassaIniziale",table:"Giornate");
        m.DropColumn(name:"ConteggioCassaIniziale",table:"Giornate");
    }
}
