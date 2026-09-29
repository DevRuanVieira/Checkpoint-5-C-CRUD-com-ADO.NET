# Roteiro da apresentação (3 a 5 minutos)

Sugestão de divisão para dupla: **Pessoa A** faz os blocos 1, 2 e 5; **Pessoa B** faz 3 e 4.
Antes de gravar: apague `produtos.db` e `logs/` da pasta `bin/Debug/net8.0-windows` para começar do zero,
e deixe abertos o Visual Studio (ou VS Code) e a aplicação.

---

## 1. Abertura — 20 s

> "Este é o nosso Cadastro de Produtos em C# com WPF. O acesso ao banco é feito com ADO.NET puro,
> usando SQLite, sem ORM. Vou mostrar o CRUD funcionando e depois o código."

## 2. Estrutura e configuração — 40 s

Mostrar no editor:

- A solução com três projetos: **Core** (modelo + repositório), **Wpf** (tela) e **Tests**.
- `appsettings.json` → *"a connection string fica aqui, fora do código."*
- `database/criar_tabela.sql` → *"este é o script da tabela; a aplicação executa ele ao abrir,
  com CREATE TABLE IF NOT EXISTS."*

## 3. Demonstração do CRUD — 1 min 45 s

1. **Inserir:** clicar em *1. Inserir produto*, cadastrar "Mouse sem fio", 129,90, 32, Periféricos → Cadastrar.
   Mostrar a mensagem verde na barra de status.
2. **Carregar exemplos:** *"este botão insere 5 produtos em uma única transação."*
3. **Listar:** clicar em *2. Listar produtos*.
4. **Buscar:** digitar um ID no campo do topo e Enter. Depois buscar um ID que não existe (ex.: 999) → mensagem de não encontrado.
5. **Atualizar:** selecionar uma linha → *4. Atualizar produto* → mudar preço e estoque → Salvar alterações.
6. **Excluir:** selecionar → *5. Excluir produto* → confirmar. Marcar **Ver lixeira** → mostrar o produto lá → **Restaurar**.
7. **Validação:** tentar cadastrar com preço `-5` ou nome vazio → mensagem com os campos a corrigir.
8. **SQL Injection:** cadastrar um produto com nome `'); DROP TABLE Produtos; --` →
   *"foi salvo como texto comum e a tabela continua funcionando, porque usamos parâmetros."*

## 4. Código — 1 min 15 s

Abrir `ProdutoRepository.cs`:

- Método **Atualizar**: apontar `@Nome`, `@Preco`... e `Parameters.Add(...)` → *"nenhum valor é concatenado no SQL."*
- `ExecuteNonQuery` no INSERT/UPDATE/DELETE e `ExecuteReader` no Listar/BuscarPorId.
- Método **Mapear**: *"mapeamento manual do DataReader para o objeto Produto, coluna por coluna."*
- Bloco `catch (SqliteException ex)` + `TradutorErrosSqlite` → *"o erro técnico vai para o log e o usuário recebe uma mensagem clara."*
- Método **Inserir**: `BeginTransaction` / `Commit`.

Abrir `MainWindow.xaml.cs` rapidamente: *"a tela só conhece a interface IProdutoRepository; não tem SQL aqui."*

## 5. Log, testes e encerramento — 40 s

- Abrir `logs/operacoes.log` e mostrar as linhas INSERIR, BUSCAR, ATUALIZAR, EXCLUIR.
- (Opcional) Mostrar um erro: trocar a connection string para `Data Source=Z:\\nao\\existe.db`,
  abrir a aplicação e mostrar a mensagem amigável + a linha `[ERRO]` no log. Voltar a configuração.
- Rodar `dotnet test` (ou o Gerenciador de Testes) e mostrar todos os testes passando.

> "Como bônus implementamos soft delete com lixeira, transações e testes unitários. Obrigado!"
