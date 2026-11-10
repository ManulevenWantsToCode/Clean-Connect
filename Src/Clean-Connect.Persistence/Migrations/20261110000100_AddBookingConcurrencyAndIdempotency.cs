using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Clean_Connect.Infrastructure.Context;

#nullable disable

namespace Clean_Connect.Persistence.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261110000100_AddBookingConcurrencyAndIdempotency")]
    public partial class AddBookingConcurrencyAndIdempotency : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "Bookings",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequestHash",
                table: "Bookings",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_ClientId_IdempotencyKey",
                table: "Bookings",
                columns: new[] { "ClientId", "IdempotencyKey" },
                unique: true,
                filter: "\"IdempotencyKey\" IS NOT NULL");

            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS btree_gist;");
            migrationBuilder.Sql(@"
DO $$
DECLARE conflict_count bigint;
DECLARE invalid_interval_count bigint;
BEGIN
    SELECT count(*) INTO invalid_interval_count FROM ""Bookings"" WHERE ""StartTime"" >= ""EndTime"";
    IF invalid_interval_count > 0 THEN
        RAISE EXCEPTION 'Booking concurrency migration blocked: % bookings have invalid time intervals. Resolve these records before retrying.', invalid_interval_count;
    END IF;

    SELECT count(*) INTO conflict_count
    FROM ""Bookings"" a
    JOIN ""Bookings"" b ON a.""WorkerId"" = b.""WorkerId"" AND a.""Id"" < b.""Id""
    WHERE a.""BookingStatus"" IN ('Pending', 'AcceptedAwaitingPayment', 'MarkAsPaid', 'AwaitingClientStartConfirmation', 'InProgress', 'AwaitingClientConfirmation')
      AND b.""BookingStatus"" IN ('Pending', 'AcceptedAwaitingPayment', 'MarkAsPaid', 'AwaitingClientStartConfirmation', 'InProgress', 'AwaitingClientConfirmation')
      AND NOT a.""IsDeleted"" AND NOT b.""IsDeleted""
      AND tstzrange(a.""StartTime"", a.""EndTime"", '[)') && tstzrange(b.""StartTime"", b.""EndTime"", '[)');
    IF conflict_count > 0 THEN
        RAISE EXCEPTION 'Booking concurrency migration blocked: % conflicting active worker booking pairs exist. Resolve these records before retrying.', conflict_count;
    END IF;
END $$;");

            migrationBuilder.Sql(@"
ALTER TABLE ""Bookings""
ADD CONSTRAINT ""CK_Bookings_ValidInterval""
CHECK (""StartTime"" < ""EndTime"");

ALTER TABLE ""Bookings""
ADD CONSTRAINT ""EX_Bookings_Worker_Time_Active""
EXCLUDE USING gist (
    ""WorkerId"" WITH =,
    tstzrange(""StartTime"", ""EndTime"", '[)') WITH &&
)
WHERE (NOT ""IsDeleted"" AND ""BookingStatus"" IN ('Pending', 'AcceptedAwaitingPayment', 'MarkAsPaid', 'AwaitingClientStartConfirmation', 'InProgress', 'AwaitingClientConfirmation'));");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE \"Bookings\" DROP CONSTRAINT IF EXISTS \"EX_Bookings_Worker_Time_Active\";");
            migrationBuilder.Sql("ALTER TABLE \"Bookings\" DROP CONSTRAINT IF EXISTS \"CK_Bookings_ValidInterval\";");
            migrationBuilder.DropIndex(name: "IX_Bookings_ClientId_IdempotencyKey", table: "Bookings");
            migrationBuilder.DropColumn(name: "IdempotencyKey", table: "Bookings");
            migrationBuilder.DropColumn(name: "RequestHash", table: "Bookings");
        }
    }
}
