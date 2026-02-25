using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddItemMaster : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_items_code",
                table: "items");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "items",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "code",
                table: "items",
                newName: "tracking_type");

            migrationBuilder.AddColumn<string>(
                name: "barcode",
                table: "items",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "category_id",
                table: "items",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                table: "items",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "item_code",
                table: "items",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<byte[]>(
                name: "row_version",
                table: "items",
                type: "bytea",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<Guid>(
                name: "unit_of_measure_id",
                table: "items",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at_utc",
                table: "items",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.CreateTable(
                name: "item_categories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    category_code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_item_categories", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "unit_of_measures",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    uom_code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_unit_of_measures", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "item_uom_conversions",
                columns: table => new
                {
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_unit_of_measure_id = table.Column<Guid>(type: "uuid", nullable: false),
                    to_unit_of_measure_id = table.Column<Guid>(type: "uuid", nullable: false),
                    factor = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_item_uom_conversions", x => new { x.item_id, x.from_unit_of_measure_id, x.to_unit_of_measure_id });
                    table.ForeignKey(
                        name: "FK_item_uom_conversions_items_item_id",
                        column: x => x.item_id,
                        principalTable: "items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_item_uom_conversions_unit_of_measures_from_unit_of_measure_~",
                        column: x => x.from_unit_of_measure_id,
                        principalTable: "unit_of_measures",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_item_uom_conversions_unit_of_measures_to_unit_of_measure_id",
                        column: x => x.to_unit_of_measure_id,
                        principalTable: "unit_of_measures",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_items_barcode",
                table: "items",
                column: "barcode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_items_category_id",
                table: "items",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "IX_items_item_code",
                table: "items",
                column: "item_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_items_unit_of_measure_id",
                table: "items",
                column: "unit_of_measure_id");

            migrationBuilder.CreateIndex(
                name: "IX_item_categories_category_code",
                table: "item_categories",
                column: "category_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_item_uom_conversions_from_unit_of_measure_id",
                table: "item_uom_conversions",
                column: "from_unit_of_measure_id");

            migrationBuilder.CreateIndex(
                name: "IX_item_uom_conversions_to_unit_of_measure_id",
                table: "item_uom_conversions",
                column: "to_unit_of_measure_id");

            migrationBuilder.CreateIndex(
                name: "IX_unit_of_measures_uom_code",
                table: "unit_of_measures",
                column: "uom_code",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_items_item_categories_category_id",
                table: "items",
                column: "category_id",
                principalTable: "item_categories",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_items_unit_of_measures_unit_of_measure_id",
                table: "items",
                column: "unit_of_measure_id",
                principalTable: "unit_of_measures",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_items_item_categories_category_id",
                table: "items");

            migrationBuilder.DropForeignKey(
                name: "FK_items_unit_of_measures_unit_of_measure_id",
                table: "items");

            migrationBuilder.DropTable(
                name: "item_categories");

            migrationBuilder.DropTable(
                name: "item_uom_conversions");

            migrationBuilder.DropTable(
                name: "unit_of_measures");

            migrationBuilder.DropIndex(
                name: "IX_items_barcode",
                table: "items");

            migrationBuilder.DropIndex(
                name: "IX_items_category_id",
                table: "items");

            migrationBuilder.DropIndex(
                name: "IX_items_item_code",
                table: "items");

            migrationBuilder.DropIndex(
                name: "IX_items_unit_of_measure_id",
                table: "items");

            migrationBuilder.DropColumn(
                name: "barcode",
                table: "items");

            migrationBuilder.DropColumn(
                name: "category_id",
                table: "items");

            migrationBuilder.DropColumn(
                name: "is_active",
                table: "items");

            migrationBuilder.DropColumn(
                name: "item_code",
                table: "items");

            migrationBuilder.DropColumn(
                name: "row_version",
                table: "items");

            migrationBuilder.DropColumn(
                name: "unit_of_measure_id",
                table: "items");

            migrationBuilder.DropColumn(
                name: "updated_at_utc",
                table: "items");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "items",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "tracking_type",
                table: "items",
                newName: "code");

            migrationBuilder.CreateIndex(
                name: "IX_items_code",
                table: "items",
                column: "code",
                unique: true);
        }
    }
}
