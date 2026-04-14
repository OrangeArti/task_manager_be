using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskManager.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskStatusEnum : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Tasks",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Todo");

            migrationBuilder.Sql(
                "UPDATE Tasks SET Status = CASE WHEN IsCompleted = 1 THEN 'Done' ELSE 'Todo' END");

            migrationBuilder.DropColumn(
                name: "IsCompleted",
                table: "Tasks");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsCompleted",
                table: "Tasks",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql(
                "UPDATE Tasks SET IsCompleted = CASE WHEN Status = 'Done' THEN 1 ELSE 0 END");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Tasks");
        }
    }
}
