using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace growmesh_API.Migrations
{
    /// <inheritdoc />
    public partial class test5 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CustomDepositIntervalDays",
                table: "SavingsGoals",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DepositAmount",
                table: "SavingsGoals",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DepositFrequency",
                table: "SavingsGoals",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastDepositDate",
                table: "SavingsGoals",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CustomDepositIntervalDays",
                table: "SavingsGoals");

            migrationBuilder.DropColumn(
                name: "DepositAmount",
                table: "SavingsGoals");

            migrationBuilder.DropColumn(
                name: "DepositFrequency",
                table: "SavingsGoals");

            migrationBuilder.DropColumn(
                name: "LastDepositDate",
                table: "SavingsGoals");
        }
    }
}
