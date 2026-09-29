using Microsoft.Data.Sqlite;
using ProdutosApp.Core.Data;
using ProdutosApp.Core.Logging;

namespace ProdutosApp.Tests;

/// <summary>
/// Cria um banco SQLite temporário (arquivo novo por teste) com a tabela Produtos
/// e o apaga no final. Assim cada teste começa do zero.
/// </summary>
public sealed class BancoDeTeste : IDisposable
{
    public string CaminhoBanco { get; }
    public string CaminhoLog { get; }
    public string ConnectionString { get; }
    public ArquivoLogService Log { get; }
    public ProdutoRepository Repositorio { get; }

    public BancoDeTeste(bool criarTabela = true)
    {
        var id = Guid.NewGuid().ToString("N");
        CaminhoBanco = Path.Combine(Path.GetTempPath(), $"produtos_teste_{id}.db");
        CaminhoLog = Path.Combine(Path.GetTempPath(), $"produtos_teste_{id}.log");
        ConnectionString = $"Data Source={CaminhoBanco}";

        Log = new ArquivoLogService(CaminhoLog);

        if (criarTabela)
        {
            var script = Path.Combine(AppContext.BaseDirectory, "database", "criar_tabela.sql");
            InicializadorBanco.CriarEstrutura(ConnectionString, script, Log);
        }

        Repositorio = new ProdutoRepository(ConnectionString, Log);
    }

    /// <summary>Executa um SQL direto no banco de teste (usado para preparar cenários).</summary>
    public void ExecutarSql(string sql)
    {
        using var conexao = new SqliteConnection(ConnectionString);
        conexao.Open();
        using var comando = conexao.CreateCommand();
        comando.CommandText = sql;
        comando.ExecuteNonQuery();
    }

    public void Dispose()
    {
        // Fecha as conexões que o pool mantém abertas para conseguir apagar o arquivo.
        SqliteConnection.ClearAllPools();

        foreach (var arquivo in new[] { CaminhoBanco, CaminhoLog })
        {
            try
            {
                if (File.Exists(arquivo))
                    File.Delete(arquivo);
            }
            catch (IOException)
            {
                // Arquivo temporário: se não der para apagar agora, o sistema limpa depois.
            }
        }
    }
}
