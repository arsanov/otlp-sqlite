using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OtlpServer.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LogEntries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TraceId = table.Column<Guid>(type: "TEXT", nullable: true),
                    SpanId = table.Column<ulong>(type: "INTEGER", nullable: true),
                    Attributes = table.Column<string>(type: "TEXT", nullable: true),
                    Body = table.Column<string>(type: "TEXT", nullable: true),
                    EventName = table.Column<string>(type: "TEXT", nullable: true),
                    Flags = table.Column<uint>(type: "INTEGER", nullable: false),
                    ObservedTimeUnixNano = table.Column<ulong>(type: "INTEGER", nullable: false),
                    SeverityNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    SeverityText = table.Column<string>(type: "TEXT", nullable: true),
                    TimeUnixNano = table.Column<ulong>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LogEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MetricEntries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetricEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TraceEntries",
                columns: table => new
                {
                    TraceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SpanId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    Attributes = table.Column<string>(type: "TEXT", nullable: true),
                    Name = table.Column<string>(type: "TEXT", nullable: true),
                    ParentSpanId = table.Column<ulong>(type: "INTEGER", nullable: true),
                    StatusMessage = table.Column<string>(type: "TEXT", nullable: true),
                    StatusCode = table.Column<int>(type: "INTEGER", nullable: true),
                    TraceState = table.Column<string>(type: "TEXT", nullable: true),
                    StartTimeUnixNano = table.Column<ulong>(type: "INTEGER", nullable: false),
                    EndTimeUnixNano = table.Column<ulong>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TraceEntries", x => new { x.TraceId, x.SpanId });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LogEntries");

            migrationBuilder.DropTable(
                name: "MetricEntries");

            migrationBuilder.DropTable(
                name: "TraceEntries");
        }
    }
}
