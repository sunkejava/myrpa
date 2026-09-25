using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentRPA.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Auto_20260925111202 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "task_approvals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TaskId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RequesterId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ReviewerId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task_approvals", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_task_approvals_Status",
                table: "task_approvals",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_task_approvals_TaskId",
                table: "task_approvals",
                column: "TaskId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "task_approvals");
        }
    }
}
