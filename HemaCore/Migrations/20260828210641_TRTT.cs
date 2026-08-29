using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HemaCore.Migrations
{
    /// <inheritdoc />
    public partial class TRTT : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CompletionDateTime",
                table: "TestRequestTestTypes",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartDateTime",
                table: "TestRequestTestTypes",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "TestRequestTestTypes",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "TestRequestId",
                table: "TestRequestTestTypes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TestRequestTestTypeId",
                table: "TestRequestTestTypes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TestTypeId",
                table: "TestRequestTestTypes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_TestRequestTestTypes_TestRequestId",
                table: "TestRequestTestTypes",
                column: "TestRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_TestRequestTestTypes_TestTypeId",
                table: "TestRequestTestTypes",
                column: "TestTypeId");

            migrationBuilder.AddForeignKey(
                name: "FK_TestRequestTestTypes_TestRequests_TestRequestId",
                table: "TestRequestTestTypes",
                column: "TestRequestId",
                principalTable: "TestRequests",
                principalColumn: "TestRequestId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TestRequestTestTypes_TestTypes_TestTypeId",
                table: "TestRequestTestTypes",
                column: "TestTypeId",
                principalTable: "TestTypes",
                principalColumn: "TestTypeId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TestRequestTestTypes_TestRequests_TestRequestId",
                table: "TestRequestTestTypes");

            migrationBuilder.DropForeignKey(
                name: "FK_TestRequestTestTypes_TestTypes_TestTypeId",
                table: "TestRequestTestTypes");

            migrationBuilder.DropIndex(
                name: "IX_TestRequestTestTypes_TestRequestId",
                table: "TestRequestTestTypes");

            migrationBuilder.DropIndex(
                name: "IX_TestRequestTestTypes_TestTypeId",
                table: "TestRequestTestTypes");

            migrationBuilder.DropColumn(
                name: "CompletionDateTime",
                table: "TestRequestTestTypes");

            migrationBuilder.DropColumn(
                name: "StartDateTime",
                table: "TestRequestTestTypes");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "TestRequestTestTypes");

            migrationBuilder.DropColumn(
                name: "TestRequestId",
                table: "TestRequestTestTypes");

            migrationBuilder.DropColumn(
                name: "TestRequestTestTypeId",
                table: "TestRequestTestTypes");

            migrationBuilder.DropColumn(
                name: "TestTypeId",
                table: "TestRequestTestTypes");
        }
    }
}
