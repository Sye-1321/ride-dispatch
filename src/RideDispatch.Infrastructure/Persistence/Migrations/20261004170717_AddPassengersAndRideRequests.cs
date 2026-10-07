using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RideDispatch.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPassengersAndRideRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "passengers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    phone_number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_passengers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ride_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    passenger_id = table.Column<Guid>(type: "uuid", nullable: true),
                    contact_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    contact_phone = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    booking_source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    timing = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    pickup_latitude = table.Column<double>(type: "double precision", nullable: false),
                    pickup_longitude = table.Column<double>(type: "double precision", nullable: false),
                    destination_latitude = table.Column<double>(type: "double precision", nullable: false),
                    destination_longitude = table.Column<double>(type: "double precision", nullable: false),
                    required_vehicle_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    requested_pickup_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    estimated_trip_duration_seconds = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ride_requests", x => x.id);
                    table.CheckConstraint("ck_ride_requests_estimated_trip_duration_positive", "estimated_trip_duration_seconds > 0");
                    table.CheckConstraint("ck_ride_requests_timing_requested_pickup_consistency", "(timing = 'Immediate' AND requested_pickup_at IS NULL) OR (timing = 'Scheduled' AND requested_pickup_at IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_ride_requests_passengers_passenger_id",
                        column: x => x.passenger_id,
                        principalTable: "passengers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ride_requests_passenger_id",
                table: "ride_requests",
                column: "passenger_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ride_requests");

            migrationBuilder.DropTable(
                name: "passengers");
        }
    }
}
