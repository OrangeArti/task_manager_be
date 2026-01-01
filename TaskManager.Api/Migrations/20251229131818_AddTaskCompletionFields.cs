using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskManager.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskCompletionFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CompletionComment",
                table: "Tasks",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FinishedByUserId",
                table: "Tasks",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_FinishedByUserId",
                table: "Tasks",
                column: "FinishedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Tasks_AspNetUsers_FinishedByUserId",
                table: "Tasks",
                column: "FinishedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tasks_AspNetUsers_FinishedByUserId",
                table: "Tasks");

            migrationBuilder.DropIndex(
                name: "IX_Tasks_FinishedByUserId",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "CompletionComment",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "FinishedByUserId",
                table: "Tasks");
        }
    }
}
