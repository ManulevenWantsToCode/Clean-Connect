using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clean_Connect.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PaymentRecoveryHardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "LastErrorCode",
                table: "PaystackWebhookInboxEvents",
                newName: "LastErrorCategory");

            migrationBuilder.Sql("UPDATE \"PaystackWebhookInboxEvents\" SET \"LastErrorCategory\" = LEFT(\"LastErrorCategory\", 64) WHERE \"LastErrorCategory\" IS NOT NULL;");
            migrationBuilder.AlterColumn<string>(
                name: "LastErrorCategory",
                table: "PaystackWebhookInboxEvents",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastRequeuedBy",
                table: "PaystackWebhookInboxEvents",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastAttemptedAtUtc",
                table: "PaystackWebhookInboxEvents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NextAttemptAtUtc",
                table: "PaystackWebhookInboxEvents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "QuarantinedAtUtc",
                table: "PaystackWebhookInboxEvents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RequeueCount",
                table: "PaystackWebhookInboxEvents",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RetryCount",
                table: "PaystackWebhookInboxEvents",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "InitializationAttemptedAtUtc",
                table: "Payments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InitializationErrorCategory",
                table: "Payments",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InitializationStatus",
                table: "Payments",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Pending");

            migrationBuilder.AddColumn<decimal>(
                name: "RefundAmount",
                table: "Payments",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.Sql(@"
UPDATE ""Payments""
SET ""InitializationStatus"" = CASE
    WHEN ""AuthorizationUrl"" IS NOT NULL THEN 'Initialized'
    WHEN ""Status"" = 'Pending' THEN 'ReconciliationRequired'
    WHEN ""Status"" IN ('Failed', 'Abandoned', 'Reversed', 'Canceled') THEN 'Failed'
    ELSE 'Initialized'
END;
UPDATE ""PaystackWebhookInboxEvents""
SET ""RetryCount"" = ""Attempts"",
    ""LastAttemptedAtUtc"" = COALESCE(""ProcessingStartedAtUtc"", ""LastAttemptedAtUtc""),
    ""NextAttemptAtUtc"" = CASE WHEN ""ProcessingStatus"" = 'Failed' THEN CURRENT_TIMESTAMP ELSE NULL END;");

            migrationBuilder.AddColumn<decimal>(
                name: "RefundedAmount",
                table: "Payments",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateIndex(
                name: "IX_PaystackWebhookInboxEvents_ProcessingStatus_NextAttemptAtUt~",
                table: "PaystackWebhookInboxEvents",
                columns: new[] { "ProcessingStatus", "NextAttemptAtUtc", "ReceivedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PaystackWebhookInboxEvents_ProcessingStatus_NextAttemptAtUt~",
                table: "PaystackWebhookInboxEvents");

            migrationBuilder.DropColumn(
                name: "LastRequeuedBy",
                table: "PaystackWebhookInboxEvents");

            migrationBuilder.RenameColumn(
                name: "LastErrorCategory",
                table: "PaystackWebhookInboxEvents",
                newName: "LastErrorCode");

            migrationBuilder.AlterColumn<string>(
                name: "LastErrorCode",
                table: "PaystackWebhookInboxEvents",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "LastAttemptedAtUtc",
                table: "PaystackWebhookInboxEvents");

            migrationBuilder.DropColumn(
                name: "NextAttemptAtUtc",
                table: "PaystackWebhookInboxEvents");

            migrationBuilder.DropColumn(
                name: "QuarantinedAtUtc",
                table: "PaystackWebhookInboxEvents");

            migrationBuilder.DropColumn(
                name: "RequeueCount",
                table: "PaystackWebhookInboxEvents");

            migrationBuilder.DropColumn(
                name: "RetryCount",
                table: "PaystackWebhookInboxEvents");

            migrationBuilder.DropColumn(
                name: "InitializationAttemptedAtUtc",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "InitializationErrorCategory",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "InitializationStatus",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "RefundAmount",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "RefundedAmount",
                table: "Payments");

        }
    }
}
