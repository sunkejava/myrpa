using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentRPA.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Auto_20260925163159 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MockSocialEmployees",
                columns: table => new
                {
                    IdNumber = table.Column<string>(type: "TEXT", maxLength: 18, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MockSocialEmployees", x => x.IdNumber);
                });

            migrationBuilder.CreateTable(
                name: "MockSocialReceipts",
                columns: table => new
                {
                    SubmissionId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Operation = table.Column<string>(type: "TEXT", maxLength: 6, nullable: false),
                    IdNumber = table.Column<string>(type: "TEXT", maxLength: 18, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Message = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MockSocialReceipts", x => x.SubmissionId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MockSocialEmployees");

            migrationBuilder.DropTable(
                name: "MockSocialReceipts");
        }
    }
}
