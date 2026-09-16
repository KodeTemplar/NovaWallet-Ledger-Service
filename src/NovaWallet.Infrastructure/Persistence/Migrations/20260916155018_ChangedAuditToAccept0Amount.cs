using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NovaWallet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ChangedAuditToAccept0Amount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_AuditLogs_AmountKobo_Positive",
                table: "AuditLogs");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AuditLogs_AmountKobo_Positive",
                table: "AuditLogs",
                sql: "[AmountKobo] >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_AuditLogs_AmountKobo_Positive",
                table: "AuditLogs");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AuditLogs_AmountKobo_Positive",
                table: "AuditLogs",
                sql: "[AmountKobo] > 0");
        }
    }
}
