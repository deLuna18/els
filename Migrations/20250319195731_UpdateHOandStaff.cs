using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SubdivisionManagement.Migrations
{
    /// <inheritdoc />
    public partial class UpdateHOandStaff : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Homeowners_Admins_AdminId",
                table: "Homeowners");

            migrationBuilder.RenameColumn(
                name: "AdminId",
                table: "Homeowners",
                newName: "StaffId");

            migrationBuilder.RenameIndex(
                name: "IX_Homeowners_AdminId",
                table: "Homeowners",
                newName: "IX_Homeowners_StaffId");

            migrationBuilder.AddForeignKey(
                name: "FK_Homeowners_Staffs_StaffId",
                table: "Homeowners",
                column: "StaffId",
                principalTable: "Staffs",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Homeowners_Staffs_StaffId",
                table: "Homeowners");

            migrationBuilder.RenameColumn(
                name: "StaffId",
                table: "Homeowners",
                newName: "AdminId");

            migrationBuilder.RenameIndex(
                name: "IX_Homeowners_StaffId",
                table: "Homeowners",
                newName: "IX_Homeowners_AdminId");

            migrationBuilder.AddForeignKey(
                name: "FK_Homeowners_Admins_AdminId",
                table: "Homeowners",
                column: "AdminId",
                principalTable: "Admins",
                principalColumn: "Id");
        }
    }
}
