using ProdutosApp.Core.Data;
using ProdutosApp.Core.Models;

namespace ProdutosApp.Tests;

public class ProdutoRepositoryTests : IDisposable
{
    private readonly BancoDeTeste _banco = new();
    private ProdutoRepository Repo => _banco.Repositorio;

    public void Dispose() => _banco.Dispose();

    private static Produto NovoProduto(string nome = "Mouse sem fio", decimal preco = 129.90m,
                                       int estoque = 10, string categoria = "Periféricos") =>
        new() { Nome = nome, Preco = preco, Estoque = estoque, Categoria = categoria };

    // ------------------------------------------------------------ Inserir

    [Fact]
    public void Inserir_ProdutoValido_GeraIdEGravaTodosOsCampos()
    {
        var produto = NovoProduto();

        var id = Repo.Inserir(produto);

        Assert.True(id > 0);
        Assert.Equal(id, produto.Id);

        var lido = Repo.BuscarPorId(id);
        Assert.NotNull(lido);
        Assert.Equal("Mouse sem fio", lido!.Nome);
        Assert.Equal(129.90m, lido.Preco);
        Assert.Equal(10, lido.Estoque);
        Assert.Equal("Periféricos", lido.Categoria);
        Assert.True(lido.Ativo);
        Assert.Null(lido.DataExclusao);
    }

    [Fact]
    public void Inserir_RemoveEspacosDoNomeEDaCategoria()
    {
        var id = Repo.Inserir(NovoProduto(nome: "   Teclado   ", categoria: "  Periféricos "));

        var lido = Repo.BuscarPorId(id)!;
        Assert.Equal("Teclado", lido.Nome);
        Assert.Equal("Periféricos", lido.Categoria);
    }

    [Theory]
    [InlineData("", 10.0, 1, "Cat")]
    [InlineData("Produto", -1.0, 1, "Cat")]
    [InlineData("Produto", 10.0, -5, "Cat")]
    [InlineData("Produto", 10.0, 1, "  ")]
    public void Inserir_ProdutoInvalido_LancaExcecaoENaoGrava(string nome, double preco, int estoque, string categoria)
    {
        // (atributos do C# não aceitam decimal, por isso o preço chega como double)
        var produto = NovoProduto(nome, (decimal)preco, estoque, categoria);

        Assert.Throws<ProdutoInvalidoException>(() => Repo.Inserir(produto));
        Assert.Empty(Repo.Listar());
    }

    // ------------------------------------------------------------ Listar

    [Fact]
    public void Listar_RetornaSomenteAtivosOrdenadosPorId()
    {
        var id1 = Repo.Inserir(NovoProduto("A"));
        var id2 = Repo.Inserir(NovoProduto("B"));
        var id3 = Repo.Inserir(NovoProduto("C"));
        Repo.Excluir(id2);

        var lista = Repo.Listar();

        Assert.Equal(new[] { id1, id3 }, lista.Select(p => p.Id));
    }

    [Fact]
    public void Listar_BancoVazio_RetornaListaVazia()
    {
        Assert.Empty(Repo.Listar());
    }

    // ------------------------------------------------------------ BuscarPorId

    [Fact]
    public void BuscarPorId_Inexistente_RetornaNull()
    {
        Assert.Null(Repo.BuscarPorId(999));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void BuscarPorId_IdInvalido_LancaArgumentOutOfRange(int id)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Repo.BuscarPorId(id));
    }

    // ------------------------------------------------------------ Atualizar

    [Fact]
    public void Atualizar_ProdutoExistente_AlteraOsDados()
    {
        var id = Repo.Inserir(NovoProduto());

        var alterado = new Produto { Id = id, Nome = "Mouse gamer", Preco = 199.99m, Estoque = 3, Categoria = "Games" };
        var ok = Repo.Atualizar(alterado);

        Assert.True(ok);
        var lido = Repo.BuscarPorId(id)!;
        Assert.Equal("Mouse gamer", lido.Nome);
        Assert.Equal(199.99m, lido.Preco);
        Assert.Equal(3, lido.Estoque);
        Assert.Equal("Games", lido.Categoria);
    }

    [Fact]
    public void Atualizar_IdInexistente_RetornaFalse()
    {
        var ok = Repo.Atualizar(new Produto { Id = 12345, Nome = "X", Preco = 1, Estoque = 1, Categoria = "Y" });

        Assert.False(ok);
    }

    [Fact]
    public void Atualizar_ProdutoNaLixeira_NaoAltera()
    {
        var id = Repo.Inserir(NovoProduto());
        Repo.Excluir(id);

        var ok = Repo.Atualizar(new Produto { Id = id, Nome = "Novo nome", Preco = 1, Estoque = 1, Categoria = "Y" });

        Assert.False(ok);
        Assert.Equal("Mouse sem fio", Repo.BuscarPorId(id, incluirExcluidos: true)!.Nome);
    }

    // ------------------------------------------------------------ Excluir (soft delete)

    [Fact]
    public void Excluir_MoveParaLixeiraSemApagarDoBanco()
    {
        var id = Repo.Inserir(NovoProduto());

        var ok = Repo.Excluir(id);

        Assert.True(ok);
        Assert.Null(Repo.BuscarPorId(id));                     // não aparece mais como ativo
        var naLixeira = Repo.BuscarPorId(id, incluirExcluidos: true);
        Assert.NotNull(naLixeira);                             // mas continua no banco
        Assert.False(naLixeira!.Ativo);
        Assert.NotNull(naLixeira.DataExclusao);
        Assert.Single(Repo.ListarExcluidos());
    }

    [Fact]
    public void Excluir_IdInexistente_RetornaFalse()
    {
        Assert.False(Repo.Excluir(777));
    }

    [Fact]
    public void Restaurar_TiraDaLixeira()
    {
        var id = Repo.Inserir(NovoProduto());
        Repo.Excluir(id);

        var ok = Repo.Restaurar(id);

        Assert.True(ok);
        var lido = Repo.BuscarPorId(id)!;
        Assert.True(lido.Ativo);
        Assert.Null(lido.DataExclusao);
        Assert.Empty(Repo.ListarExcluidos());
    }

    [Fact]
    public void ExcluirDefinitivamente_SoApagaQuemEstaNaLixeira()
    {
        var id = Repo.Inserir(NovoProduto());

        Assert.False(Repo.ExcluirDefinitivamente(id));        // ativo: não pode apagar direto
        Repo.Excluir(id);
        Assert.True(Repo.ExcluirDefinitivamente(id));         // na lixeira: DELETE

        Assert.Null(Repo.BuscarPorId(id, incluirExcluidos: true));
    }

    // ------------------------------------------------------------ SQL Injection

    [Theory]
    [InlineData("'); DROP TABLE Produtos; --")]
    [InlineData("x' OR '1'='1")]
    [InlineData("Robert\"); DELETE FROM Produtos; --")]
    public void Inserir_TextoMalicioso_EhGravadoComoTextoComum(string textoMalicioso)
    {
        var outroId = Repo.Inserir(NovoProduto("Produto legítimo"));

        var id = Repo.Inserir(NovoProduto(nome: textoMalicioso, categoria: textoMalicioso));

        var lido = Repo.BuscarPorId(id)!;
        Assert.Equal(textoMalicioso, lido.Nome);               // salvo literalmente
        Assert.Equal(textoMalicioso, lido.Categoria);
        Assert.Equal(2, Repo.Listar().Count);                  // tabela intacta, nada apagado
        Assert.NotNull(Repo.BuscarPorId(outroId));
    }

    [Fact]
    public void Atualizar_TextoMalicioso_AlteraSomenteOProdutoInformado()
    {
        var id1 = Repo.Inserir(NovoProduto("Primeiro"));
        var id2 = Repo.Inserir(NovoProduto("Segundo"));

        Repo.Atualizar(new Produto { Id = id1, Nome = "Hack', Nome = 'Hack", Preco = 1, Estoque = 1, Categoria = "C" });

        Assert.Equal("Hack', Nome = 'Hack", Repo.BuscarPorId(id1)!.Nome);
        Assert.Equal("Segundo", Repo.BuscarPorId(id2)!.Nome);
    }

    // ------------------------------------------------------------ Transações

    [Fact]
    public void InserirVarios_GravaTodosNaMesmaTransacao()
    {
        var qtd = Repo.InserirVarios(new[] { NovoProduto("A"), NovoProduto("B"), NovoProduto("C") });

        Assert.Equal(3, qtd);
        Assert.Equal(3, Repo.Listar().Count);
    }

    [Fact]
    public void InserirVarios_FalhaNoMeio_DesfazTudo()
    {
        // Um trigger simula um erro do banco no 3º produto do lote.
        _banco.ExecutarSql(@"
            CREATE TRIGGER trg_falha BEFORE INSERT ON Produtos
            WHEN NEW.Nome = 'FALHA'
            BEGIN
                SELECT RAISE(ABORT, 'falha simulada');
            END;");

        var lote = new[] { NovoProduto("A"), NovoProduto("B"), NovoProduto("FALHA"), NovoProduto("D") };

        Assert.Throws<RepositoryException>(() => Repo.InserirVarios(lote));
        Assert.Empty(Repo.Listar());                            // rollback: A e B também foram desfeitos
    }

    // ------------------------------------------------------------ Tratamento de erros

    [Fact]
    public void Operacoes_SemTabelaCriada_LancamRepositoryExceptionAmigavel()
    {
        using var bancoSemTabela = new BancoDeTeste(criarTabela: false);

        var ex = Assert.Throws<RepositoryException>(() => bancoSemTabela.Repositorio.Listar());

        Assert.Contains("tabela Produtos não existe", ex.Message);
        Assert.NotNull(ex.InnerException);                      // SqliteException original preservada
    }

    [Fact]
    public void Operacoes_CaminhoDeBancoInvalido_LancamRepositoryException()
    {
        var pastaInexistente = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "nao_existe");
        var repo = new ProdutoRepository($"Data Source={Path.Combine(pastaInexistente, "x.db")}", _banco.Log);

        var ex = Assert.Throws<RepositoryException>(() => repo.Listar());

        Assert.Contains("Não foi possível abrir", ex.Message);
    }

    [Fact]
    public void ViolacaoDeRegraNoBanco_EhTraduzidaParaMensagemAmigavel()
    {
        // Insere direto no banco um valor que a tabela recusa (CHECK Estoque >= 0).
        var ex = Assert.Throws<Microsoft.Data.Sqlite.SqliteException>(() =>
            _banco.ExecutarSql("INSERT INTO Produtos (Nome, Preco, Estoque, Categoria) VALUES ('X', 1, -1, 'C');"));

        Assert.Contains("violam uma regra", TradutorErrosSqlite.Traduzir(ex));
    }

    [Fact]
    public void Construtor_ConnectionStringVazia_LancaArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new ProdutoRepository("  ", _banco.Log));
    }

    // ------------------------------------------------------------ Log em arquivo

    [Fact]
    public void Operacoes_SaoRegistradasNoArquivoDeLog()
    {
        var id = Repo.Inserir(NovoProduto("Produto logado"));
        Repo.Listar();
        Repo.BuscarPorId(id);
        Repo.Atualizar(new Produto { Id = id, Nome = "Produto alterado", Preco = 5, Estoque = 5, Categoria = "C" });
        Repo.Excluir(id);

        var log = File.ReadAllText(_banco.CaminhoLog);

        Assert.Contains("INSERIR", log);
        Assert.Contains("LISTAR", log);
        Assert.Contains("BUSCAR", log);
        Assert.Contains("ATUALIZAR", log);
        Assert.Contains("EXCLUIR", log);
        Assert.Contains("Produto logado", log);
    }
}
