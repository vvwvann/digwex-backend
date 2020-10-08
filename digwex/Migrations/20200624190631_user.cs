using Microsoft.EntityFrameworkCore.Migrations;

namespace Digwex.Migrations
{
    public partial class user : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UserId",
                table: "PlaylistContent",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UserId",
                table: "Calendars",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UserId",
                table: "CalendarPlaylist",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlaylistContent_UserId",
                table: "PlaylistContent",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Calendars_UserId",
                table: "Calendars",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarPlaylist_UserId",
                table: "CalendarPlaylist",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_CalendarPlaylist_AspNetUsers_UserId",
                table: "CalendarPlaylist",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Calendars_AspNetUsers_UserId",
                table: "Calendars",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PlaylistContent_AspNetUsers_UserId",
                table: "PlaylistContent",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CalendarPlaylist_AspNetUsers_UserId",
                table: "CalendarPlaylist");

            migrationBuilder.DropForeignKey(
                name: "FK_Calendars_AspNetUsers_UserId",
                table: "Calendars");

            migrationBuilder.DropForeignKey(
                name: "FK_PlaylistContent_AspNetUsers_UserId",
                table: "PlaylistContent");

            migrationBuilder.DropIndex(
                name: "IX_PlaylistContent_UserId",
                table: "PlaylistContent");

            migrationBuilder.DropIndex(
                name: "IX_Calendars_UserId",
                table: "Calendars");

            migrationBuilder.DropIndex(
                name: "IX_CalendarPlaylist_UserId",
                table: "CalendarPlaylist");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "PlaylistContent");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Calendars");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "CalendarPlaylist");
        }
    }
}
