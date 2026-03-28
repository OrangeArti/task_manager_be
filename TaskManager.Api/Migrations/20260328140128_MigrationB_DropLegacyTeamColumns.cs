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
            // Phase 3 will implement this migration.
            // Planned destructive operations (NOT applied in Phase 2):
            //   - DropColumn("TeamId", "AspNetUsers")
            //   - DropColumn("TeamId", "Tasks")
            //   - DropTable("Teams")
            // Do not add operations here until Phase 3 is ready to execute them.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Inverse of Up() — to be implemented in Phase 3.
        }
    }
}
