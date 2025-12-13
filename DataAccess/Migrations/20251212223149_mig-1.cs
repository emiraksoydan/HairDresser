using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class mig1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Ratings_TargetId_Score",
                table: "Ratings",
                columns: new[] { "TargetId", "Score" });

            migrationBuilder.CreateIndex(
                name: "IX_FreeBarbers_FreeBarberUserId",
                table: "FreeBarbers",
                column: "FreeBarberUserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FreeBarbers_IsAvailable_Latitude_Longitude",
                table: "FreeBarbers",
                columns: new[] { "IsAvailable", "Latitude", "Longitude" });

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_BarberStoreUserId_Status",
                table: "Appointments",
                columns: new[] { "BarberStoreUserId", "Status" },
                filter: "[Status] IN (0, 1)");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_CustomerUserId_Status",
                table: "Appointments",
                columns: new[] { "CustomerUserId", "Status" },
                filter: "[Status] IN (0, 1)");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_FreeBarberUserId_Status",
                table: "Appointments",
                columns: new[] { "FreeBarberUserId", "Status" },
                filter: "[Status] IN (0, 1)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Ratings_TargetId_Score",
                table: "Ratings");

            migrationBuilder.DropIndex(
                name: "IX_FreeBarbers_FreeBarberUserId",
                table: "FreeBarbers");

            migrationBuilder.DropIndex(
                name: "IX_FreeBarbers_IsAvailable_Latitude_Longitude",
                table: "FreeBarbers");

            migrationBuilder.DropIndex(
                name: "IX_Appointments_BarberStoreUserId_Status",
                table: "Appointments");

            migrationBuilder.DropIndex(
                name: "IX_Appointments_CustomerUserId_Status",
                table: "Appointments");

            migrationBuilder.DropIndex(
                name: "IX_Appointments_FreeBarberUserId_Status",
                table: "Appointments");
        }
    }
}
