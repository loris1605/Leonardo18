using Microsoft.EntityFrameworkCore.Migrations;
#nullable disable
namespace Models.Migrations;
public partial class PersonMaiuscolo:Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.Sql("UPDATE People SET FirstName=UPPER(FirstName), SurName=UPPER(SurName);");
    }
    protected override void Down(MigrationBuilder m)
    {
        // Original capitalization cannot be reconstructed.
    }
}
