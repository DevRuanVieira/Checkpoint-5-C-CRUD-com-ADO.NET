using System.Windows;

namespace ProdutosApp.Wpf;

/// <summary>
/// Pequena janela para pedir um ID ao usuário (usada em Buscar, Atualizar e Excluir
/// quando nenhum produto está selecionado na lista).
/// </summary>
public partial class InputIdWindow : Window
{
    public int? IdInformado { get; private set; }

    public InputIdWindow(string titulo, string mensagem)
    {
        InitializeComponent();
        Title = titulo;
        txtMensagem.Text = mensagem;
        Loaded += (_, _) => txtValor.Focus();
    }

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        if (int.TryParse(txtValor.Text.Trim(), out var id) && id > 0)
        {
            IdInformado = id;
            DialogResult = true;
            return;
        }

        txtErro.Text = "Digite um número inteiro maior que zero.";
        txtErro.Visibility = Visibility.Visible;
        txtValor.SelectAll();
        txtValor.Focus();
    }

    /// <summary>Mostra a janela e devolve o ID digitado, ou null se o usuário cancelar.</summary>
    public static int? Perguntar(Window dono, string titulo, string mensagem)
    {
        var janela = new InputIdWindow(titulo, mensagem) { Owner = dono };
        return janela.ShowDialog() == true ? janela.IdInformado : null;
    }
}
