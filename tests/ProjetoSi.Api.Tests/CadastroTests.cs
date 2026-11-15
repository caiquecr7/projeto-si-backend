using ProjetoSi.Api.Common.Biometria;
using ProjetoSi.Api.Features.Cadastro;

namespace ProjetoSi.Api.Tests;

public class CadastroTests
{
    private const string Senha = "senha123";
    private const int AmostrasEsperadas = 5;

    [Fact]
    public void Aceita_cadastro_com_senha_e_cinco_amostras()
    {
        var request = CriarRequest();

        var erro = CadastrarUsuario.Validar(request, AmostrasEsperadas, out var vetores);

        Assert.Null(erro);
        Assert.Equal(AmostrasEsperadas, vetores.Count);
        Assert.All(vetores, vetor => Assert.Equal(VetorCaracteristicas.TamanhoPara(Senha.Length), vetor.Valores.Length));
    }

    [Fact]
    public void Rejeita_quantidade_de_amostras_diferente_da_configurada()
    {
        var request = new CadastrarUsuario.Request(
            "ana",
            Senha,
            Enumerable.Range(1, AmostrasEsperadas - 1).Select(indice => CriarAmostra(indice)).ToArray());

        var erro = CadastrarUsuario.Validar(request, AmostrasEsperadas, out var vetores);

        Assert.Contains("Número de amostras inválido", erro);
        Assert.Empty(vetores);
    }

    [Fact]
    public void Rejeita_amostras_com_texto_diferente_da_senha_informada()
    {
        const string outroTexto = "outrasen";
        var request = new CadastrarUsuario.Request(
            "ana",
            Senha,
            Enumerable.Range(1, AmostrasEsperadas).Select(indice => CriarAmostra(indice, outroTexto)).ToArray());

        var erro = CadastrarUsuario.Validar(request, AmostrasEsperadas, out var vetores);

        Assert.Contains("não correspondem à senha informada", erro);
        Assert.Empty(vetores);
    }

    [Fact]
    public void Rejeita_amostras_com_textos_divergentes_entre_si()
    {
        var amostras = Enumerable.Range(1, AmostrasEsperadas).Select(indice => CriarAmostra(indice)).ToArray();
        amostras[^1] = CriarAmostra(AmostrasEsperadas, "outrasen");
        var request = new CadastrarUsuario.Request("ana", Senha, amostras);

        var erro = CadastrarUsuario.Validar(request, AmostrasEsperadas, out var vetores);

        Assert.Contains("mesmo texto digitado", erro);
        Assert.Empty(vetores);
    }

    [Fact]
    public void Rejeita_senha_fora_do_tamanho_permitido()
    {
        const string senhaCurta = "curta";
        var request = new CadastrarUsuario.Request(
            "ana",
            senhaCurta,
            Enumerable.Range(1, AmostrasEsperadas).Select(indice => CriarAmostra(indice, senhaCurta)).ToArray());

        var erro = CadastrarUsuario.Validar(request, AmostrasEsperadas, out var vetores);

        Assert.Contains("A senha deve ter entre", erro);
        Assert.Empty(vetores);
    }

    [Fact]
    public void Rejeita_features_incompativeis_com_a_senha()
    {
        var request = new CadastrarUsuario.Request(
            "ana",
            Senha,
            Enumerable.Range(1, AmostrasEsperadas)
                .Select(indice => new AmostraDigitacaoRequest(
                    indice,
                    CriarFeatures("outrasen"),
                    Senha,
                    1200d))
                .ToArray());

        var erro = CadastrarUsuario.Validar(request, AmostrasEsperadas, out var vetores);

        Assert.Contains("features inválidas", erro);
        Assert.Empty(vetores);
    }

    [Fact]
    public void Rejeita_username_invalido()
    {
        var request = new CadastrarUsuario.Request(
            "ab",
            Senha,
            Enumerable.Range(1, AmostrasEsperadas).Select(indice => CriarAmostra(indice)).ToArray());

        var erro = CadastrarUsuario.Validar(request, AmostrasEsperadas, out var vetores);

        Assert.Contains("O usuário deve ter entre", erro);
        Assert.Empty(vetores);
    }

    private static CadastrarUsuario.Request CriarRequest() =>
        new("ana", Senha, Enumerable.Range(1, AmostrasEsperadas).Select(indice => CriarAmostra(indice)).ToArray());

    private static AmostraDigitacaoRequest CriarAmostra(int repeticao, string texto = Senha) =>
        new(repeticao, CriarFeatures(texto), texto, 1200d);

    private static IReadOnlyDictionary<string, double> CriarFeatures(string senha) =>
        VetorCaracteristicas.DeterminarIntervalosEsperados(senha)
            .Select((chave, indice) => (Chave: chave, Valor: indice % 3 == 2 ? 0.05d : 0.1d))
            .ToDictionary(item => item.Chave, item => item.Valor);
}


public class DisponibilidadeUsuarioTests
{
    [Theory]
    [InlineData("  Ana.Silva  ", "ana.silva")]
    [InlineData("usuario+tag@ufabc", "usuario+tag@ufabc")]
    public void Normaliza_username_valido(string entrada, string esperado)
    {
        var erro = VerificarDisponibilidadeUsuario.ValidarNome(entrada, out var nome);

        Assert.Null(erro);
        Assert.Equal(esperado, nome);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ab")]
    [InlineData("nome com espaco")]
    [InlineData("user#1")]
    public void Rejeita_username_vazio_ou_fora_do_padrao(string? entrada)
    {
        var erro = VerificarDisponibilidadeUsuario.ValidarNome(entrada, out _);

        Assert.NotNull(erro);
    }
}
