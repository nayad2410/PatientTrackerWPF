using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PatientTrackerWPF.Migrations
{
    /// <inheritdoc />
    public partial class UpdatedScoreEntryWithAuditFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PCL5Total",
                table: "ScoreEntries");

            migrationBuilder.AlterColumn<string>(
                name: "PatientId",
                table: "ScoreEntries",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Note",
                table: "ScoreEntries",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "ScoreEntries",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETUTCDATE()");

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "ScoreEntries",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "ScoreEntries",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "ScoreEntries",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScoreEntry_Date",
                table: "ScoreEntries",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_ScoreEntry_PatientId",
                table: "ScoreEntries",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_ScoreEntry_PatientId_Date",
                table: "ScoreEntries",
                columns: new[] { "PatientId", "Date" });

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ScoreEntry_Date",
                table: "ScoreEntries");

            migrationBuilder.DropIndex(
                name: "IX_ScoreEntry_PatientId",
                table: "ScoreEntries");

            migrationBuilder.DropIndex(
                name: "IX_ScoreEntry_PatientId_Date",
                table: "ScoreEntries");

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

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "ScoreEntries");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "ScoreEntries");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "ScoreEntries");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "ScoreEntries");

            migrationBuilder.AlterColumn<string>(
                name: "PatientId",
                table: "ScoreEntries",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "Note",
                table: "ScoreEntries",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PCL5Total",
                table: "ScoreEntries",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }
    }
}
