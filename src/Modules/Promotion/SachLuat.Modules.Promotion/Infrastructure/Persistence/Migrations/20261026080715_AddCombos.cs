using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SachLuat.Modules.Promotion.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCombos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "combos",
                schema: "promotion",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    slug = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    image_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    combo_price = table.Column<decimal>(type: "numeric(12,0)", precision: 12, scale: 0, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    starts_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ends_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_combos", x => x.id);
                    table.CheckConstraint("ck_combos_price", "combo_price > 0");
                    table.CheckConstraint("ck_combos_schedule", "ends_at IS NULL OR ends_at > starts_at");
                });

            migrationBuilder.CreateTable(
                name: "combo_items",
                schema: "promotion",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    combo_id = table.Column<long>(type: "bigint", nullable: false),
                    sku_id = table.Column<long>(type: "bigint", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_combo_items", x => x.id);
                    table.CheckConstraint("ck_combo_items_quantity", "quantity > 0");
                    table.ForeignKey(
                        name: "fk_combo_items_combos_combo_id",
                        column: x => x.combo_id,
                        principalSchema: "promotion",
                        principalTable: "combos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_combo_items_combo_id_sku_id",
                schema: "promotion",
                table: "combo_items",
                columns: new[] { "combo_id", "sku_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_combo_items_sku_id",
                schema: "promotion",
                table: "combo_items",
                column: "sku_id");

            migrationBuilder.CreateIndex(
                name: "ix_combos_is_active_starts_at",
                schema: "promotion",
                table: "combos",
                columns: new[] { "is_active", "starts_at" });

            migrationBuilder.CreateIndex(
                name: "ux_combos_slug",
                schema: "promotion",
                table: "combos",
                column: "slug",
                unique: true,
                filter: "deleted_at IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "combo_items",
                schema: "promotion");

            migrationBuilder.DropTable(
                name: "combos",
                schema: "promotion");
        }
    }
}
