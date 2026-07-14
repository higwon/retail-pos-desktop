using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RetailPOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PersistCashTenderMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CashTenderedAmount",
                table: "PendingCheckouts",
                type: "TEXT",
                precision: 18,
                scale: 0,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ChangeAmount",
                table: "PendingCheckouts",
                type: "TEXT",
                precision: 18,
                scale: 0,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CashTenderedAmount",
                table: "Payments",
                type: "TEXT",
                precision: 18,
                scale: 0,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ChangeAmount",
                table: "Payments",
                type: "TEXT",
                precision: 18,
                scale: 0,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CashTenderedAmount",
                table: "PendingCheckouts");

            migrationBuilder.DropColumn(
                name: "ChangeAmount",
                table: "PendingCheckouts");

            migrationBuilder.DropColumn(
                name: "CashTenderedAmount",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "ChangeAmount",
                table: "Payments");
        }
    }
}
