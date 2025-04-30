using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace growmesh_API.Migrations
{
    /// <inheritdoc />
    public partial class second : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SavingsGoalId1",
                table: "Transactions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedAt",
                table: "SavingsGoals",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "SavingsGoals",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "SavingsGoals",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "InitialAutomaticPayment",
                table: "SavingsGoals",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "InitialManualPayment",
                table: "SavingsGoals",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_SavingsGoalId1",
                table: "Transactions",
                column: "SavingsGoalId1");

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_SavingsGoals_SavingsGoalId1",
                table: "Transactions",
                column: "SavingsGoalId1",
                principalTable: "SavingsGoals",
                principalColumn: "SavingsGoalId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_SavingsGoals_SavingsGoalId1",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_SavingsGoalId1",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "SavingsGoalId1",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "SavingsGoals");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "SavingsGoals");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "SavingsGoals");

            migrationBuilder.DropColumn(
                name: "InitialAutomaticPayment",
                table: "SavingsGoals");

            migrationBuilder.DropColumn(
                name: "InitialManualPayment",
                table: "SavingsGoals");
        }
    }
}
