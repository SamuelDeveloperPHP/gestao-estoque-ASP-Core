using GestaoEstoque.Modules.Identidade.Domain;
using Microsoft.EntityFrameworkCore;

namespace GestaoEstoque.Modules.Identidade.Infrastructure;

public sealed class IdentidadeDbContext(DbContextOptions<IdentidadeDbContext> options) : DbContext(options)
{
    public const string Schema = "identidade";

    public DbSet<Usuario> Usuarios => Set<Usuario>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<Usuario>(b =>
        {
            b.ToTable("usuarios");
            b.HasKey(x => x.Id);
            b.Ignore(x => x.DomainEvents);
            b.Property(x => x.Nome).HasMaxLength(150).IsRequired();
            b.Property(x => x.Email).HasMaxLength(254).IsRequired();
            b.Property(x => x.SenhaHash).HasMaxLength(500).IsRequired();
            b.Property(x => x.Perfil).HasConversion<string>().HasMaxLength(20);
            b.HasIndex(x => x.Email).IsUnique();
        });
    }
}
