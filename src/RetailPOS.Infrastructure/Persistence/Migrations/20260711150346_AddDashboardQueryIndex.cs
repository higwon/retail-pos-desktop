using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RetailPOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDashboardQueryIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Orders_BusinessDate_CreatedAtUtc",
                table: "Orders",
                columns: new[] { "BusinessDate", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Orders_BusinessDate_CreatedAtUtc",
                table: "Orders");
        }
    }
}
