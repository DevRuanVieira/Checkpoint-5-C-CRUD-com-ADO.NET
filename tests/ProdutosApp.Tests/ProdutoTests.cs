using ProdutosApp.Core.Models;

namespace ProdutosApp.Tests;

public class ProdutoTests
{
    [Fact]
    public void Validar_ProdutoCompleto_NaoRetornaErros()
    {
        var produto = new Produto { Nome = "Caneta", Preco = 2.5m, Estoque = 100, Categoria = "Escritório" };

        Assert.Empty(produto.Validar());
    }

    [Fact]
    public void Validar_ProdutoVazio_RetornaErrosDeNomeECategoria()
    {
        var erros = new Produto().Validar();

        Assert.Contains(erros, e => e.Contains("nome"));
        Assert.Contains(erros, e => e.Contains("categoria"));
    }

    [Fact]
    public void Validar_NomeMuitoLongo_RetornaErro()
    {
        var produto = new Produto { Nome = new string('x', 101), Preco = 1, Estoque = 1, Categoria = "C" };

        Assert.Contains(produto.Validar(), e => e.Contains("100"));
    }

    [Fact]
    public void Validar_PrecoEEstoqueNegativos_RetornaDoisErros()
    {
        var produto = new Produto { Nome = "X", Preco = -0.01m, Estoque = -1, Categoria = "C" };

        Assert.Equal(2, produto.Validar().Count);
    }

    [Fact]
    public void Validar_PrecoZero_EhPermitido()
    {
        var produto = new Produto { Nome = "Brinde", Preco = 0, Estoque = 1, Categoria = "Promoção" };

        Assert.Empty(produto.Validar());
    }
}
