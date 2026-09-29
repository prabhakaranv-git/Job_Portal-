using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobPortal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase3CompanyJobManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Jobs_RecruiterId",
                table: "Jobs");

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_CreatedAt",
                table: "Jobs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_ExpiresAt",
                table: "Jobs",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_JobType",
                table: "Jobs",
                column: "JobType");

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_Location",
                table: "Jobs",
                column: "Location");

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_RecruiterId_Status",
                table: "Jobs",
                columns: new[] { "RecruiterId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_Status_CreatedAt",
                table: "Jobs",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_Status_ExpiresAt",
                table: "Jobs",
                columns: new[] { "Status", "ExpiresAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Jobs_CreatedAt",
                table: "Jobs");

            migrationBuilder.DropIndex(
                name: "IX_Jobs_ExpiresAt",
                table: "Jobs");

            migrationBuilder.DropIndex(
                name: "IX_Jobs_JobType",
                table: "Jobs");

            migrationBuilder.DropIndex(
                name: "IX_Jobs_Location",
                table: "Jobs");

            migrationBuilder.DropIndex(
                name: "IX_Jobs_RecruiterId_Status",
                table: "Jobs");

            migrationBuilder.DropIndex(
                name: "IX_Jobs_Status_CreatedAt",
                table: "Jobs");

            migrationBuilder.DropIndex(
                name: "IX_Jobs_Status_ExpiresAt",
                table: "Jobs");

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_RecruiterId",
                table: "Jobs",
                column: "RecruiterId");
        }
    }
}
