using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskManager.Api.Migrations
{
    /// <inheritdoc />
    public partial class TaskItem_AddOwnerNavigation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 0) На всякий случай: если есть FK — снимем (старый/частично применённый)
            migrationBuilder.Sql(@"
        IF EXISTS (
            SELECT 1 FROM sys.foreign_keys 
            WHERE name = 'FK_Tasks_AspNetUsers_OwnerId' AND parent_object_id = OBJECT_ID('dbo.Tasks')
        )
        BEGIN
            ALTER TABLE [dbo].[Tasks] DROP CONSTRAINT [FK_Tasks_AspNetUsers_OwnerId];
        END
        ");

            // 1) Индекс IX_Tasks_OwnerId — если есть, снимем перед изменением столбца
            migrationBuilder.Sql(@"
        IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Tasks_OwnerId' AND object_id = OBJECT_ID('dbo.Tasks'))
        BEGIN
            DROP INDEX [IX_Tasks_OwnerId] ON [dbo].[Tasks];
        END
        ");

            // 2) Привести OwnerId к nvarchar(450) NULL, только если это ещё не так
            migrationBuilder.Sql(@"
        IF EXISTS (
            SELECT 1
            FROM sys.columns c
            JOIN sys.types t ON c.user_type_id = t.user_type_id
            WHERE c.object_id = OBJECT_ID('dbo.Tasks')
            AND c.name = 'OwnerId'
            AND (c.max_length <> 900 OR t.name <> 'nvarchar' OR c.is_nullable = 0)
        )
        BEGIN
            -- снять дефолт-constraint если есть
            DECLARE @dc sysname;
            SELECT @dc = d.[name]
            FROM sys.default_constraints d
            JOIN sys.columns c ON d.parent_column_id = c.column_id AND d.parent_object_id = c.object_id
            WHERE d.parent_object_id = OBJECT_ID('dbo.Tasks') AND c.name = 'OwnerId';
            IF @dc IS NOT NULL EXEC('ALTER TABLE [dbo].[Tasks] DROP CONSTRAINT [' + @dc + ']');

            ALTER TABLE [dbo].[Tasks] ALTER COLUMN [OwnerId] nvarchar(450) NULL;
        END
        ");

            // 3) Колонка isPublic — создать, если её ещё нет
            migrationBuilder.Sql(@"
        IF COL_LENGTH('dbo.Tasks', 'isPublic') IS NULL
        BEGIN
            ALTER TABLE [dbo].[Tasks] ADD [isPublic] bit NOT NULL CONSTRAINT DF_Tasks_isPublic DEFAULT(0);
        END
        ");

            // 4) Почистить данные: OwnerId -> NULL, если нет такого пользователя или пустая строка
            migrationBuilder.Sql(@"
        UPDATE T SET T.OwnerId = NULL
        FROM [dbo].[Tasks] AS T
        LEFT JOIN [dbo].[AspNetUsers] AS U ON U.[Id] = T.[OwnerId]
        WHERE U.[Id] IS NULL OR T.[OwnerId] = '';
        ");

            // 5) Восстановить индекс IX_Tasks_OwnerId, если его нет
            migrationBuilder.Sql(@"
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Tasks_OwnerId' AND object_id = OBJECT_ID('dbo.Tasks'))
        BEGIN
            CREATE INDEX [IX_Tasks_OwnerId] ON [dbo].[Tasks]([OwnerId]);
        END
        ");

            // 6) Добавить FK, если его ещё нет
            migrationBuilder.Sql(@"
        IF NOT EXISTS (
            SELECT 1 FROM sys.foreign_keys 
            WHERE name = 'FK_Tasks_AspNetUsers_OwnerId' AND parent_object_id = OBJECT_ID('dbo.Tasks')
        )
        BEGIN
            ALTER TABLE [dbo].[Tasks]
            ADD CONSTRAINT [FK_Tasks_AspNetUsers_OwnerId]
                FOREIGN KEY ([OwnerId]) REFERENCES [dbo].[AspNetUsers]([Id]) ON DELETE SET NULL;
        END
        ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // снять FK, если есть
            migrationBuilder.Sql(@"
        IF EXISTS (
            SELECT 1 FROM sys.foreign_keys 
            WHERE name = 'FK_Tasks_AspNetUsers_OwnerId' AND parent_object_id = OBJECT_ID('dbo.Tasks')
        )
        BEGIN
            ALTER TABLE [dbo].[Tasks] DROP CONSTRAINT [FK_Tasks_AspNetUsers_OwnerId];
        END
        ");

            // снять индекс, если есть
            migrationBuilder.Sql(@"
        IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Tasks_OwnerId' AND object_id = OBJECT_ID('dbo.Tasks'))
        BEGIN
            DROP INDEX [IX_Tasks_OwnerId] ON [dbo].[Tasks];
        END
        ");

            // вернуть OwnerId к старому типу (как было в auto-Down)
            migrationBuilder.Sql(@"
        IF EXISTS (
            SELECT 1
            FROM sys.columns c
            JOIN sys.types t ON c.user_type_id = t.user_type_id
            WHERE c.object_id = OBJECT_ID('dbo.Tasks')
            AND c.name = 'OwnerId'
            AND (c.max_length <> -1 OR t.name <> 'nvarchar' OR c.is_nullable = 1)
        )
        BEGIN
            ALTER TABLE [dbo].[Tasks] ALTER COLUMN [OwnerId] nvarchar(max) NOT NULL;
        END
        ");

            // снести default + колонку isPublic (опционально, для симметрии)
            migrationBuilder.Sql(@"
        IF OBJECT_ID('DF_Tasks_isPublic', 'D') IS NOT NULL
        BEGIN
            ALTER TABLE [dbo].[Tasks] DROP CONSTRAINT DF_Tasks_isPublic;
        END
        IF COL_LENGTH('dbo.Tasks', 'isPublic') IS NOT NULL
        BEGIN
            ALTER TABLE [dbo].[Tasks] DROP COLUMN [isPublic];
        END
        ");
        }
    }
}
