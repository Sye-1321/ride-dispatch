using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RideDispatch.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddScheduledDispatchActivation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "dispatch_activated_at",
                table: "ride_requests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_ride_requests_dispatch_activation_after_creation",
                table: "ride_requests",
                sql: "dispatch_activated_at IS NULL OR dispatch_activated_at >= created_at");

            migrationBuilder.AddCheckConstraint(
                name: "ck_ride_requests_dispatch_activation_before_pickup",
                table: "ride_requests",
                sql: "dispatch_activated_at IS NULL OR dispatch_activated_at <= requested_pickup_at");

            migrationBuilder.AddCheckConstraint(
                name: "ck_ride_requests_dispatch_activation_scheduled",
                table: "ride_requests",
                sql: "dispatch_activated_at IS NULL OR timing = 'Scheduled'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_ride_requests_dispatch_activation_after_creation",
                table: "ride_requests");

            migrationBuilder.DropCheckConstraint(
                name: "ck_ride_requests_dispatch_activation_before_pickup",
                table: "ride_requests");

            migrationBuilder.DropCheckConstraint(
                name: "ck_ride_requests_dispatch_activation_scheduled",
                table: "ride_requests");

            migrationBuilder.DropColumn(
                name: "dispatch_activated_at",
                table: "ride_requests");
        }
    }
}
