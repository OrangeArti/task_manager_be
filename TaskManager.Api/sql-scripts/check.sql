-- Список баз
SELECT name FROM sys.databases;

-- Перейти в вашу БД и посмотреть таблицы
USE TaskManagerDb;
SELECT TABLE_SCHEMA, TABLE_NAME
FROM INFORMATION_SCHEMA.TABLES
ORDER BY TABLE_SCHEMA, TABLE_NAME;

-- Проверить содержимое таблицы
SELECT TOP (5) * FROM dbo.Tasks;