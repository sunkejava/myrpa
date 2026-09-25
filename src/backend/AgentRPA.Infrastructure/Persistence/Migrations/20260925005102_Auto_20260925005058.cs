using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentRPA.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Auto_20260925005058 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Denied",
                table: "role_access_policies",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "Denied",
                table: "access_policies",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Denied",
                table: "role_access_policies");

            migrationBuilder.DropColumn(
                name: "Denied",
                table: "access_policies");
        }
    }
}
