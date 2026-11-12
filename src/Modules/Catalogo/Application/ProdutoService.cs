using GestaoEstoque.BuildingBlocks;
using GestaoEstoque.Modules.Catalogo.Domain;
using GestaoEstoque.Modules.Catalogo.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace GestaoEstoque.Modules.Catalogo.Application;

/// <summary>Contrato público do Catálogo para outros módulos (via composition root).</summary>
public interface IConsultaProdutos
{
    Task<bool> ExisteAtivoAsync(Guid produtoId, CancellationToken ct);
}

public sealed class ProdutoService(CatalogoDbContext db) : IConsultaProdutos
{
    public async Task<bool> ExisteAtivoAsync(Guid produtoId, CancellationToken ct) =>
        await db.Produtos.AsNoTracking().AnyAsync(p => p.Id == produtoId && p.Ativo, ct);

    public async Task<Produto> CriarAsync(string sku, string nome, string? unidade, CancellationToken ct)
    {
        var produto = new Produto(sku, nome, unidade);

        if (await db.Produtos.AnyAsync(p => p.Sku == produto.Sku, ct))
            throw new DomainException("SKU já cadastrado.");

        db.Produtos.Add(produto);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new DomainException("SKU já cadastrado.");
        }

        return produto;
    }

    public Task<Produto?> ObterAsync(Guid id, CancellationToken ct) =>
        db.Produtos.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);

    private IQueryable<Produto> Filtrar(string? busca)
    {
        var consulta = db.Produtos.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(busca))
        {
            // Escapa curingas do LIKE para que a busca seja literal.
            var padrao = "%" + busca.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
            consulta = consulta.Where(p => EF.Functions.ILike(p.Nome, padrao) || EF.Functions.ILike(p.Sku, padrao));
        }

        return consulta;
    }

    public Task<int> ContarAsync(string? busca, CancellationToken ct) => Filtrar(busca).CountAsync(ct);

    public async Task<IReadOnlyList<Produto>> ListarAsync(string? busca, int skip, int take, CancellationToken ct)
    {
        return await Filtrar(busca)
            .OrderBy(p => p.Sku)
            .Skip(Math.Max(skip, 0))
            .Take(Math.Clamp(take, 1, 200))
            .ToListAsync(ct);
    }

    public async Task<bool> DesativarAsync(Guid id, CancellationToken ct)
    {
        var produto = await db.Produtos.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (produto is null) return false;

        produto.Desativar();
        await db.SaveChangesAsync(ct);
        return true;
    }
}
