using ProdutosApp.Core.Models;

namespace ProdutosApp.Core.Data;

/// <summary>
/// Contrato do acesso a dados de produtos.
/// A interface gráfica depende apenas deste contrato.
/// </summary>
public interface IProdutoRepository
{
    /// <summary>Insere o produto e devolve o Id gerado.</summary>
    int Inserir(Produto produto);

    /// <summary>Insere vários produtos em uma única transação (tudo ou nada).</summary>
    int InserirVarios(IEnumerable<Produto> produtos);

    /// <summary>Lista os produtos ativos.</summary>
    List<Produto> Listar();

    /// <summary>Lista os produtos que estão na lixeira (excluídos logicamente).</summary>
    List<Produto> ListarExcluidos();

    /// <summary>Busca um produto pelo Id. Retorna null se não existir.</summary>
    Produto? BuscarPorId(int id, bool incluirExcluidos = false);

    /// <summary>Atualiza um produto ativo. Retorna false se o Id não existir.</summary>
    bool Atualizar(Produto produto);

    /// <summary>Soft delete: move o produto para a lixeira.</summary>
    bool Excluir(int id);

    /// <summary>Tira o produto da lixeira.</summary>
    bool Restaurar(int id);

    /// <summary>Remove de vez (DELETE) um produto que já está na lixeira.</summary>
    bool ExcluirDefinitivamente(int id);

    /// <summary>Categorias já usadas pelos produtos ativos.</summary>
    List<string> ListarCategorias();
}
