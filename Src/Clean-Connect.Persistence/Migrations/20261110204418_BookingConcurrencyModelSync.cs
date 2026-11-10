using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clean_Connect.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BookingConcurrencyModelSync : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Bookings_ClientId",
                table: "Bookings");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Bookings_ClientId",
                table: "Bookings",
                column: "ClientId");
        }
    }
}
