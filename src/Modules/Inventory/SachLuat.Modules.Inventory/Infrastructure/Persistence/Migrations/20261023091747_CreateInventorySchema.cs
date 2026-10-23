using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SachLuat.Modules.Inventory.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CreateInventorySchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "inventory");

            migrationBuilder.CreateTable(
                name: "goods_receipts",
                schema: "inventory",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_by = table.Column<long>(type: "bigint", nullable: false),
                    decided_by = table.Column<long>(type: "bigint", nullable: true),
                    decided_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    reject_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_goods_receipts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "print_batches",
                schema: "inventory",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    sku_id = table.Column<long>(type: "bigint", nullable: false),
                    batch_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    printer_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    unit_cost = table.Column<decimal>(type: "numeric(12,0)", precision: 12, scale: 0, nullable: false),
                    received_at = table.Column<DateOnly>(type: "date", nullable: false),
                    remaining = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_print_batches", x => x.id);
                    table.CheckConstraint("ck_print_batches_quantity", "quantity > 0");
                    table.CheckConstraint("ck_print_batches_remaining", "remaining >= 0 AND remaining <= quantity");
                });

            migrationBuilder.CreateTable(
                name: "stock_items",
                schema: "inventory",
                columns: table => new
                {
                    sku_id = table.Column<long>(type: "bigint", nullable: false),
                    available = table.Column<int>(type: "integer", nullable: false),
                    reserved = table.Column<int>(type: "integer", nullable: false),
                    low_stock_threshold = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stock_items", x => x.sku_id);
                    table.CheckConstraint("ck_stock_items_available", "available >= 0");
                    table.CheckConstraint("ck_stock_items_reserved", "reserved >= 0");
                });

            migrationBuilder.CreateTable(
                name: "stock_reservations",
                schema: "inventory",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    reference = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    sku_id = table.Column<long>(type: "bigint", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stock_reservations", x => x.id);
                    table.CheckConstraint("ck_stock_reservations_quantity", "quantity > 0");
                });

            migrationBuilder.CreateTable(
                name: "goods_receipt_lines",
                schema: "inventory",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    goods_receipt_id = table.Column<long>(type: "bigint", nullable: false),
                    sku_id = table.Column<long>(type: "bigint", nullable: false),
                    batch_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    printer_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    unit_cost = table.Column<decimal>(type: "numeric(12,0)", precision: 12, scale: 0, nullable: false),
                    received_at = table.Column<DateOnly>(type: "date", nullable: false),
                    print_batch_id = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_goods_receipt_lines", x => x.id);
                    table.CheckConstraint("ck_goods_receipt_lines_quantity", "quantity > 0");
                    table.CheckConstraint("ck_goods_receipt_lines_unit_cost", "unit_cost >= 0");
                    table.ForeignKey(
                        name: "fk_goods_receipt_lines_goods_receipts_goods_receipt_id",
                        column: x => x.goods_receipt_id,
                        principalSchema: "inventory",
                        principalTable: "goods_receipts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "inventory_transactions",
                schema: "inventory",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    sku_id = table.Column<long>(type: "bigint", nullable: false),
                    print_batch_id = table.Column<long>(type: "bigint", nullable: true),
                    type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    reference = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inventory_transactions", x => x.id);
                    table.ForeignKey(
                        name: "fk_inventory_transactions_print_batches_print_batch_id",
                        column: x => x.print_batch_id,
                        principalSchema: "inventory",
                        principalTable: "print_batches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_goods_receipt_lines_goods_receipt_id",
                schema: "inventory",
                table: "goods_receipt_lines",
                column: "goods_receipt_id");

            migrationBuilder.CreateIndex(
                name: "ix_goods_receipts_status",
                schema: "inventory",
                table: "goods_receipts",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_inventory_transactions_print_batch_id",
                schema: "inventory",
                table: "inventory_transactions",
                column: "print_batch_id");

            migrationBuilder.CreateIndex(
                name: "ix_inventory_transactions_reference",
                schema: "inventory",
                table: "inventory_transactions",
                column: "reference");

            migrationBuilder.CreateIndex(
                name: "ix_inventory_transactions_sku_id_created_at",
                schema: "inventory",
                table: "inventory_transactions",
                columns: new[] { "sku_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_print_batches_sku_id_batch_code",
                schema: "inventory",
                table: "print_batches",
                columns: new[] { "sku_id", "batch_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_print_batches_sku_id_received_at_id",
                schema: "inventory",
                table: "print_batches",
                columns: new[] { "sku_id", "received_at", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_stock_reservations_reference_sku_id",
                schema: "inventory",
                table: "stock_reservations",
                columns: new[] { "reference", "sku_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "goods_receipt_lines",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "inventory_transactions",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "stock_items",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "stock_reservations",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "goods_receipts",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "print_batches",
                schema: "inventory");
        }
    }
}
