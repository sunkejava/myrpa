using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentRPA.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Auto_20260916011513 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExecutionArtifacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ExecutionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TaskItemId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ArtifactType = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    StorageKey = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                    ContentType = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Size = table.Column<long>(type: "INTEGER", nullable: false),
                    Sha256 = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExecutionArtifacts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExecutionLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ExecutionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Sequence = table.Column<long>(type: "INTEGER", nullable: false),
                    Level = table.Column<int>(type: "INTEGER", nullable: false),
                    EventType = table.Column<int>(type: "INTEGER", nullable: false),
                    StepId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    Message = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    MetadataJson = table.Column<string>(type: "TEXT", maxLength: 16000, nullable: true),
                    Sensitive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExecutionLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionArtifacts_ExecutionId_CreatedAt",
                table: "ExecutionArtifacts",
                columns: new[] { "ExecutionId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionArtifacts_TaskItemId_CreatedAt",
                table: "ExecutionArtifacts",
                columns: new[] { "TaskItemId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionLogs_ExecutionId_Level",
                table: "ExecutionLogs",
                columns: new[] { "ExecutionId", "Level" });

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionLogs_ExecutionId_Sequence",
                table: "ExecutionLogs",
                columns: new[] { "ExecutionId", "Sequence" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExecutionArtifacts");

            migrationBuilder.DropTable(
                name: "ExecutionLogs");
        }
    }
}
