using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clean_Connect.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingChatUnreadState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ClientLastReadAt",
                table: "BookingChats",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ClientUnreadCount",
                table: "BookingChats",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "LastMessageSender",
                table: "BookingChats",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastMessageSentAt",
                table: "BookingChats",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastMessageText",
                table: "BookingChats",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "WorkerLastReadAt",
                table: "BookingChats",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WorkerUnreadCount",
                table: "BookingChats",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClientLastReadAt",
                table: "BookingChats");

            migrationBuilder.DropColumn(
                name: "ClientUnreadCount",
                table: "BookingChats");

            migrationBuilder.DropColumn(
                name: "LastMessageSender",
                table: "BookingChats");

            migrationBuilder.DropColumn(
                name: "LastMessageSentAt",
                table: "BookingChats");

            migrationBuilder.DropColumn(
                name: "LastMessageText",
                table: "BookingChats");

            migrationBuilder.DropColumn(
                name: "WorkerLastReadAt",
                table: "BookingChats");

            migrationBuilder.DropColumn(
                name: "WorkerUnreadCount",
                table: "BookingChats");
        }
    }
}
