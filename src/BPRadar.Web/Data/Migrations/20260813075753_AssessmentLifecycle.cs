using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BPRadar.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AssessmentLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BaselineProfiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OrganizationId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    IsDefault = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BaselineProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BaselineProfiles_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Assessments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OrganizationId = table.Column<int>(type: "INTEGER", nullable: false),
                    FrameworkId = table.Column<int>(type: "INTEGER", nullable: false),
                    BaselineProfileId = table.Column<int>(type: "INTEGER", nullable: true),
                    Label = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    SnapshotDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Assessments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Assessments_BaselineProfiles_BaselineProfileId",
                        column: x => x.BaselineProfileId,
                        principalTable: "BaselineProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Assessments_Frameworks_FrameworkId",
                        column: x => x.FrameworkId,
                        principalTable: "Frameworks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Assessments_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BaselineTargets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BaselineProfileId = table.Column<int>(type: "INTEGER", nullable: false),
                    FrameworkId = table.Column<int>(type: "INTEGER", nullable: false),
                    DomainId = table.Column<int>(type: "INTEGER", nullable: true),
                    TargetCompliancePercent = table.Column<decimal>(type: "TEXT", precision: 5, scale: 2, nullable: true),
                    TargetScore = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BaselineTargets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BaselineTargets_BaselineProfiles_BaselineProfileId",
                        column: x => x.BaselineProfileId,
                        principalTable: "BaselineProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BaselineTargets_Domains_DomainId",
                        column: x => x.DomainId,
                        principalTable: "Domains",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BaselineTargets_Frameworks_FrameworkId",
                        column: x => x.FrameworkId,
                        principalTable: "Frameworks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AssessmentResults",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AssessmentId = table.Column<int>(type: "INTEGER", nullable: false),
                    ControlId = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Score = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    EvidenceUrl = table.Column<string>(type: "TEXT", nullable: true),
                    Source = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssessmentResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssessmentResults_Assessments_AssessmentId",
                        column: x => x.AssessmentId,
                        principalTable: "Assessments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AssessmentResults_Controls_ControlId",
                        column: x => x.ControlId,
                        principalTable: "Controls",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AssessmentResults_AssessmentId_ControlId",
                table: "AssessmentResults",
                columns: new[] { "AssessmentId", "ControlId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssessmentResults_ControlId",
                table: "AssessmentResults",
                column: "ControlId");

            migrationBuilder.CreateIndex(
                name: "IX_Assessments_BaselineProfileId",
                table: "Assessments",
                column: "BaselineProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_Assessments_FrameworkId",
                table: "Assessments",
                column: "FrameworkId");

            migrationBuilder.CreateIndex(
                name: "IX_Assessments_OrganizationId",
                table: "Assessments",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_BaselineProfiles_OrganizationId",
                table: "BaselineProfiles",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_BaselineTargets_BaselineProfileId_FrameworkId",
                table: "BaselineTargets",
                columns: new[] { "BaselineProfileId", "FrameworkId" },
                unique: true,
                filter: "\"DomainId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BaselineTargets_BaselineProfileId_FrameworkId_DomainId",
                table: "BaselineTargets",
                columns: new[] { "BaselineProfileId", "FrameworkId", "DomainId" },
                unique: true,
                filter: "\"DomainId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BaselineTargets_DomainId",
                table: "BaselineTargets",
                column: "DomainId");

            migrationBuilder.CreateIndex(
                name: "IX_BaselineTargets_FrameworkId",
                table: "BaselineTargets",
                column: "FrameworkId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AssessmentResults");

            migrationBuilder.DropTable(
                name: "BaselineTargets");

            migrationBuilder.DropTable(
                name: "Assessments");

            migrationBuilder.DropTable(
                name: "BaselineProfiles");
        }
    }
}
