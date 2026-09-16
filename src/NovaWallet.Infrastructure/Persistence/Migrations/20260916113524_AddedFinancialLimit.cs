using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NovaWallet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddedFinancialLimit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FinancialLimits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AmountKobo = table.Column<long>(type: "bigint", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinancialLimits", x => x.Id);
                    table.CheckConstraint("CK_FinancialLimits_AmountKobo_Positive", "[AmountKobo] > 0");
                });

            migrationBuilder.InsertData(
                table: "FinancialLimits",
                columns: new[] { "Id", "AmountKobo", "Code", "IsActive" },
                values: new object[] { 1, 50000000L, "DAILY_OUTBOUND_TRANSFER", true });

            migrationBuilder.CreateIndex(
                name: "IX_FinancialLimits_Code",
                table: "FinancialLimits",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FinancialLimits");
        }
    }
}
