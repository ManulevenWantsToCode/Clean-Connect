using Clean_Connect.Infrastructure.Context;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clean_Connect.Persistence.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261029000100_AddEscrowPayoutLifecycle")]
    public partial class AddEscrowPayoutLifecycle : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PaystackTransferReference",
                table: "Escrows",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PayoutAttemptCount",
                table: "Escrows",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "TransferStatus",
                table: "Escrows",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "NotStarted");

            migrationBuilder.CreateIndex(
                name: "IX_Escrows_PaystackTransferReference",
                table: "Escrows",
                column: "PaystackTransferReference",
                unique: true,
                filter: "\"PaystackTransferReference\" IS NOT NULL");

            migrationBuilder.Sql("UPDATE \"Escrows\" SET \"TransferStatus\" = 'LegacyUnverified' WHERE \"Status\" = 'PaidOut'");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Escrows_PaystackTransferReference",
                table: "Escrows");

            migrationBuilder.DropColumn(
                name: "PaystackTransferReference",
                table: "Escrows");

            migrationBuilder.DropColumn(
                name: "PayoutAttemptCount",
                table: "Escrows");

            migrationBuilder.DropColumn(
                name: "TransferStatus",
                table: "Escrows");
        }
    }
}
