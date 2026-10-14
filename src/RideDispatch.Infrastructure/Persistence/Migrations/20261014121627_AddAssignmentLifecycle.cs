using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RideDispatch.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAssignmentLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "ALTER TABLE assignments DROP CONSTRAINT ex_assignments_driver_time;");

            migrationBuilder.DropIndex(
                name: "ux_assignments_ride_request_id",
                table: "assignments");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "resolved_at",
                table: "assignments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "status",
                table: "assignments",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Active");

            migrationBuilder.CreateIndex(
                name: "ux_assignments_ride_request_id",
                table: "assignments",
                column: "ride_request_id",
                unique: true,
                filter: "status <> 'Cancelled'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_assignments_completed_not_before_start",
                table: "assignments",
                sql: "status <> 'Completed' OR resolved_at >= starts_at");

            migrationBuilder.AddCheckConstraint(
                name: "ck_assignments_resolved_not_before_created",
                table: "assignments",
                sql: "resolved_at IS NULL OR resolved_at >= created_at");

            migrationBuilder.AddCheckConstraint(
                name: "ck_assignments_status_resolved",
                table: "assignments",
                sql: "(status = 'Active' AND resolved_at IS NULL) OR (status IN ('Completed', 'Cancelled') AND resolved_at IS NOT NULL)");

            migrationBuilder.Sql(
                """
                ALTER TABLE assignments
                ADD CONSTRAINT ex_assignments_driver_time
                EXCLUDE USING gist (
                    driver_id WITH =,
                    tstzrange(starts_at, ends_at, '[)') WITH &&
                )
                WHERE (status = 'Active');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "ALTER TABLE assignments DROP CONSTRAINT ex_assignments_driver_time;");

            migrationBuilder.DropIndex(
                name: "ux_assignments_ride_request_id",
                table: "assignments");

            migrationBuilder.DropCheckConstraint(
                name: "ck_assignments_completed_not_before_start",
                table: "assignments");

            migrationBuilder.DropCheckConstraint(
                name: "ck_assignments_resolved_not_before_created",
                table: "assignments");

            migrationBuilder.DropCheckConstraint(
                name: "ck_assignments_status_resolved",
                table: "assignments");

            migrationBuilder.DropColumn(
                name: "resolved_at",
                table: "assignments");

            migrationBuilder.DropColumn(
                name: "status",
                table: "assignments");

            migrationBuilder.CreateIndex(
                name: "ux_assignments_ride_request_id",
                table: "assignments",
                column: "ride_request_id",
                unique: true);

            migrationBuilder.Sql(
                """
                ALTER TABLE assignments
                ADD CONSTRAINT ex_assignments_driver_time
                EXCLUDE USING gist (
                    driver_id WITH =,
                    tstzrange(starts_at, ends_at, '[)') WITH &&
                );
                """);
        }
    }
}
