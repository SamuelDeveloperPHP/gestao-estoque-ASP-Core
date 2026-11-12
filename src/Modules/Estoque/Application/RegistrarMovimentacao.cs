using GestaoEstoque.BuildingBlocks;
using GestaoEstoque.Modules.Estoque.Domain;
using GestaoEstoque.Modules.Estoque.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
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

/// <summary>
/// Registra entrada/saída de forma atômica. O saldo é alterado por um único UPDATE condicional
/// ("quantidade = quantidade + delta ... WHERE quantidade + delta >= 0"): requisições concorrentes no mesmo
/// saldo apenas enfileiram na trava da linha, sem conflitos nem retries — e o saldo nunca fica negativo.
/// Saldo, ledger e Outbox são gravados na mesma transação.
/// </summary>
public sealed class RegistrarMovimentacaoHandler(EstoqueDbContext db, IVerificadorProduto produtos)
{
    private const int MaxTentativas = 3;

    private const string SqlGarantirLinha = """
        INSERT INTO estoque.saldos ("Id", "ProdutoId", "DepositoId", "Quantidade")
        VALUES (@id, @produto, @deposito, 0)
        ON CONFLICT ("ProdutoId", "DepositoId") DO NOTHING
        """;

    private const string SqlAjustarSaldo = """
        UPDATE estoque.saldos
        SET "Quantidade" = "Quantidade" + @delta
        WHERE "ProdutoId" = @produto AND "DepositoId" = @deposito AND "Quantidade" + @delta >= 0
        RETURNING "Quantidade"
        """;

    public async Task<MovimentacaoResultado> HandleAsync(RegistrarMovimentacaoCommand cmd, CancellationToken ct)
    {
        if (cmd.Quantidade <= 0) throw new DomainException("A quantidade deve ser maior que zero.");
        if (!Enum.IsDefined(cmd.Tipo)) throw new DomainException("Tipo de movimentação inválido.");

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

            await using var tx = await db.Database.BeginTransactionAsync(ct);
            try
            {
                var saldoApos = await AjustarSaldoAsync(cmd, ct);

                var movimentacao = new MovimentacaoEstoque(cmd.ProdutoId, cmd.DepositoId, cmd.Tipo, cmd.Quantidade, saldoApos);
                movimentacao.DefinirChave(cmd.ChaveIdempotencia);
                movimentacao.DefinirAutor(cmd.UsuarioId);
                db.Movimentacoes.Add(movimentacao);

                // Outbox: um publicador em segundo plano envia ao broker depois do commit.
                db.Outbox.Add(MensagemOutbox.De(RotasEstoque.MovimentacaoRegistrada, new MovimentacaoRegistradaIntegracao(
                    movimentacao.Id, movimentacao.ProdutoId, movimentacao.DepositoId, movimentacao.Tipo,
                    movimentacao.Quantidade, saldoApos, cmd.UsuarioId, movimentacao.CriadoEm)));

                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return new MovimentacaoResultado(movimentacao.Id, saldoApos, Repetida: false);
            }
            catch (DbUpdateException e) when (tentativa < MaxTentativas
                && e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                // Mesma Idempotency-Key chegando em paralelo: a outra requisição venceu; relê e devolve o resultado dela.
                db.ChangeTracker.Clear();
            }
        }
    }

    private async Task<decimal> AjustarSaldoAsync(RegistrarMovimentacaoCommand cmd, CancellationToken ct)
    {
        var conexao = (NpgsqlConnection)db.Database.GetDbConnection();
        var transacao = (NpgsqlTransaction)db.Database.CurrentTransaction!.GetDbTransaction();
        var delta = cmd.Tipo == TipoMovimentacao.Entrada ? cmd.Quantidade : -cmd.Quantidade;

        await using (var garantir = new NpgsqlCommand(SqlGarantirLinha, conexao, transacao))
        {
            garantir.Parameters.AddWithValue("id", Guid.NewGuid());
            garantir.Parameters.AddWithValue("produto", cmd.ProdutoId);
            garantir.Parameters.AddWithValue("deposito", cmd.DepositoId);
            await garantir.ExecuteNonQueryAsync(ct);
        }

        await using var ajustar = new NpgsqlCommand(SqlAjustarSaldo, conexao, transacao);
        ajustar.Parameters.AddWithValue("delta", delta);
        ajustar.Parameters.AddWithValue("produto", cmd.ProdutoId);
        ajustar.Parameters.AddWithValue("deposito", cmd.DepositoId);

        // Nenhuma linha afetada = a condição "saldo >= 0" falhou (a transação é descartada e desfaz o INSERT).
        return await ajustar.ExecuteScalarAsync(ct) is decimal novo
            ? novo
            : throw new DomainException("Saldo insuficiente para a saída.");
    }
}
