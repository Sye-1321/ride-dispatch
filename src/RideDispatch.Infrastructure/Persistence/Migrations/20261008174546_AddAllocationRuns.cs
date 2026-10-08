using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RideDispatch.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAllocationRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "allocation_runs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ride_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ranking_policy = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    recommended_driver_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_allocation_runs", x => x.id);
                    table.ForeignKey(
                        name: "FK_allocation_runs_drivers_recommended_driver_id",
                        column: x => x.recommended_driver_id,
                        principalTable: "drivers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_allocation_runs_ride_requests_ride_request_id",
                        column: x => x.ride_request_id,
                        principalTable: "ride_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_allocation_runs_recommended_driver_id",
                table: "allocation_runs",
                column: "recommended_driver_id");

            migrationBuilder.CreateIndex(
                name: "IX_allocation_runs_ride_request_id",
                table: "allocation_runs",
                column: "ride_request_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "allocation_runs");
        }
    }
}
