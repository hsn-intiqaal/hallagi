using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hallagi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CommerceBookingTaxAndSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DurationMinutes",
                table: "Services",
                type: "int",
                nullable: false,
                defaultValue: 30);

            migrationBuilder.AddColumn<Guid>(
                name: "AppointmentId",
                table: "Sales",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CommissionBase",
                table: "Sales",
                type: "decimal(10,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "CouponCode",
                table: "Sales",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CouponId",
                table: "Sales",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountAmount",
                table: "Sales",
                type: "decimal(10,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "GrossAmount",
                table: "Sales",
                type: "decimal(10,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "InvoiceSellerName",
                table: "Sales",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InvoiceVatNumber",
                table: "Sales",
                type: "nvarchar(15)",
                maxLength: 15,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PointsEarned",
                table: "Sales",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PointsRedeemed",
                table: "Sales",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "SubtotalAmount",
                table: "Sales",
                type: "decimal(10,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "VatAmount",
                table: "Sales",
                type: "decimal(10,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "VatRate",
                table: "Sales",
                type: "decimal(8,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "ItemType",
                table: "SaleItems",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "ProductId",
                table: "SaleItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BookingMaxDaysAhead",
                table: "OrganizationSettings",
                type: "int",
                nullable: false,
                defaultValue: 14);

            migrationBuilder.AddColumn<int>(
                name: "BookingSlotMinutes",
                table: "OrganizationSettings",
                type: "int",
                nullable: false,
                defaultValue: 15);

            migrationBuilder.AddColumn<string>(
                name: "CommercialRegistration",
                table: "OrganizationSettings",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegalName",
                table: "OrganizationSettings",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "LoyaltyEnabled",
                table: "OrganizationSettings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "LoyaltyMinRedeem",
                table: "OrganizationSettings",
                type: "int",
                nullable: false,
                defaultValue: 100);

            migrationBuilder.AddColumn<decimal>(
                name: "LoyaltyPointValue",
                table: "OrganizationSettings",
                type: "decimal(8,4)",
                nullable: false,
                defaultValue: 0.05m);

            migrationBuilder.AddColumn<decimal>(
                name: "LoyaltyPointsPerSar",
                table: "OrganizationSettings",
                type: "decimal(8,4)",
                nullable: false,
                defaultValue: 1m);

            migrationBuilder.AddColumn<decimal>(
                name: "MaxManualDiscountPercent",
                table: "OrganizationSettings",
                type: "decimal(8,4)",
                nullable: false,
                defaultValue: 30m);

            migrationBuilder.AddColumn<bool>(
                name: "OnlineBookingEnabled",
                table: "OrganizationSettings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "PricesIncludeVat",
                table: "OrganizationSettings",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "TaxAddress",
                table: "OrganizationSettings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "VatEnabled",
                table: "OrganizationSettings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "VatNumber",
                table: "OrganizationSettings",
                type: "nvarchar(15)",
                maxLength: 15,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "VatRate",
                table: "OrganizationSettings",
                type: "decimal(8,4)",
                nullable: false,
                defaultValue: 0.15m);

            migrationBuilder.AddColumn<string>(
                name: "Slug",
                table: "Organizations",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LoyaltyPoints",
                table: "Customers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "ClosesAt",
                table: "Branches",
                type: "time",
                nullable: false,
                defaultValue: new TimeOnly(23, 0, 0));

            migrationBuilder.AddColumn<TimeOnly>(
                name: "OpensAt",
                table: "Branches",
                type: "time",
                nullable: false,
                defaultValue: new TimeOnly(9, 0, 0));

            migrationBuilder.AddColumn<string>(
                name: "Phone",
                table: "Branches",
                type: "nvarchar(max)",
                nullable: true);

            // Historical sales carried no discount/VAT breakdown: their paid total is both the
            // taxable amount and the barber commission base.
            migrationBuilder.Sql(
                "UPDATE Sales SET GrossAmount = TotalAmount, SubtotalAmount = TotalAmount, CommissionBase = TotalAmount");

            migrationBuilder.CreateTable(
                name: "Appointments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BarberId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CustomerName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    CustomerPhone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    StartAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Source = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SaleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CancelReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Reference = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Appointments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Coupons",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Value = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    MinSubtotal = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    MaxDiscount = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    MaxUses = table.Column<int>(type: "int", nullable: true),
                    UsedCount = table.Column<int>(type: "int", nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: true),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Coupons", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LoyaltyTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Points = table.Column<int>(type: "int", nullable: false),
                    BalanceAfter = table.Column<int>(type: "int", nullable: false),
                    SaleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoyaltyTransactions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Products",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Sku = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Barcode = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Price = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    Cost = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RefreshTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TokenHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReplacedByHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefreshTokens", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StockMovements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    BalanceAfter = table.Column<int>(type: "int", nullable: false),
                    UnitCost = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    SaleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockMovements", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppointmentItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppointmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ServiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ServiceName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Price = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    DurationMinutes = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppointmentItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppointmentItems_Appointments_AppointmentId",
                        column: x => x.AppointmentId,
                        principalTable: "Appointments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProductStocks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    LowStockThreshold = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductStocks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductStocks_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Organizations_Slug",
                table: "Organizations",
                column: "Slug",
                unique: true,
                filter: "[Slug] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentItems_AppointmentId",
                table: "AppointmentItems",
                column: "AppointmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_BarberId_StartAt",
                table: "Appointments",
                columns: new[] { "BarberId", "StartAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_BranchId_StartAt",
                table: "Appointments",
                columns: new[] { "BranchId", "StartAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_OrganizationId_Reference",
                table: "Appointments",
                columns: new[] { "OrganizationId", "Reference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Coupons_OrganizationId_Code",
                table: "Coupons",
                columns: new[] { "OrganizationId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyTransactions_CustomerId_CreatedAt",
                table: "LoyaltyTransactions",
                columns: new[] { "CustomerId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Products_OrganizationId_Barcode",
                table: "Products",
                columns: new[] { "OrganizationId", "Barcode" });

            migrationBuilder.CreateIndex(
                name: "IX_Products_OrganizationId_Name",
                table: "Products",
                columns: new[] { "OrganizationId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductStocks_ProductId_BranchId",
                table: "ProductStocks",
                columns: new[] { "ProductId", "BranchId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_ProfileId",
                table: "RefreshTokens",
                column: "ProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_TokenHash",
                table: "RefreshTokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_ProductId_BranchId_CreatedAt",
                table: "StockMovements",
                columns: new[] { "ProductId", "BranchId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppointmentItems");

            migrationBuilder.DropTable(
                name: "Coupons");

            migrationBuilder.DropTable(
                name: "LoyaltyTransactions");

            migrationBuilder.DropTable(
                name: "ProductStocks");

            migrationBuilder.DropTable(
                name: "RefreshTokens");

            migrationBuilder.DropTable(
                name: "StockMovements");

            migrationBuilder.DropTable(
                name: "Appointments");

            migrationBuilder.DropTable(
                name: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Organizations_Slug",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "DurationMinutes",
                table: "Services");

            migrationBuilder.DropColumn(
                name: "AppointmentId",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "CommissionBase",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "CouponCode",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "CouponId",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "DiscountAmount",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "GrossAmount",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "InvoiceSellerName",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "InvoiceVatNumber",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "PointsEarned",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "PointsRedeemed",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "SubtotalAmount",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "VatAmount",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "VatRate",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "ItemType",
                table: "SaleItems");

            migrationBuilder.DropColumn(
                name: "ProductId",
                table: "SaleItems");

            migrationBuilder.DropColumn(
                name: "BookingMaxDaysAhead",
                table: "OrganizationSettings");

            migrationBuilder.DropColumn(
                name: "BookingSlotMinutes",
                table: "OrganizationSettings");

            migrationBuilder.DropColumn(
                name: "CommercialRegistration",
                table: "OrganizationSettings");

            migrationBuilder.DropColumn(
                name: "LegalName",
                table: "OrganizationSettings");

            migrationBuilder.DropColumn(
                name: "LoyaltyEnabled",
                table: "OrganizationSettings");

            migrationBuilder.DropColumn(
                name: "LoyaltyMinRedeem",
                table: "OrganizationSettings");

            migrationBuilder.DropColumn(
                name: "LoyaltyPointValue",
                table: "OrganizationSettings");

            migrationBuilder.DropColumn(
                name: "LoyaltyPointsPerSar",
                table: "OrganizationSettings");

            migrationBuilder.DropColumn(
                name: "MaxManualDiscountPercent",
                table: "OrganizationSettings");

            migrationBuilder.DropColumn(
                name: "OnlineBookingEnabled",
                table: "OrganizationSettings");

            migrationBuilder.DropColumn(
                name: "PricesIncludeVat",
                table: "OrganizationSettings");

            migrationBuilder.DropColumn(
                name: "TaxAddress",
                table: "OrganizationSettings");

            migrationBuilder.DropColumn(
                name: "VatEnabled",
                table: "OrganizationSettings");

            migrationBuilder.DropColumn(
                name: "VatNumber",
                table: "OrganizationSettings");

            migrationBuilder.DropColumn(
                name: "VatRate",
                table: "OrganizationSettings");

            migrationBuilder.DropColumn(
                name: "Slug",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "LoyaltyPoints",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "ClosesAt",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "OpensAt",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "Phone",
                table: "Branches");
        }
    }
}
