using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RideDispatch.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOffers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "offers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    allocation_run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    driver_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    resolved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_offers", x => x.id);
                    table.CheckConstraint("ck_offers_expires_after_created", "expires_at > created_at");
                    table.CheckConstraint("ck_offers_resolved_not_before_created", "resolved_at IS NULL OR resolved_at >= created_at");
                    table.CheckConstraint("ck_offers_status_resolved", "(status = 'Pending' AND resolved_at IS NULL) OR (status <> 'Pending' AND resolved_at IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_offers_allocation_runs_allocation_run_id",
                        column: x => x.allocation_run_id,
                        principalTable: "allocation_runs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_offers_drivers_driver_id",
                        column: x => x.driver_id,
                        principalTable: "drivers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_offers_driver_id",
                table: "offers",
                column: "driver_id");

            migrationBuilder.CreateIndex(
                name: "ux_offers_allocation_run_id",
                table: "offers",
                column: "allocation_run_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "offers");
        }
    }
}
