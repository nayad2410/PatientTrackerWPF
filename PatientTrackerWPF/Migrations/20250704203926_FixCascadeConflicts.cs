using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace PatientTrackerWPF.Migrations
{
    /// <inheritdoc />
    public partial class FixCascadeConflicts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ScoreEntry_BDI2_Range",
                table: "ScoreEntries");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ScoreEntry_GAD7_Range",
                table: "ScoreEntries");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ScoreEntry_PCL5_Range",
                table: "ScoreEntries");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ScoreEntry_PHQ9_Range",
                table: "ScoreEntries");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ScoreEntry_YBOCS_Range",
                table: "ScoreEntries");

            migrationBuilder.AlterColumn<int>(
                name: "YBOCS",
                table: "ScoreEntries",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<int>(
                name: "PHQ9",
                table: "ScoreEntries",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<int>(
                name: "PCL5",
                table: "ScoreEntries",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<int>(
                name: "GAD7",
                table: "ScoreEntries",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<int>(
                name: "BDI2",
                table: "ScoreEntries",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<int>(
                name: "CreatedByUserId",
                table: "ScoreEntries",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UpdatedByUserId",
                table: "ScoreEntries",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Username = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Salt = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Role = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "User"),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    LastLogin = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FailedLoginAttempts = table.Column<int>(type: "int", nullable: false),
                    LockedUntil = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PasswordResetToken = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PasswordResetExpires = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "CreatedAt", "CreatedBy", "Email", "FailedLoginAttempts", "FullName", "IsActive", "LastLogin", "LockedUntil", "PasswordHash", "PasswordResetExpires", "PasswordResetToken", "Role", "Salt", "UpdatedAt", "UpdatedBy", "Username" },
                values: new object[,]
                {
                    { 1, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "System", "admin@mentalhealth.clinic", 0, "System Administrator", true, null, null, "dX1BlT191I7AM5wWntGH8/3xkkDyiS6noRsuOogGHKw=", null, null, "Admin", "Q2tL8K9mN5pR7sT1vW3xZ6cF4hJ2kM8q", null, null, "admin" },
                    { 2, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "System", "dr.smith@mentalhealth.clinic", 0, "Dr. John Smith", true, null, null, "uZhFenZ9S3QNv/n2IMjMww59FHNmF4cemZT1bwJHOnY=", null, null, "Doctor", "A1bC3dE5fG7hI9jK2lM4nO6pQ8rS0tU", null, null, "dr.smith" },
                    { 3, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "System", "nurse.jane@mentalhealth.clinic", 0, "Jane Doe, RN", true, null, null, "L9arRXBEO+qbMKDPDpN8+84S4mDbFF0OOqw9AH0Wyrk=", null, null, "Nurse", "X1yZ3aB5cD7eF9gH2iJ4kL6mN8oP0qR", null, null, "nurse.jane" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScoreEntry_CreatedByUserId",
                table: "ScoreEntries",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ScoreEntry_UpdatedByUserId",
                table: "ScoreEntries",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_User_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_User_Username",
                table: "Users",
                column: "Username",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ScoreEntries_Users_CreatedByUserId",
                table: "ScoreEntries",
                column: "CreatedByUserId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ScoreEntries_Users_UpdatedByUserId",
                table: "ScoreEntries",
                column: "UpdatedByUserId",
                principalTable: "Users",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ScoreEntries_Users_CreatedByUserId",
                table: "ScoreEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_ScoreEntries_Users_UpdatedByUserId",
                table: "ScoreEntries");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropIndex(
                name: "IX_ScoreEntry_CreatedByUserId",
                table: "ScoreEntries");

            migrationBuilder.DropIndex(
                name: "IX_ScoreEntry_UpdatedByUserId",
                table: "ScoreEntries");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "ScoreEntries");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "ScoreEntries");

            migrationBuilder.AlterColumn<int>(
                name: "YBOCS",
                table: "ScoreEntries",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "PHQ9",
                table: "ScoreEntries",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "PCL5",
                table: "ScoreEntries",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "GAD7",
                table: "ScoreEntries",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "BDI2",
                table: "ScoreEntries",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_ScoreEntry_BDI2_Range",
                table: "ScoreEntries",
                sql: "[BDI2] >= 0 AND [BDI2] <= 100");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ScoreEntry_GAD7_Range",
                table: "ScoreEntries",
                sql: "[GAD7] >= 0 AND [GAD7] <= 100");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ScoreEntry_PCL5_Range",
                table: "ScoreEntries",
                sql: "[PCL5] >= 0 AND [PCL5] <= 100");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ScoreEntry_PHQ9_Range",
                table: "ScoreEntries",
                sql: "[PHQ9] >= 0 AND [PHQ9] <= 100");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ScoreEntry_YBOCS_Range",
                table: "ScoreEntries",
                sql: "[YBOCS] >= 0 AND [YBOCS] <= 100");
        }
    }
}
