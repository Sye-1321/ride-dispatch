using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RideDispatch.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LinkReplacementAllocationRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "source_offer_id",
                table: "allocation_runs",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ux_allocation_runs_source_offer_id",
                table: "allocation_runs",
                column: "source_offer_id",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_allocation_runs_offers_source_offer_id",
                table: "allocation_runs",
                column: "source_offer_id",
                principalTable: "offers",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_allocation_runs_offers_source_offer_id",
                table: "allocation_runs");

            migrationBuilder.DropIndex(
                name: "ux_allocation_runs_source_offer_id",
                table: "allocation_runs");

            migrationBuilder.DropColumn(
                name: "source_offer_id",
                table: "allocation_runs");
        }
    }
}
