using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BPRadar.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class ImportExternalRecordId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExternalRecordId",
                table: "AssessmentResults",
                type: "TEXT",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExternalRecordId",
                table: "AssessmentResults");
        }
    }
}
