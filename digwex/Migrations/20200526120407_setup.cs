using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using Digwex.Data;

namespace Digwex.Migrations
{
    public partial class setup : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Owner",
                table: "AspNetUsers",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "Settings",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Language = table.Column<string>(nullable: true),
                    TimeFormat = table.Column<bool>(nullable: false),
                    FirstWeekDay = table.Column<int>(nullable: false),
                    UserId = table.Column<string>(nullable: true),
                    Main = table.Column<bool>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Settings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Settings_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Setup",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Language = table.Column<string>(nullable: true),
                    Company = table.Column<CompanyJson>(type: "jsonb", nullable: true),
                    Server = table.Column<ServerJson>(type: "jsonb", nullable: true),
                    Smtp = table.Column<SmtpJson>(type: "jsonb", nullable: true),
                    UserId = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Setup", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Setup_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Setup",
                columns: new[] { "Id", "Company", "Language", "Server", "Smtp", "UserId" },
                values: new object[] { 1, null, "RU", null, null, null });

            migrationBuilder.CreateIndex(
                name: "IX_Settings_UserId",
                table: "Settings",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Setup_UserId",
                table: "Setup",
                column: "UserId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Settings");

            migrationBuilder.DropTable(
                name: "Setup");

            migrationBuilder.DropColumn(
                name: "Owner",
                table: "AspNetUsers");
        }
    }
}
