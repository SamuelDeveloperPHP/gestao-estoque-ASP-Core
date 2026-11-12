using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestaoEstoque.Modules.Estoque.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Depositos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "depositos",
                schema: "estoque",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Codigo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Nome = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_depositos", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_depositos_Codigo",
                schema: "estoque",
                table: "depositos",
                column: "Codigo",
                unique: true);

            // Depósito padrão: mantém válidos os saldos criados antes do cadastro de depósitos
            // (a UI usava este Id fixo). O Id é o mesmo de Deposito.PrincipalId.
            migrationBuilder.InsertData(
                schema: "estoque",
                table: "depositos",
                columns: new[] { "Id", "Codigo", "Nome", "Ativo" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), "PRINC", "Depósito Principal", true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "depositos",
                schema: "estoque");
        }
    }
}
