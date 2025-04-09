using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SubdivisionManagement.Migrations
{
    /// <inheritdoc />
    public partial class AddedHomeOwner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Block",
                table: "Homeowners",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "EmergencyContactName",
                table: "Homeowners",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "EmergencyContactNumber",
                table: "Homeowners",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "FirstName",
                table: "Homeowners",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "HouseNumber",
                table: "Homeowners",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "LastName",
                table: "Homeowners",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "MiddleName",
                table: "Homeowners",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Phase",
                table: "Homeowners",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PhoneNumber",
                table: "Homeowners",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Street",
                table: "Homeowners",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Username",
                table: "Homeowners",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Block",
                table: "Homeowners");

            migrationBuilder.DropColumn(
                name: "EmergencyContactName",
                table: "Homeowners");

            migrationBuilder.DropColumn(
                name: "EmergencyContactNumber",
                table: "Homeowners");

            migrationBuilder.DropColumn(
                name: "FirstName",
                table: "Homeowners");

            migrationBuilder.DropColumn(
                name: "HouseNumber",
                table: "Homeowners");

            migrationBuilder.DropColumn(
                name: "LastName",
                table: "Homeowners");

            migrationBuilder.DropColumn(
                name: "MiddleName",
                table: "Homeowners");

            migrationBuilder.DropColumn(
                name: "Phase",
                table: "Homeowners");

            migrationBuilder.DropColumn(
                name: "PhoneNumber",
                table: "Homeowners");

            migrationBuilder.DropColumn(
                name: "Street",
                table: "Homeowners");

            migrationBuilder.DropColumn(
                name: "Username",
                table: "Homeowners");
        }
    }
}
