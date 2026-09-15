using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HemaCore.Migrations
{
    /// <inheritdoc />
    public partial class AddReleaseFieldsToTestRequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReleaseNote",
                table: "TestRequests",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReleasedToPatientDate",
                table: "TestRequests",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReleaseNote",
                table: "TestRequests");

            migrationBuilder.DropColumn(
                name: "ReleasedToPatientDate",
                table: "TestRequests");
        }
    }
}
