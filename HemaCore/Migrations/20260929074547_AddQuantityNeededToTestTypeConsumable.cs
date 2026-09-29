using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HemaCore.Migrations
{
    /// <inheritdoc />
    public partial class AddQuantityNeededToTestTypeConsumable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_testTypeConsumables_Consumables_ConsumableId",
                table: "testTypeConsumables");

            migrationBuilder.DropForeignKey(
                name: "FK_testTypeConsumables_TestTypes_TestTypeId",
                table: "testTypeConsumables");

            migrationBuilder.DropPrimaryKey(
                name: "PK_testTypeConsumables",
                table: "testTypeConsumables");

            migrationBuilder.RenameTable(
                name: "testTypeConsumables",
                newName: "TestTypeConsumables");

            migrationBuilder.RenameIndex(
                name: "IX_testTypeConsumables_ConsumableId",
                table: "TestTypeConsumables",
                newName: "IX_TestTypeConsumables_ConsumableId");

            migrationBuilder.AddColumn<int>(
                name: "QuantityNeeded",
                table: "TestTypeConsumables",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddPrimaryKey(
                name: "PK_TestTypeConsumables",
                table: "TestTypeConsumables",
                columns: new[] { "TestTypeId", "ConsumableId" });

            migrationBuilder.AddForeignKey(
                name: "FK_TestTypeConsumables_Consumables_ConsumableId",
                table: "TestTypeConsumables",
                column: "ConsumableId",
                principalTable: "Consumables",
                principalColumn: "ConsumableId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TestTypeConsumables_TestTypes_TestTypeId",
                table: "TestTypeConsumables",
                column: "TestTypeId",
                principalTable: "TestTypes",
                principalColumn: "TestTypeId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TestTypeConsumables_Consumables_ConsumableId",
                table: "TestTypeConsumables");

            migrationBuilder.DropForeignKey(
                name: "FK_TestTypeConsumables_TestTypes_TestTypeId",
                table: "TestTypeConsumables");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TestTypeConsumables",
                table: "TestTypeConsumables");

            migrationBuilder.DropColumn(
                name: "QuantityNeeded",
                table: "TestTypeConsumables");

            migrationBuilder.RenameTable(
                name: "TestTypeConsumables",
                newName: "testTypeConsumables");

            migrationBuilder.RenameIndex(
                name: "IX_TestTypeConsumables_ConsumableId",
                table: "testTypeConsumables",
                newName: "IX_testTypeConsumables_ConsumableId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_testTypeConsumables",
                table: "testTypeConsumables",
                columns: new[] { "TestTypeId", "ConsumableId" });

            migrationBuilder.AddForeignKey(
                name: "FK_testTypeConsumables_Consumables_ConsumableId",
                table: "testTypeConsumables",
                column: "ConsumableId",
                principalTable: "Consumables",
                principalColumn: "ConsumableId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_testTypeConsumables_TestTypes_TestTypeId",
                table: "testTypeConsumables",
                column: "TestTypeId",
                principalTable: "TestTypes",
                principalColumn: "TestTypeId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
