using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clean_Connect.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingChatMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BookingChats",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BookingId = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ProviderConversationId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ClientProviderUserId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    WorkerProviderUserId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DateCreated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true),
                    DateModified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookingChats", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BookingChats_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.Sql("""
                INSERT INTO "BookingChats" (
                    "Id",
                    "BookingId",
                    "Provider",
                    "ProviderConversationId",
                    "ClientProviderUserId",
                    "WorkerProviderUserId",
                    "DateCreated",
                    "ModifiedBy",
                    "DateModified",
                    "CreatedBy",
                    "IsDeleted")
                SELECT
                    b."Id",
                    b."Id",
                    'Agora',
                    'booking-' || replace(b."Id"::text, '-', ''),
                    'client-' || replace(b."ClientId"::text, '-', ''),
                    'worker-' || replace(b."WorkerId"::text, '-', ''),
                    b."DateCreated",
                    b."CreatedBy",
                    b."DateModified",
                    b."CreatedBy",
                    false
                FROM "Bookings" b
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM "BookingChats" bc
                    WHERE bc."BookingId" = b."Id");
                """);

            migrationBuilder.CreateIndex(
                name: "IX_BookingChats_BookingId",
                table: "BookingChats",
                column: "BookingId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BookingChats_ClientProviderUserId",
                table: "BookingChats",
                column: "ClientProviderUserId");

            migrationBuilder.CreateIndex(
                name: "IX_BookingChats_ProviderConversationId",
                table: "BookingChats",
                column: "ProviderConversationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BookingChats_WorkerProviderUserId",
                table: "BookingChats",
                column: "WorkerProviderUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BookingChats");
        }
    }
}
