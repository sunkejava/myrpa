using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentRPA.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Auto_20260916015759 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LlmUsageRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SubjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TaskId = table.Column<Guid>(type: "TEXT", nullable: true),
                    TaskItemId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ProviderId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Model = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    InputTokens = table.Column<int>(type: "INTEGER", nullable: false),
                    OutputTokens = table.Column<int>(type: "INTEGER", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LlmUsageRecords", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LlmUsageRecords_Id",
                table: "LlmUsageRecords",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_LlmUsageRecords_SubjectId",
                table: "LlmUsageRecords",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_LlmUsageRecords_TaskId",
                table: "LlmUsageRecords",
                column: "TaskId");

            migrationBuilder.CreateIndex(
                name: "IX_LlmUsageRecords_TaskItemId",
                table: "LlmUsageRecords",
                column: "TaskItemId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LlmUsageRecords");
        }
    }
}
