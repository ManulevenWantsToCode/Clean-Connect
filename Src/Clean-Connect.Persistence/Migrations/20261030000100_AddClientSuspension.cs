using Clean_Connect.Infrastructure.Context;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clean_Connect.Persistence.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261030000100_AddClientSuspension")]
    public partial class AddClientSuspension : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(name: "IsSuspended", table: "Clients", type: "boolean", nullable: false, defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "IsSuspended", table: "Clients");
        }
    }
}
