using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CaseGig.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIdempotencyToOrdens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Ordens_IdCliente",
                table: "Ordens");

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "Ordens",
                type: "varchar(200)",
                maxLength: 200,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyOperation",
                table: "Ordens",
                type: "varchar(80)",
                maxLength: 80,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyRequestHash",
                table: "Ordens",
                type: "varchar(64)",
                maxLength: 64,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Ordens_IdCliente_IdempotencyOperation_IdempotencyKey",
                table: "Ordens",
                columns: new[] { "IdCliente", "IdempotencyOperation", "IdempotencyKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Ordens_IdCliente_IdempotencyOperation_IdempotencyKey",
                table: "Ordens");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "Ordens");

            migrationBuilder.DropColumn(
                name: "IdempotencyOperation",
                table: "Ordens");

            migrationBuilder.DropColumn(
                name: "IdempotencyRequestHash",
                table: "Ordens");

            migrationBuilder.CreateIndex(
                name: "IX_Ordens_IdCliente",
                table: "Ordens",
                column: "IdCliente");
        }
    }
}
