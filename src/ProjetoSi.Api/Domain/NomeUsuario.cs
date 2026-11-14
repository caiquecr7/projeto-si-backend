using System.Text.RegularExpressions;

namespace ProjetoSi.Api.Domain;

public static partial class NomeUsuario
{
    public const int TamanhoMinimo = 3;
    public const int TamanhoMaximo = 50;

    public static string Normalizar(string nome) => nome.Trim().ToLowerInvariant();

    public static bool EhValido(string nomeNormalizado) => Formato().IsMatch(nomeNormalizado);

    [GeneratedRegex(@"^[a-z0-9._@+-]{3,50}$")]
    private static partial Regex Formato();
}
