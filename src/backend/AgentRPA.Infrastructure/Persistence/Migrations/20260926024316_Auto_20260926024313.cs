using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentRPA.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Auto_20260926024313 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "CpuUsage",
                table: "execution_nodes",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "MemoryUsage",
                table: "execution_nodes",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReportedAvailableSlots",
                table: "execution_nodes",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CpuUsage",
                table: "execution_nodes");

            migrationBuilder.DropColumn(
                name: "MemoryUsage",
                table: "execution_nodes");

            migrationBuilder.DropColumn(
                name: "ReportedAvailableSlots",
                table: "execution_nodes");
        }
    }
}
