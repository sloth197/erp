using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStockTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "expiry_date",
                table: "stock_ledger_entries",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "lot_id",
                table: "stock_ledger_entries",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "serial_no",
                table: "stock_ledger_entries",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "stock_lots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lot_no = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    expiry_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_lots", x => x.id);
                    table.ForeignKey(
                        name: "FK_stock_lots_items_item_id",
                        column: x => x.item_id,
                        principalTable: "items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_stock_ledger_entries_lot_id",
                table: "stock_ledger_entries",
                column: "lot_id");

            migrationBuilder.CreateIndex(
                name: "IX_stock_ledger_entries_serial_no",
                table: "stock_ledger_entries",
                column: "serial_no");

            migrationBuilder.CreateIndex(
                name: "IX_stock_lots_item_id_lot_no",
                table: "stock_lots",
                columns: new[] { "item_id", "lot_no" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_stock_ledger_entries_stock_lots_lot_id",
                table: "stock_ledger_entries",
                column: "lot_id",
                principalTable: "stock_lots",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_stock_ledger_entries_stock_lots_lot_id",
                table: "stock_ledger_entries");

            migrationBuilder.DropTable(
                name: "stock_lots");

            migrationBuilder.DropIndex(
                name: "IX_stock_ledger_entries_lot_id",
                table: "stock_ledger_entries");

            migrationBuilder.DropIndex(
                name: "IX_stock_ledger_entries_serial_no",
                table: "stock_ledger_entries");

            migrationBuilder.DropColumn(
                name: "expiry_date",
                table: "stock_ledger_entries");

            migrationBuilder.DropColumn(
                name: "lot_id",
                table: "stock_ledger_entries");

            migrationBuilder.DropColumn(
                name: "serial_no",
                table: "stock_ledger_entries");
        }
    }
}
