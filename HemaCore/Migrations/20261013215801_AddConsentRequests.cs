using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HemaCore.Migrations
{
    /// <inheritdoc />
    public partial class AddConsentRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConsentRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientID = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    DoctorId = table.Column<int>(type: "int", nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RespondedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConsentRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConsentRequests_AspNetUsers_PatientID",
                        column: x => x.PatientID,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ConsentRequests_Doctors_DoctorId",
                        column: x => x.DoctorId,
                        principalTable: "Doctors",
                        principalColumn: "DoctorId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConsentRequests_DoctorId",
                table: "ConsentRequests",
                column: "DoctorId");

            migrationBuilder.CreateIndex(
                name: "IX_ConsentRequests_PatientID_DoctorId_Status",
                table: "ConsentRequests",
                columns: new[] { "PatientID", "DoctorId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConsentRequests");
        }
    }
}
