using Microsoft.Data.Sqlite;

namespace ProdutosApp.Core.Data;

/// <summary>
/// Converte os códigos de erro do SQLite em mensagens que o usuário entende.
/// Referência dos códigos: https://www.sqlite.org/rescode.html
/// </summary>
public static class TradutorErrosSqlite
{
    private const int SQLITE_ERROR = 1;
    private const int SQLITE_BUSY = 5;
    private const int SQLITE_LOCKED = 6;
    private const int SQLITE_READONLY = 8;
    private const int SQLITE_CANTOPEN = 14;
    private const int SQLITE_CONSTRAINT = 19;
    private const int SQLITE_NOTADB = 26;

    public static string Traduzir(SqliteException ex)
    {
        switch (ex.SqliteErrorCode)
        {
            case SQLITE_CONSTRAINT:
                return "Os dados violam uma regra do banco (campo obrigatório vazio, " +
                       "preço ou estoque negativo, ou texto longo demais).";

            case SQLITE_BUSY:
            case SQLITE_LOCKED:
                return "O banco de dados está em uso por outro programa. " +
                       "Feche-o (ex.: DB Browser) e tente novamente.";

            case SQLITE_READONLY:
                return "O arquivo do banco de dados está somente leitura. " +
                       "Verifique as permissões da pasta.";

            case SQLITE_CANTOPEN:
                return "Não foi possível abrir o arquivo do banco de dados. " +
                       "Confira o caminho em \"ConnectionStrings:ProdutosDb\" no appsettings.json.";

            case SQLITE_NOTADB:
                return "O arquivo configurado não é um banco SQLite válido.";

            case SQLITE_ERROR when ex.Message.Contains("no such table", StringComparison.OrdinalIgnoreCase):
                return "A tabela Produtos não existe. Execute o script database/criar_tabela.sql.";

            default:
                return $"Erro no banco de dados (código {ex.SqliteErrorCode}): {ex.Message}";
        }
    }
}
