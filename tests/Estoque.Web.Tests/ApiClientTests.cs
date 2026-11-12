using System.Net;
using System.Net.Http.Headers;
using System.Text;
using FluentAssertions;
using GestaoEstoque.Web.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Estoque.Web.Tests;

public class ApiClientTests
{
    private sealed class Stub(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public HttpRequestMessage? Ultima { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Ultima = request;
            return Task.FromResult(responder(request));
        }
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string corpo) =>
        new(status) { Content = new StringContent(corpo, Encoding.UTF8, "application/json") };

    private static (ApiClient Api, Stub Stub) Criar(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        var stub = new Stub(responder);
        var http = new HttpClient(stub) { BaseAddress = new Uri("http://api.test/") };
        return (new ApiClient(http, NullLogger<ApiClient>.Instance), stub);
    }

    [Fact]
    public async Task Login_valido_devolve_token()
    {
        var (api, _) = Criar(_ => Json(HttpStatusCode.OK,
            """{"accessToken":"abc","tokenType":"Bearer","expiraEm":"2030-01-01T00:00:00Z","perfil":"Gestor"}"""));

        var r = await api.LoginAsync("a@b.com", "senha", null, default);

        r.Sucesso.Should().BeTrue();
        r.Dados!.AccessToken.Should().Be("abc");
        r.Dados.Perfil.Should().Be("Gestor");
    }

    [Fact]
    public async Task Login_invalido_devolve_titulo_do_problema_sem_expirar_sessao()
    {
        var (api, _) = Criar(_ => Json(HttpStatusCode.Unauthorized, """{"title":"Credenciais inválidas.","status":401}"""));

        var r = await api.LoginAsync("a@b.com", "errada", null, default);

        r.Sucesso.Should().BeFalse();
        r.Status.Should().Be(401);
        r.Erro.Should().Be("Credenciais inválidas.");
    }

    [Fact]
    public async Task Login_repassa_o_ip_do_cliente()
    {
        var (api, stub) = Criar(_ => Json(HttpStatusCode.Unauthorized, "{}"));

        await api.LoginAsync("a@b.com", "x", "203.0.113.7", default);

        stub.Ultima!.Headers.GetValues("X-Forwarded-For").Should().ContainSingle("203.0.113.7");
    }

    [Fact]
    public async Task Chamada_autenticada_com_401_sinaliza_sessao_expirada()
    {
        var (api, _) = Criar(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));

        var acao = () => api.PerfilAsync("token-vencido", default);

        await acao.Should().ThrowAsync<SessaoExpiradaException>();
    }

    [Fact]
    public async Task Movimentar_envia_a_chave_de_idempotencia_e_o_corpo()
    {
        var (api, stub) = Criar(_ => Json(HttpStatusCode.Created, """{"movimentacaoId":"00000000-0000-0000-0000-000000000001","saldoApos":7}"""));

        var r = await api.MovimentarAsync(Guid.NewGuid(), Guid.NewGuid(), "Saida", 3m, "chave-123", default);

        r.Sucesso.Should().BeTrue();
        r.Dados!.SaldoApos.Should().Be(7m);
        stub.Ultima!.Headers.GetValues("Idempotency-Key").Should().ContainSingle("chave-123");
        (await stub.Ultima.Content!.ReadAsStringAsync()).Should().Contain("\"tipo\":\"Saida\"");
    }

    [Theory]
    [InlineData(403, "Você não tem permissão")]
    [InlineData(429, "Muitas tentativas")]
    public async Task Status_sem_corpo_tem_mensagem_padrao(int status, string trecho)
    {
        var (api, _) = Criar(_ => new HttpResponseMessage((HttpStatusCode)status));

        var r = await api.CriarProdutoAsync("SKU", "Nome", null, default);

        r.Sucesso.Should().BeFalse();
        r.Erro.Should().Contain(trecho);
    }

    [Fact]
    public async Task Falha_de_rede_vira_503_amigavel()
    {
        var (api, _) = Criar(_ => throw new HttpRequestException("conexão recusada"));

        var r = await api.ListarProdutosAsync(null, 0, 20, default);

        r.Status.Should().Be(503);
        r.Erro.Should().Contain("indisponível");
    }

    [Fact]
    public async Task Depositos_lista_apenas_ativos_e_cria_com_codigo_e_nome()
    {
        var (api, stub) = Criar(r => r.Method == HttpMethod.Get
            ? Json(HttpStatusCode.OK, """[{"id":"00000000-0000-0000-0000-000000000001","codigo":"PRINC","nome":"Principal","ativo":true}]""")
            : Json(HttpStatusCode.Created, """{"id":"00000000-0000-0000-0000-000000000002","codigo":"CD1","nome":"CD 1","ativo":true}"""));

        var lista = await api.ListarDepositosAsync(apenasAtivos: true, default);
        stub.Ultima!.RequestUri!.Query.Should().Contain("apenasAtivos=true");
        lista.Dados.Should().ContainSingle().Which.Codigo.Should().Be("PRINC");

        var criado = await api.CriarDepositoAsync("CD1", "CD 1", default);
        criado.Sucesso.Should().BeTrue();
        (await stub.Ultima!.Content!.ReadAsStringAsync()).Should().Contain("\"codigo\":\"CD1\"");
    }

    [Fact]
    public async Task Busca_e_escapada_na_url()
    {
        var (api, stub) = Criar(_ => Json(HttpStatusCode.OK, "[]"));

        await api.ListarProdutosAsync("a&b c", 0, 20, default);

        stub.Ultima!.RequestUri!.Query.Should().Contain("q=a%26b%20c");
    }
}
