using GestaoEstoque.BuildingBlocks;
using GestaoEstoque.Modules.Estoque.Domain;
using GestaoEstoque.Modules.Estoque.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace GestaoEstoque.Modules.Estoque.Application;

public sealed record RegistrarMovimentacaoCommand(
    Guid ProdutoId,
    Guid DepositoId,
    TipoMovimentacao Tipo,
    decimal Quantidade,
    string ChaveIdempotencia,
    Guid? UsuarioId = null);

public sealed record MovimentacaoResultado(Guid MovimentacaoId, decimal SaldoApos, bool Repetida);

public sealed class RegistrarMovimentacaoHandler(EstoqueDbContext db, IVerificadorProduto produtos)
{
    private const int MaxTentativas = 4;

    public async Task<MovimentacaoResultado> HandleAsync(RegistrarMovimentacaoCommand cmd, CancellationToken ct)
    {
        for (var tentativa = 1; ; tentativa++)
        {
            // Idempotência: a mesma chave devolve o resultado original, sem nova baixa.
            var existente = await db.Movimentacoes.AsNoTracking()
                .FirstOrDefaultAsync(m => m.ChaveIdempotencia == cmd.ChaveIdempotencia, ct);
            if (existente is not null)
            {
                var mesmoConteudo = existente.ProdutoId == cmd.ProdutoId
                                    && existente.DepositoId == cmd.DepositoId
                                    && existente.Tipo == cmd.Tipo
                                    && existente.Quantidade == cmd.Quantidade;
                if (!mesmoConteudo)
                    throw new DomainException("Chave de idempotência já usada com outro conteúdo.");
                return new MovimentacaoResultado(existente.Id, existente.SaldoApos, Repetida: true);
            }

            if (!await produtos.ExisteAtivoAsync(cmd.ProdutoId, ct))
                throw new DomainException("Produto inexistente ou inativo.");

            if (!await db.Depositos.AsNoTracking().AnyAsync(d => d.Id == cmd.DepositoId && d.Ativo, ct))
                throw new DomainException("Depósito inexistente ou inativo.");

            var saldo = await db.Saldos.FirstOrDefaultAsync(
                s => s.ProdutoId == cmd.ProdutoId && s.DepositoId == cmd.DepositoId, ct);
            if (saldo is null)
            {
                saldo = new SaldoEstoque(cmd.ProdutoId, cmd.DepositoId);
                db.Saldos.Add(saldo);
            }

            var movimentacao = saldo.Registrar(cmd.Tipo, cmd.Quantidade);
            movimentacao.DefinirChave(cmd.ChaveIdempotencia);
            movimentacao.DefinirAutor(cmd.UsuarioId);
            db.Movimentacoes.Add(movimentacao);

            // Outbox: gravado na mesma transação; um publicador em segundo plano envia ao broker.
            db.Outbox.Add(MensagemOutbox.De(RotasEstoque.MovimentacaoRegistrada, new MovimentacaoRegistradaIntegracao(
                movimentacao.Id, movimentacao.ProdutoId, movimentacao.DepositoId, movimentacao.Tipo,
                movimentacao.Quantidade, movimentacao.SaldoApos, cmd.UsuarioId, movimentacao.CriadoEm)));

            try
            {
                // Saldo e ledger no mesmo SaveChanges = mesma transação.
                await db.SaveChangesAsync(ct);
                return new MovimentacaoResultado(movimentacao.Id, movimentacao.SaldoApos, Repetida: false);
            }
            catch (Exception e) when (tentativa < MaxTentativas && ConflitoRecuperavel(e))
            {
                // Outra requisição alterou o saldo (xmin) ou criou a mesma linha: recarrega e tenta de novo.
                db.ChangeTracker.Clear();
            }
        }
    }

    private static bool ConflitoRecuperavel(Exception e) =>
        e is DbUpdateConcurrencyException
        || e is DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } };
}
