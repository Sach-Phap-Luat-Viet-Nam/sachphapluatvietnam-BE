using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SachLuat.Modules.Promotion.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CreatePromotionSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "promotion");

            migrationBuilder.CreateTable(
                name: "promotions",
                schema: "promotion",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    title = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    mode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    discount_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    discount_value = table.Column<decimal>(type: "numeric(12,0)", precision: 12, scale: 0, nullable: false),
                    max_discount_amount = table.Column<decimal>(type: "numeric(12,0)", precision: 12, scale: 0, nullable: true),
                    min_order_amount = table.Column<decimal>(type: "numeric(12,0)", precision: 12, scale: 0, nullable: false),
                    scope_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    exclude_on_sale_items = table.Column<bool>(type: "boolean", nullable: false),
                    audience = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    usage_limit = table.Column<int>(type: "integer", nullable: true),
                    per_customer_limit = table.Column<int>(type: "integer", nullable: true),
                    used_count = table.Column<int>(type: "integer", nullable: false),
                    starts_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ends_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    priority = table.Column<int>(type: "integer", nullable: false),
                    is_public = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_promotions", x => x.id);
                    table.CheckConstraint("ck_promotions_discount", "discount_value > 0");
                    table.CheckConstraint("ck_promotions_limits", "min_order_amount >= 0 AND used_count >= 0 AND (usage_limit IS NULL OR usage_limit > 0) AND (per_customer_limit IS NULL OR per_customer_limit > 0)");
                    table.CheckConstraint("ck_promotions_schedule", "ends_at IS NULL OR ends_at > starts_at");
                });

            migrationBuilder.CreateTable(
                name: "coupons",
                schema: "promotion",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    promotion_id = table.Column<long>(type: "bigint", nullable: false),
                    code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    usage_limit = table.Column<int>(type: "integer", nullable: true),
                    used_count = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_coupons", x => x.id);
                    table.CheckConstraint("ck_coupons_limits", "used_count >= 0 AND (usage_limit IS NULL OR usage_limit > 0)");
                    table.ForeignKey(
                        name: "fk_coupons_promotions_promotion_id",
                        column: x => x.promotion_id,
                        principalSchema: "promotion",
                        principalTable: "promotions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "promotion_targets",
                schema: "promotion",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    promotion_id = table.Column<long>(type: "bigint", nullable: false),
                    target_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_promotion_targets", x => x.id);
                    table.ForeignKey(
                        name: "fk_promotion_targets_promotions_promotion_id",
                        column: x => x.promotion_id,
                        principalSchema: "promotion",
                        principalTable: "promotions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "promotion_redemptions",
                schema: "promotion",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    reference = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    promotion_id = table.Column<long>(type: "bigint", nullable: false),
                    coupon_id = table.Column<long>(type: "bigint", nullable: true),
                    customer_key = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(12,0)", precision: 12, scale: 0, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    redeemed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    released_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_promotion_redemptions", x => x.id);
                    table.CheckConstraint("ck_promotion_redemptions_amount", "amount >= 0");
                    table.ForeignKey(
                        name: "fk_promotion_redemptions_coupons_coupon_id",
                        column: x => x.coupon_id,
                        principalSchema: "promotion",
                        principalTable: "coupons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_promotion_redemptions_promotions_promotion_id",
                        column: x => x.promotion_id,
                        principalSchema: "promotion",
                        principalTable: "promotions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_coupons_promotion_id",
                schema: "promotion",
                table: "coupons",
                column: "promotion_id");

            migrationBuilder.CreateIndex(
                name: "ux_coupons_code",
                schema: "promotion",
                table: "coupons",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_promotion_redemptions_coupon_id",
                schema: "promotion",
                table: "promotion_redemptions",
                column: "coupon_id");

            migrationBuilder.CreateIndex(
                name: "ix_promotion_redemptions_promotion_id_customer_key",
                schema: "promotion",
                table: "promotion_redemptions",
                columns: new[] { "promotion_id", "customer_key" },
                filter: "status = 'redeemed'");

            migrationBuilder.CreateIndex(
                name: "ux_promotion_redemptions_reference_promotion",
                schema: "promotion",
                table: "promotion_redemptions",
                columns: new[] { "reference", "promotion_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_promotion_targets_promotion_id_target_id",
                schema: "promotion",
                table: "promotion_targets",
                columns: new[] { "promotion_id", "target_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_promotions_is_active_starts_at",
                schema: "promotion",
                table: "promotions",
                columns: new[] { "is_active", "starts_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "promotion_redemptions",
                schema: "promotion");

            migrationBuilder.DropTable(
                name: "promotion_targets",
                schema: "promotion");

            migrationBuilder.DropTable(
                name: "coupons",
                schema: "promotion");

            migrationBuilder.DropTable(
                name: "promotions",
                schema: "promotion");
        }
    }
}
