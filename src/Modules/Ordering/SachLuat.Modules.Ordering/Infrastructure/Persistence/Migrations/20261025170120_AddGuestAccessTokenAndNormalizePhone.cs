using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SachLuat.Modules.Ordering.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGuestAccessTokenAndNormalizePhone : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "guest_access_token",
                schema: "ordering",
                table: "orders",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            // Đơn cũ lưu SĐT nguyên văn; đưa +84xxxxxxxxx về 0xxxxxxxxx như PhoneNumbers.Normalize để tra cứu khớp.
            // Down không khôi phục được dạng khách đã nhập (không cần: hai dạng là cùng một số).
            migrationBuilder.Sql(
                @"UPDATE ordering.orders SET phone = '0' || substr(phone, 4) WHERE phone ~ '^\+84[0-9]{9}$';");

            migrationBuilder.CreateIndex(
                name: "ix_orders_guest_access_token",
                schema: "ordering",
                table: "orders",
                column: "guest_access_token",
                unique: true,
                filter: "guest_access_token IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_orders_phone",
                schema: "ordering",
                table: "orders",
                column: "phone");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_orders_guest_access_token",
                schema: "ordering",
                table: "orders");

            migrationBuilder.DropIndex(
                name: "ix_orders_phone",
                schema: "ordering",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "guest_access_token",
                schema: "ordering",
                table: "orders");
        }
    }
}
