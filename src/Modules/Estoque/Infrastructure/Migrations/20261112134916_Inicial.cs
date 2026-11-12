using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestaoEstoque.Modules.Estoque.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "estoque");

            migrationBuilder.CreateTable(
                name: "movimentacoes",
                schema: "estoque",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProdutoId = table.Column<Guid>(type: "uuid", nullable: false),
                    DepositoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Quantidade = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    SaldoApos = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ChaveIdempotencia = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_movimentacoes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "saldos",
                schema: "estoque",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProdutoId = table.Column<Guid>(type: "uuid", nullable: false),
                    DepositoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantidade = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_saldos", x => x.Id);
                    table.CheckConstraint("ck_saldos_quantidade_nao_negativa", "\"Quantidade\" >= 0");
                });

            migrationBuilder.CreateIndex(
                name: "IX_movimentacoes_ChaveIdempotencia",
                schema: "estoque",
                table: "movimentacoes",
                column: "ChaveIdempotencia",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_movimentacoes_ProdutoId_DepositoId_CriadoEm",
                schema: "estoque",
                table: "movimentacoes",
                columns: new[] { "ProdutoId", "DepositoId", "CriadoEm" });

            migrationBuilder.CreateIndex(
                name: "IX_saldos_ProdutoId_DepositoId",
                schema: "estoque",
                table: "saldos",
                columns: new[] { "ProdutoId", "DepositoId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "movimentacoes",
                schema: "estoque");

            migrationBuilder.DropTable(
                name: "saldos",
                schema: "estoque");
        }
    }
}
