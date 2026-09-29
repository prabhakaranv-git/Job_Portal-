using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobPortal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase4ResumeAndApplications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Resumes_JobSeekerId",
                table: "Resumes");

            migrationBuilder.AddColumn<string>(
                name: "StoredFileName",
                table: "Resumes",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Resumes_JobSeekerId",
                table: "Resumes",
                column: "JobSeekerId",
                unique: true,
                filter: "\"IsDefault\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_Resumes_JobSeekerId_IsDefault",
                table: "Resumes",
                columns: new[] { "JobSeekerId", "IsDefault" });

            migrationBuilder.CreateIndex(
                name: "IX_Applications_AppliedAt",
                table: "Applications",
                column: "AppliedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Applications_JobId",
                table: "Applications",
                column: "JobId");

            migrationBuilder.CreateIndex(
                name: "IX_Applications_JobId_Status",
                table: "Applications",
                columns: new[] { "JobId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Applications_JobSeekerId_AppliedAt",
                table: "Applications",
                columns: new[] { "JobSeekerId", "AppliedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Resumes_JobSeekerId",
                table: "Resumes");

            migrationBuilder.DropIndex(
                name: "IX_Resumes_JobSeekerId_IsDefault",
                table: "Resumes");

            migrationBuilder.DropIndex(
                name: "IX_Applications_AppliedAt",
                table: "Applications");

            migrationBuilder.DropIndex(
                name: "IX_Applications_JobId",
                table: "Applications");

            migrationBuilder.DropIndex(
                name: "IX_Applications_JobId_Status",
                table: "Applications");

            migrationBuilder.DropIndex(
                name: "IX_Applications_JobSeekerId_AppliedAt",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "StoredFileName",
                table: "Resumes");

            migrationBuilder.CreateIndex(
                name: "IX_Resumes_JobSeekerId",
                table: "Resumes",
                column: "JobSeekerId");
        }
    }
}
