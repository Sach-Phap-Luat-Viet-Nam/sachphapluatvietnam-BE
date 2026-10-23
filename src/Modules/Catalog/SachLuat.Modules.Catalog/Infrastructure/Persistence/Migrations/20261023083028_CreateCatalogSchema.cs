using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SachLuat.Modules.Catalog.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CreateCatalogSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "catalog");

            migrationBuilder.CreateTable(
                name: "authors",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    slug = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    biography = table.Column<string>(type: "text", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_authors", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "books",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    title = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    slug = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    book_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    target_audiences = table.Column<string[]>(type: "character varying(30)[]", nullable: false),
                    meta_title = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    meta_description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_books", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "categories",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    slug = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    parent_id = table.Column<long>(type: "bigint", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_categories", x => x.id);
                    table.ForeignKey(
                        name: "fk_categories_categories_parent_id",
                        column: x => x.parent_id,
                        principalSchema: "catalog",
                        principalTable: "categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "legal_documents",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    doc_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    doc_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    issued_date = table.Column<DateOnly>(type: "date", nullable: true),
                    effective_date = table.Column<DateOnly>(type: "date", nullable: true),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_legal_documents", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "book_authors",
                schema: "catalog",
                columns: table => new
                {
                    book_id = table.Column<long>(type: "bigint", nullable: false),
                    author_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_book_authors", x => new { x.book_id, x.author_id });
                    table.ForeignKey(
                        name: "fk_book_authors_authors_author_id",
                        column: x => x.author_id,
                        principalSchema: "catalog",
                        principalTable: "authors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_book_authors_books_book_id",
                        column: x => x.book_id,
                        principalSchema: "catalog",
                        principalTable: "books",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "editions",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    book_id = table.Column<long>(type: "bigint", nullable: false),
                    edition_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    isbn = table.Column<string>(type: "character(13)", fixedLength: true, maxLength: 13, nullable: false),
                    publisher = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    publishing_decision_no = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    registration_no = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    publish_year = table.Column<short>(type: "smallint", nullable: true),
                    updated_to_text = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    updated_to_date = table.Column<DateOnly>(type: "date", nullable: true),
                    pages = table.Column<int>(type: "integer", nullable: true),
                    size = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    weight_gram = table.Column<int>(type: "integer", nullable: true),
                    cover_image_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    preview_file_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    table_of_contents = table.Column<string>(type: "text", nullable: true),
                    release_date = table.Column<DateOnly>(type: "date", nullable: true),
                    is_latest = table.Column<bool>(type: "boolean", nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_editions", x => x.id);
                    table.CheckConstraint("ck_editions_isbn_format", "isbn ~ '^97[89][0-9]{10}$'");
                    table.CheckConstraint("ck_editions_pages_positive", "pages IS NULL OR pages > 0");
                    table.CheckConstraint("ck_editions_weight_positive", "weight_gram IS NULL OR weight_gram > 0");
                    table.ForeignKey(
                        name: "fk_editions_books_book_id",
                        column: x => x.book_id,
                        principalSchema: "catalog",
                        principalTable: "books",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "book_categories",
                schema: "catalog",
                columns: table => new
                {
                    book_id = table.Column<long>(type: "bigint", nullable: false),
                    category_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_book_categories", x => new { x.book_id, x.category_id });
                    table.ForeignKey(
                        name: "fk_book_categories_books_book_id",
                        column: x => x.book_id,
                        principalSchema: "catalog",
                        principalTable: "books",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_book_categories_categories_category_id",
                        column: x => x.category_id,
                        principalSchema: "catalog",
                        principalTable: "categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "edition_legal_documents",
                schema: "catalog",
                columns: table => new
                {
                    edition_id = table.Column<long>(type: "bigint", nullable: false),
                    legal_document_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_edition_legal_documents", x => new { x.edition_id, x.legal_document_id });
                    table.ForeignKey(
                        name: "fk_edition_legal_documents_editions_edition_id",
                        column: x => x.edition_id,
                        principalSchema: "catalog",
                        principalTable: "editions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_edition_legal_documents_legal_documents_legal_document_id",
                        column: x => x.legal_document_id,
                        principalSchema: "catalog",
                        principalTable: "legal_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "skus",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    edition_id = table.Column<long>(type: "bigint", nullable: false),
                    sku_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    cover_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    list_price = table.Column<decimal>(type: "numeric(12,0)", precision: 12, scale: 0, nullable: false),
                    sale_price = table.Column<decimal>(type: "numeric(12,0)", precision: 12, scale: 0, nullable: false),
                    allow_preorder = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_skus", x => x.id);
                    table.CheckConstraint("ck_skus_prices", "list_price > 0 AND sale_price > 0 AND sale_price <= list_price");
                    table.ForeignKey(
                        name: "fk_skus_editions_edition_id",
                        column: x => x.edition_id,
                        principalSchema: "catalog",
                        principalTable: "editions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_authors_slug",
                schema: "catalog",
                table: "authors",
                column: "slug",
                unique: true,
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_book_authors_author_id",
                schema: "catalog",
                table: "book_authors",
                column: "author_id");

            migrationBuilder.CreateIndex(
                name: "ix_book_categories_category_id",
                schema: "catalog",
                table: "book_categories",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ix_books_slug",
                schema: "catalog",
                table: "books",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_books_status",
                schema: "catalog",
                table: "books",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_categories_parent_id",
                schema: "catalog",
                table: "categories",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "ix_categories_slug",
                schema: "catalog",
                table: "categories",
                column: "slug",
                unique: true,
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_edition_legal_documents_legal_document_id",
                schema: "catalog",
                table: "edition_legal_documents",
                column: "legal_document_id");

            migrationBuilder.CreateIndex(
                name: "ix_editions_book_id",
                schema: "catalog",
                table: "editions",
                column: "book_id");

            migrationBuilder.CreateIndex(
                name: "ix_editions_isbn",
                schema: "catalog",
                table: "editions",
                column: "isbn",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_editions_book_id_latest",
                schema: "catalog",
                table: "editions",
                column: "book_id",
                unique: true,
                filter: "is_latest");

            migrationBuilder.CreateIndex(
                name: "ix_legal_documents_doc_number",
                schema: "catalog",
                table: "legal_documents",
                column: "doc_number");

            migrationBuilder.CreateIndex(
                name: "ix_skus_edition_id_cover_type",
                schema: "catalog",
                table: "skus",
                columns: new[] { "edition_id", "cover_type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_skus_sku_code",
                schema: "catalog",
                table: "skus",
                column: "sku_code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "book_authors",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "book_categories",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "edition_legal_documents",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "skus",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "authors",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "categories",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "legal_documents",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "editions",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "books",
                schema: "catalog");
        }
    }
}
