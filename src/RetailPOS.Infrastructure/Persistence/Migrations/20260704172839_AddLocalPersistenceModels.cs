using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RetailPOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLocalPersistenceModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Orders",
                columns: table => new
                {
                    LocalOrderId = table.Column<Guid>(type: "TEXT", nullable: false),
                    LocalOrderNumber = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    StoreId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TerminalId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CashierId = table.Column<Guid>(type: "TEXT", nullable: false),
                    BusinessDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    SubtotalAmount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 0, nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 0, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 0, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Orders", x => x.LocalOrderId);
                });

            migrationBuilder.CreateTable(
                name: "PendingCheckouts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    StoreId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TerminalId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CashierId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    RecoveryStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    CartSnapshotJson = table.Column<string>(type: "TEXT", nullable: false),
                    PaymentSnapshotJson = table.Column<string>(type: "TEXT", nullable: false),
                    PaymentStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    ApprovalCode = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    ApprovedAmount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 0, nullable: true),
                    PaymentApprovedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    OrderId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastUpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PendingCheckouts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Products",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Sku = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Barcode = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    CategoryName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "TEXT", precision: 18, scale: 0, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SyncQueue",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ItemType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    AggregateId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PayloadJson = table.Column<string>(type: "TEXT", nullable: true),
                    ReferenceKey = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    RetryCount = table.Column<int>(type: "INTEGER", nullable: false),
                    NextAttemptAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastErrorSummary = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncQueue", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OrderLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    LocalOrderId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    ProductId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProductNameSnapshot = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "TEXT", precision: 18, scale: 0, nullable: false),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false),
                    GrossAmount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 0, nullable: false),
                    LineDiscountAmount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 0, nullable: false),
                    LineTotalAmount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 0, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderLines_Orders_LocalOrderId",
                        column: x => x.LocalOrderId,
                        principalTable: "Orders",
                        principalColumn: "LocalOrderId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Payments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    LocalOrderId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    Method = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    RequestedAmount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 0, nullable: false),
                    ApprovedAmount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 0, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ApprovedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ApprovalCode = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    TransactionReference = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    FailureReason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Payments_Orders_LocalOrderId",
                        column: x => x.LocalOrderId,
                        principalTable: "Orders",
                        principalColumn: "LocalOrderId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrderLines_LocalOrderId_SortOrder",
                table: "OrderLines",
                columns: new[] { "LocalOrderId", "SortOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_CreatedAtUtc",
                table: "Orders",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_LocalOrderNumber",
                table: "Orders",
                column: "LocalOrderNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_LocalOrderId_SortOrder",
                table: "Payments",
                columns: new[] { "LocalOrderId", "SortOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PendingCheckouts_RecoveryStatus_CreatedAtUtc",
                table: "PendingCheckouts",
                columns: new[] { "RecoveryStatus", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Products_Barcode",
                table: "Products",
                column: "Barcode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Products_Sku",
                table: "Products",
                column: "Sku",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SyncQueue_Status_NextAttemptAtUtc_CreatedAtUtc_Id",
                table: "SyncQueue",
                columns: new[] { "Status", "NextAttemptAtUtc", "CreatedAtUtc", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrderLines");

            migrationBuilder.DropTable(
                name: "Payments");

            migrationBuilder.DropTable(
                name: "PendingCheckouts");

            migrationBuilder.DropTable(
                name: "Products");

            migrationBuilder.DropTable(
                name: "SyncQueue");

            migrationBuilder.DropTable(
                name: "Orders");
        }
    }
}
