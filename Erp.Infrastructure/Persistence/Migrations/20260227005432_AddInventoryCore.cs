using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryCore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "warehouses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_warehouses", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "locations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_locations", x => x.id);
                    table.ForeignKey(
                        name: "FK_locations_warehouses_warehouse_id",
                        column: x => x.warehouse_id,
                        principalTable: "warehouses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "inventory_balances",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    location_id = table.Column<Guid>(type: "uuid", nullable: true),
                    qty_on_hand = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    qty_allocated = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                    row_version = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_balances", x => x.id);
                    table.ForeignKey(
                        name: "FK_inventory_balances_items_item_id",
                        column: x => x.item_id,
                        principalTable: "items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_inventory_balances_locations_location_id",
                        column: x => x.location_id,
                        principalTable: "locations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_inventory_balances_warehouses_warehouse_id",
                        column: x => x.warehouse_id,
                        principalTable: "warehouses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "stock_ledger_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tx_no = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    tx_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    location_id = table.Column<Guid>(type: "uuid", nullable: true),
                    qty = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    unit_cost = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    occurred_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    reference_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    reference_id = table.Column<Guid>(type: "uuid", nullable: true),
                    note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_ledger_entries", x => x.id);
                    table.ForeignKey(
                        name: "FK_stock_ledger_entries_items_item_id",
                        column: x => x.item_id,
                        principalTable: "items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_ledger_entries_locations_location_id",
                        column: x => x.location_id,
                        principalTable: "locations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_ledger_entries_users_actor_user_id",
                        column: x => x.actor_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_stock_ledger_entries_warehouses_warehouse_id",
                        column: x => x.warehouse_id,
                        principalTable: "warehouses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_inventory_balances_location_id",
                table: "inventory_balances",
                column: "location_id");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_balances_warehouse_id",
                table: "inventory_balances",
                column: "warehouse_id");

            migrationBuilder.CreateIndex(
                name: "ux_inventory_balances_item_wh_loc_notnull",
                table: "inventory_balances",
                columns: new[] { "item_id", "warehouse_id", "location_id" },
                unique: true,
                filter: "location_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_inventory_balances_item_wh_null",
                table: "inventory_balances",
                columns: new[] { "item_id", "warehouse_id" },
                unique: true,
                filter: "location_id IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_locations_warehouse_id_code",
                table: "locations",
                columns: new[] { "warehouse_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_stock_ledger_entries_actor_user_id",
                table: "stock_ledger_entries",
                column: "actor_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_stock_ledger_entries_item_id",
                table: "stock_ledger_entries",
                column: "item_id");

            migrationBuilder.CreateIndex(
                name: "IX_stock_ledger_entries_location_id",
                table: "stock_ledger_entries",
                column: "location_id");

            migrationBuilder.CreateIndex(
                name: "IX_stock_ledger_entries_occurred_at_utc",
                table: "stock_ledger_entries",
                column: "occurred_at_utc");

            migrationBuilder.CreateIndex(
                name: "IX_stock_ledger_entries_tx_no",
                table: "stock_ledger_entries",
                column: "tx_no");

            migrationBuilder.CreateIndex(
                name: "IX_stock_ledger_entries_warehouse_id",
                table: "stock_ledger_entries",
                column: "warehouse_id");

            migrationBuilder.CreateIndex(
                name: "IX_warehouses_code",
                table: "warehouses",
                column: "code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "inventory_balances");

            migrationBuilder.DropTable(
                name: "stock_ledger_entries");

            migrationBuilder.DropTable(
                name: "locations");

            migrationBuilder.DropTable(
                name: "warehouses");
        }
    }
}
