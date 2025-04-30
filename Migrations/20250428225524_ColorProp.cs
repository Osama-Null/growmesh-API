using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace growmesh_API.Migrations
{
    /// <inheritdoc />
    public partial class ColorProp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Color",
                table: "SavingsGoals",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Color",
                table: "SavingsGoals");
        }
    }
}
