using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HemaCore.Migrations
{
    /// <inheritdoc />
    public partial class TestRequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TestRequests_Doctors_DoctorId",
                table: "TestRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_TestRequestTestTypes_LabTechnicians_LabTechnicianId",
                table: "TestRequestTestTypes");

            migrationBuilder.DropForeignKey(
                name: "FK_TestRequestTestTypes_TestResults_TestResultId",
                table: "TestRequestTestTypes");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TestRequestTestTypes",
                table: "TestRequestTestTypes");

            migrationBuilder.DropColumn(
                name: "TestRequestTestTypeId",
                table: "TestRequestTestTypes");

            migrationBuilder.AddColumn<int>(
                name: "TestRequestTestTypeId",
                table: "TestRequestTestTypes",
                type: "int",
                nullable: false)
                .Annotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AlterColumn<int>(
                name: "TestResultId",
                table: "TestRequestTestTypes",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<int>(
                name: "LabTechnicianId",
                table: "TestRequestTestTypes",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<int>(
                name: "DoctorId",
                table: "TestRequests",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClinicalNotes",
                table: "TestRequests",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "PatientId",
                table: "TestRequests",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "RequestDate",
                table: "TestRequests",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "TestRequests",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Urgency",
                table: "TestRequests",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TestRequestTestTypes",
                table: "TestRequestTestTypes",
                column: "TestRequestTestTypeId");
        }
    }
}