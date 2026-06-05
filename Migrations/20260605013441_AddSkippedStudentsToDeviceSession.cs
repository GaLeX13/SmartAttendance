using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartAttendance.Migrations
{
    /// <inheritdoc />
    public partial class AddSkippedStudentsToDeviceSession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SkippedStudentIds",
                table: "DeviceSessions",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SkippedStudentIds",
                table: "DeviceSessions");
        }
    }
}
