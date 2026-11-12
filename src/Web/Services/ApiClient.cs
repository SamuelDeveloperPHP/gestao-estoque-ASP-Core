using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace GestaoEstoque.Web.Services;

public sealed record ProdutoDto(Guid Id, string Sku, string Nome, string Unidade, bool Ativo);
public sealed record DepositoDto(Guid Id, string Codigo, string Nome, bool Ativo);
public sealed record SaldoDto(Guid Id, Guid ProdutoId, Guid DepositoId, decimal Quantidade);
public sealed record MovimentacaoDto(Guid MovimentacaoId, decimal SaldoApos);
public sealed record LoginDto(string AccessToken, string TokenType, DateTime ExpiraEm, string Perfil);
public sealed record PerfilDto(string? Id, string? Nome, string? Email, string? Perfil);
public sealed record UsuarioDto(Guid Id, string Nome, string Email, string Perfil);

public record ApiResult(int Status, string? Erro)
{
    public bool Sucesso => Status is >= 200 and < 300;
}

public sealed record ApiResult<T>(int Status, string? Erro, T? Dados) : ApiResult(Status, Erro);

/// <summary>O token expirou ou foi revogado: a sessão web deve ser encerrada.</summary>
public sealed class SessaoExpiradaException : Exception;

/// <summary>Cliente tipado da API REST. Traduz status HTTP e ProblemDetails em resultados simples.</summary>
public sealed class ApiClient(HttpClient http, ILogger<ApiClient> logger)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public Task<ApiResult<LoginDto>> LoginAsync(string email, string senha, string? ipCliente, CancellationToken ct)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "api/v1/auth/login") { Content = JsonContent.Create(new { email, senha }) };
        // O limite de tentativas da API é por IP: repassa o IP real do navegador (a API só confia se configurada para isso).
        if (!string.IsNullOrEmpty(ipCliente)) req.Headers.TryAddWithoutValidation("X-Forwarded-For", ipCliente);
        return EnviarAsync<LoginDto>(req, ct);
    }

    public Task<ApiResult<PerfilDto>> PerfilAsync(string accessToken, CancellationToken ct)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, "api/v1/auth/me");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return EnviarAsync<PerfilDto>(req, ct);
    }

    public Task<ApiResult<List<ProdutoDto>>> ListarProdutosAsync(string? busca, int skip, int take, CancellationToken ct)
    {
        var url = $"api/v1/produtos?skip={skip}&take={take}" +
                  (string.IsNullOrWhiteSpace(busca) ? "" : $"&q={Uri.EscapeDataString(busca)}");
        return EnviarAsync<List<ProdutoDto>>(new HttpRequestMessage(HttpMethod.Get, url), ct);
    }

    public Task<ApiResult<ProdutoDto>> CriarProdutoAsync(string sku, string nome, string? unidade, CancellationToken ct) =>
        EnviarAsync<ProdutoDto>(new HttpRequestMessage(HttpMethod.Post, "api/v1/produtos")
        {
            Content = JsonContent.Create(new { sku, nome, unidade })
        }, ct);

    public Task<ApiResult<object>> DesativarProdutoAsync(Guid id, CancellationToken ct) =>
        EnviarAsync<object>(new HttpRequestMessage(HttpMethod.Post, $"api/v1/produtos/{id}/desativar"), ct);

    public Task<ApiResult<List<DepositoDto>>> ListarDepositosAsync(bool apenasAtivos, CancellationToken ct) =>
        EnviarAsync<List<DepositoDto>>(new HttpRequestMessage(HttpMethod.Get, $"api/v1/depositos?apenasAtivos={apenasAtivos.ToString().ToLowerInvariant()}"), ct);

    public Task<ApiResult<DepositoDto>> CriarDepositoAsync(string codigo, string nome, CancellationToken ct) =>
        EnviarAsync<DepositoDto>(new HttpRequestMessage(HttpMethod.Post, "api/v1/depositos")
        {
            Content = JsonContent.Create(new { codigo, nome })
        }, ct);

    public Task<ApiResult<object>> DesativarDepositoAsync(Guid id, CancellationToken ct) =>
        EnviarAsync<object>(new HttpRequestMessage(HttpMethod.Post, $"api/v1/depositos/{id}/desativar"), ct);

    public Task<ApiResult<List<SaldoDto>>> ListarSaldosAsync(Guid? produtoId, int skip, int take, CancellationToken ct)
    {
        var url = $"api/v1/estoque/saldos?skip={skip}&take={take}" + (produtoId is null ? "" : $"&produtoId={produtoId}");
        return EnviarAsync<List<SaldoDto>>(new HttpRequestMessage(HttpMethod.Get, url), ct);
    }

    public Task<ApiResult<MovimentacaoDto>> MovimentarAsync(
        Guid produtoId, Guid depositoId, string tipo, decimal quantidade, string chaveIdempotencia, CancellationToken ct)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "api/v1/estoque/movimentacoes")
        {
            Content = JsonContent.Create(new { produtoId, depositoId, tipo, quantidade })
        };
        // A mesma chave em reenvios (duplo clique, F5) garante que a baixa seja feita uma única vez.
        req.Headers.TryAddWithoutValidation("Idempotency-Key", chaveIdempotencia);
        return EnviarAsync<MovimentacaoDto>(req, ct);
    }

    public Task<ApiResult<UsuarioDto>> CriarUsuarioAsync(string nome, string email, string senha, string perfil, CancellationToken ct) =>
        EnviarAsync<UsuarioDto>(new HttpRequestMessage(HttpMethod.Post, "api/v1/usuarios")
        {
            Content = JsonContent.Create(new { nome, email, senha, perfil })
        }, ct);

    private async Task<ApiResult<T>> EnviarAsync<T>(HttpRequestMessage req, CancellationToken ct)
    {
        HttpResponseMessage resp;
        try
        {
            resp = await http.SendAsync(req, ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Falha ao chamar a API: {Metodo} {Url}", req.Method, req.RequestUri);
            return new ApiResult<T>(503, "A API está indisponível. Tente novamente em instantes.", default);
        }

        using (resp)
        {
            var status = (int)resp.StatusCode;

            // 401 em chamada autenticada = sessão expirada. No login (sem token) é credencial inválida.
            if (resp.StatusCode == HttpStatusCode.Unauthorized && req.Headers.Authorization is not null)
                throw new SessaoExpiradaException();

            if (resp.IsSuccessStatusCode)
            {
                if (resp.StatusCode == HttpStatusCode.NoContent || resp.Content.Headers.ContentLength == 0)
                    return new ApiResult<T>(status, null, default);

                var dados = await resp.Content.ReadFromJsonAsync<T>(Json, ct);
                return new ApiResult<T>(status, null, dados);
            }

            return new ApiResult<T>(status, await ExtrairErroAsync(resp, ct), default);
        }
    }

    private static async Task<string> ExtrairErroAsync(HttpResponseMessage resp, CancellationToken ct)
    {
        try
        {
            var corpo = await resp.Content.ReadFromJsonAsync<JsonElement>(Json, ct);
            if (corpo.ValueKind == JsonValueKind.Object && corpo.TryGetProperty("title", out var titulo)
                && titulo.GetString() is { Length: > 0 } texto)
                return texto;
        }
        catch (JsonException) { /* corpo vazio ou não-JSON: usa a mensagem padrão abaixo */ }

        return resp.StatusCode switch
        {
            HttpStatusCode.Forbidden => "Você não tem permissão para esta ação.",
            HttpStatusCode.TooManyRequests => "Muitas tentativas. Aguarde um minuto e tente novamente.",
            HttpStatusCode.NotFound => "Registro não encontrado.",
            _ => "Não foi possível concluir a operação."
        };
    }
}
