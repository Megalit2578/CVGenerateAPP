-- ============================================================
--  CV Builder Pro — PostgreSQL init script
--  Dùng trên Railway PostgreSQL hoặc psql local
--  App sẽ tự chạy migration khi khởi động — script này
--  chỉ cần dùng nếu muốn tạo bảng thủ công.
-- ============================================================

-- ── Bảng Users ───────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS "Users" (
    "Id"              SERIAL          PRIMARY KEY,
    "Email"           VARCHAR(256)    NOT NULL,
    "PasswordHash"    VARCHAR(100)    NOT NULL,
    "Plan"            VARCHAR(20)     NOT NULL DEFAULT 'free',
    "CreatedAt"       TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    "PlanActivatedAt" TIMESTAMPTZ     NULL,

    CONSTRAINT "CK_Users_Plan" CHECK ("Plan" IN ('free', 'pro', 'business'))
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_Users_Email" ON "Users" ("Email");

-- ── Bảng PendingPayments ─────────────────────────────────────
CREATE TABLE IF NOT EXISTS "PendingPayments" (
    "Id"        SERIAL          PRIMARY KEY,
    "OrderCode" BIGINT          NOT NULL,
    "UserId"    INT             NULL REFERENCES "Users" ("Id"),
    "Plan"      VARCHAR(20)     NOT NULL,
    "CreatedAt" TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    "Fulfilled" BOOLEAN         NOT NULL DEFAULT FALSE,

    CONSTRAINT "CK_PendingPayments_Plan" CHECK ("Plan" IN ('pro', 'business'))
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_PendingPayments_OrderCode" ON "PendingPayments" ("OrderCode");
CREATE        INDEX IF NOT EXISTS "IX_PendingPayments_UserId"    ON "PendingPayments" ("UserId");

-- ── Bảng EF Migrations History ───────────────────────────────
CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId"    VARCHAR(150) PRIMARY KEY,
    "ProductVersion" VARCHAR(32)  NOT NULL
);

-- Đánh dấu migration đã chạy (để EF không tạo lại)
INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260421031126_InitialCreate', '8.0.10')
ON CONFLICT DO NOTHING;

-- ── Kiểm tra kết quả ─────────────────────────────────────────
SELECT table_name, column_name, data_type, is_nullable
FROM information_schema.columns
WHERE table_name IN ('Users', 'PendingPayments')
ORDER BY table_name, ordinal_position;
