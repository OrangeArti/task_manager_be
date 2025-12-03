-- List databases
SELECT name FROM sys.databases;

-- Switch to your DB and view tables
USE TaskManagerDb;
SELECT TABLE_SCHEMA, TABLE_NAME
FROM INFORMATION_SCHEMA.TABLES
ORDER BY TABLE_SCHEMA, TABLE_NAME;

-- Check sample table contents
SELECT TOP (5) * FROM dbo.Tasks;
