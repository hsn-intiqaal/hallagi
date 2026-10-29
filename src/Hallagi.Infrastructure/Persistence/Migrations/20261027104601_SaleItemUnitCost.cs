using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hallagi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SaleItemUnitCost : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "UnitCost",
                table: "SaleItems",
                type: "decimal(10,2)",
                nullable: true);

            // Lines sold before cost snapshots existed: best available value is the current product cost
            migrationBuilder.Sql(
                "UPDATE SaleItems SET UnitCost = (SELECT p.Cost FROM Products p WHERE p.Id = SaleItems.ProductId) WHERE ProductId IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UnitCost",
                table: "SaleItems");
        }
    }
}
