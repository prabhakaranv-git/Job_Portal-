using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobPortal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase5RecruiterNotesAndCandidateFeedback : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CandidateFeedbacks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecruiterId = table.Column<Guid>(type: "uuid", nullable: false),
                    OverallRating = table.Column<int>(type: "integer", nullable: false),
                    Strengths = table.Column<string>(type: "character varying(3000)", maxLength: 3000, nullable: true),
                    Weaknesses = table.Column<string>(type: "character varying(3000)", maxLength: 3000, nullable: true),
                    Recommendation = table.Column<int>(type: "integer", nullable: false),
                    DetailedFeedback = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CandidateFeedbacks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CandidateFeedbacks_Applications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalTable: "Applications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CandidateFeedbacks_Recruiters_RecruiterId",
                        column: x => x.RecruiterId,
                        principalTable: "Recruiters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RecruiterNotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecruiterId = table.Column<Guid>(type: "uuid", nullable: false),
                    Content = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecruiterNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecruiterNotes_Applications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalTable: "Applications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RecruiterNotes_Recruiters_RecruiterId",
                        column: x => x.RecruiterId,
                        principalTable: "Recruiters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CandidateFeedbacks_ApplicationId",
                table: "CandidateFeedbacks",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_CandidateFeedbacks_ApplicationId_RecruiterId",
                table: "CandidateFeedbacks",
                columns: new[] { "ApplicationId", "RecruiterId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CandidateFeedbacks_RecruiterId",
                table: "CandidateFeedbacks",
                column: "RecruiterId");

            migrationBuilder.CreateIndex(
                name: "IX_RecruiterNotes_ApplicationId",
                table: "RecruiterNotes",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_RecruiterNotes_ApplicationId_CreatedAt",
                table: "RecruiterNotes",
                columns: new[] { "ApplicationId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RecruiterNotes_RecruiterId",
                table: "RecruiterNotes",
                column: "RecruiterId");

            migrationBuilder.CreateIndex(
                name: "IX_RecruiterNotes_RecruiterId_CreatedAt",
                table: "RecruiterNotes",
                columns: new[] { "RecruiterId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CandidateFeedbacks");

            migrationBuilder.DropTable(
                name: "RecruiterNotes");
        }
    }
}
