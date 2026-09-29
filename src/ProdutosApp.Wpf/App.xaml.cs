using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Threading;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using ProdutosApp.Core.Data;
using ProdutosApp.Core.Logging;

namespace ProdutosApp.Wpf;

/// <summary>
/// Ponto de entrada: lê o appsettings.json, prepara o log e o banco
/// e entrega o repositório pronto para a janela principal.
/// </summary>
public partial class App : Application
{
    private ILogService? _log;

    private void Application_Startup(object sender, StartupEventArgs e)
    {
        ConfigurarCulturaBrasileira();

        try
        {
            var configuracao = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
                .Build();

            // 1) Log em arquivo
            var caminhoLog = ResolverCaminho(configuracao["Log:Arquivo"] ?? "logs/operacoes.log");
            _log = new ArquivoLogService(caminhoLog);
            _log.Info("APP | Aplicação iniciada");

            // 2) Connection string vinda do appsettings.json
            var connectionString = configuracao.GetConnectionString("ProdutosDb");
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException(
                    "A connection string \"ProdutosDb\" não foi encontrada no appsettings.json.");

            connectionString = AjustarCaminhoDoBanco(connectionString);

            // 3) Garante que a tabela exista (executa database/criar_tabela.sql)
            var caminhoScript = Path.Combine(AppContext.BaseDirectory, "database", "criar_tabela.sql");
            InicializadorBanco.CriarEstrutura(connectionString, caminhoScript, _log);

            // 4) Abre a janela principal com o repositório
            var repositorio = new ProdutoRepository(connectionString, _log);
            var janela = new MainWindow(repositorio, _log);
            MainWindow = janela;
            janela.Show();
        }
        catch (Exception ex)
        {
            _log?.Erro("APP | Falha na inicialização", ex);
            MessageBox.Show(
                $"Não foi possível iniciar a aplicação.\n\n{ex.Message}",
                "Erro na inicialização",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private void Application_Exit(object sender, ExitEventArgs e)
    {
        _log?.Info("APP | Aplicação encerrada");
    }

    /// <summary>Última barreira: qualquer erro não tratado é registrado e exibido sem fechar o programa.</summary>
    private void Application_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        _log?.Erro("APP | Erro inesperado", e.Exception);
        MessageBox.Show(
            $"Ocorreu um erro inesperado:\n\n{e.Exception.Message}",
            "Erro",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }

    /// <summary>Datas, moeda (R$) e vírgula decimal no padrão brasileiro.</summary>
    private static void ConfigurarCulturaBrasileira()
    {
        var cultura = new CultureInfo("pt-BR");
        CultureInfo.CurrentCulture = cultura;
        CultureInfo.CurrentUICulture = cultura;
        CultureInfo.DefaultThreadCurrentCulture = cultura;
        CultureInfo.DefaultThreadCurrentUICulture = cultura;

        FrameworkElement.LanguageProperty.OverrideMetadata(
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(cultura.IetfLanguageTag)));
    }

    /// <summary>Caminhos relativos passam a ser relativos à pasta do executável.</summary>
    private static string ResolverCaminho(string caminho) =>
        Path.IsPathRooted(caminho) ? caminho : Path.Combine(AppContext.BaseDirectory, caminho);

    /// <summary>
    /// "Data Source=produtos.db" vira o caminho completo ao lado do executável,
    /// assim o banco é o mesmo rodando pelo Visual Studio, por "dotnet run" ou pelo .exe.
    /// </summary>
    private static string AjustarCaminhoDoBanco(string connectionString)
    {
        var builder = new SqliteConnectionStringBuilder(connectionString);

        if (!string.IsNullOrWhiteSpace(builder.DataSource)
            && builder.DataSource != ":memory:"
            && !Path.IsPathRooted(builder.DataSource))
        {
            builder.DataSource = ResolverCaminho(builder.DataSource);
        }

        return builder.ToString();
    }
}
