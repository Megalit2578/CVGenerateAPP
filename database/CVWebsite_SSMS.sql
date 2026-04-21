-- ============================================================
--  CV Builder Pro — SQL Server script
--  Dán vào SSMS và chạy (F5)
--  Tương thích: SQL Server 2016+ / Azure SQL
-- ============================================================

-- ── 1. Tạo database (bỏ qua nếu đã có) ─────────────────────
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'CVWebsite')
BEGIN
    CREATE DATABASE CVWebsite;
    PRINT 'Database CVWebsite created.';
END
GO

USE CVWebsite;
GO

-- ── 2. Xoá bảng cũ nếu cần tạo lại (bỏ comment nếu muốn reset) ──
-- DROP TABLE IF EXISTS PendingPayments;
-- DROP TABLE IF EXISTS Users;
-- DROP TABLE IF EXISTS __EFMigrationsHistory;
-- GO

-- ── 3. Bảng Users ───────────────────────────────────────────
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Users')
BEGIN
    CREATE TABLE Users (
        Id              INT           NOT NULL IDENTITY(1,1),
        Email           NVARCHAR(256) NOT NULL,
        PasswordHash    NVARCHAR(100) NOT NULL,
        [Plan]          NVARCHAR(20)  NOT NULL CONSTRAINT DF_Users_Plan DEFAULT 'free',
        CreatedAt       DATETIME2     NOT NULL CONSTRAINT DF_Users_CreatedAt DEFAULT SYSUTCDATETIME(),
        PlanActivatedAt DATETIME2     NULL,

        CONSTRAINT PK_Users PRIMARY KEY (Id),
        CONSTRAINT CK_Users_Plan CHECK ([Plan] IN ('free', 'pro', 'business'))
    );

    CREATE UNIQUE INDEX IX_Users_Email ON Users (Email);

    PRINT 'Table Users created.';
END
ELSE
    PRINT 'Table Users already exists — skipped.';
GO

-- ── 4. Bảng PendingPayments ──────────────────────────────────
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'PendingPayments')
BEGIN
    CREATE TABLE PendingPayments (
        Id          INT          NOT NULL IDENTITY(1,1),
        OrderCode   BIGINT       NOT NULL,
        UserId      INT          NULL,
        [Plan]      NVARCHAR(20) NOT NULL,
        CreatedAt   DATETIME2    NOT NULL CONSTRAINT DF_PendingPayments_CreatedAt DEFAULT SYSUTCDATETIME(),
        Fulfilled   BIT          NOT NULL CONSTRAINT DF_PendingPayments_Fulfilled DEFAULT 0,

        CONSTRAINT PK_PendingPayments PRIMARY KEY (Id),
        CONSTRAINT FK_PendingPayments_Users FOREIGN KEY (UserId) REFERENCES Users (Id),
        CONSTRAINT CK_PendingPayments_Plan CHECK ([Plan] IN ('pro', 'business'))
    );

    CREATE UNIQUE INDEX IX_PendingPayments_OrderCode ON PendingPayments (OrderCode);
    CREATE INDEX        IX_PendingPayments_UserId    ON PendingPayments (UserId);

    PRINT 'Table PendingPayments created.';
END
ELSE
    PRINT 'Table PendingPayments already exists — skipped.';
GO

-- ── 5. Bảng EF Migrations history ───────────────────────────
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = '__EFMigrationsHistory')
BEGIN
    CREATE TABLE __EFMigrationsHistory (
        MigrationId    NVARCHAR(150) NOT NULL,
        ProductVersion NVARCHAR(32)  NOT NULL,
        CONSTRAINT PK___EFMigrationsHistory PRIMARY KEY (MigrationId)
    );

    INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion)
    VALUES ('20260421031126_InitialCreate', '8.0.10');

    PRINT 'Table __EFMigrationsHistory created.';
END
ELSE
    PRINT 'Table __EFMigrationsHistory already exists — skipped.';
GO

-- ── 6. Kiểm tra kết quả ──────────────────────────────────────
SELECT
    t.name       AS TableName,
    c.name       AS ColumnName,
    tp.name      AS DataType,
    c.max_length AS MaxLength,
    c.is_nullable AS Nullable,
    c.is_identity AS IsIdentity,
    dc.definition AS DefaultValue
FROM sys.tables t
JOIN sys.columns c  ON c.object_id = t.object_id
JOIN sys.types  tp  ON tp.user_type_id = c.user_type_id
LEFT JOIN sys.default_constraints dc
    ON dc.parent_object_id = c.object_id
    AND dc.parent_column_id = c.column_id
WHERE t.name IN ('Users', 'PendingPayments', '__EFMigrationsHistory')
ORDER BY t.name, c.column_id;
GO

PRINT '=== Done! CVWebsite database is ready. ===';
GO
