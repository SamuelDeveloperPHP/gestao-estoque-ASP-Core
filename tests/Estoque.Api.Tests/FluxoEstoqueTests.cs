using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace Estoque.Api.Tests;

[Collection("api")]
public class FluxoEstoqueTests(ApiFixture api)
{
    private const string Mov = "/api/v1/estoque/movimentacoes";

    private async Task<Guid> CriarDepositoAsync(string token)
    {
        var codigo = "D" + Guid.NewGuid().ToString("N")[..10];
        var resp = await api.Client.SendAsync(api.Req(HttpMethod.Post, "/api/v1/depositos", token, new { codigo, nome = "Depósito " + codigo }));
        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private async Task<Guid> CriarProdutoAsync(string token, string prefixo = "P")
    {
        var resp = await api.Client.SendAsync(api.Req(HttpMethod.Post, "/api/v1/produtos", token,
            new { sku = prefixo + "-" + Guid.NewGuid().ToString("N")[..8], nome = "Produto de teste" }));
        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private Task<HttpResponseMessage> MovimentarAsync(string token, Guid produto, Guid deposito, string tipo, decimal qtd, string chave) =>
        api.Client.SendAsync(api.Req(HttpMethod.Post, Mov, token,
            new { produtoId = produto, depositoId = deposito, tipo, quantidade = qtd }, chave));

    [FactRequiresPostgres]
    public async Task Endpoints_protegidos_exigem_token()
    {
        foreach (var url in new[] { "/api/v1/produtos", "/api/v1/depositos", "/api/v1/estoque/saldos", "/api/v1/auth/me" })
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
    public async Task Operador_nao_cria_produto_nem_deposito_mas_consegue_movimentar()
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

        (await api.Client.SendAsync(api.Req(HttpMethod.Post, "/api/v1/produtos", operador,
            new { sku = "OP-" + Guid.NewGuid().ToString("N")[..8], nome = "Proibido" })))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        (await api.Client.SendAsync(api.Req(HttpMethod.Post, "/api/v1/depositos", operador,
            new { codigo = "PROIBIDO", nome = "Proibido" })))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        (await api.Client.SendAsync(api.Req(HttpMethod.Post, "/api/v1/usuarios", operador,
            new { nome = "X", email = "x@x.com", senha = "SenhaQualquer2026", perfil = "Administrador" })))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Mas lê depósitos e movimenta estoque normalmente.
        (await api.Client.SendAsync(api.Req(HttpMethod.Get, "/api/v1/depositos", operador))).StatusCode.Should().Be(HttpStatusCode.OK);
        var produto = await CriarProdutoAsync(admin, "OPR");
        var deposito = await CriarDepositoAsync(admin);
        (await MovimentarAsync(operador, produto, deposito, "Entrada", 3, "op-" + Guid.NewGuid().ToString("N")))
            .StatusCode.Should().Be(HttpStatusCode.Created);
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

        (await api.Client.SendAsync(api.Req(HttpMethod.Post, "/api/v1/produtos", token, new { sku, nome = "Outro" })))
            .StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var deposito = await CriarDepositoAsync(token);

        (await MovimentarAsync(token, produtoId, deposito, "Entrada", 10, "e-" + sku)).StatusCode.Should().Be(HttpStatusCode.Created);
        (await MovimentarAsync(token, produtoId, deposito, "Entrada", 10, "e-" + sku)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await MovimentarAsync(token, produtoId, deposito, "Saida", 4, "s-" + sku)).StatusCode.Should().Be(HttpStatusCode.Created);
        (await MovimentarAsync(token, produtoId, deposito, "Saida", 100, "x-" + sku)).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var saldos = await api.Client.SendAsync(api.Req(HttpMethod.Get, $"/api/v1/estoque/saldos?produtoId={produtoId}", token));
        var lista = await saldos.Content.ReadFromJsonAsync<JsonElement>();
        lista.GetArrayLength().Should().Be(1);
        lista[0].GetProperty("quantidade").GetDecimal().Should().Be(6m);
    }

    [FactRequiresPostgres]
    public async Task Movimentar_produto_inexistente_ou_inativo_e_rejeitado()
    {
        var token = await api.LoginAsync(ApiFixture.AdminEmail, ApiFixture.AdminSenha);
        var deposito = await CriarDepositoAsync(token);

        (await MovimentarAsync(token, Guid.NewGuid(), deposito, "Entrada", 1, "i-" + Guid.NewGuid().ToString("N")))
            .StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var id = await CriarProdutoAsync(token, "INA");
        (await api.Client.SendAsync(api.Req(HttpMethod.Post, $"/api/v1/produtos/{id}/desativar", token)))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await MovimentarAsync(token, id, deposito, "Entrada", 1, "j-" + Guid.NewGuid().ToString("N")))
            .StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [FactRequiresPostgres]
    public async Task Deposito_padrao_existe_e_codigo_duplicado_e_rejeitado()
    {
        var token = await api.LoginAsync(ApiFixture.AdminEmail, ApiFixture.AdminSenha);

        var lista = await (await api.Client.SendAsync(api.Req(HttpMethod.Get, "/api/v1/depositos", token)))
            .Content.ReadFromJsonAsync<JsonElement>();
        lista.EnumerateArray().Select(d => d.GetProperty("codigo").GetString()).Should().Contain("PRINC");

        (await api.Client.SendAsync(api.Req(HttpMethod.Post, "/api/v1/depositos", token, new { codigo = "princ", nome = "Outro" })))
            .StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [FactRequiresPostgres]
    public async Task Movimentar_em_deposito_inexistente_ou_inativo_e_rejeitado()
    {
        var token = await api.LoginAsync(ApiFixture.AdminEmail, ApiFixture.AdminSenha);
        var produto = await CriarProdutoAsync(token, "DEP");

        (await MovimentarAsync(token, produto, Guid.NewGuid(), "Entrada", 1, "d1-" + Guid.NewGuid().ToString("N")))
            .StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var deposito = await CriarDepositoAsync(token);
        (await api.Client.SendAsync(api.Req(HttpMethod.Post, $"/api/v1/depositos/{deposito}/desativar", token)))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await MovimentarAsync(token, produto, deposito, "Entrada", 1, "d2-" + Guid.NewGuid().ToString("N")))
            .StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var ativos = await (await api.Client.SendAsync(api.Req(HttpMethod.Get, "/api/v1/depositos?apenasAtivos=true", token)))
            .Content.ReadFromJsonAsync<JsonElement>();
        ativos.EnumerateArray().Select(d => d.GetProperty("id").GetGuid()).Should().NotContain(deposito);
    }

    [FactRequiresPostgres]
    public async Task Nao_desativa_deposito_com_saldo()
    {
        var token = await api.LoginAsync(ApiFixture.AdminEmail, ApiFixture.AdminSenha);
        var produto = await CriarProdutoAsync(token, "SLD");
        var deposito = await CriarDepositoAsync(token);

        (await MovimentarAsync(token, produto, deposito, "Entrada", 5, "s1-" + Guid.NewGuid().ToString("N")))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        (await api.Client.SendAsync(api.Req(HttpMethod.Post, $"/api/v1/depositos/{deposito}/desativar", token)))
            .StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        // Zerando o saldo, a desativação passa.
        (await MovimentarAsync(token, produto, deposito, "Saida", 5, "s2-" + Guid.NewGuid().ToString("N")))
            .StatusCode.Should().Be(HttpStatusCode.Created);
        (await api.Client.SendAsync(api.Req(HttpMethod.Post, $"/api/v1/depositos/{deposito}/desativar", token)))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
