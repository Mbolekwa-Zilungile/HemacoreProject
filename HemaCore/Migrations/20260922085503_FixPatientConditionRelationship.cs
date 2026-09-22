using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HemaCore.Migrations
{
    /// <inheritdoc />
    public partial class FixPatientConditionRelationship : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PatientAllergies_AspNetUsers_PatientID",
                table: "PatientAllergies");

            migrationBuilder.DropForeignKey(
                name: "FK_PatientConditions_AspNetUsers_PatientID",
                table: "PatientConditions");

            migrationBuilder.DropForeignKey(
                name: "FK_PatientMedications_AspNetUsers_PatientID",
                table: "PatientMedications");

            migrationBuilder.AlterColumn<int>(
                name: "PatientID",
                table: "PatientMedications",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<int>(
                name: "PatientID",
                table: "PatientConditions",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<int>(
                name: "PatientID",
                table: "PatientAllergies",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AddForeignKey(
                name: "FK_PatientAllergies_Patients_PatientID",
                table: "PatientAllergies",
                column: "PatientID",
                principalTable: "Patients",
                principalColumn: "PatientID");

            migrationBuilder.AddForeignKey(
                name: "FK_PatientConditions_Patients_PatientID",
                table: "PatientConditions",
                column: "PatientID",
                principalTable: "Patients",
                principalColumn: "PatientID");

            migrationBuilder.AddForeignKey(
                name: "FK_PatientMedications_Patients_PatientID",
                table: "PatientMedications",
                column: "PatientID",
                principalTable: "Patients",
                principalColumn: "PatientID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PatientAllergies_Patients_PatientID",
                table: "PatientAllergies");

            migrationBuilder.DropForeignKey(
                name: "FK_PatientConditions_Patients_PatientID",
                table: "PatientConditions");

            migrationBuilder.DropForeignKey(
                name: "FK_PatientMedications_Patients_PatientID",
                table: "PatientMedications");

            migrationBuilder.AlterColumn<string>(
                name: "PatientID",
                table: "PatientMedications",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<string>(
                name: "PatientID",
                table: "PatientConditions",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<string>(
                name: "PatientID",
                table: "PatientAllergies",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

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
        }
    }
}
