using System.Globalization;
using Microsoft.Data.Sqlite;
using ProdutosApp.Core.Logging;
using ProdutosApp.Core.Models;

namespace ProdutosApp.Core.Data;

/// <summary>
/// Acesso à tabela Produtos usando ADO.NET puro (SqliteConnection / SqliteCommand / SqliteDataReader).
///
/// Regras seguidas em TODOS os métodos:
///  • Os valores nunca são concatenados no SQL: sempre vão como parâmetros (@Nome, @Id...),
///    o que impede SQL Injection.
///  • INSERT / UPDATE / DELETE usam ExecuteNonQuery.
///  • SELECT usa ExecuteReader, com mapeamento manual do DataReader para Produto.
///  • Conexões, comandos e readers ficam em "using" (são fechados mesmo se der erro).
///  • SqliteException é registrada no log e relançada como RepositoryException com mensagem amigável.
/// </summary>
public class ProdutoRepository : IProdutoRepository
{
    private static readonly CultureInfo PtBr = new("pt-BR");

    private readonly string _connectionString;
    private readonly ILogService _log;

    public ProdutoRepository(string connectionString, ILogService log)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("A connection string não foi informada.", nameof(connectionString));

        try
        {
            // Só para validar o formato da connection string logo na criação.
            _ = new SqliteConnectionStringBuilder(connectionString);
        }
        catch (ArgumentException ex)
        {
            throw new ArgumentException($"Connection string inválida: {ex.Message}", nameof(connectionString), ex);
        }

        _connectionString = connectionString;
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    // ------------------------------------------------------------------
    //  INSERIR  (ExecuteNonQuery + transação)
    // ------------------------------------------------------------------
    public int Inserir(Produto produto)
    {
        ArgumentNullException.ThrowIfNull(produto);
        ValidarOuLancar(produto, "inserção");

        const string sqlInsert = @"
            INSERT INTO Produtos (Nome, Preco, Estoque, Categoria)
            VALUES (@Nome, @Preco, @Estoque, @Categoria);";

        try
        {
            using var conexao = AbrirConexao();

            // A transação garante que o INSERT e a leitura do Id gerado
            // acontecem juntos. Se algo falhar, o "using" desfaz tudo (rollback).
            using var transacao = conexao.BeginTransaction();

            using (var comando = conexao.CreateCommand())
            {
                comando.Transaction = transacao;
                comando.CommandText = sqlInsert;
                AdicionarParametrosDoProduto(comando, produto);
                comando.ExecuteNonQuery();
            }

            int novoId;
            using (var comandoId = conexao.CreateCommand())
            {
                comandoId.Transaction = transacao;
                comandoId.CommandText = "SELECT last_insert_rowid();";
                using var leitorId = comandoId.ExecuteReader();
                leitorId.Read();
                novoId = leitorId.GetInt32(0);
            }

            transacao.Commit();

            produto.Id = novoId;
            _log.Info($"INSERIR | {Descrever(produto)}");
            return novoId;
        }
        catch (SqliteException ex)
        {
            throw Falha("inserir produto", ex);
        }
    }

    // ------------------------------------------------------------------
    //  INSERIR VÁRIOS  (bônus: transação "tudo ou nada")
    // ------------------------------------------------------------------
    public int InserirVarios(IEnumerable<Produto> produtos)
    {
        ArgumentNullException.ThrowIfNull(produtos);

        var lista = produtos.ToList();
        if (lista.Count == 0)
            return 0;

        foreach (var produto in lista)
        {
            if (produto is null)
                throw new ArgumentException("A lista contém um produto nulo.", nameof(produtos));
            ValidarOuLancar(produto, "inserção em lote");
        }

        const string sqlInsert = @"
            INSERT INTO Produtos (Nome, Preco, Estoque, Categoria)
            VALUES (@Nome, @Preco, @Estoque, @Categoria);";

        try
        {
            using var conexao = AbrirConexao();
            using var transacao = conexao.BeginTransaction();

            using var comando = conexao.CreateCommand();
            comando.Transaction = transacao;
            comando.CommandText = sqlInsert;

            // O mesmo comando é reaproveitado: só os valores dos parâmetros mudam.
            var pNome = comando.Parameters.Add("@Nome", SqliteType.Text);
            var pPreco = comando.Parameters.Add("@Preco", SqliteType.Real);
            var pEstoque = comando.Parameters.Add("@Estoque", SqliteType.Integer);
            var pCategoria = comando.Parameters.Add("@Categoria", SqliteType.Text);

            foreach (var produto in lista)
            {
                pNome.Value = produto.Nome.Trim();
                pPreco.Value = (double)produto.Preco;
                pEstoque.Value = produto.Estoque;
                pCategoria.Value = produto.Categoria.Trim();
                comando.ExecuteNonQuery();
            }

            transacao.Commit();
            _log.Info($"INSERIR LOTE | {lista.Count} produto(s) gravados em uma única transação");
            return lista.Count;
        }
        catch (SqliteException ex)
        {
            // Nada foi gravado: a transação não recebeu Commit e foi desfeita no Dispose.
            _log.Erro("INSERIR LOTE | Transação desfeita (rollback) - nenhum produto foi gravado");
            throw Falha("inserir produtos em lote", ex);
        }
    }

    // ------------------------------------------------------------------
    //  LISTAR  (ExecuteReader)
    // ------------------------------------------------------------------
    public List<Produto> Listar() => ListarPorSituacao(ativo: true);

    public List<Produto> ListarExcluidos() => ListarPorSituacao(ativo: false);

    private List<Produto> ListarPorSituacao(bool ativo)
    {
        const string sql = @"
            SELECT Id, Nome, Preco, Estoque, Categoria, Ativo, DataCadastro, DataExclusao
            FROM Produtos
            WHERE Ativo = @Ativo
            ORDER BY Id;";

        try
        {
            using var conexao = AbrirConexao();
            using var comando = conexao.CreateCommand();
            comando.CommandText = sql;
            comando.Parameters.Add("@Ativo", SqliteType.Integer).Value = ativo ? 1 : 0;

            var produtos = new List<Produto>();
            using (var leitor = comando.ExecuteReader())
            {
                while (leitor.Read())
                    produtos.Add(Mapear(leitor));
            }

            _log.Info(ativo
                ? $"LISTAR | {produtos.Count} produto(s) ativo(s)"
                : $"LISTAR LIXEIRA | {produtos.Count} produto(s) excluído(s)");
            return produtos;
        }
        catch (SqliteException ex)
        {
            throw Falha(ativo ? "listar produtos" : "listar a lixeira", ex);
        }
    }

    // ------------------------------------------------------------------
    //  BUSCAR POR ID  (ExecuteReader)
    // ------------------------------------------------------------------
    public Produto? BuscarPorId(int id, bool incluirExcluidos = false)
    {
        ValidarId(id);

        const string sql = @"
            SELECT Id, Nome, Preco, Estoque, Categoria, Ativo, DataCadastro, DataExclusao
            FROM Produtos
            WHERE Id = @Id
              AND (@IncluirExcluidos = 1 OR Ativo = 1);";

        try
        {
            using var conexao = AbrirConexao();
            using var comando = conexao.CreateCommand();
            comando.CommandText = sql;
            comando.Parameters.Add("@Id", SqliteType.Integer).Value = id;
            comando.Parameters.Add("@IncluirExcluidos", SqliteType.Integer).Value = incluirExcluidos ? 1 : 0;

            Produto? produto = null;
            using (var leitor = comando.ExecuteReader())
            {
                if (leitor.Read())
                    produto = Mapear(leitor);
            }

            _log.Info(produto is null
                ? $"BUSCAR | ID {id} não encontrado"
                : $"BUSCAR | ID {id} encontrado: {Descrever(produto)}");
            return produto;
        }
        catch (SqliteException ex)
        {
            throw Falha($"buscar o produto {id}", ex);
        }
    }

    // ------------------------------------------------------------------
    //  ATUALIZAR  (ExecuteNonQuery)
    // ------------------------------------------------------------------
    public bool Atualizar(Produto produto)
    {
        ArgumentNullException.ThrowIfNull(produto);
        ValidarId(produto.Id);
        ValidarOuLancar(produto, "atualização");

        const string sql = @"
            UPDATE Produtos
               SET Nome = @Nome,
                   Preco = @Preco,
                   Estoque = @Estoque,
                   Categoria = @Categoria
             WHERE Id = @Id
               AND Ativo = 1;";

        try
        {
            using var conexao = AbrirConexao();
            using var comando = conexao.CreateCommand();
            comando.CommandText = sql;
            AdicionarParametrosDoProduto(comando, produto);
            comando.Parameters.Add("@Id", SqliteType.Integer).Value = produto.Id;

            var linhasAfetadas = comando.ExecuteNonQuery();

            if (linhasAfetadas > 0)
                _log.Info($"ATUALIZAR | {Descrever(produto)}");
            else
                _log.Info($"ATUALIZAR | ID {produto.Id} não encontrado - nada foi alterado");

            return linhasAfetadas > 0;
        }
        catch (SqliteException ex)
        {
            throw Falha($"atualizar o produto {produto.Id}", ex);
        }
    }

    // ------------------------------------------------------------------
    //  EXCLUIR  (bônus: soft delete -> UPDATE com ExecuteNonQuery)
    // ------------------------------------------------------------------
    public bool Excluir(int id)
    {
        ValidarId(id);

        const string sql = @"
            UPDATE Produtos
               SET Ativo = 0,
                   DataExclusao = datetime('now', 'localtime')
             WHERE Id = @Id
               AND Ativo = 1;";

        var ok = ExecutarComandoPorId(sql, id, $"excluir o produto {id}");
        _log.Info(ok
            ? $"EXCLUIR | ID {id} movido para a lixeira (soft delete)"
            : $"EXCLUIR | ID {id} não encontrado");
        return ok;
    }

    public bool Restaurar(int id)
    {
        ValidarId(id);

        const string sql = @"
            UPDATE Produtos
               SET Ativo = 1,
                   DataExclusao = NULL
             WHERE Id = @Id
               AND Ativo = 0;";

        var ok = ExecutarComandoPorId(sql, id, $"restaurar o produto {id}");
        _log.Info(ok
            ? $"RESTAURAR | ID {id} saiu da lixeira"
            : $"RESTAURAR | ID {id} não está na lixeira");
        return ok;
    }

    // ------------------------------------------------------------------
    //  EXCLUIR DEFINITIVAMENTE  (DELETE com ExecuteNonQuery)
    // ------------------------------------------------------------------
    public bool ExcluirDefinitivamente(int id)
    {
        ValidarId(id);

        // Só apaga fisicamente o que já estiver na lixeira (evita perda acidental).
        const string sql = @"
            DELETE FROM Produtos
             WHERE Id = @Id
               AND Ativo = 0;";

        var ok = ExecutarComandoPorId(sql, id, $"excluir definitivamente o produto {id}");
        _log.Info(ok
            ? $"EXCLUIR DEFINITIVO | ID {id} removido do banco (DELETE)"
            : $"EXCLUIR DEFINITIVO | ID {id} não está na lixeira");
        return ok;
    }

    // ------------------------------------------------------------------
    //  CATEGORIAS  (ExecuteReader) - usado para sugerir valores na tela
    // ------------------------------------------------------------------
    public List<string> ListarCategorias()
    {
        const string sql = @"
            SELECT DISTINCT Categoria
            FROM Produtos
            WHERE Ativo = @Ativo
            ORDER BY Categoria;";

        try
        {
            using var conexao = AbrirConexao();
            using var comando = conexao.CreateCommand();
            comando.CommandText = sql;
            comando.Parameters.Add("@Ativo", SqliteType.Integer).Value = 1;

            var categorias = new List<string>();
            using var leitor = comando.ExecuteReader();
            while (leitor.Read())
                categorias.Add(leitor.GetString(0));

            return categorias;
        }
        catch (SqliteException ex)
        {
            throw Falha("listar categorias", ex);
        }
    }

    // ==================================================================
    //  Métodos auxiliares
    // ==================================================================

    private SqliteConnection AbrirConexao()
    {
        var conexao = new SqliteConnection(_connectionString);
        conexao.Open();
        return conexao;
    }

    /// <summary>Executa um UPDATE/DELETE que recebe apenas o @Id.</summary>
    private bool ExecutarComandoPorId(string sql, int id, string descricaoOperacao)
    {
        try
        {
            using var conexao = AbrirConexao();
            using var comando = conexao.CreateCommand();
            comando.CommandText = sql;
            comando.Parameters.Add("@Id", SqliteType.Integer).Value = id;
            return comando.ExecuteNonQuery() > 0;
        }
        catch (SqliteException ex)
        {
            throw Falha(descricaoOperacao, ex);
        }
    }

    /// <summary>
    /// Adiciona os parâmetros comuns a INSERT e UPDATE.
    /// O valor digitado pelo usuário vai no parâmetro, nunca no texto do SQL.
    /// </summary>
    private static void AdicionarParametrosDoProduto(SqliteCommand comando, Produto produto)
    {
        comando.Parameters.Add("@Nome", SqliteType.Text).Value = produto.Nome.Trim();
        comando.Parameters.Add("@Preco", SqliteType.Real).Value = (double)produto.Preco;
        comando.Parameters.Add("@Estoque", SqliteType.Integer).Value = produto.Estoque;
        comando.Parameters.Add("@Categoria", SqliteType.Text).Value = produto.Categoria.Trim();
    }

    /// <summary>
    /// Mapeamento manual: cada coluna do DataReader vira uma propriedade do Produto.
    /// </summary>
    private static Produto Mapear(SqliteDataReader leitor)
    {
        var ordinalExclusao = leitor.GetOrdinal("DataExclusao");

        return new Produto
        {
            Id = leitor.GetInt32(leitor.GetOrdinal("Id")),
            Nome = leitor.GetString(leitor.GetOrdinal("Nome")),
            Preco = Math.Round((decimal)leitor.GetDouble(leitor.GetOrdinal("Preco")), 2),
            Estoque = leitor.GetInt32(leitor.GetOrdinal("Estoque")),
            Categoria = leitor.GetString(leitor.GetOrdinal("Categoria")),
            Ativo = leitor.GetInt32(leitor.GetOrdinal("Ativo")) == 1,
            DataCadastro = leitor.GetDateTime(leitor.GetOrdinal("DataCadastro")),
            DataExclusao = leitor.IsDBNull(ordinalExclusao) ? null : leitor.GetDateTime(ordinalExclusao)
        };
    }

    private void ValidarOuLancar(Produto produto, string operacao)
    {
        var erros = produto.Validar();
        if (erros.Count == 0)
            return;

        _log.Info($"VALIDAÇÃO | {operacao} recusada: {string.Join(" ", erros)}");
        throw new ProdutoInvalidoException(erros);
    }

    private static void ValidarId(int id)
    {
        if (id <= 0)
            throw new ArgumentOutOfRangeException(nameof(id), id, "O ID deve ser um número maior que zero.");
    }

    private RepositoryException Falha(string operacao, SqliteException ex)
    {
        _log.Erro($"FALHA ao {operacao} | código SQLite {ex.SqliteErrorCode}", ex);
        return new RepositoryException(TradutorErrosSqlite.Traduzir(ex), ex);
    }

    private static string Descrever(Produto p) =>
        $"#{p.Id} '{p.Nome}' | Categoria: {p.Categoria} | Preço: {p.Preco.ToString("C", PtBr)} | Estoque: {p.Estoque}";
}
