using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskManager.Api.Migrations
{
    /// <inheritdoc />
    public partial class MigrationB_DropLegacyTeamColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Data migration: create default org + groups + members from old Teams data
            // Runs only if Teams has data and Organizations is empty — safe for fresh and existing DBs.
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM Teams) AND NOT EXISTS (SELECT 1 FROM Organizations)
                BEGIN
                    DECLARE @OwnerId NVARCHAR(450);
                    SELECT TOP 1 @OwnerId = u.Id
                    FROM AspNetUsers u
                    JOIN AspNetUserRoles ur ON ur.UserId = u.Id
                    JOIN AspNetRoles r ON r.Id = ur.RoleId AND r.Name = 'Admin'
                    ORDER BY u.Id;

                    IF @OwnerId IS NULL
                        SELECT TOP 1 @OwnerId = Id FROM AspNetUsers ORDER BY Id;

                    IF @OwnerId IS NOT NULL
                    BEGIN
                        INSERT INTO Organizations (Name, OwnerId, CreatedAt)
                        VALUES ('Default Organization', @OwnerId, GETUTCDATE());

                        DECLARE @OrgId INT = SCOPE_IDENTITY();

                        INSERT INTO Subscriptions (PlanType, OrganizationId, CreatedAt)
                        VALUES ('Free', @OrgId, GETUTCDATE());

                        INSERT INTO OrgMembers (OrgId, UserId, JoinedAt)
                        SELECT @OrgId, Id, GETUTCDATE() FROM AspNetUsers;

                        INSERT INTO Groups (Name, Description, OrganizationId, CreatedAt)
                        SELECT Name, Description, @OrgId, CreatedAt FROM Teams;

                        UPDATE t SET t.GroupId = g.Id
                        FROM Tasks t
                        JOIN Teams tm ON tm.Id = t.TeamId
                        JOIN Groups g ON g.Name = tm.Name AND g.OrganizationId = @OrgId
                        WHERE t.TeamId IS NOT NULL;

                        INSERT INTO GroupMembers (GroupId, UserId, JoinedAt)
                        SELECT g.Id, u.Id, GETUTCDATE()
                        FROM AspNetUsers u
                        JOIN Teams tm ON tm.Id = u.TeamId
                        JOIN Groups g ON g.Name = tm.Name AND g.OrganizationId = @OrgId
                        WHERE u.TeamId IS NOT NULL;
                    END
                END
            ");


            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_Teams_TeamId",
                table: "AspNetUsers");

            migrationBuilder.DropForeignKey(
                name: "FK_Tasks_Teams_TeamId",
                table: "Tasks");

            migrationBuilder.DropTable(
                name: "Teams");

            migrationBuilder.DropIndex(
                name: "IX_Tasks_TeamId",
                table: "Tasks");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_TeamId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "TeamId",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "TeamId",
                table: "AspNetUsers");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TeamId",
                table: "Tasks",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TeamId",
                table: "AspNetUsers",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Teams",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Teams", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_TeamId",
                table: "Tasks",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_TeamId",
                table: "AspNetUsers",
                column: "TeamId");

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_Teams_TeamId",
                table: "AspNetUsers",
                column: "TeamId",
                principalTable: "Teams",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Tasks_Teams_TeamId",
                table: "Tasks",
                column: "TeamId",
                principalTable: "Teams",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
