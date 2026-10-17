using Microsoft.EntityFrameworkCore.Migrations;
#nullable disable
namespace Models.Migrations;
public partial class AnnullaIngresso:Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.AddColumn<bool>(name:"IngressoAnnullato",table:"Schede",type:"bit",nullable:false,defaultValue:false);
        m.AddColumn<bool>(name:"IngressoAnnullato",table:"SchedeUscite",type:"bit",nullable:false,defaultValue:false);
        m.AddColumn<bool>(name:"IsIngresso",table:"SchedeConto",type:"bit",nullable:false,defaultValue:false);
        m.AddColumn<bool>(name:"IsIngresso",table:"SchedeUsciteConto",type:"bit",nullable:false,defaultValue:false);
        m.AddColumn<int>(name:"SchedaIngressoId",table:"AbbonamentoConti",type:"int",nullable:true);
        m.Sql("UPDATE SchedeConto SET IsIngresso=1 WHERE DescSettore='Ingresso' AND EXISTS (SELECT 1 FROM Schede s WHERE s.Id=SchedeConto.SchedaId AND s.CheckinTime=SchedeConto.DataOra);");
        m.Sql("UPDATE SchedeUsciteConto SET IsIngresso=1 WHERE DescSettore='Ingresso' AND EXISTS (SELECT 1 FROM SchedeUscite s WHERE s.Id=SchedeUsciteConto.SchedaId AND s.CheckinTime=SchedeUsciteConto.DataOra);");
        if(ActiveProvider=="Microsoft.EntityFrameworkCore.SqlServer")
            m.Sql("UPDATE a SET SchedaIngressoId=s.Id FROM AbbonamentoConti a INNER JOIN Schede s ON a.Note LIKE '%; scheda '+CONVERT(varchar(20),s.Id)+'; operatore %' WHERE a.Entrato=1;");
    }
    protected override void Down(MigrationBuilder m)
    {
        m.DropColumn(name:"IngressoAnnullato",table:"Schede");m.DropColumn(name:"IngressoAnnullato",table:"SchedeUscite");
        m.DropColumn(name:"IsIngresso",table:"SchedeConto");m.DropColumn(name:"IsIngresso",table:"SchedeUsciteConto");m.DropColumn(name:"SchedaIngressoId",table:"AbbonamentoConti");
    }
}
