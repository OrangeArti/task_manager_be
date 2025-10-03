using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskManager.Api.Migrations
{
    /// <inheritdoc />
    public partial class TaskItem_ProblemFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsProblem",
                table: "Tasks",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ProblemDescription",
                table: "Tasks",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ProblemReportedAt",
                table: "Tasks",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProblemReporterId",
                table: "Tasks",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_IsProblem",
                table: "Tasks",
                column: "IsProblem");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tasks_IsProblem",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "IsProblem",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "ProblemDescription",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "ProblemReportedAt",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "ProblemReporterId",
                table: "Tasks");
        }
    }
}
