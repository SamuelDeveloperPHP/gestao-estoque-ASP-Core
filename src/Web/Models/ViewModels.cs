using System.ComponentModel.DataAnnotations;
using GestaoEstoque.Web.Services;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace GestaoEstoque.Web.Models;

public sealed class LoginViewModel
{
    [Required(ErrorMessage = "Informe o e-mail."), EmailAddress(ErrorMessage = "E-mail inválido.")]
    [Display(Name = "E-mail")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Informe a senha."), DataType(DataType.Password)]
    [Display(Name = "Senha")]
    public string Senha { get; set; } = "";

    public string? ReturnUrl { get; set; }
    public bool SessaoExpirada { get; set; }
}

public sealed class HomeViewModel
{
    public string Nome { get; set; } = "";
    public string Perfil { get; set; } = "";
    public string? TotalProdutos { get; set; }
    public string? TotalSaldos { get; set; }
    public string? Erro { get; set; }
}

public sealed class ProdutosIndexViewModel
{
    public IReadOnlyList<ProdutoDto> Itens { get; set; } = [];
    public string? Busca { get; set; }
    public int Pagina { get; set; } = 1;
    public bool TemProxima { get; set; }
    public string? Erro { get; set; }
}

public sealed class NovoProdutoViewModel
{
    [Required(ErrorMessage = "Informe o SKU."), StringLength(50)]
    public string Sku { get; set; } = "";

    [Required(ErrorMessage = "Informe o nome."), StringLength(200)]
    public string Nome { get; set; } = "";

    [StringLength(10)]
    [Display(Name = "Unidade")]
    public string? Unidade { get; set; } = "UN";
}

public sealed class SaldoLinha
{
    public string Produto { get; set; } = "";
    public string Sku { get; set; } = "";
    public string Deposito { get; set; } = "";
    public decimal Quantidade { get; set; }
}

public sealed class EstoqueIndexViewModel
{
    public IReadOnlyList<SaldoLinha> Linhas { get; set; } = [];
    public IReadOnlyList<SelectListItem> Produtos { get; set; } = [];
    public Guid? ProdutoId { get; set; }
    public string? Erro { get; set; }
}

public sealed class MovimentarViewModel
{
    [Required(ErrorMessage = "Selecione o produto.")]
    public Guid? ProdutoId { get; set; }

    [Required(ErrorMessage = "Selecione o depósito.")]
    public Guid? DepositoId { get; set; }

    [Required(ErrorMessage = "Selecione o tipo.")]
    public string Tipo { get; set; } = "Entrada";

    [Required(ErrorMessage = "Informe a quantidade."), Range(0.0001, 999999999, ErrorMessage = "A quantidade deve ser maior que zero.")]
    public decimal? Quantidade { get; set; }

    /// <summary>Gerada ao abrir o formulário; reenvios (duplo clique, F5) não duplicam a movimentação.</summary>
    public string ChaveIdempotencia { get; set; } = Guid.NewGuid().ToString("N");

    public IReadOnlyList<SelectListItem> Produtos { get; set; } = [];
    public IReadOnlyList<SelectListItem> Depositos { get; set; } = [];
}

public sealed class NovoUsuarioViewModel
{
    [Required(ErrorMessage = "Informe o nome."), StringLength(150)]
    public string Nome { get; set; } = "";

    [Required(ErrorMessage = "Informe o e-mail."), EmailAddress(ErrorMessage = "E-mail inválido.")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Informe a senha."), DataType(DataType.Password), StringLength(128)]
    public string Senha { get; set; } = "";

    [Required]
    public string Perfil { get; set; } = "Operador";
}
