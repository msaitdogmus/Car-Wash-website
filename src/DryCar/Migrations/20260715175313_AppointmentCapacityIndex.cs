using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DryCar.Migrations
{
    /// <inheritdoc />
    public partial class AppointmentCapacityIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Appointments_AppointmentDate",
                table: "Appointments",
                column: "AppointmentDate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Appointments_AppointmentDate",
                table: "Appointments");
        }
    }
}
