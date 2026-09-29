using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HemaCore.Migrations
{
    /// <inheritdoc />
    public partial class newConsent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TechnicianTestTypeId",
                table: "TechnicianTestTypes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiresAt",
                table: "Consents",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ConditionId1",
                table: "ConsentConditions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ConsentId1",
                table: "ConsentConditions",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ConsentAllergies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ConsentId = table.Column<int>(type: "int", nullable: false),
                    AllergyId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConsentAllergies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConsentAllergies_Allergies_AllergyId",
                        column: x => x.AllergyId,
                        principalTable: "Allergies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ConsentAllergies_Consents_ConsentId",
                        column: x => x.ConsentId,
                        principalTable: "Consents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ConsentMedications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ConsentId = table.Column<int>(type: "int", nullable: false),
                    MedicationId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConsentMedications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConsentMedications_Consents_ConsentId",
                        column: x => x.ConsentId,
                        principalTable: "Consents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ConsentMedications_Medications_MedicationId",
                        column: x => x.MedicationId,
                        principalTable: "Medications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConsentConditions_ConditionId1",
                table: "ConsentConditions",
                column: "ConditionId1");

            migrationBuilder.CreateIndex(
                name: "IX_ConsentConditions_ConsentId1",
                table: "ConsentConditions",
                column: "ConsentId1");

            migrationBuilder.CreateIndex(
                name: "IX_ConsentAllergies_AllergyId",
                table: "ConsentAllergies",
                column: "AllergyId");

            migrationBuilder.CreateIndex(
                name: "IX_ConsentAllergies_ConsentId",
                table: "ConsentAllergies",
                column: "ConsentId");

            migrationBuilder.CreateIndex(
                name: "IX_ConsentMedications_ConsentId",
                table: "ConsentMedications",
                column: "ConsentId");

            migrationBuilder.CreateIndex(
                name: "IX_ConsentMedications_MedicationId",
                table: "ConsentMedications",
                column: "MedicationId");

            migrationBuilder.AddForeignKey(
                name: "FK_ConsentConditions_Conditions_ConditionId1",
                table: "ConsentConditions",
                column: "ConditionId1",
                principalTable: "Conditions",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ConsentConditions_Consents_ConsentId1",
                table: "ConsentConditions",
                column: "ConsentId1",
                principalTable: "Consents",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ConsentConditions_Conditions_ConditionId1",
                table: "ConsentConditions");

            migrationBuilder.DropForeignKey(
                name: "FK_ConsentConditions_Consents_ConsentId1",
                table: "ConsentConditions");

            migrationBuilder.DropTable(
                name: "ConsentAllergies");

            migrationBuilder.DropTable(
                name: "ConsentMedications");

            migrationBuilder.DropIndex(
                name: "IX_ConsentConditions_ConditionId1",
                table: "ConsentConditions");

            migrationBuilder.DropIndex(
                name: "IX_ConsentConditions_ConsentId1",
                table: "ConsentConditions");

            migrationBuilder.DropColumn(
                name: "TechnicianTestTypeId",
                table: "TechnicianTestTypes");

            migrationBuilder.DropColumn(
                name: "ExpiresAt",
                table: "Consents");

            migrationBuilder.DropColumn(
                name: "ConditionId1",
                table: "ConsentConditions");

            migrationBuilder.DropColumn(
                name: "ConsentId1",
                table: "ConsentConditions");
        }
    }
}
