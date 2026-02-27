using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStockTxNoUnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_stock_ledger_entries_tx_no",
                table: "stock_ledger_entries");

            migrationBuilder.CreateIndex(
                name: "IX_stock_ledger_entries_tx_no",
                table: "stock_ledger_entries",
                column: "tx_no",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_stock_ledger_entries_tx_no",
                table: "stock_ledger_entries");

            migrationBuilder.CreateIndex(
                name: "IX_stock_ledger_entries_tx_no",
                table: "stock_ledger_entries",
                column: "tx_no");
        }
    }
}
