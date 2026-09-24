-- Melhorias opcionais para o banco Filmes no SQL Server.
-- Rode depois de 01-criar-banco.sql. Adiciona integridade referencial e índices
-- que o script original não tem (o SQLite do Claquete já nasce com eles).

USE Filmes;
GO

ALTER TABLE ElencoFilme ADD CONSTRAINT FK_ElencoFilme_Atores
    FOREIGN KEY (IdAtor) REFERENCES Atores (Id) ON DELETE CASCADE;
ALTER TABLE ElencoFilme ADD CONSTRAINT FK_ElencoFilme_Filmes
    FOREIGN KEY (IdFilme) REFERENCES Filmes (Id) ON DELETE CASCADE;
ALTER TABLE FilmesGenero ADD CONSTRAINT FK_FilmesGenero_Generos
    FOREIGN KEY (IdGenero) REFERENCES Generos (Id) ON DELETE CASCADE;
ALTER TABLE FilmesGenero ADD CONSTRAINT FK_FilmesGenero_Filmes
    FOREIGN KEY (IdFilme) REFERENCES Filmes (Id) ON DELETE CASCADE;
ALTER TABLE Atores ADD CONSTRAINT CK_Atores_Genero CHECK (Genero IN ('M', 'F'));
GO

CREATE INDEX IX_Filmes_Ano ON Filmes (Ano);
CREATE INDEX IX_ElencoFilme_IdFilme ON ElencoFilme (IdFilme);
CREATE INDEX IX_ElencoFilme_IdAtor ON ElencoFilme (IdAtor);
CREATE INDEX IX_FilmesGenero_IdFilme ON FilmesGenero (IdFilme);
CREATE INDEX IX_FilmesGenero_IdGenero ON FilmesGenero (IdGenero);
GO
