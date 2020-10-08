using Microsoft.EntityFrameworkCore.Migrations;

namespace Digwex.Migrations
{
    public partial class key : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_CalendarPlaylist",
                table: "CalendarPlaylist");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "CalendarPlaylist");

            migrationBuilder.AddPrimaryKey(
                name: "PK_CalendarPlaylist",
                table: "CalendarPlaylist",
                column: "Guid");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarPlaylist_CalendarId",
                table: "CalendarPlaylist",
                column: "CalendarId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_CalendarPlaylist",
                table: "CalendarPlaylist");

            migrationBuilder.DropIndex(
                name: "IX_CalendarPlaylist_CalendarId",
                table: "CalendarPlaylist");

            migrationBuilder.AddColumn<int>(
                name: "Id",
                table: "CalendarPlaylist",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddPrimaryKey(
                name: "PK_CalendarPlaylist",
                table: "CalendarPlaylist",
                columns: new[] { "CalendarId", "PlaylistId", "Guid" });
        }
    }
}
