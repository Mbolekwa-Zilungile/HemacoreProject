using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HemaCore.Migrations
{
    /// <inheritdoc />
    public partial class AddCancelFieldsToTestRequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CancelReason",
                table: "TestRequests",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CancellationDate",
                table: "TestRequests",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CancelReason",
                table: "TestRequests");

            migrationBuilder.DropColumn(
                name: "CancellationDate",
                table: "TestRequests");
        }
    }
}
