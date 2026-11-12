using GestaoEstoque.BuildingBlocks;
using GestaoEstoque.Modules.Identidade.Domain;
using GestaoEstoque.Modules.Identidade.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace GestaoEstoque.Modules.Identidade.Application;

public sealed record LoginResultado(string AccessToken, DateTime ExpiraEm, Perfil Perfil);

public sealed class AutenticacaoService(
    IdentidadeDbContext db,
    IPasswordHasher<Usuario> hasher,
    TokenService tokens)
{
    // Usado para gastar o mesmo custo de hash quando o e-mail não existe (evita enumeração por tempo).
    private static readonly Usuario Fantasma = new("fantasma", "fantasma@local", Perfil.Operador, "-");

    public async Task<LoginResultado?> LoginAsync(string? email, string? senha, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(senha) || senha.Length > 128)
            return null;

        var chave = email.Trim().ToLowerInvariant();
        var usuario = await db.Usuarios.AsNoTracking().FirstOrDefaultAsync(u => u.Email == chave && u.Ativo, ct);

        if (usuario is null)
        {
            hasher.HashPassword(Fantasma, senha);
            return null;
        }

        var verificacao = hasher.VerifyHashedPassword(usuario, usuario.SenhaHash, senha);
        if (verificacao == PasswordVerificationResult.Failed)
            return null;

        var (token, expira) = tokens.Gerar(usuario);
        return new LoginResultado(token, expira, usuario.Perfil);
    }
}

public sealed class CadastroUsuarioService(IdentidadeDbContext db, IPasswordHasher<Usuario> hasher)
{
    public async Task<Usuario> CriarAsync(string nome, string email, string senha, Perfil perfil, CancellationToken ct)
    {
        PoliticaSenha.Validar(senha);

        var usuario = new Usuario(nome, email, perfil, hasher.HashPassword(null!, senha));

        if (await db.Usuarios.AnyAsync(u => u.Email == usuario.Email, ct))
            throw new DomainException("E-mail já cadastrado.");

        db.Usuarios.Add(usuario);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new DomainException("E-mail já cadastrado.");
        }

        return usuario;
    }
}
