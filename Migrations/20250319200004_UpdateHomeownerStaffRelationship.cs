using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SubdivisionManagement.Migrations
{
    /// <inheritdoc />
    public partial class UpdateHomeownerStaffRelationship : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Homeowners_Staffs_StaffId",
                table: "Homeowners");

            migrationBuilder.AddForeignKey(
                name: "FK_Homeowners_Staffs_StaffId",
                table: "Homeowners",
                column: "StaffId",
                principalTable: "Staffs",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Homeowners_Staffs_StaffId",
                table: "Homeowners");

            migrationBuilder.AddForeignKey(
                name: "FK_Homeowners_Staffs_StaffId",
                table: "Homeowners",
                column: "StaffId",
                principalTable: "Staffs",
                principalColumn: "Id");
        }
    }
}
