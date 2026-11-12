using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace Estoque.Api.Tests;

[Collection("api")]
public class FluxoEstoqueTests(ApiFixture api)
{
    [FactRequiresPostgres]
    public async Task Endpoints_protegidos_exigem_token()
    {
        foreach (var url in new[] { "/api/v1/produtos", "/api/v1/estoque/saldos", "/api/v1/auth/me" })
        {
            var resp = await api.Client.GetAsync(url);
            resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized, url);
        }
    }

    [FactRequiresPostgres]
    public async Task Health_e_raiz_sao_anonimos()
    {
        (await api.Client.GetAsync("/health/live")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await api.Client.GetAsync("/health/ready")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await api.Client.GetAsync("/")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [FactRequiresPostgres]
    public async Task Login_com_senha_errada_ou_email_inexistente_retorna_401_igual()
    {
        var errada = await api.Client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = ApiFixture.AdminEmail, senha = "senha-errada-123" });
        var inexistente = await api.Client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = "ninguem@teste.local", senha = "qualquer-coisa-123" });

        errada.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        inexistente.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var tituloErrada = (await errada.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("title").GetString();
        var tituloInexistente = (await inexistente.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("title").GetString();
        tituloErrada.Should().Be(tituloInexistente);
    }

    [FactRequiresPostgres]
    public async Task Operador_nao_cria_produto_mas_consegue_movimentar()
    {
        var admin = await api.LoginAsync(ApiFixture.AdminEmail, ApiFixture.AdminSenha);

        var criarOperador = await api.Client.SendAsync(api.Req(HttpMethod.Post, "/api/v1/usuarios", admin, new
        {
            nome = "Operador Um",
            email = $"op-{Guid.NewGuid():N}@teste.local",
            senha = "OperadorSenha2026",
            perfil = "Operador"
        }));
        criarOperador.StatusCode.Should().Be(HttpStatusCode.Created);
        var emailOp = (await criarOperador.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("email").GetString()!;
        var operador = await api.LoginAsync(emailOp, "OperadorSenha2026");

        var tentativa = await api.Client.SendAsync(api.Req(HttpMethod.Post, "/api/v1/produtos", operador,
            new { sku = "OP-" + Guid.NewGuid().ToString("N")[..8], nome = "Proibido" }));
        tentativa.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var semPermissao = await api.Client.SendAsync(api.Req(HttpMethod.Post, "/api/v1/usuarios", operador,
            new { nome = "X", email = "x@x.com", senha = "SenhaQualquer2026", perfil = "Administrador" }));
        semPermissao.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [FactRequiresPostgres]
    public async Task Fluxo_completo_produto_entrada_saida_e_idempotencia()
    {
        var token = await api.LoginAsync(ApiFixture.AdminEmail, ApiFixture.AdminSenha);
        var sku = "FLX-" + Guid.NewGuid().ToString("N")[..8];

        var criado = await api.Client.SendAsync(api.Req(HttpMethod.Post, "/api/v1/produtos", token,
            new { sku, nome = "Parafuso M8", unidade = "cx" }));
        criado.StatusCode.Should().Be(HttpStatusCode.Created);
        var produtoId = (await criado.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var duplicado = await api.Client.SendAsync(api.Req(HttpMethod.Post, "/api/v1/produtos", token,
            new { sku, nome = "Outro" }));
        duplicado.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var deposito = Guid.NewGuid();
        object Mov(string tipo, decimal qtd) => new { produtoId, depositoId = deposito, tipo, quantidade = qtd };

        var entrada = await api.Client.SendAsync(api.Req(HttpMethod.Post, "/api/v1/estoque/movimentacoes", token, Mov("Entrada", 10), "e-" + sku));
        entrada.StatusCode.Should().Be(HttpStatusCode.Created);

        var repetida = await api.Client.SendAsync(api.Req(HttpMethod.Post, "/api/v1/estoque/movimentacoes", token, Mov("Entrada", 10), "e-" + sku));
        repetida.StatusCode.Should().Be(HttpStatusCode.OK);

        var saida = await api.Client.SendAsync(api.Req(HttpMethod.Post, "/api/v1/estoque/movimentacoes", token, Mov("Saida", 4), "s-" + sku));
        saida.StatusCode.Should().Be(HttpStatusCode.Created);

        var insuficiente = await api.Client.SendAsync(api.Req(HttpMethod.Post, "/api/v1/estoque/movimentacoes", token, Mov("Saida", 100), "x-" + sku));
        insuficiente.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var saldos = await api.Client.SendAsync(api.Req(HttpMethod.Get, $"/api/v1/estoque/saldos?produtoId={produtoId}", token));
        var lista = await saldos.Content.ReadFromJsonAsync<JsonElement>();
        lista.GetArrayLength().Should().Be(1);
        lista[0].GetProperty("quantidade").GetDecimal().Should().Be(6m);
    }

    [FactRequiresPostgres]
    public async Task Movimentar_produto_inexistente_ou_inativo_e_rejeitado()
    {
        var token = await api.LoginAsync(ApiFixture.AdminEmail, ApiFixture.AdminSenha);

        var inexistente = await api.Client.SendAsync(api.Req(HttpMethod.Post, "/api/v1/estoque/movimentacoes", token,
            new { produtoId = Guid.NewGuid(), depositoId = Guid.NewGuid(), tipo = "Entrada", quantidade = 1 },
            "i-" + Guid.NewGuid().ToString("N")));
        inexistente.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var criado = await api.Client.SendAsync(api.Req(HttpMethod.Post, "/api/v1/produtos", token,
            new { sku = "INA-" + Guid.NewGuid().ToString("N")[..8], nome = "Descontinuado" }));
        var id = (await criado.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        (await api.Client.SendAsync(api.Req(HttpMethod.Post, $"/api/v1/produtos/{id}/desativar", token)))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var inativo = await api.Client.SendAsync(api.Req(HttpMethod.Post, "/api/v1/estoque/movimentacoes", token,
            new { produtoId = id, depositoId = Guid.NewGuid(), tipo = "Entrada", quantidade = 1 },
            "j-" + Guid.NewGuid().ToString("N")));
        inativo.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }
}
