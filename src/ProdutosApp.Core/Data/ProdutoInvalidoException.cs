namespace ProdutosApp.Core.Data;

/// <summary>
/// Lançada quando se tenta gravar um produto que não passa na validação.
/// Nesse caso o banco nem chega a ser acessado.
/// </summary>
public class ProdutoInvalidoException : Exception
{
    public IReadOnlyList<string> Erros { get; }

    public ProdutoInvalidoException(IReadOnlyList<string> erros)
        : base("Produto inválido: " + string.Join(" ", erros))
    {
        Erros = erros;
    }
}
