using System.Diagnostics.CodeAnalysis;

namespace ProjetoSi.Api.Common.Biometria;

public sealed class VetorCaracteristicas
{
    public const int MinimoTeclas = 2;
    public const int MaximoTeclas = 128;
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

    public static int TamanhoPara(int quantidadeTeclas) => quantidadeTeclas * CaracteristicasPorTecla - 2;

    public static double CalcularMediaHoldMs(double[] valores) =>
        valores.Where((_, indice) => indice % CaracteristicasPorTecla == 0).Average() * MilissegundosPorSegundo;

    public static bool TentarExtrair(IReadOnlyList<EventoTeclaRequest>? eventos, [NotNullWhen(true)] out VetorCaracteristicas? vetor)
    {
        vetor = null;

        if (eventos is null || eventos.Count is < MinimoTeclas or > MaximoTeclas)
        {
            return false;
        }

        var teclas = eventos.OrderBy(evento => evento.PressTime).ToArray();
        var valores = new double[TamanhoPara(teclas.Length)];

        for (var indice = 0; indice < teclas.Length; indice++)
        {
            var atual = teclas[indice];
            var soltura = atual.ReleaseTime ?? atual.PressTime;

            if (!double.IsFinite(atual.PressTime) || !double.IsFinite(soltura) || soltura < atual.PressTime)
            {
                return false;
            }

            var posicao = indice * CaracteristicasPorTecla;
            valores[posicao] = (soltura - atual.PressTime) / MilissegundosPorSegundo;

            if (indice == teclas.Length - 1)
            {
                break;
            }

            var proxima = teclas[indice + 1];
            valores[posicao + 1] = (proxima.PressTime - atual.PressTime) / MilissegundosPorSegundo;
            valores[posicao + 2] = (proxima.PressTime - soltura) / MilissegundosPorSegundo;
        }

        vetor = new VetorCaracteristicas(valores, teclas.Length);
        return true;
    }
}
