using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentRPA.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Auto_20260916013942 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SecureEntry",
                table: "HumanInterventions");

            migrationBuilder.AddColumn<string>(
                name: "SecureEntryHash",
                table: "HumanInterventions",
                type: "TEXT",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SubjectId",
                table: "HumanInterventions",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "TokenConsumedAt",
                table: "HumanInterventions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_HumanInterventions_SecureEntryHash",
                table: "HumanInterventions",
                column: "SecureEntryHash");

            migrationBuilder.CreateIndex(
                name: "IX_HumanInterventions_SubjectId_Status",
                table: "HumanInterventions",
                columns: new[] { "SubjectId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_HumanInterventions_SecureEntryHash",
                table: "HumanInterventions");

            migrationBuilder.DropIndex(
                name: "IX_HumanInterventions_SubjectId_Status",
                table: "HumanInterventions");

            migrationBuilder.DropColumn(
                name: "SecureEntryHash",
                table: "HumanInterventions");

            migrationBuilder.DropColumn(
                name: "SubjectId",
                table: "HumanInterventions");

            migrationBuilder.DropColumn(
                name: "TokenConsumedAt",
                table: "HumanInterventions");

            migrationBuilder.AddColumn<string>(
                name: "SecureEntry",
                table: "HumanInterventions",
                type: "TEXT",
                maxLength: 2048,
                nullable: true);
        }
    }
}
