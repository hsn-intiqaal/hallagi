using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hallagi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OrganizationBranding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CoverImageUrl",
                table: "Organizations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PrimaryColor",
                table: "Organizations",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "#2563EB");

            migrationBuilder.AddColumn<string>(
                name: "SecondaryColor",
                table: "Organizations",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "#173653");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CoverImageUrl",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "PrimaryColor",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "SecondaryColor",
                table: "Organizations");
        }
    }
}
