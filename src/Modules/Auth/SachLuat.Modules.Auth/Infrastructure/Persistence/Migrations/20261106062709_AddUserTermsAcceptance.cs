using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SachLuat.Modules.Auth.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserTermsAcceptance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "terms_accepted_at",
                schema: "auth",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "terms_version",
                schema: "auth",
                table: "users",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "terms_accepted_at",
                schema: "auth",
                table: "users");

            migrationBuilder.DropColumn(
                name: "terms_version",
                schema: "auth",
                table: "users");
        }
    }
}
