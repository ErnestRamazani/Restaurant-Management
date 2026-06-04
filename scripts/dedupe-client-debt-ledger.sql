-- One-time cleanup: remove duplicate client debt ledger rows (keeps highest Id per key).
-- Run against your EliteRestaurant PostgreSQL database after backing up.
--
-- Preview duplicates:
-- SELECT "RestaurantClientId", "OrderId", "EntryType", "AmountUsd", COUNT(*)
-- FROM "ClientDebtLedgerEntries"
-- WHERE "OrderId" IS NOT NULL
-- GROUP BY 1, 2, 3, 4
-- HAVING COUNT(*) > 1;

DELETE FROM "ClientDebtLedgerEntries" e
USING (
    SELECT "Id",
           ROW_NUMBER() OVER (
               PARTITION BY "RestaurantClientId", "OrderId", "EntryType",
                            CASE WHEN "EntryType" = 'Payment' THEN "AmountUsd" ELSE NULL END
               ORDER BY "Id" DESC
           ) AS rn
    FROM "ClientDebtLedgerEntries"
    WHERE "OrderId" IS NOT NULL
) d
WHERE e."Id" = d."Id" AND d.rn > 1;

-- Legacy batch payments (no OrderId) — collapse exact amount/note duplicates:
DELETE FROM "ClientDebtLedgerEntries" e
USING (
    SELECT "Id",
           ROW_NUMBER() OVER (
               PARTITION BY "RestaurantClientId", "EntryType", "AmountUsd", "Note"
               ORDER BY "Id" DESC
           ) AS rn
    FROM "ClientDebtLedgerEntries"
    WHERE "OrderId" IS NULL AND "EntryType" = 'Payment'
) d
WHERE e."Id" = d."Id" AND d.rn > 1;
