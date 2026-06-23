using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApplication1.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectApprovalStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ApprovalStatus",
                table: "Projects",
                type: "varchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Approved")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "ApprovedByUserId",
                table: "Projects",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedAt",
                table: "Projects",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Projects_ApprovedByUserId",
                table: "Projects",
                column: "ApprovedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Projects_Users_ApprovedByUserId",
                table: "Projects",
                column: "ApprovedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Projects_Users_ApprovedByUserId",
                table: "Projects");

            migrationBuilder.DropIndex(
                name: "IX_Projects_ApprovedByUserId",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "ApprovalStatus",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "ApprovedByUserId",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "ApprovedAt",
                table: "Projects");
        }
    }
}
