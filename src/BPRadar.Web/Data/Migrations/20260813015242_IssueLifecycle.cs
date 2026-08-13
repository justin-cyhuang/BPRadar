using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BPRadar.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class IssueLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Issues",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OrganizationId = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    RootCause = table.Column<string>(type: "TEXT", nullable: false),
                    MatchingStatus = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    MatchingError = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    MatchedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Issues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Issues_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ViolationMatches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    IssueId = table.Column<int>(type: "INTEGER", nullable: false),
                    ControlId = table.Column<int>(type: "INTEGER", nullable: false),
                    MatchedKeywords = table.Column<string>(type: "TEXT", nullable: false),
                    MatchScore = table.Column<decimal>(type: "TEXT", precision: 5, scale: 4, nullable: false),
                    IsSelfAssessmentDiscrepancy = table.Column<bool>(type: "INTEGER", nullable: false),
                    ReviewStatus = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ViolationMatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ViolationMatches_Controls_ControlId",
                        column: x => x.ControlId,
                        principalTable: "Controls",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ViolationMatches_Issues_IssueId",
                        column: x => x.IssueId,
                        principalTable: "Issues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Issues_OrganizationId",
                table: "Issues",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_ViolationMatches_ControlId",
                table: "ViolationMatches",
                column: "ControlId");

            migrationBuilder.CreateIndex(
                name: "IX_ViolationMatches_IssueId_ControlId",
                table: "ViolationMatches",
                columns: new[] { "IssueId", "ControlId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ViolationMatches");

            migrationBuilder.DropTable(
                name: "Issues");
        }
    }
}
