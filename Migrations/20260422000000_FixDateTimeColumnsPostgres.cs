using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CVWebsite.Migrations
{
    /// <inheritdoc />
    public partial class FixDateTimeColumnsPostgres : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Only needed on PostgreSQL — SQLite/SQL Server store DateTime natively.
            if (migrationBuilder.ActiveProvider != "Npgsql.EntityFrameworkCore.PostgreSQL")
                return;

            migrationBuilder.Sql("""
                ALTER TABLE "Users"
                    ALTER COLUMN "CreatedAt"       TYPE timestamp with time zone
                        USING "CreatedAt"::timestamp with time zone,
                    ALTER COLUMN "PlanActivatedAt" TYPE timestamp with time zone
                        USING "PlanActivatedAt"::timestamp with time zone;

                ALTER TABLE "PendingPayments"
                    ALTER COLUMN "CreatedAt" TYPE timestamp with time zone
                        USING "CreatedAt"::timestamp with time zone;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            if (migrationBuilder.ActiveProvider != "Npgsql.EntityFrameworkCore.PostgreSQL")
                return;

            migrationBuilder.Sql("""
                ALTER TABLE "Users"
                    ALTER COLUMN "CreatedAt"       TYPE text USING "CreatedAt"::text,
                    ALTER COLUMN "PlanActivatedAt" TYPE text USING "PlanActivatedAt"::text;

                ALTER TABLE "PendingPayments"
                    ALTER COLUMN "CreatedAt" TYPE text USING "CreatedAt"::text;
                """);
        }
    }
}
