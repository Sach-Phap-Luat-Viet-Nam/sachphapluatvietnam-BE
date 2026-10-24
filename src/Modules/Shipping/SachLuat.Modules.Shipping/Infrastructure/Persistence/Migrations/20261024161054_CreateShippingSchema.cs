using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SachLuat.Modules.Shipping.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CreateShippingSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "shipping");

            migrationBuilder.CreateTable(
                name: "shipping_settings",
                schema: "shipping",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false),
                    free_shipping_threshold = table.Column<decimal>(type: "numeric(12,0)", precision: 12, scale: 0, nullable: true),
                    packaging_weight_gram = table.Column<int>(type: "integer", nullable: false),
                    default_book_weight_gram = table.Column<int>(type: "integer", nullable: false),
                    flat_fee = table.Column<decimal>(type: "numeric(12,0)", precision: 12, scale: 0, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_shipping_settings", x => x.id);
                    table.CheckConstraint("ck_shipping_settings_singleton", "id = 1");
                    table.CheckConstraint("ck_shipping_settings_values", "packaging_weight_gram >= 0 AND default_book_weight_gram > 0 AND flat_fee >= 0");
                });

            migrationBuilder.CreateTable(
                name: "shipping_zones",
                schema: "shipping",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    base_weight_gram = table.Column<int>(type: "integer", nullable: false),
                    base_fee = table.Column<decimal>(type: "numeric(12,0)", precision: 12, scale: 0, nullable: false),
                    step_weight_gram = table.Column<int>(type: "integer", nullable: false),
                    step_fee = table.Column<decimal>(type: "numeric(12,0)", precision: 12, scale: 0, nullable: false),
                    eta_min_days = table.Column<int>(type: "integer", nullable: false),
                    eta_max_days = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_shipping_zones", x => x.id);
                    table.CheckConstraint("ck_shipping_zones_eta", "eta_min_days >= 0 AND eta_max_days >= eta_min_days");
                    table.CheckConstraint("ck_shipping_zones_fees", "base_fee >= 0 AND step_fee >= 0");
                    table.CheckConstraint("ck_shipping_zones_weights", "base_weight_gram > 0 AND step_weight_gram > 0");
                });

            migrationBuilder.CreateTable(
                name: "shipping_zone_provinces",
                schema: "shipping",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    zone_id = table.Column<long>(type: "bigint", nullable: false),
                    province_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_shipping_zone_provinces", x => x.id);
                    table.ForeignKey(
                        name: "fk_shipping_zone_provinces_shipping_zones_zone_id",
                        column: x => x.zone_id,
                        principalSchema: "shipping",
                        principalTable: "shipping_zones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_shipping_zone_provinces_zone_id",
                schema: "shipping",
                table: "shipping_zone_provinces",
                column: "zone_id");

            migrationBuilder.CreateIndex(
                name: "ux_shipping_zone_provinces_key",
                schema: "shipping",
                table: "shipping_zone_provinces",
                column: "province_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_shipping_zones_sort_order",
                schema: "shipping",
                table: "shipping_zones",
                column: "sort_order");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "shipping_settings",
                schema: "shipping");

            migrationBuilder.DropTable(
                name: "shipping_zone_provinces",
                schema: "shipping");

            migrationBuilder.DropTable(
                name: "shipping_zones",
                schema: "shipping");
        }
    }
}
