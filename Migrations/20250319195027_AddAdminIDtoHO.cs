using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SubdivisionManagement.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminIDtoHO : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AdminId",
                table: "Homeowners",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Homeowners_AdminId",
                table: "Homeowners",
                column: "AdminId");

            migrationBuilder.AddForeignKey(
                name: "FK_Homeowners_Admins_AdminId",
                table: "Homeowners",
                column: "AdminId",
                principalTable: "Admins",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Homeowners_Admins_AdminId",
                table: "Homeowners");

            migrationBuilder.DropIndex(
                name: "IX_Homeowners_AdminId",
                table: "Homeowners");

            migrationBuilder.DropColumn(
                name: "AdminId",
                table: "Homeowners");
        }
    }
}
