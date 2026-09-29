using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ProdutosApp.Core.Data;
using ProdutosApp.Core.Logging;
using ProdutosApp.Core.Models;

namespace ProdutosApp.Wpf;

/// <summary>
/// Janela principal. Aqui fica apenas a lógica de TELA: ler campos, mostrar mensagens
/// e chamar o repositório. Nenhum SQL é escrito nesta camada.
/// </summary>
public partial class MainWindow : Window
{
    private enum ModoFormulario { Visualizacao, Insercao, Edicao }
    private enum TipoStatus { Info, Sucesso, Erro }

    private static readonly string[] CategoriasSugeridas =
    {
        "Alimentos", "Bebidas", "Eletrônicos", "Escritório", "Higiene",
        "Informática", "Limpeza", "Periféricos", "Vestuário"
    };

    private static readonly Brush CorStatusInfo = CriarPincel(0x64, 0x74, 0x8B);
    private static readonly Brush CorStatusSucesso = CriarPincel(0x16, 0xA3, 0x4A);
    private static readonly Brush CorStatusErro = CriarPincel(0xDC, 0x26, 0x26);
    private static readonly Brush CorBordaPadrao = CriarPincel(0xE2, 0xE8, 0xF0);
    private static readonly Brush CorBordaEdicao = CriarPincel(0x25, 0x63, 0xEB);

    private readonly IProdutoRepository _repositorio;
    private readonly ILogService _log;

    private ModoFormulario _modo = ModoFormulario.Visualizacao;
    private int _idEmEdicao;

    public MainWindow(IProdutoRepository repositorio, ILogService log)
    {
        _repositorio = repositorio;
        _log = log;

        InitializeComponent();

        Loaded += (_, _) =>
        {
            DefinirModo(ModoFormulario.Visualizacao);
            AtualizarCategorias();
            CarregarProdutos();
        };
    }

    private bool ExibindoLixeira => chkLixeira.IsChecked == true;

    // =====================================================================
    //  MENU
    // =====================================================================

    // 1. Inserir produto
    private void MenuInserir_Click(object sender, RoutedEventArgs e)
    {
        if (ExibindoLixeira)
        {
            chkLixeira.IsChecked = false;
            CarregarProdutos();
        }

        dgProdutos.SelectedItem = null;
        LimparFormulario();
        DefinirModo(ModoFormulario.Insercao);
        txtNome.Focus();
        MostrarStatus("Preencha os dados do novo produto e clique em Salvar.");
    }

    // 2. Listar produtos
    private void MenuListar_Click(object sender, RoutedEventArgs e)
    {
        chkLixeira.IsChecked = false;
        DefinirModo(ModoFormulario.Visualizacao);
        LimparFormulario();

        if (CarregarProdutos())
            MostrarStatus("Lista atualizada.");
    }

    // 3. Buscar produto por ID
    private void MenuBuscar_Click(object sender, RoutedEventArgs e)
    {
        int? id = int.TryParse(txtBuscaId.Text.Trim(), out var digitado) && digitado > 0
            ? digitado
            : InputIdWindow.Perguntar(this, "Buscar produto", "Informe o ID do produto que deseja buscar:");

        txtBuscaId.Clear();
        if (id is null)
            return;

        Executar("buscar o produto", () =>
        {
            var produto = _repositorio.BuscarPorId(id.Value);
            if (produto is null)
            {
                AvisarNaoEncontrado(id.Value);
                return;
            }

            chkLixeira.IsChecked = false;
            ConfigurarVisaoLixeira(false);
            dgProdutos.ItemsSource = new List<Produto> { produto };
            txtTituloLista.Text = $"Resultado da busca: ID {produto.Id}";
            txtContagem.Text = "Clique em \"2. Listar produtos\" para voltar à lista completa.";

            DefinirModo(ModoFormulario.Visualizacao);
            dgProdutos.SelectedItem = produto;
            ExibirNoFormulario(produto);
            MostrarStatus($"Produto #{produto.Id} encontrado.", TipoStatus.Sucesso);
        });
    }

    // 4. Atualizar produto
    private void MenuAtualizar_Click(object sender, RoutedEventArgs e)
    {
        var id = ObterIdAlvo("Atualizar produto", "Informe o ID do produto que deseja atualizar:");
        if (id is null)
            return;

        Executar("carregar o produto", () =>
        {
            var produto = _repositorio.BuscarPorId(id.Value);
            if (produto is null)
            {
                AvisarNaoEncontrado(id.Value);
                return;
            }

            if (ExibindoLixeira)
            {
                chkLixeira.IsChecked = false;
                CarregarProdutos(produto.Id);
            }

            ExibirNoFormulario(produto);
            _idEmEdicao = produto.Id;
            DefinirModo(ModoFormulario.Edicao);
            txtNome.Focus();
            txtNome.SelectAll();
            MostrarStatus($"Editando o produto #{produto.Id}. Altere os campos e clique em Salvar.");
        });
    }

    // 5. Excluir produto (soft delete)
    private void MenuExcluir_Click(object sender, RoutedEventArgs e)
    {
        var id = ObterIdAlvo("Excluir produto", "Informe o ID do produto que deseja excluir:");
        if (id is null)
            return;

        Executar("excluir o produto", () =>
        {
            var produto = _repositorio.BuscarPorId(id.Value);
            if (produto is null)
            {
                AvisarNaoEncontrado(id.Value);
                return;
            }

            var resposta = MessageBox.Show(this,
                $"Deseja excluir o produto #{produto.Id} – \"{produto.Nome}\"?\n\n" +
                "Ele será movido para a lixeira e poderá ser restaurado depois.",
                "Confirmar exclusão", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);

            if (resposta != MessageBoxResult.Yes)
            {
                MostrarStatus("Exclusão cancelada.");
                return;
            }

            if (_repositorio.Excluir(produto.Id))
                MostrarStatus($"Produto #{produto.Id} \"{produto.Nome}\" movido para a lixeira.", TipoStatus.Sucesso);
            else
                AvisarNaoEncontrado(produto.Id);

            chkLixeira.IsChecked = false;
            DefinirModo(ModoFormulario.Visualizacao);
            LimparFormulario();
            CarregarProdutos();
            AtualizarCategorias();
        });
    }

    // 6. Sair
    private void MenuSair_Click(object sender, RoutedEventArgs e) => Close();

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_modo == ModoFormulario.Visualizacao)
            return;

        var resposta = MessageBox.Show(this,
            "Há um cadastro em andamento que não foi salvo. Deseja sair mesmo assim?",
            "Sair", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);

        e.Cancel = resposta != MessageBoxResult.Yes;
    }

    // =====================================================================
    //  FORMULÁRIO
    // =====================================================================

    private void BtnSalvar_Click(object sender, RoutedEventArgs e)
    {
        if (!TentarLerFormulario(out var produto, out var erros))
        {
            MostrarStatus("Corrija os campos do formulário.", TipoStatus.Erro);
            MessageBox.Show(this, "Verifique os campos:\n\n• " + string.Join("\n• ", erros),
                "Dados inválidos", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (_modo == ModoFormulario.Insercao)
        {
            Executar("inserir o produto", () =>
            {
                var novoId = _repositorio.Inserir(produto);

                DefinirModo(ModoFormulario.Visualizacao);
                AtualizarCategorias();
                CarregarProdutos(selecionarId: novoId);
                MostrarStatus($"Produto #{novoId} \"{produto.Nome}\" cadastrado com sucesso.", TipoStatus.Sucesso);
            });
        }
        else if (_modo == ModoFormulario.Edicao)
        {
            produto.Id = _idEmEdicao;

            Executar("atualizar o produto", () =>
            {
                var atualizado = _repositorio.Atualizar(produto);

                DefinirModo(ModoFormulario.Visualizacao);
                AtualizarCategorias();
                CarregarProdutos(selecionarId: produto.Id);

                if (atualizado)
                    MostrarStatus($"Produto #{produto.Id} atualizado com sucesso.", TipoStatus.Sucesso);
                else
                    AvisarNaoEncontrado(produto.Id);
            });
        }
    }

    private void BtnCancelar_Click(object sender, RoutedEventArgs e)
    {
        if (_modo == ModoFormulario.Visualizacao)
            return;

        DefinirModo(ModoFormulario.Visualizacao);

        if (dgProdutos.SelectedItem is Produto selecionado)
            ExibirNoFormulario(selecionado);
        else
            LimparFormulario();

        MostrarStatus("Operação cancelada.");
    }

    // =====================================================================
    //  LISTA / LIXEIRA / EXEMPLOS
    // =====================================================================

    private void DgProdutos_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        // Enquanto o usuário está inserindo/editando, o formulário não muda.
        if (_modo != ModoFormulario.Visualizacao)
            return;

        if (dgProdutos.SelectedItem is Produto produto)
            ExibirNoFormulario(produto);
        else
            LimparFormulario();
    }

    private void DgProdutos_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (!ExibindoLixeira && dgProdutos.SelectedItem is Produto)
            MenuAtualizar_Click(sender, e);
    }

    private void TxtBuscaId_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            MenuBuscar_Click(sender, e);
            e.Handled = true;
        }
    }

    private void ChkLixeira_Click(object sender, RoutedEventArgs e)
    {
        DefinirModo(ModoFormulario.Visualizacao);
        LimparFormulario();
        CarregarProdutos();
        MostrarStatus(ExibindoLixeira
            ? "Exibindo a lixeira. Selecione um item para restaurar ou excluir definitivamente."
            : "Exibindo produtos ativos.");
    }

    private void BtnRestaurar_Click(object sender, RoutedEventArgs e)
    {
        if (dgProdutos.SelectedItem is not Produto produto)
        {
            MessageBox.Show(this, "Selecione um produto da lixeira.", "Restaurar",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        Executar("restaurar o produto", () =>
        {
            if (_repositorio.Restaurar(produto.Id))
                MostrarStatus($"Produto #{produto.Id} \"{produto.Nome}\" restaurado.", TipoStatus.Sucesso);
            else
                AvisarNaoEncontrado(produto.Id);

            CarregarProdutos();
            AtualizarCategorias();
        });
    }

    private void BtnExcluirDefinitivo_Click(object sender, RoutedEventArgs e)
    {
        if (dgProdutos.SelectedItem is not Produto produto)
        {
            MessageBox.Show(this, "Selecione um produto da lixeira.", "Excluir definitivamente",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var resposta = MessageBox.Show(this,
            $"Apagar DEFINITIVAMENTE o produto #{produto.Id} – \"{produto.Nome}\"?\n\nEsta ação não pode ser desfeita.",
            "Excluir definitivamente", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);

        if (resposta != MessageBoxResult.Yes)
            return;

        Executar("excluir definitivamente o produto", () =>
        {
            if (_repositorio.ExcluirDefinitivamente(produto.Id))
                MostrarStatus($"Produto #{produto.Id} removido permanentemente do banco.", TipoStatus.Sucesso);
            else
                AvisarNaoEncontrado(produto.Id);

            LimparFormulario();
            CarregarProdutos();
        });
    }

    private void BtnExemplos_Click(object sender, RoutedEventArgs e)
    {
        var exemplos = new List<Produto>
        {
            new() { Nome = "Teclado mecânico ABNT2", Preco = 349.90m, Estoque = 15, Categoria = "Periféricos" },
            new() { Nome = "Mouse sem fio", Preco = 129.90m, Estoque = 32, Categoria = "Periféricos" },
            new() { Nome = "Monitor 24\" Full HD", Preco = 899.00m, Estoque = 8, Categoria = "Informática" },
            new() { Nome = "Café em grãos 1 kg", Preco = 64.50m, Estoque = 40, Categoria = "Alimentos" },
            new() { Nome = "Caderno universitário", Preco = 24.90m, Estoque = 120, Categoria = "Escritório" }
        };

        Executar("inserir os produtos de exemplo", () =>
        {
            var quantidade = _repositorio.InserirVarios(exemplos);

            chkLixeira.IsChecked = false;
            DefinirModo(ModoFormulario.Visualizacao);
            AtualizarCategorias();
            CarregarProdutos();
            MostrarStatus($"{quantidade} produtos de exemplo inseridos em uma única transação.", TipoStatus.Sucesso);
        });
    }

    // =====================================================================
    //  AUXILIARES
    // =====================================================================

    /// <summary>
    /// Centraliza o tratamento de erros da tela: qualquer chamada ao repositório passa por aqui.
    /// Retorna true se a ação terminou sem erro.
    /// </summary>
    private bool Executar(string operacao, Action acao)
    {
        try
        {
            acao();
            return true;
        }
        catch (ProdutoInvalidoException ex)
        {
            MostrarStatus("Corrija os campos do formulário.", TipoStatus.Erro);
            MessageBox.Show(this, "Verifique os campos:\n\n• " + string.Join("\n• ", ex.Erros),
                "Dados inválidos", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch (RepositoryException ex)
        {
            // O repositório já registrou o erro técnico no log.
            MostrarStatus($"Erro ao {operacao}.", TipoStatus.Erro);
            MessageBox.Show(this, ex.Message, "Erro de banco de dados",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch (ArgumentException ex)
        {
            _log.Erro($"TELA | Não foi possível {operacao}", ex);
            MostrarStatus($"Não foi possível {operacao}.", TipoStatus.Erro);
            MessageBox.Show(this, ex.Message, "Atenção", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        return false;
    }

    /// <summary>Carrega a grade (produtos ativos ou lixeira). Opcionalmente seleciona um ID.</summary>
    private bool CarregarProdutos(int? selecionarId = null)
    {
        var lixeira = ExibindoLixeira;

        return Executar(lixeira ? "carregar a lixeira" : "listar os produtos", () =>
        {
            var produtos = lixeira ? _repositorio.ListarExcluidos() : _repositorio.Listar();

            ConfigurarVisaoLixeira(lixeira);
            dgProdutos.ItemsSource = produtos;

            txtTituloLista.Text = lixeira ? "Lixeira" : "Produtos cadastrados";
            txtContagem.Text = produtos.Count switch
            {
                0 when lixeira => "A lixeira está vazia.",
                0 => "Nenhum produto cadastrado. Use \"1. Inserir produto\" ou \"Carregar exemplos\".",
                1 => "1 produto",
                _ => $"{produtos.Count} produtos"
            };

            if (selecionarId is int id)
            {
                var item = produtos.FirstOrDefault(p => p.Id == id);
                if (item is not null)
                {
                    dgProdutos.SelectedItem = item;
                    dgProdutos.ScrollIntoView(item);
                }
            }
        });
    }

    private void ConfigurarVisaoLixeira(bool lixeira)
    {
        colExcluidoEm.Visibility = lixeira ? Visibility.Visible : Visibility.Collapsed;
        barraLixeira.Visibility = lixeira ? Visibility.Visible : Visibility.Collapsed;
    }

    private void AtualizarCategorias()
    {
        Executar("carregar as categorias", () =>
        {
            var textoAtual = cmbCategoria.Text;

            cmbCategoria.ItemsSource = CategoriasSugeridas
                .Concat(_repositorio.ListarCategorias())
                .Distinct(StringComparer.CurrentCultureIgnoreCase)
                .OrderBy(c => c, StringComparer.CurrentCulture)
                .ToList();

            cmbCategoria.Text = textoAtual;
        });
    }

    /// <summary>Usa o produto selecionado na grade; se não houver, pergunta o ID.</summary>
    private int? ObterIdAlvo(string titulo, string mensagem)
    {
        if (!ExibindoLixeira && dgProdutos.SelectedItem is Produto selecionado)
            return selecionado.Id;

        return InputIdWindow.Perguntar(this, titulo, mensagem);
    }

    private void DefinirModo(ModoFormulario modo)
    {
        _modo = modo;
        var editando = modo != ModoFormulario.Visualizacao;

        painelCampos.IsEnabled = editando;
        painelBotoesFormulario.Visibility = editando ? Visibility.Visible : Visibility.Collapsed;
        bordaFormulario.BorderBrush = editando ? CorBordaEdicao : CorBordaPadrao;

        switch (modo)
        {
            case ModoFormulario.Insercao:
                txtTituloFormulario.Text = "Novo produto";
                txtSubtituloFormulario.Text = "Campos com * são obrigatórios.";
                btnSalvar.Content = "Cadastrar";
                break;

            case ModoFormulario.Edicao:
                txtTituloFormulario.Text = $"Editando produto #{_idEmEdicao}";
                txtSubtituloFormulario.Text = "Altere os campos e clique em Salvar alterações.";
                btnSalvar.Content = "Salvar alterações";
                break;

            default:
                _idEmEdicao = 0;
                txtTituloFormulario.Text = "Detalhes do produto";
                txtSubtituloFormulario.Text = "Selecione um produto na lista ou escolha uma opção do menu.";
                break;
        }
    }

    private void ExibirNoFormulario(Produto produto)
    {
        txtId.Text = produto.Id.ToString(CultureInfo.CurrentCulture);
        txtNome.Text = produto.Nome;
        txtPreco.Text = produto.Preco.ToString("N2", CultureInfo.CurrentCulture);
        txtEstoque.Text = produto.Estoque.ToString(CultureInfo.CurrentCulture);
        cmbCategoria.Text = produto.Categoria;
    }

    private void LimparFormulario()
    {
        txtId.Text = string.Empty;
        txtNome.Text = string.Empty;
        txtPreco.Text = string.Empty;
        txtEstoque.Text = string.Empty;
        cmbCategoria.Text = string.Empty;
    }

    /// <summary>Converte os campos da tela em um Produto e junta todos os erros encontrados.</summary>
    private bool TentarLerFormulario(out Produto produto, out List<string> erros)
    {
        erros = new List<string>();
        produto = new Produto
        {
            Nome = txtNome.Text.Trim(),
            Categoria = cmbCategoria.Text.Trim()
        };

        if (TentarConverterPreco(txtPreco.Text, out var preco))
            produto.Preco = Math.Round(preco, 2);
        else
            erros.Add("Preço inválido. Use um número, por exemplo 19,90.");

        if (int.TryParse(txtEstoque.Text.Trim(), NumberStyles.Integer, CultureInfo.CurrentCulture, out var estoque))
            produto.Estoque = estoque;
        else
            erros.Add("Estoque inválido. Use um número inteiro, por exemplo 10.");

        erros.AddRange(produto.Validar());
        return erros.Count == 0;
    }

    /// <summary>Aceita "19,90", "1.234,56", "R$ 19,90" e também "19.90".</summary>
    private static bool TentarConverterPreco(string texto, out decimal valor)
    {
        texto = texto.Replace("R$", string.Empty).Trim();

        var cultura = texto.Contains(',')
            ? new CultureInfo("pt-BR")
            : CultureInfo.InvariantCulture;

        return decimal.TryParse(texto, NumberStyles.Number, cultura, out valor);
    }

    private void AvisarNaoEncontrado(int id)
    {
        MostrarStatus($"Nenhum produto ativo com o ID {id}.", TipoStatus.Erro);
        MessageBox.Show(this, $"Nenhum produto ativo foi encontrado com o ID {id}.",
            "Produto não encontrado", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void MostrarStatus(string mensagem, TipoStatus tipo = TipoStatus.Info)
    {
        txtStatus.Text = $"{DateTime.Now:HH:mm:ss}   {mensagem}";
        txtStatus.Foreground = tipo switch
        {
            TipoStatus.Sucesso => CorStatusSucesso,
            TipoStatus.Erro => CorStatusErro,
            _ => CorStatusInfo
        };
    }

    private static Brush CriarPincel(byte r, byte g, byte b)
    {
        var pincel = new SolidColorBrush(Color.FromRgb(r, g, b));
        pincel.Freeze();
        return pincel;
    }
}
