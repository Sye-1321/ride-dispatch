using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RideDispatch.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAllocationCandidateEvaluations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "allocation_candidate_evaluations",
                columns: table => new
                {
                    allocation_run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    driver_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_eligible = table.Column<bool>(type: "boolean", nullable: false),
                    rank = table.Column<int>(type: "integer", nullable: true),
                    distance_meters = table.Column<double>(type: "double precision", nullable: false),
                    available_since = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    location_recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_allocation_candidate_evaluations", x => new { x.allocation_run_id, x.driver_id });
                    table.CheckConstraint("ck_allocation_candidate_evaluations_rank", "(is_eligible AND rank IS NOT NULL AND rank > 0) OR (NOT is_eligible AND rank IS NULL)");
                    table.ForeignKey(
                        name: "FK_allocation_candidate_evaluations_allocation_runs_allocation~",
                        column: x => x.allocation_run_id,
                        principalTable: "allocation_runs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_allocation_candidate_evaluations_drivers_driver_id",
                        column: x => x.driver_id,
                        principalTable: "drivers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "allocation_candidate_rejections",
                columns: table => new
                {
                    allocation_run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    driver_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_allocation_candidate_rejections", x => new { x.allocation_run_id, x.driver_id, x.reason });
                    table.ForeignKey(
                        name: "FK_allocation_candidate_rejections_allocation_candidate_evalua~",
                        columns: x => new { x.allocation_run_id, x.driver_id },
                        principalTable: "allocation_candidate_evaluations",
                        principalColumns: new[] { "allocation_run_id", "driver_id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_allocation_candidate_evaluations_driver_id",
                table: "allocation_candidate_evaluations",
                column: "driver_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "allocation_candidate_rejections");

            migrationBuilder.DropTable(
                name: "allocation_candidate_evaluations");
        }
    }
}
