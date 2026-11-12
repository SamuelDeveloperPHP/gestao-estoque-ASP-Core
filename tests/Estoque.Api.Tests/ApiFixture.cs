using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using Xunit;

namespace Estoque.Api.Tests;

/// <summary>
/// Testes de integração contra um PostgreSQL real. Defina TEST_POSTGRES com uma connection string
/// que possa criar bancos (ex.: Host=localhost;Username=postgres;Password=...;Database=postgres).
/// Sem a variável, os testes são ignorados. Cada execução usa um banco novo e o apaga no final.
/// </summary>
public sealed class FactRequiresPostgres : FactAttribute
{
    public FactRequiresPostgres()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TEST_POSTGRES")))
            Skip = "Defina TEST_POSTGRES para rodar os testes de integração.";
    }
}

public sealed class ApiFixture : IAsyncLifetime
{
    public const string AdminEmail = "admin@teste.local";
    public const string AdminSenha = "SenhaAdmin2026ok";

    private readonly string? _adminConnection = Environment.GetEnvironmentVariable("TEST_POSTGRES");
    private readonly string _database = "ge_test_" + Guid.NewGuid().ToString("N");
    private WebApplicationFactory<Program>? _factory;

    public HttpClient Client { get; private set; } = default!;

    public async Task InitializeAsync()
    {
        if (string.IsNullOrWhiteSpace(_adminConnection)) return;

        await using (var conn = new NpgsqlConnection(_adminConnection))
        {
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand($"CREATE DATABASE \"{_database}\"", conn);
            await cmd.ExecuteNonQueryAsync();
        }

        var cs = new NpgsqlConnectionStringBuilder(_adminConnection) { Database = _database }.ConnectionString;

        // Variáveis de ambiente: lidas na construção do host, antes do Program registrar os módulos.
        Environment.SetEnvironmentVariable("ConnectionStrings__Postgres", cs);
        Environment.SetEnvironmentVariable("Jwt__Key", new string('t', 48));
        Environment.SetEnvironmentVariable("Identidade__AdminEmail", AdminEmail);
        Environment.SetEnvironmentVariable("Identidade__AdminSenha", AdminSenha);
        Environment.SetEnvironmentVariable("Database__MigrateOnStartup", "true");
        Environment.SetEnvironmentVariable("RateLimit__LoginPorMinuto", "1000");

        _factory = new WebApplicationFactory<Program>();
        Client = _factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        _factory?.Dispose();
        if (string.IsNullOrWhiteSpace(_adminConnection)) return;

        NpgsqlConnection.ClearAllPools();
        await using var conn = new NpgsqlConnection(_adminConnection);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_database}\" WITH (FORCE)", conn);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<string> LoginAsync(string email, string senha)
    {
        var resp = await Client.PostAsJsonAsync("/api/v1/auth/login", new { email, senha });
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("accessToken").GetString()!;
    }

    public HttpRequestMessage Req(HttpMethod metodo, string url, string? token = null, object? corpo = null, string? chave = null)
    {
        var msg = new HttpRequestMessage(metodo, url);
        if (token is not null) msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (chave is not null) msg.Headers.Add("Idempotency-Key", chave);
        if (corpo is not null) msg.Content = JsonContent.Create(corpo);
        return msg;
    }
}

[CollectionDefinition("api")]
public sealed class ApiCollection : ICollectionFixture<ApiFixture>;
