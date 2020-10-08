using Microsoft.EntityFrameworkCore.Migrations;

namespace Digwex.Migrations
{
    public partial class color : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Color",
                table: "Playlists",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Color",
                table: "Calendars",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Color",
                table: "Playlists");

            migrationBuilder.DropColumn(
                name: "Color",
                table: "Calendars");
        }
    }
}
