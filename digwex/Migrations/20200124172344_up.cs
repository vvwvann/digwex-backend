using Microsoft.EntityFrameworkCore.Migrations;

namespace Digwex.Migrations
{
    public partial class up : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Online",
                table: "Players");

            migrationBuilder.AddColumn<bool>(
                name: "Landscape",
                table: "Contents",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Id",
                table: "CalendarPlaylist",
                nullable: false,
                defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Landscape",
                table: "Contents");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "CalendarPlaylist");

            migrationBuilder.AddColumn<bool>(
                name: "Online",
                table: "Players",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
