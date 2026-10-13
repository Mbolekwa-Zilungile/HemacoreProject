using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HemaCore.Migrations
{
    /// <inheritdoc />
    public partial class lil : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TestResultHistories",
                columns: table => new
                {
                    TestResultHistoryId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TestRequestTestTypeId = table.Column<int>(type: "int", nullable: false),
                    LabTechnicianId = table.Column<int>(type: "int", nullable: true),
                    Action = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PreviousStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    NewStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ResultValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ResultNotes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsAbnormal = table.Column<bool>(type: "bit", nullable: true),
                    ReviewNotes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ActionDateTime = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestResultHistories", x => x.TestResultHistoryId);
                    table.ForeignKey(
                        name: "FK_TestResultHistories_LabTechnicians_LabTechnicianId",
                        column: x => x.LabTechnicianId,
                        principalTable: "LabTechnicians",
                        principalColumn: "LabTechnicianId");
                    table.ForeignKey(
                        name: "FK_TestResultHistories_TestRequestTestTypes_TestRequestTestTypeId",
                        column: x => x.TestRequestTestTypeId,
                        principalTable: "TestRequestTestTypes",
                        principalColumn: "TestRequestTestTypeId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_TestResultHistories_LabTechnicianId",
                table: "TestResultHistories",
                column: "LabTechnicianId");

            migrationBuilder.CreateIndex(
                name: "IX_TestResultHistories_TestRequestTestTypeId",
                table: "TestResultHistories",
                column: "TestRequestTestTypeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TestResultHistories");
        }
    }
}
