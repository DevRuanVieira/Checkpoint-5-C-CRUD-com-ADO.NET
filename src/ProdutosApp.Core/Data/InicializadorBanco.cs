using Microsoft.Data.Sqlite;
using ProdutosApp.Core.Logging;

namespace ProdutosApp.Core.Data;

/// <summary>
/// Executa o script database/criar_tabela.sql para garantir que a tabela exista.
/// O script usa "CREATE TABLE IF NOT EXISTS", então rodar várias vezes não apaga dados.
/// </summary>
public static class InicializadorBanco
{
    public static void CriarEstrutura(string connectionString, string caminhoScript, ILogService log)
    {
        if (!File.Exists(caminhoScript))
            throw new FileNotFoundException("Script de criação do banco não encontrado.", caminhoScript);

        var script = File.ReadAllText(caminhoScript);

        try
        {
            using var conexao = new SqliteConnection(connectionString);
            conexao.Open();

            using var comando = conexao.CreateCommand();
            comando.CommandText = script;   // script fixo do projeto, sem entrada do usuário
            comando.ExecuteNonQuery();

            log.Info($"BANCO | Estrutura verificada em '{conexao.DataSource}'");
        }
        catch (SqliteException ex)
        {
            log.Erro($"BANCO | Falha ao executar o script de criação (código SQLite {ex.SqliteErrorCode})", ex);
            throw new RepositoryException(TradutorErrosSqlite.Traduzir(ex), ex);
        }
    }
}
