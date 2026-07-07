using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RetailPOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(LocalPosDbContext))]
    [Migration("20260707000000_AddPendingCheckoutTransactionReference")]
    public partial class AddPendingCheckoutTransactionReference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TransactionReference",
                table: "PendingCheckouts",
                type: "TEXT",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TransactionReference",
                table: "PendingCheckouts");
        }
    }
}
