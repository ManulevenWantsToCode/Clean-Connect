using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clean_Connect.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkerHourlyRateAndBookingTimes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Amount",
                table: "ServiceTypes");

            migrationBuilder.AddColumn<decimal>(
                name: "HourlyRate",
                table: "Workers",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "EndTime",
                table: "Bookings",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<decimal>(
                name: "HourlyRate",
                table: "Bookings",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartTime",
                table: "Bookings",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<decimal>(
                name: "TotalAmount",
                table: "Bookings",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.Sql("""
                UPDATE "Workers" SET "HourlyRate" = 2000.00 WHERE "HourlyRate" = 0;

                UPDATE "Bookings"
                   SET "StartTime"  = "DateOfService",
                       "EndTime"    = "DateOfService" + INTERVAL '3 hours',
                       "HourlyRate" = 2000.00,
                       "TotalAmount"= "Amount"
                 WHERE "TotalAmount" = 0;

                ALTER TABLE "Workers"  ALTER COLUMN "HourlyRate" DROP DEFAULT;
                ALTER TABLE "Bookings" ALTER COLUMN "StartTime"  DROP DEFAULT;
                ALTER TABLE "Bookings" ALTER COLUMN "EndTime"    DROP DEFAULT;
                ALTER TABLE "Bookings" ALTER COLUMN "HourlyRate" DROP DEFAULT;
                ALTER TABLE "Bookings" ALTER COLUMN "TotalAmount" DROP DEFAULT;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HourlyRate",
                table: "Workers");

            migrationBuilder.DropColumn(
                name: "EndTime",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "HourlyRate",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "StartTime",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "TotalAmount",
                table: "Bookings");

            migrationBuilder.AddColumn<decimal>(
                name: "Amount",
                table: "ServiceTypes",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);
        }
    }
}
