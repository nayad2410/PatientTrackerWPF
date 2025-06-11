using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PatientTrackerWPF.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ScoreEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PHQ9 = table.Column<int>(type: "int", nullable: false),
                    GAD7 = table.Column<int>(type: "int", nullable: false),
                    PCL5 = table.Column<int>(type: "int", nullable: false),
                    BDI2 = table.Column<int>(type: "int", nullable: false),
                    PCL5Total = table.Column<int>(type: "int", nullable: false),
                    YBOCS = table.Column<int>(type: "int", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScoreEntries", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ScoreEntries");
        }
    }
}
