using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HemaCore.Migrations
{
    /// <inheritdoc />
    public partial class AddLabTechnicianToSample : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LabTechnicianId",
                table: "Samples",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Samples_LabTechnicianId",
                table: "Samples",
                column: "LabTechnicianId");

            migrationBuilder.AddForeignKey(
                name: "FK_Samples_LabTechnicians_LabTechnicianId",
                table: "Samples",
                column: "LabTechnicianId",
                principalTable: "LabTechnicians",
                principalColumn: "LabTechnicianId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Samples_LabTechnicians_LabTechnicianId",
                table: "Samples");

            migrationBuilder.DropIndex(
                name: "IX_Samples_LabTechnicianId",
                table: "Samples");

            migrationBuilder.DropColumn(
                name: "LabTechnicianId",
                table: "Samples");
        }
    }
}
