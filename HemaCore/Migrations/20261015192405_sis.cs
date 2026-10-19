using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HemaCore.Migrations
{
    /// <inheritdoc />
    public partial class sis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Barcode",
                table: "Samples",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<int>(
                name: "RejectedByLabTechnicianId",
                table: "Samples",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RejectedDateTime",
                table: "Samples",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "Samples",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Samples",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RejectedByLabTechnicianId",
                table: "Samples");

            migrationBuilder.DropColumn(
                name: "RejectedDateTime",
                table: "Samples");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "Samples");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Samples");

            migrationBuilder.AlterColumn<string>(
                name: "Barcode",
                table: "Samples",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);
        }
    }
}
