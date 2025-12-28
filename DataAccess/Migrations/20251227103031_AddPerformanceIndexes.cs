using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Images_ImageOwnerId",
                table: "Images",
                column: "ImageOwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Images_OwnerType_ImageOwnerId",
                table: "Images",
                columns: new[] { "OwnerType", "ImageOwnerId" });

            migrationBuilder.CreateIndex(
                name: "IX_Favorites_FavoritedFromId_FavoritedToId_IsActive",
                table: "Favorites",
                columns: new[] { "FavoritedFromId", "FavoritedToId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_Favorites_FavoritedToId_FavoritedFromId_IsActive",
                table: "Favorites",
                columns: new[] { "FavoritedToId", "FavoritedFromId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_Favorites_FavoritedToId_IsActive",
                table: "Favorites",
                columns: new[] { "FavoritedToId", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Images_ImageOwnerId",
                table: "Images");

            migrationBuilder.DropIndex(
                name: "IX_Images_OwnerType_ImageOwnerId",
                table: "Images");

            migrationBuilder.DropIndex(
                name: "IX_Favorites_FavoritedFromId_FavoritedToId_IsActive",
                table: "Favorites");

            migrationBuilder.DropIndex(
                name: "IX_Favorites_FavoritedToId_FavoritedFromId_IsActive",
                table: "Favorites");

            migrationBuilder.DropIndex(
                name: "IX_Favorites_FavoritedToId_IsActive",
                table: "Favorites");
        }
    }
}
