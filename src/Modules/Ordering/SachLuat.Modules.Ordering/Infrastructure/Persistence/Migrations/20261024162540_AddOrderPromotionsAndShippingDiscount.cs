using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SachLuat.Modules.Ordering.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderPromotionsAndShippingDiscount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_orders_amounts",
                schema: "ordering",
                table: "orders");

            migrationBuilder.AddColumn<string>(
                name: "applied_promotions",
                schema: "ordering",
                table: "orders",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "shipping_base_fee",
                schema: "ordering",
                table: "orders",
                type: "numeric(12,0)",
                precision: 12,
                scale: 0,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "shipping_discount",
                schema: "ordering",
                table: "orders",
                type: "numeric(12,0)",
                precision: 12,
                scale: 0,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "discount_amount",
                schema: "ordering",
                table: "order_items",
                type: "numeric(12,0)",
                precision: 12,
                scale: 0,
                nullable: false,
                defaultValue: 0m);

            // Đơn cũ: phí thu = phí theo biểu giá (không biết đơn nào từng được miễn phí theo ngưỡng). Bắt buộc chạy
            // trước check constraint shipping_base_fee >= shipping_fee, nếu không migration hỏng trên DB đã có đơn.
            migrationBuilder.Sql("UPDATE ordering.orders SET shipping_base_fee = shipping_fee;");

            migrationBuilder.AddCheckConstraint(
                name: "ck_orders_amounts",
                schema: "ordering",
                table: "orders",
                sql: "subtotal >= 0 AND discount >= 0 AND discount <= subtotal AND shipping_fee >= 0 AND shipping_base_fee >= shipping_fee AND shipping_discount >= 0 AND shipping_discount <= shipping_fee AND total >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_order_items_discount",
                schema: "ordering",
                table: "order_items",
                sql: "discount_amount >= 0 AND discount_amount <= line_total");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_orders_amounts",
                schema: "ordering",
                table: "orders");

            migrationBuilder.DropCheckConstraint(
                name: "ck_order_items_discount",
                schema: "ordering",
                table: "order_items");

            migrationBuilder.DropColumn(
                name: "applied_promotions",
                schema: "ordering",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "shipping_base_fee",
                schema: "ordering",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "shipping_discount",
                schema: "ordering",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "discount_amount",
                schema: "ordering",
                table: "order_items");

            migrationBuilder.AddCheckConstraint(
                name: "ck_orders_amounts",
                schema: "ordering",
                table: "orders",
                sql: "subtotal >= 0 AND discount >= 0 AND shipping_fee >= 0 AND total >= 0");
        }
    }
}
