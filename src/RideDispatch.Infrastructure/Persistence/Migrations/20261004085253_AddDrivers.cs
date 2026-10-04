using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RideDispatch.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDrivers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "drivers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    approval_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    operational_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    available_since = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_drivers", x => x.id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "drivers");
        }
    }
}
