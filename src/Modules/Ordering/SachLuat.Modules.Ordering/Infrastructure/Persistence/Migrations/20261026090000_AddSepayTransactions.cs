using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SachLuat.Modules.Ordering.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSepayTransactions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sepay_transactions",
                schema: "ordering",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    sepay_id = table.Column<long>(type: "bigint", nullable: false),
                    gateway = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    account_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    content = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    reference_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    amount = table.Column<decimal>(type: "numeric(14,0)", precision: 14, scale: 0, nullable: false),
                    transaction_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    order_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    result = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    received_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sepay_transactions", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_sepay_transactions_result_received_at",
                schema: "ordering",
                table: "sepay_transactions",
                columns: new[] { "result", "received_at" });

            migrationBuilder.CreateIndex(
                name: "ix_sepay_transactions_sepay_id",
                schema: "ordering",
                table: "sepay_transactions",
                column: "sepay_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sepay_transactions",
                schema: "ordering");
        }
    }
}
