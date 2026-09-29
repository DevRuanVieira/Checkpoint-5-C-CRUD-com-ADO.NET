namespace ProdutosApp.Core.Logging;

/// <summary>
/// Registro das operações realizadas pela aplicação.
/// </summary>
public interface ILogService
{
    void Info(string mensagem);
    void Erro(string mensagem, Exception? excecao = null);
}
