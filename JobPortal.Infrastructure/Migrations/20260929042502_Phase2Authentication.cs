using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobPortal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase2Authentication : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ApprovalStatus",
                table: "Recruiters",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedAt",
                table: "Recruiters",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RejectedAt",
                table: "Recruiters",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "Recruiters",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Recruiters_ApprovalStatus",
                table: "Recruiters",
                column: "ApprovalStatus");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Recruiters_ApprovalStatus",
                table: "Recruiters");

            migrationBuilder.DropColumn(
                name: "ApprovalStatus",
                table: "Recruiters");

            migrationBuilder.DropColumn(
                name: "ApprovedAt",
                table: "Recruiters");

            migrationBuilder.DropColumn(
                name: "RejectedAt",
                table: "Recruiters");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "Recruiters");
        }
    }
}
