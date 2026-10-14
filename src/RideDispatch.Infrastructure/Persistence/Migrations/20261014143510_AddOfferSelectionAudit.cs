using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RideDispatch.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOfferSelectionAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "override_reason",
                table: "offers",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "selection_mode",
                table: "offers",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE offers SET selection_mode = 'Recommended';");

            migrationBuilder.AlterColumn<string>(
                name: "selection_mode",
                table: "offers",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30,
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_offers_selection_override_reason",
                table: "offers",
                sql: "(selection_mode = 'Recommended' AND override_reason IS NULL) OR (selection_mode = 'DispatcherOverride' AND override_reason IS NOT NULL AND length(btrim(override_reason)) > 0)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_offers_selection_override_reason",
                table: "offers");

            migrationBuilder.DropColumn(
                name: "override_reason",
                table: "offers");

            migrationBuilder.DropColumn(
                name: "selection_mode",
                table: "offers");
        }
    }
}
