using Microsoft.EntityFrameworkCore.Migrations;
using Digwex.Controllers.Api;

namespace Digwex.Migrations
{
    public partial class file : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<ResponseFile>(
                name: "LastLog",
                table: "Players",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<ResponseFile>(
                name: "LastScreen",
                table: "Players",
                type: "jsonb",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastLog",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "LastScreen",
                table: "Players");
        }
    }
}
