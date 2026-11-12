using GestaoEstoque.Modules.Estoque.Domain;
using Microsoft.EntityFrameworkCore;

namespace GestaoEstoque.Modules.Estoque.Infrastructure;

public sealed class EstoqueDbContext(DbContextOptions<EstoqueDbContext> options) : DbContext(options)
{
    public const string Schema = "estoque";

    public DbSet<SaldoEstoque> Saldos => Set<SaldoEstoque>();
    public DbSet<MovimentacaoEstoque> Movimentacoes => Set<MovimentacaoEstoque>();
    public DbSet<Deposito> Depositos => Set<Deposito>();
    public DbSet<MensagemOutbox> Outbox => Set<MensagemOutbox>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<SaldoEstoque>(b =>
        {
            b.ToTable("saldos");
            b.HasKey(x => x.Id);
            b.Ignore(x => x.DomainEvents);
            b.Property(x => x.Quantidade).HasPrecision(18, 4);
            b.HasIndex(x => new { x.ProdutoId, x.DepositoId }).IsUnique();
            b.ToTable(t => t.HasCheckConstraint("ck_saldos_quantidade_nao_negativa", "\"Quantidade\" >= 0"));
            // Concorrência otimista via coluna de sistema xmin do PostgreSQL.
            b.Property<uint>("xmin").IsRowVersion();
        });

        // Sem FK de saldos/movimentações para depósitos: a referência é por Id e validada no caso de uso.
        modelBuilder.Entity<Deposito>(b =>
        {
            b.ToTable("depositos");
            b.HasKey(x => x.Id);
            b.Ignore(x => x.DomainEvents);
            b.Property(x => x.Codigo).HasMaxLength(20).IsRequired();
            b.Property(x => x.Nome).HasMaxLength(100).IsRequired();
            b.HasIndex(x => x.Codigo).IsUnique();
        });

        modelBuilder.Entity<MensagemOutbox>(b =>
        {
            b.ToTable("outbox");
            b.HasKey(x => x.Id);
            b.Property(x => x.RotaChave).HasMaxLength(200).IsRequired();
            b.Property(x => x.Payload).HasColumnType("jsonb").IsRequired();
            // Índice parcial: só as pendentes, para o publicador varrer rápido.
            b.HasIndex(x => x.CriadoEm).HasFilter("\"ProcessadoEm\" IS NULL");
        });

        modelBuilder.Entity<MovimentacaoEstoque>(b =>
        {
            b.ToTable("movimentacoes");
            b.HasKey(x => x.Id);
            b.Ignore(x => x.DomainEvents);
            b.Property(x => x.Tipo).HasConversion<string>().HasMaxLength(20);
            b.Property(x => x.Quantidade).HasPrecision(18, 4);
            b.Property(x => x.SaldoApos).HasPrecision(18, 4);
            b.Property(x => x.ChaveIdempotencia).HasMaxLength(100).IsRequired();
            // A chave única garante idempotência mesmo com requisições concorrentes.
            b.HasIndex(x => x.ChaveIdempotencia).IsUnique();
            b.HasIndex(x => new { x.ProdutoId, x.DepositoId, x.CriadoEm });
        });
    }
}
