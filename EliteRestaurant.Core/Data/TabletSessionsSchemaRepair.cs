using Microsoft.EntityFrameworkCore;

namespace EliteRestaurant.Core.Data;

/// <summary>
/// Self-heal for PostgreSQL databases where EF history says migrations are applied but
/// <c>TabletSessions</c> was never created (e.g. DB created from an older schema snapshot).
/// </summary>
internal static class TabletSessionsSchemaRepair
{
    public static void EnsureTableExists(AppDbContext db)
    {
        const string sql = """
DO $elite$
BEGIN
  IF NOT EXISTS (
    SELECT 1
    FROM pg_catalog.pg_class c
    INNER JOIN pg_catalog.pg_namespace n ON n.oid = c.relnamespace
    WHERE n.nspname = 'public' AND c.relname = 'TabletSessions' AND c.relkind = 'r'
  ) THEN
    CREATE TABLE public."TabletSessions" (
        "Token" character varying(32) NOT NULL,
        "EmployeeId" integer NOT NULL,
        "Portal" text NOT NULL,
        "EmployeeUniqueId" text NOT NULL,
        "Name" text NOT NULL,
        "Role" text NOT NULL,
        "SignInId" text NOT NULL,
        "ExpiresAtUtc" timestamp with time zone NOT NULL,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_TabletSessions" PRIMARY KEY ("Token"),
        CONSTRAINT "FK_TabletSessions_Employees_EmployeeId" FOREIGN KEY ("EmployeeId") REFERENCES public."Employees" ("Id") ON DELETE CASCADE
    );
    CREATE INDEX "IX_TabletSessions_EmployeeId" ON public."TabletSessions" ("EmployeeId");
    CREATE INDEX "IX_TabletSessions_ExpiresAtUtc" ON public."TabletSessions" ("ExpiresAtUtc");
  END IF;
END $elite$;
""";

        db.Database.ExecuteSqlRaw(sql);
    }
}
