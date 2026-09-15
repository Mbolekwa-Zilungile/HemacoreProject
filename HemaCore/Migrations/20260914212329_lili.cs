using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HemaCore.Migrations
{
    /// <inheritdoc />
    public partial class lili : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Consents_AspNetUsers_PatientId",
                table: "Consents");

            migrationBuilder.DropForeignKey(
                name: "FK_ConsentTestAccesses_Consents_ConsentId1",
                table: "ConsentTestAccesses");

            migrationBuilder.DropForeignKey(
                name: "FK_PatientAllergies_AspNetUsers_PatientId",
                table: "PatientAllergies");

            migrationBuilder.DropForeignKey(
                name: "FK_PatientConditions_AspNetUsers_PatientId",
                table: "PatientConditions");

            migrationBuilder.DropForeignKey(
                name: "FK_PatientMedications_AspNetUsers_PatientId",
                table: "PatientMedications");

            migrationBuilder.DropForeignKey(
                name: "FK_TestRequests_Patients_PatientId",
                table: "TestRequests");

            migrationBuilder.DropIndex(
                name: "IX_ConsentTestAccesses_ConsentId1",
                table: "ConsentTestAccesses");

            migrationBuilder.DropColumn(
                name: "ConsentId1",
                table: "ConsentTestAccesses");

            migrationBuilder.RenameColumn(
                name: "PatientId",
                table: "TestRequests",
                newName: "PatientID");

            migrationBuilder.RenameIndex(
                name: "IX_TestRequests_PatientId",
                table: "TestRequests",
                newName: "IX_TestRequests_PatientID");

            migrationBuilder.RenameColumn(
                name: "PatientId",
                table: "PatientMedications",
                newName: "PatientID");

            migrationBuilder.RenameIndex(
                name: "IX_PatientMedications_PatientId",
                table: "PatientMedications",
                newName: "IX_PatientMedications_PatientID");

            migrationBuilder.RenameColumn(
                name: "PatientId",
                table: "PatientConditions",
                newName: "PatientID");

            migrationBuilder.RenameIndex(
                name: "IX_PatientConditions_PatientId",
                table: "PatientConditions",
                newName: "IX_PatientConditions_PatientID");

            migrationBuilder.RenameColumn(
                name: "PatientId",
                table: "PatientAllergies",
                newName: "PatientID");

            migrationBuilder.RenameIndex(
                name: "IX_PatientAllergies_PatientId",
                table: "PatientAllergies",
                newName: "IX_PatientAllergies_PatientID");

            migrationBuilder.RenameColumn(
                name: "PatientId",
                table: "Consents",
                newName: "PatientID");

            migrationBuilder.RenameIndex(
                name: "IX_Consents_PatientId",
                table: "Consents",
                newName: "IX_Consents_PatientID");

            migrationBuilder.AddForeignKey(
                name: "FK_Consents_AspNetUsers_PatientID",
                table: "Consents",
                column: "PatientID",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PatientAllergies_AspNetUsers_PatientID",
                table: "PatientAllergies",
                column: "PatientID",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PatientConditions_AspNetUsers_PatientID",
                table: "PatientConditions",
                column: "PatientID",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PatientMedications_AspNetUsers_PatientID",
                table: "PatientMedications",
                column: "PatientID",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TestRequests_Patients_PatientID",
                table: "TestRequests",
                column: "PatientID",
                principalTable: "Patients",
                principalColumn: "PatientID",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Consents_AspNetUsers_PatientID",
                table: "Consents");

            migrationBuilder.DropForeignKey(
                name: "FK_PatientAllergies_AspNetUsers_PatientID",
                table: "PatientAllergies");

            migrationBuilder.DropForeignKey(
                name: "FK_PatientConditions_AspNetUsers_PatientID",
                table: "PatientConditions");

            migrationBuilder.DropForeignKey(
                name: "FK_PatientMedications_AspNetUsers_PatientID",
                table: "PatientMedications");

            migrationBuilder.DropForeignKey(
                name: "FK_TestRequests_Patients_PatientID",
                table: "TestRequests");

            migrationBuilder.RenameColumn(
                name: "PatientID",
                table: "TestRequests",
                newName: "PatientId");

            migrationBuilder.RenameIndex(
                name: "IX_TestRequests_PatientID",
                table: "TestRequests",
                newName: "IX_TestRequests_PatientId");

            migrationBuilder.RenameColumn(
                name: "PatientID",
                table: "PatientMedications",
                newName: "PatientId");

            migrationBuilder.RenameIndex(
                name: "IX_PatientMedications_PatientID",
                table: "PatientMedications",
                newName: "IX_PatientMedications_PatientId");

            migrationBuilder.RenameColumn(
                name: "PatientID",
                table: "PatientConditions",
                newName: "PatientId");

            migrationBuilder.RenameIndex(
                name: "IX_PatientConditions_PatientID",
                table: "PatientConditions",
                newName: "IX_PatientConditions_PatientId");

            migrationBuilder.RenameColumn(
                name: "PatientID",
                table: "PatientAllergies",
                newName: "PatientId");

            migrationBuilder.RenameIndex(
                name: "IX_PatientAllergies_PatientID",
                table: "PatientAllergies",
                newName: "IX_PatientAllergies_PatientId");

            migrationBuilder.RenameColumn(
                name: "PatientID",
                table: "Consents",
                newName: "PatientId");

            migrationBuilder.RenameIndex(
                name: "IX_Consents_PatientID",
                table: "Consents",
                newName: "IX_Consents_PatientId");

            migrationBuilder.AddColumn<int>(
                name: "ConsentId1",
                table: "ConsentTestAccesses",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConsentTestAccesses_ConsentId1",
                table: "ConsentTestAccesses",
                column: "ConsentId1");

            migrationBuilder.AddForeignKey(
                name: "FK_Consents_AspNetUsers_PatientId",
                table: "Consents",
                column: "PatientId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ConsentTestAccesses_Consents_ConsentId1",
                table: "ConsentTestAccesses",
                column: "ConsentId1",
                principalTable: "Consents",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PatientAllergies_AspNetUsers_PatientId",
                table: "PatientAllergies",
                column: "PatientId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PatientConditions_AspNetUsers_PatientId",
                table: "PatientConditions",
                column: "PatientId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PatientMedications_AspNetUsers_PatientId",
                table: "PatientMedications",
                column: "PatientId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TestRequests_Patients_PatientId",
                table: "TestRequests",
                column: "PatientId",
                principalTable: "Patients",
                principalColumn: "PatientID",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
