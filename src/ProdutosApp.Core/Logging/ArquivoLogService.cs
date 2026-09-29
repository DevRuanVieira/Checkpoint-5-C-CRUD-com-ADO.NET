using System.Text;

namespace ProdutosApp.Core.Logging;

/// <summary>
/// Grava cada operação em uma linha de um arquivo texto, por exemplo:
/// 2026-09-29 20:15:02.418 [INFO] INSERIR | #3 'Mouse sem fio' | Categoria: Periféricos | Preço: R$ 129,90 | Estoque: 32
/// </summary>
public sealed class ArquivoLogService : ILogService
{
    private static readonly object Trava = new();
    private static readonly Encoding Utf8SemBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    public string CaminhoArquivo { get; }

    public ArquivoLogService(string caminhoArquivo)
    {
        if (string.IsNullOrWhiteSpace(caminhoArquivo))
            throw new ArgumentException("O caminho do arquivo de log não foi informado.", nameof(caminhoArquivo));

        CaminhoArquivo = Path.GetFullPath(caminhoArquivo);

        var pasta = Path.GetDirectoryName(CaminhoArquivo);
        if (!string.IsNullOrEmpty(pasta))
            Directory.CreateDirectory(pasta);
    }

    public void Info(string mensagem) => Escrever("INFO", mensagem);

    public void Erro(string mensagem, Exception? excecao = null) =>
        Escrever("ERRO", excecao is null ? mensagem : $"{mensagem} | {excecao.GetType().Name}: {excecao.Message}");

    private void Escrever(string nivel, string mensagem)
    {
        var linha = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{nivel}] {mensagem}{Environment.NewLine}";

        try
        {
            lock (Trava)
            {
                File.AppendAllText(CaminhoArquivo, linha, Utf8SemBom);
            }
        }
        catch (IOException)
        {
            // Uma falha ao gravar o log nunca deve derrubar a aplicação.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
