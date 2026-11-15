using System.Diagnostics.CodeAnalysis;

namespace ProjetoSi.Api.Common.Biometria;

public sealed class VetorCaracteristicas
{
    public const int MinimoTeclas = 2;
    public const int MaximoTeclas = 128;
    public const double MaximoIntervaloSegundos = 5d;
    private const int CaracteristicasPorTecla = 3;
    private const double MilissegundosPorSegundo = 1000d;

    private VetorCaracteristicas(double[] valores, int quantidadeTeclas)
    {
        Valores = valores;
        QuantidadeTeclas = quantidadeTeclas;
    }

    public double[] Valores { get; }
    public int QuantidadeTeclas { get; }
    public double MediaHoldMs => CalcularMediaHoldMs(Valores);
    public IReadOnlyList<string> FormatoMascarado => FormatoPosicionalPara(QuantidadeTeclas);

    public static int TamanhoPara(int quantidadeTeclas) => quantidadeTeclas * CaracteristicasPorTecla - 2;

    public static double CalcularMediaHoldMs(double[] valores) =>
        valores.Where((_, indice) => indice % CaracteristicasPorTecla == 0).Average() * MilissegundosPorSegundo;

    public static string NormalizarTecla(char tecla) => tecla switch
    {
        '.' => "period",
        '5' => "five",
        >= 'A' and <= 'Z' => $"Shift.{char.ToLowerInvariant(tecla)}",
        _ => tecla.ToString()
    };

    public static IReadOnlyList<string> DeterminarIntervalosEsperados(string senha)
    {
        if (string.IsNullOrEmpty(senha))
        {
            return [];
        }

        var contadores = new Dictionary<string, int>(StringComparer.Ordinal);
        var esperadas = new List<string>(Math.Max(1, TamanhoPara(senha.Length)));

        for (var indice = 0; indice < senha.Length - 1; indice++)
        {
            var teclaAtual = NormalizarTecla(senha[indice]);
            var proximaTecla = NormalizarTecla(senha[indice + 1]);

            var chaveBaseHold = $"H.{teclaAtual}";
            var contagemHold = contadores.GetValueOrDefault(chaveBaseHold);
            esperadas.Add($"{chaveBaseHold}.{contagemHold}");
            contadores[chaveBaseHold] = contagemHold + 1;

            var chaveBaseDd = $"DD.{teclaAtual}.{proximaTecla}";
            var contagemDd = contadores.GetValueOrDefault(chaveBaseDd);
            esperadas.Add($"{chaveBaseDd}.{contagemDd}");
            contadores[chaveBaseDd] = contagemDd + 1;

            var chaveBaseUd = $"UD.{teclaAtual}.{proximaTecla}";
            var contagemUd = contadores.GetValueOrDefault(chaveBaseUd);
            esperadas.Add($"{chaveBaseUd}.{contagemUd}");
            contadores[chaveBaseUd] = contagemUd + 1;
        }

        var ultimaTecla = NormalizarTecla(senha[^1]);
        var chaveBaseUltimoHold = $"H.{ultimaTecla}";
        var contagemUltimoHold = contadores.GetValueOrDefault(chaveBaseUltimoHold);
        esperadas.Add($"{chaveBaseUltimoHold}.{contagemUltimoHold}");

        return esperadas;
    }

    public static IReadOnlyList<string> FormatoPosicionalPara(int quantidadeTeclas)
    {
        if (quantidadeTeclas < MinimoTeclas)
        {
            return [];
        }

        var formato = new List<string>(TamanhoPara(quantidadeTeclas));
        for (var indice = 0; indice < quantidadeTeclas; indice++)
        {
            formato.Add($"H.{indice}");
            if (indice < quantidadeTeclas - 1)
            {
                formato.Add($"DD.{indice}.{indice + 1}");
                formato.Add($"UD.{indice}.{indice + 1}");
            }
        }

        return formato;
    }

    public static bool TentarExtrair(
        IReadOnlyDictionary<string, double>? features,
        string? senhaEsperada,
        [NotNullWhen(true)] out VetorCaracteristicas? vetor)
    {
        vetor = null;

        if (features is null || string.IsNullOrEmpty(senhaEsperada))
        {
            return false;
        }

        if (senhaEsperada.Length is < MinimoTeclas or > MaximoTeclas)
        {
            return false;
        }

        var esperadas = DeterminarIntervalosEsperados(senhaEsperada);
        if (features.Count != esperadas.Count)
        {
            return false;
        }

        var valores = new double[esperadas.Count];
        var indice = 0;

        foreach (var (chave, valor) in features)
        {
            if (!string.Equals(chave, esperadas[indice], StringComparison.Ordinal) || !double.IsFinite(valor))
            {
                return false;
            }

            var ehUd = indice % CaracteristicasPorTecla == 2;
            var dentroDoIntervalo = ehUd
                ? valor is >= -MaximoIntervaloSegundos and <= MaximoIntervaloSegundos
                : valor is > 0d and <= MaximoIntervaloSegundos;

            if (!dentroDoIntervalo)
            {
                return false;
            }

            valores[indice] = valor;
            indice++;
        }

        vetor = new VetorCaracteristicas(valores, senhaEsperada.Length);
        return true;
    }
}
