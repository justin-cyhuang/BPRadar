using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BPRadar.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class UniqueSurveySubmissionSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SurveySubmissions_OrganizationId_SurveyTemplateId_SnapshotDate",
                table: "SurveySubmissions");

            migrationBuilder.CreateIndex(
                name: "IX_SurveySubmissions_OrganizationId_SurveyTemplateId_SnapshotDate",
                table: "SurveySubmissions",
                columns: new[] { "OrganizationId", "SurveyTemplateId", "SnapshotDate" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SurveySubmissions_OrganizationId_SurveyTemplateId_SnapshotDate",
                table: "SurveySubmissions");

            migrationBuilder.CreateIndex(
                name: "IX_SurveySubmissions_OrganizationId_SurveyTemplateId_SnapshotDate",
                table: "SurveySubmissions",
                columns: new[] { "OrganizationId", "SurveyTemplateId", "SnapshotDate" });
        }
    }
}
