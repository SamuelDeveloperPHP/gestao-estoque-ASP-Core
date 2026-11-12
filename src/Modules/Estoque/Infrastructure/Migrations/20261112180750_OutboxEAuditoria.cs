using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestaoEstoque.Modules.Estoque.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class OutboxEAuditoria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "UsuarioId",
                schema: "estoque",
                table: "movimentacoes",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "outbox",
                schema: "estoque",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RotaChave = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Payload = table.Column<string>(type: "jsonb", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProcessadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbox", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_outbox_CriadoEm",
                schema: "estoque",
                table: "outbox",
                column: "CriadoEm",
                filter: "\"ProcessadoEm\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "outbox",
                schema: "estoque");

            migrationBuilder.DropColumn(
                name: "UsuarioId",
                schema: "estoque",
                table: "movimentacoes");
        }
    }
}
