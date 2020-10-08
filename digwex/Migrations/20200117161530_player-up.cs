using Microsoft.EntityFrameworkCore.Migrations;

namespace Digwex.Migrations
{
    public partial class playerup : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ProblemSync",
                table: "Players",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProblemSync",
                table: "Players");
        }
    }
}
