using GestaoEstoque.BuildingBlocks;
using GestaoEstoque.Modules.Estoque.Domain;
using GestaoEstoque.Modules.Estoque.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace GestaoEstoque.Modules.Estoque.Application;

public sealed class DepositoService(EstoqueDbContext db)
{
    public async Task<Deposito> CriarAsync(string codigo, string nome, CancellationToken ct)
    {
        var deposito = new Deposito(codigo, nome);

        if (await db.Depositos.AnyAsync(d => d.Codigo == deposito.Codigo, ct))
            throw new DomainException("Código de depósito já cadastrado.");

        db.Depositos.Add(deposito);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new DomainException("Código de depósito já cadastrado.");
        }

        return deposito;
    }

    public async Task<IReadOnlyList<Deposito>> ListarAsync(bool apenasAtivos, CancellationToken ct)
    {
        var consulta = db.Depositos.AsNoTracking().AsQueryable();
        if (apenasAtivos) consulta = consulta.Where(d => d.Ativo);
        return await consulta.OrderBy(d => d.Nome).ToListAsync(ct);
    }

    /// <returns>false se o depósito não existe.</returns>
    public async Task<bool> DesativarAsync(Guid id, CancellationToken ct)
    {
        var deposito = await db.Depositos.FirstOrDefaultAsync(d => d.Id == id, ct);
        if (deposito is null) return false;

        // Desativar com mercadoria dentro esconderia estoque real: exige zerar antes.
        if (await db.Saldos.AnyAsync(s => s.DepositoId == id && s.Quantidade > 0, ct))
            throw new DomainException("O depósito ainda possui saldo em estoque. Zere ou transfira antes de desativar.");

        deposito.Desativar();
        await db.SaveChangesAsync(ct);
        return true;
    }
}
