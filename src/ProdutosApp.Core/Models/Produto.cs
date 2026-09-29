namespace ProdutosApp.Core.Models;

/// <summary>
/// Representa um registro da tabela Produtos.
/// </summary>
public class Produto
{
    public const int TamanhoMaximoNome = 100;
    public const int TamanhoMaximoCategoria = 50;

    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public decimal Preco { get; set; }
    public int Estoque { get; set; }
    public string Categoria { get; set; } = string.Empty;

    // Campos do soft delete (exclusão lógica)
    public bool Ativo { get; set; } = true;
    public DateTime DataCadastro { get; set; }
    public DateTime? DataExclusao { get; set; }

    /// <summary>
    /// Valida as regras de negócio do produto.
    /// Retorna a lista de erros encontrados (vazia = produto válido).
    /// </summary>
    public IReadOnlyList<string> Validar()
    {
        var erros = new List<string>();

        if (string.IsNullOrWhiteSpace(Nome))
            erros.Add("O nome é obrigatório.");
        else if (Nome.Trim().Length > TamanhoMaximoNome)
            erros.Add($"O nome deve ter no máximo {TamanhoMaximoNome} caracteres.");

        if (Preco < 0)
            erros.Add("O preço não pode ser negativo.");

        if (Estoque < 0)
            erros.Add("O estoque não pode ser negativo.");

        if (string.IsNullOrWhiteSpace(Categoria))
            erros.Add("A categoria é obrigatória.");
        else if (Categoria.Trim().Length > TamanhoMaximoCategoria)
            erros.Add($"A categoria deve ter no máximo {TamanhoMaximoCategoria} caracteres.");

        return erros;
    }

    public override string ToString() => $"#{Id} - {Nome} ({Categoria})";
}
