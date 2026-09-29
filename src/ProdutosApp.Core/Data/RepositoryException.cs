namespace ProdutosApp.Core.Data;

/// <summary>
/// Erro de banco de dados já "traduzido" para uma mensagem amigável.
/// A exceção original (SqliteException) fica em InnerException.
/// A interface só precisa conhecer esta classe, e não o provedor do banco.
/// </summary>
public class RepositoryException : Exception
{
    public RepositoryException(string mensagem, Exception? excecaoOriginal = null)
        : base(mensagem, excecaoOriginal)
    {
    }
}
