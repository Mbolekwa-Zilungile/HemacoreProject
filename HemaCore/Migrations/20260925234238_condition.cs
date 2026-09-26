using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HemaCore.Migrations
{
    /// <inheritdoc />
    public partial class condition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ConsentConditions_Conditions_ConditionId",
                table: "ConsentConditions");

            migrationBuilder.AddForeignKey(
                name: "FK_ConsentConditions_Conditions_ConditionId",
                table: "ConsentConditions",
                column: "ConditionId",
                principalTable: "Conditions",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ConsentConditions_Conditions_ConditionId",
                table: "ConsentConditions");

            migrationBuilder.AddForeignKey(
                name: "FK_ConsentConditions_Conditions_ConditionId",
                table: "ConsentConditions",
                column: "ConditionId",
                principalTable: "Conditions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
