using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace OneSTools.EventLog.Exporter.SqlServer.Migrations
{
    public partial class Init : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EventLogPositions",
                columns: table => new
                {
                    LgpFileName = table.Column<string>(nullable: false),
                    LgpFilePosition = table.Column<long>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventLogPositions", x => x.LgpFileName);
                });

            migrationBuilder.CreateTable(
                name: "EventLogItems",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DateTime = table.Column<DateTime>(nullable: false),
                    TransactionStatus = table.Column<string>(nullable: true),
                    TransactionDateTime = table.Column<DateTime>(nullable: false),
                    TransactionNumber = table.Column<int>(nullable: false),
                    UserUuid = table.Column<string>(nullable: true),
                    User = table.Column<string>(nullable: true),
                    Computer = table.Column<string>(nullable: true),
                    Application = table.Column<string>(nullable: true),
                    Connection = table.Column<int>(nullable: false),
                    Event = table.Column<string>(nullable: true),
                    Severity = table.Column<string>(nullable: true),
                    Comment = table.Column<string>(nullable: true),
                    MetadataUuid = table.Column<string>(nullable: true),
                    Metadata = table.Column<string>(nullable: true),
                    Data = table.Column<string>(nullable: true),
                    DataUuid = table.Column<string>(nullable: true),
                    DataPresentation = table.Column<string>(nullable: true),
                    Server = table.Column<string>(nullable: true),
                    MainPort = table.Column<int>(nullable: false),
                    AddPort = table.Column<int>(nullable: false),
                    Session = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventLogItems", x => x.Id);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EventLogPositions");

            migrationBuilder.DropTable(
                name: "EventLogItems");
        }
    }
}
