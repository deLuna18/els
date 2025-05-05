using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SubdivisionManagement.Migrations
{
    /// <inheritdoc />
    public partial class did : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "SecurityPolicies",
                keyColumn: "Id",
                keyValue: 1,
                column: "LastModified",
                value: new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "SecurityPolicies",
                keyColumn: "Id",
                keyValue: 1,
                column: "LastModified",
                value: new DateTime(2025, 5, 5, 20, 0, 0, 913, DateTimeKind.Utc).AddTicks(5542));
        }
    }
}
