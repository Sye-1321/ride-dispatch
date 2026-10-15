using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RideDispatch.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnforceRideOfferConsistency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ride_request_id",
                table: "offers",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE offers AS offer
                SET ride_request_id = run.ride_request_id
                FROM allocation_runs AS run
                WHERE run.id = offer.allocation_run_id;

                UPDATE offers
                SET status = 'Expired', resolved_at = CURRENT_TIMESTAMP
                WHERE status = 'Pending' AND expires_at <= CURRENT_TIMESTAMP;

                DO $migration$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM offers
                        WHERE status IN ('Pending', 'Accepted')
                        GROUP BY ride_request_id
                        HAVING count(*) > 1
                    ) THEN
                        RAISE EXCEPTION 'Cannot enforce ride offer consistency: a ride has multiple actionable offers.'
                            USING HINT = 'Resolve duplicate Pending or Accepted offers before retrying the migration.';
                    END IF;
                END $migration$;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "ride_request_id",
                table: "offers",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ux_offers_actionable_ride_request_id",
                table: "offers",
                column: "ride_request_id",
                unique: true,
                filter: "status IN ('Pending', 'Accepted')");

            migrationBuilder.AddForeignKey(
                name: "FK_offers_ride_requests_ride_request_id",
                table: "offers",
                column: "ride_request_id",
                principalTable: "ride_requests",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_offers_ride_requests_ride_request_id",
                table: "offers");

            migrationBuilder.DropIndex(
                name: "ux_offers_actionable_ride_request_id",
                table: "offers");

            migrationBuilder.DropColumn(
                name: "ride_request_id",
                table: "offers");
        }
    }
}
