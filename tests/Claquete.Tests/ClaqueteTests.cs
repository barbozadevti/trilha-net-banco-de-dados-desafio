using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;

namespace Claquete.Tests;

/// <summary>Sobe a API com um banco SQLite novo, num arquivo temporário, para cada classe de teste.</summary>
public sealed class ClaqueteFactory : WebApplicationFactory<Program>
{
    private readonly string _arquivo = Path.Combine(Path.GetTempPath(), $"claquete-teste-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.UseSetting("Banco:Provedor", "Sqlite");
        builder.UseSetting("Banco:ConnectionString", $"Data Source={_arquivo}");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        SqliteConnection.ClearAllPools();
        File.Delete(_arquivo);
    }
}

public sealed class LaboratorioTests(ClaqueteFactory factory) : IClassFixture<ClaqueteFactory>
{
    private readonly HttpClient _cliente = factory.CreateClient();

    private async Task<(string[] Colunas, JsonElement[][] Linhas)> ExecutarAsync(int numero)
    {
        var json = await _cliente.GetFromJsonAsync<JsonElement>($"/api/consultas/{numero}");
        var colunas = json.GetProperty("colunas").EnumerateArray().Select(c => c.GetString()!).ToArray();
        var linhas = json.GetProperty("linhas").EnumerateArray().Select(l => l.EnumerateArray().ToArray()).ToArray();
        return (colunas, linhas);
    }

    [Fact]
    public async Task Catalogo_TemAs12ConsultasDoScript()
    {
        var consultas = await _cliente.GetFromJsonAsync<JsonElement[]>("/api/consultas");
        Assert.Equal(Enumerable.Range(1, 12), consultas!.Select(c => c.GetProperty("numero").GetInt32()));
    }

    [Fact]
    public async Task Consulta1_TodosOsFilmesComNomeEAno()
    {
        var (colunas, linhas) = await ExecutarAsync(1);
        Assert.Equal(["Nome", "Ano"], colunas);
        Assert.Equal(28, linhas.Length);
    }

    [Fact]
    public async Task Consulta2_OrdenadaPorAnoCrescente()
    {
        var (_, linhas) = await ExecutarAsync(2);
        var anos = linhas.Select(l => l[1].GetInt32()).ToList();
        Assert.Equal(anos.Order(), anos);
        Assert.Equal("Os Sete Samurais", linhas[0][0].GetString());
    }

    [Fact]
    public async Task Consulta3_DeVoltaParaOFuturo()
    {
        var (colunas, linhas) = await ExecutarAsync(3);
        Assert.Equal(["Nome", "Ano", "Duracao"], colunas);
        var filme = Assert.Single(linhas);
        Assert.Equal(1985, filme[1].GetInt32());
        Assert.Equal(116, filme[2].GetInt32());
    }

    [Fact]
    public async Task Consulta4_QuatroFilmesDe1997()
    {
        var (_, linhas) = await ExecutarAsync(4);
        Assert.Equal(
            ["Boogie Nights - Prazer Sem Limites", "Princesa Mononoke", "Titanic", "Gênio Indomável"],
            linhas.Select(l => l[0].GetString()));
    }

    [Fact]
    public async Task Consulta5_FilmesApos2000()
    {
        var (_, linhas) = await ExecutarAsync(5);
        Assert.Equal(6, linhas.Length);
        Assert.All(linhas, l => Assert.True(l[1].GetInt32() > 2000));
    }

    [Fact]
    public async Task Consulta6_DuracaoEntre100e150Crescente()
    {
        var (_, linhas) = await ExecutarAsync(6);
        var duracoes = linhas.Select(l => l[2].GetInt32()).ToList();
        Assert.All(duracoes, d => Assert.InRange(d, 101, 149));
        Assert.Equal(duracoes.Order(), duracoes);
    }

    [Fact]
    public async Task Consulta7_QuantidadePorAnoDecrescente()
    {
        var (colunas, linhas) = await ExecutarAsync(7);
        Assert.Equal(["Ano", "Quantidade"], colunas);
        Assert.Equal(1997, linhas[0][0].GetInt32());
        Assert.Equal(4, linhas[0][1].GetInt32());
        var quantidades = linhas.Select(l => l[1].GetInt32()).ToList();
        Assert.Equal(quantidades.OrderDescending(), quantidades);
        Assert.Equal(28, quantidades.Sum());
    }

    [Fact]
    public async Task Consultas8e9_AtoresPorGenero()
    {
        var (_, masculinos) = await ExecutarAsync(8);
        var (_, femininos) = await ExecutarAsync(9);
        Assert.Equal(16, masculinos.Length);
        Assert.Equal(6, femininos.Length);
        var nomes = femininos.Select(l => l[0].GetString()!).ToList();
        Assert.Equal(nomes.Order(StringComparer.Ordinal), nomes);
    }

    [Fact]
    public async Task Consultas10a12_Relacionamentos()
    {
        var (_, generos) = await ExecutarAsync(10);
        var (_, misterio) = await ExecutarAsync(11);
        var (colunas, elenco) = await ExecutarAsync(12);

        Assert.Equal(19, generos.Length);
        Assert.All(misterio, l => Assert.Equal("Mistério", l[1].GetString()));
        Assert.NotEmpty(misterio);
        Assert.Equal(["Nome", "PrimeiroNome", "UltimoNome", "Papel"], colunas);
        Assert.Equal(21, elenco.Length);
    }
}

public sealed class CatalogoTests(ClaqueteFactory factory) : IClassFixture<ClaqueteFactory>
{
    private readonly HttpClient _cliente = factory.CreateClient();

    [Fact]
    public async Task Busca_IgnoraAcentosEMaiusculas()
    {
        var filmes = await _cliente.GetFromJsonAsync<JsonElement[]>("/api/filmes?busca=GENIO");
        Assert.Equal("Gênio Indomável", Assert.Single(filmes!).GetProperty("nome").GetString());
    }

    [Fact]
    public async Task Filtros_PorGeneroEAno()
    {
        var generos = await _cliente.GetFromJsonAsync<JsonElement[]>("/api/generos");
        var drama = generos!.Single(g => g.GetProperty("genero").GetString() == "Drama").GetProperty("id").GetInt32();

        var filmes = await _cliente.GetFromJsonAsync<JsonElement[]>($"/api/filmes?genero={drama}&anoDe=1990&ordem=ano");
        Assert.NotEmpty(filmes!);
        Assert.All(filmes!, f =>
        {
            Assert.Contains("Drama", f.GetProperty("generos").EnumerateArray().Select(g => g.GetString()));
            Assert.True(f.GetProperty("ano").GetInt32() >= 1990);
        });
    }

    [Fact]
    public async Task Detalhe_TrazGenerosEElenco()
    {
        var filme = await _cliente.GetFromJsonAsync<JsonElement>("/api/filmes/6");
        Assert.Equal("Blade Runner", filme.GetProperty("nome").GetString());
        var ator = Assert.Single(filme.GetProperty("elenco").EnumerateArray());
        Assert.Equal("Harrison", ator.GetProperty("primeiroNome").GetString());
    }

    [Fact]
    public async Task CicloCompleto_CriarEditarEscalarExcluir()
    {
        var criado = await _cliente.PostAsJsonAsync("/api/filmes", new { nome = "Cidade de Deus", ano = 2002, duracao = 130, generos = new[] { 6, 7 } });
        Assert.Equal(HttpStatusCode.Created, criado.StatusCode);
        var filme = await criado.Content.ReadFromJsonAsync<JsonElement>();
        var id = filme.GetProperty("id").GetInt32();
        Assert.Equal(2, filme.GetProperty("generos").GetArrayLength());

        var editado = await _cliente.PutAsJsonAsync($"/api/filmes/{id}", new { nome = "Cidade de Deus", ano = 2002, duracao = 135, generos = new[] { 6 } });
        var aposEdicao = await editado.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(135, aposEdicao.GetProperty("duracao").GetInt32());
        Assert.Equal(1, aposEdicao.GetProperty("generos").GetArrayLength());

        var escalado = await _cliente.PostAsJsonAsync($"/api/filmes/{id}/elenco", new { idAtor = 1, papel = "Narrador" });
        Assert.Equal(HttpStatusCode.Created, escalado.StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await _cliente.DeleteAsync($"/api/filmes/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _cliente.GetAsync($"/api/filmes/{id}")).StatusCode);

        var ator = await _cliente.GetFromJsonAsync<JsonElement>("/api/atores/1");
        Assert.DoesNotContain(ator.GetProperty("filmografia").EnumerateArray(), p => p.GetProperty("idFilme").GetInt32() == id);
    }

    [Fact]
    public async Task Validacao_RecusaFilmeInvalido()
    {
        var resposta = await _cliente.PostAsJsonAsync("/api/filmes", new { nome = "", ano = 1500, duracao = 0 });
        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        var problema = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        var erros = problema.GetProperty("errors");
        Assert.True(erros.TryGetProperty("Nome", out _));
        Assert.True(erros.TryGetProperty("Ano", out _));
        Assert.True(erros.TryGetProperty("Duracao", out _));
    }

    [Fact]
    public async Task Estatisticas_ResumemOAcervo()
    {
        var est = await _cliente.GetFromJsonAsync<JsonElement>("/api/estatisticas");
        Assert.Equal(22, est.GetProperty("totalAtores").GetInt32());
        Assert.Equal("Lawrence da Arábia", est.GetProperty("maisLongo").GetProperty("nome").GetString());
        Assert.Equal("Noivo Neurótico, Noiva Nervosa", est.GetProperty("maisCurto").GetProperty("nome").GetString());
        Assert.NotEmpty(est.GetProperty("porDecada").EnumerateArray());
    }
}
