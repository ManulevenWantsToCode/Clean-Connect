using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Clean_Connect.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPlatformSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlatformSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PlatformName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SupportEmail = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    CustomerServicePhone = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    BaseCurrency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Timezone = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    MaintenanceMode = table.Column<bool>(type: "boolean", nullable: false),
                    AutoMatchCleanerTimeoutMinutes = table.Column<int>(type: "integer", nullable: false),
                    MinimumAdvanceNoticeHours = table.Column<int>(type: "integer", nullable: false),
                    FreeCancellationWindowHours = table.Column<int>(type: "integer", nullable: false),
                    MaxActiveJobsPerCleanerPerDay = table.Column<int>(type: "integer", nullable: false),
                    CommissionPercent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    AutoReleaseEscrowDelayHours = table.Column<int>(type: "integer", nullable: false),
                    MinimumPayoutAmount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    EmailAlertsOnNewBooking = table.Column<bool>(type: "boolean", nullable: false),
                    SmsToCleanerOnAssignment = table.Column<bool>(type: "boolean", nullable: false),
                    EmailBookingConfirmation = table.Column<bool>(type: "boolean", nullable: false),
                    EmailBookingCancellation = table.Column<bool>(type: "boolean", nullable: false),
                    EmailPaymentNotifications = table.Column<bool>(type: "boolean", nullable: false),
                    SmsBookingNotifications = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformSettings", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "PlatformSettings",
                columns: new[] { "Id", "AutoMatchCleanerTimeoutMinutes", "AutoReleaseEscrowDelayHours", "BaseCurrency", "CommissionPercent", "CustomerServicePhone", "EmailAlertsOnNewBooking", "EmailBookingCancellation", "EmailBookingConfirmation", "EmailPaymentNotifications", "FreeCancellationWindowHours", "MaintenanceMode", "MaxActiveJobsPerCleanerPerDay", "MinimumAdvanceNoticeHours", "MinimumPayoutAmount", "PlatformName", "SmsBookingNotifications", "SmsToCleanerOnAssignment", "SupportEmail", "Timezone", "UpdatedAtUtc" },
                values: new object[] { 1, 15, 24, "NGN", 15m, "+234 800 253 2626", true, true, true, true, 4, false, 2, 2, 0m, "Clean Connect", true, true, "support@cleanconnect.com", "Africa/Lagos", new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlatformSettings");
        }
    }
}
