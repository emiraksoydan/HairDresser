using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddFavoriteThreadSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ChatThreads_AppointmentId",
                table: "ChatThreads");

            migrationBuilder.AlterColumn<Guid>(
                name: "AppointmentId",
                table: "ChatThreads",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<Guid>(
                name: "FavoriteFromUserId",
                table: "ChatThreads",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FavoriteToUserId",
                table: "ChatThreads",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "AppointmentId",
                table: "ChatMessages",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.CreateIndex(
                name: "IX_ChatThreads_AppointmentId",
                table: "ChatThreads",
                column: "AppointmentId",
                unique: true,
                filter: "[AppointmentId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ChatThreads_FavoriteFromUserId_FavoriteToUserId",
                table: "ChatThreads",
                columns: new[] { "FavoriteFromUserId", "FavoriteToUserId" },
                filter: "[FavoriteFromUserId] IS NOT NULL AND [FavoriteToUserId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ChatThreads_AppointmentId",
                table: "ChatThreads");

            migrationBuilder.DropIndex(
                name: "IX_ChatThreads_FavoriteFromUserId_FavoriteToUserId",
                table: "ChatThreads");

            migrationBuilder.DropColumn(
                name: "FavoriteFromUserId",
                table: "ChatThreads");

            migrationBuilder.DropColumn(
                name: "FavoriteToUserId",
                table: "ChatThreads");

            migrationBuilder.AlterColumn<Guid>(
                name: "AppointmentId",
                table: "ChatThreads",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "AppointmentId",
                table: "ChatMessages",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChatThreads_AppointmentId",
                table: "ChatThreads",
                column: "AppointmentId",
                unique: true);
        }
    }
}
