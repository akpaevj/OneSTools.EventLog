using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace OneSTools.EventLog.Exporter.SqlServer.Migrations
{
    public partial class Init : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EventLogItems",
                columns: table => new
                {
                    Id = table.Column<long>(nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FileName = table.Column<string>(nullable: false),
                    EndPosition = table.Column<long>(nullable: false),
                    DateTime = table.Column<DateTime>(nullable: false),
                    TransactionStatus = table.Column<string>(nullable: false),
                    TransactionDateTime = table.Column<DateTime>(nullable: false),
                    TransactionNumber = table.Column<int>(nullable: false),
                    UserUuid = table.Column<string>(nullable: false),
                    User = table.Column<string>(nullable: false),
                    Computer = table.Column<string>(nullable: false),
                    Application = table.Column<string>(nullable: false),
                    Connection = table.Column<int>(nullable: false),
                    Event = table.Column<string>(nullable: false),
                    Severity = table.Column<string>(nullable: false),
                    Comment = table.Column<string>(nullable: false),
                    MetadataUuid = table.Column<string>(nullable: false),
                    Metadata = table.Column<string>(nullable: false),
                    Data = table.Column<string>(nullable: false),
                    DataPresentation = table.Column<string>(nullable: false),
                    Server = table.Column<string>(nullable: false),
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
                name: "EventLogItems");
        }
    }
}
