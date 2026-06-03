using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartAttendance.Migrations
{
    /// <inheritdoc />
    public partial class AddCourseSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AutoFillAbsencesEnabled",
                table: "Courses",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "AutoFillDayOfWeek",
                table: "Courses",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<TimeSpan>(
                name: "AutoFillTime",
                table: "Courses",
                type: "time",
                nullable: false,
                defaultValue: new TimeSpan(0, 0, 0, 0, 0));

            migrationBuilder.AddColumn<int>(
                name: "MinimumAttendanceRequired",
                table: "Courses",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ProfessorContactEmail",
                table: "Courses",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AutoFillAbsencesEnabled",
                table: "Courses");

            migrationBuilder.DropColumn(
                name: "AutoFillDayOfWeek",
                table: "Courses");

            migrationBuilder.DropColumn(
                name: "AutoFillTime",
                table: "Courses");

            migrationBuilder.DropColumn(
                name: "MinimumAttendanceRequired",
                table: "Courses");

            migrationBuilder.DropColumn(
                name: "ProfessorContactEmail",
                table: "Courses");
        }
    }
}
