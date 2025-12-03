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
            // 0) Just in case: drop FK if it exists (old/partially applied)
            migrationBuilder.Sql(@"
        IF EXISTS (
            SELECT 1 FROM sys.foreign_keys 
            WHERE name = 'FK_Tasks_AspNetUsers_OwnerId' AND parent_object_id = OBJECT_ID('dbo.Tasks')
        )
        BEGIN
            ALTER TABLE [dbo].[Tasks] DROP CONSTRAINT [FK_Tasks_AspNetUsers_OwnerId];
        END
        ");

            // 1) Drop IX_Tasks_OwnerId index before changing the column, if it exists
            migrationBuilder.Sql(@"
        IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Tasks_OwnerId' AND object_id = OBJECT_ID('dbo.Tasks'))
        BEGIN
            DROP INDEX [IX_Tasks_OwnerId] ON [dbo].[Tasks];
        END
        ");

            // 2) Convert OwnerId to nvarchar(450) NULL if it is still in the old shape
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
            -- drop default constraint if it exists
            DECLARE @dc sysname;
            SELECT @dc = d.[name]
            FROM sys.default_constraints d
            JOIN sys.columns c ON d.parent_column_id = c.column_id AND d.parent_object_id = c.object_id
            WHERE d.parent_object_id = OBJECT_ID('dbo.Tasks') AND c.name = 'OwnerId';
            IF @dc IS NOT NULL EXEC('ALTER TABLE [dbo].[Tasks] DROP CONSTRAINT [' + @dc + ']');

            ALTER TABLE [dbo].[Tasks] ALTER COLUMN [OwnerId] nvarchar(450) NULL;
        END
        ");

            // 3) Add isPublic column if it does not exist
            migrationBuilder.Sql(@"
        IF COL_LENGTH('dbo.Tasks', 'isPublic') IS NULL
        BEGIN
            ALTER TABLE [dbo].[Tasks] ADD [isPublic] bit NOT NULL CONSTRAINT DF_Tasks_isPublic DEFAULT(0);
        END
        ");

            // 4) Clean data: OwnerId -> NULL if the user does not exist or value is empty
            migrationBuilder.Sql(@"
        UPDATE T SET T.OwnerId = NULL
        FROM [dbo].[Tasks] AS T
        LEFT JOIN [dbo].[AspNetUsers] AS U ON U.[Id] = T.[OwnerId]
        WHERE U.[Id] IS NULL OR T.[OwnerId] = '';
        ");

            // 5) Recreate IX_Tasks_OwnerId index if missing
            migrationBuilder.Sql(@"
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Tasks_OwnerId' AND object_id = OBJECT_ID('dbo.Tasks'))
        BEGIN
            CREATE INDEX [IX_Tasks_OwnerId] ON [dbo].[Tasks]([OwnerId]);
        END
        ");

            // 6) Add FK if missing
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
            // Drop FK if it exists
            migrationBuilder.Sql(@"
        IF EXISTS (
            SELECT 1 FROM sys.foreign_keys 
            WHERE name = 'FK_Tasks_AspNetUsers_OwnerId' AND parent_object_id = OBJECT_ID('dbo.Tasks')
        )
        BEGIN
            ALTER TABLE [dbo].[Tasks] DROP CONSTRAINT [FK_Tasks_AspNetUsers_OwnerId];
        END
        ");

            // Drop index if it exists
            migrationBuilder.Sql(@"
        IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Tasks_OwnerId' AND object_id = OBJECT_ID('dbo.Tasks'))
        BEGIN
            DROP INDEX [IX_Tasks_OwnerId] ON [dbo].[Tasks];
        END
        ");

            // Revert OwnerId to the old type (same as auto-Down)
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

            // Remove default and isPublic column (optional for symmetry)
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
