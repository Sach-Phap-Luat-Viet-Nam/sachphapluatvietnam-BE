using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SachLuat.Modules.Catalog.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUnaccentSearch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // unaccent và pg_trgm là extension "trusted" (PostgreSQL 13+): chủ database cài được, không cần superuser.
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS unaccent SCHEMA public;");
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pg_trgm SCHEMA public;");

            // unaccent() chỉ là STABLE (phụ thuộc search_path) nên không đánh index được; bọc lại thành IMMUTABLE,
            // ghi rõ schema của hàm và từ điển để kết quả không đổi theo search_path.
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION catalog.f_unaccent(text) RETURNS text
                LANGUAGE sql IMMUTABLE PARALLEL SAFE STRICT
                AS $$ SELECT public.unaccent('public.unaccent'::regdictionary, $1) $$;
                """);

            migrationBuilder.Sql("""
                CREATE INDEX ix_books_title_unaccent_trgm
                ON catalog.books USING gin (catalog.f_unaccent(title) public.gin_trgm_ops);
                """);
            migrationBuilder.Sql("""
                CREATE INDEX ix_authors_name_unaccent_trgm
                ON catalog.authors USING gin (catalog.f_unaccent(name) public.gin_trgm_ops);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS catalog.ix_authors_name_unaccent_trgm;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS catalog.ix_books_title_unaccent_trgm;");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS catalog.f_unaccent(text);");

            // Giữ extension: module khác có thể đã dùng.
        }
    }
}
