using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hallagi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PlatformOwnerAndSuperAdmin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsSuperAdmin",
                table: "Profiles",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsSuperAdmin",
                table: "Profiles");
        }
    }
}
