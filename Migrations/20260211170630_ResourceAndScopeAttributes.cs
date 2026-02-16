using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OtlpServer.Migrations
{
    /// <inheritdoc />
    public partial class ResourceAndScopeAttributes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ResourceAttributes",
                table: "TraceEntries",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ScopeAttributes",
                table: "TraceEntries",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ResourceAttributes",
                table: "TraceEntries");

            migrationBuilder.DropColumn(
                name: "ScopeAttributes",
                table: "TraceEntries");
        }
    }
}
