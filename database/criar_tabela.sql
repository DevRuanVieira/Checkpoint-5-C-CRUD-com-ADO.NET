-- =====================================================================
--  Cadastro de Produtos - Script de criação do banco (SQLite)
-- ---------------------------------------------------------------------
--  Este script é executado automaticamente pela aplicação ao iniciar
--  (é idempotente: pode rodar várias vezes sem apagar dados).
--  Também pode ser executado manualmente, por exemplo:
--      sqlite3 produtos.db < database/criar_tabela.sql
--  ou abrindo o arquivo no "DB Browser for SQLite" (aba Executar SQL).
-- =====================================================================

CREATE TABLE IF NOT EXISTS Produtos (
    Id            INTEGER PRIMARY KEY AUTOINCREMENT,
    Nome          TEXT    NOT NULL CHECK (length(trim(Nome)) BETWEEN 1 AND 100),
    Preco         REAL    NOT NULL CHECK (Preco >= 0),
    Estoque       INTEGER NOT NULL CHECK (Estoque >= 0),
    Categoria     TEXT    NOT NULL CHECK (length(trim(Categoria)) BETWEEN 1 AND 50),

    -- Colunas extras usadas no soft delete (exclusão lógica - bônus)
    Ativo         INTEGER NOT NULL DEFAULT 1 CHECK (Ativo IN (0, 1)),
    DataCadastro  TEXT    NOT NULL DEFAULT (datetime('now', 'localtime')),
    DataExclusao  TEXT    NULL
);

-- Acelera as consultas que filtram produtos ativos / na lixeira
CREATE INDEX IF NOT EXISTS IX_Produtos_Ativo ON Produtos (Ativo);
