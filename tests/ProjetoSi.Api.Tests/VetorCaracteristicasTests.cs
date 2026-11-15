using System.Text.Json;
using ProjetoSi.Api.Common.Biometria;

namespace ProjetoSi.Api.Tests;

public class VetorCaracteristicasTests
{
    private static readonly JsonSerializerOptions JsonOpcoes = new(JsonSerializerDefaults.Web);

    [Fact]
    public void Determina_features_esperadas_identicas_ao_computeSample()
    {
        var cmu = VetorCaracteristicas.DeterminarIntervalosEsperados(".tie5Roanl");

        Assert.Equal(28, cmu.Count);
        Assert.Equal(
            [
                "H.period.0", "DD.period.t.0", "UD.period.t.0",
                "H.t.0", "DD.t.i.0", "UD.t.i.0",
                "H.i.0", "DD.i.e.0", "UD.i.e.0",
                "H.e.0", "DD.e.five.0", "UD.e.five.0",
                "H.five.0", "DD.five.Shift.r.0", "UD.five.Shift.r.0",
                "H.Shift.r.0", "DD.Shift.r.o.0", "UD.Shift.r.o.0",
                "H.o.0", "DD.o.a.0", "UD.o.a.0",
                "H.a.0", "DD.a.n.0", "UD.a.n.0",
                "H.n.0", "DD.n.l.0", "UD.n.l.0",
                "H.l.0"
            ],
            cmu);

        var repetidas = VetorCaracteristicas.DeterminarIntervalosEsperados("banana");
        Assert.Equal(
            [
                "H.b.0", "DD.b.a.0", "UD.b.a.0",
                "H.a.0", "DD.a.n.0", "UD.a.n.0",
                "H.n.0", "DD.n.a.0", "UD.n.a.0",
                "H.a.1", "DD.a.n.1", "UD.a.n.1",
                "H.n.1", "DD.n.a.1", "UD.n.a.1",
                "H.a.2"
            ],
            repetidas);
    }

    [Fact]
    public void Extrai_vetor_mascarado_direto_de_features_json_sem_recalcular_rawTimings()
    {
        const string json = """
        {
          "repetition": 1,
          "features": {
            "H.a.0": 0.1,
            "DD.a.b.0": 0.3,
            "UD.a.b.0": 0.2,
            "H.b.0": 0.05,
            "DD.b.a.0": 0.1,
            "UD.b.a.0": -0.02,
            "H.a.1": 0.12
          },
          "rawTimings": [
            { "key": "a", "code": "KeyA", "timestamp": 0, "eventType": "KEY_DOWN" },
            { "key": "a", "code": "KeyA", "timestamp": 999999, "eventType": "KEY_UP" }
          ],
          "textEntered": "aba",
          "totalDurationMs": 520
        }
        """;

        var amostra = JsonSerializer.Deserialize<AmostraDigitacaoRequest>(json, JsonOpcoes);
        Assert.NotNull(amostra);

        var extraiu = VetorCaracteristicas.TentarExtrair(amostra!.Features, "aba", out var vetor);

        Assert.True(extraiu);
        Assert.Equal(3, vetor!.QuantidadeTeclas);
        Assert.Equal(VetorCaracteristicas.TamanhoPara(3), vetor.Valores.Length);
        Assert.Equal([0.1, 0.3, 0.2, 0.05, 0.1, -0.02, 0.12], vetor.Valores, new ToleranciaComparer(1e-9));
        Assert.Equal(270d / 3, vetor.MediaHoldMs, 6);
        Assert.Equal(["H.0", "DD.0.1", "UD.0.1", "H.1", "DD.1.2", "UD.1.2", "H.2"], vetor.FormatoMascarado);
    }

    [Fact]
    public void Rejeita_features_fora_de_ordem_na_desserializacao_json()
    {
        const string jsonForaDeOrdem = """
        {
          "repetition": 1,
          "features": {
            "H.a.0": 0.1,
            "UD.a.b.0": 0.2,
            "DD.a.b.0": 0.3,
            "H.b.0": 0.08
          },
          "textEntered": "ab",
          "totalDurationMs": 380
        }
        """;

        var amostra = JsonSerializer.Deserialize<AmostraDigitacaoRequest>(jsonForaDeOrdem, JsonOpcoes)!;

        Assert.False(VetorCaracteristicas.TentarExtrair(amostra.Features, "ab", out _));
    }

    [Fact]
    public void Rejeita_features_incompativeis_ou_fora_dos_limites()
    {
        Assert.False(VetorCaracteristicas.TentarExtrair(null, "ab", out _));
        Assert.False(VetorCaracteristicas.TentarExtrair(new Dictionary<string, double>(), "ab", out _));
        Assert.False(VetorCaracteristicas.TentarExtrair(new Dictionary<string, double> { ["H.a.0"] = 0.1 }, "a", out _));

        var paraAb = new Dictionary<string, double>
        {
            ["H.a.0"] = 0.1,
            ["DD.a.b.0"] = 0.2,
            ["UD.a.b.0"] = 0.1,
            ["H.b.0"] = 0.09
        };
        Assert.False(VetorCaracteristicas.TentarExtrair(paraAb, "ac", out _));

        var faltando = new Dictionary<string, double>
        {
            ["H.a.0"] = 0.1,
            ["DD.a.b.0"] = 0.2,
            ["UD.a.b.0"] = 0.1
        };
        Assert.False(VetorCaracteristicas.TentarExtrair(faltando, "ab", out _));

        var holdZero = new Dictionary<string, double>(paraAb) { ["H.a.0"] = 0.0 };
        Assert.False(VetorCaracteristicas.TentarExtrair(holdZero, "ab", out _));

        var ddMuitoLongo = new Dictionary<string, double>(paraAb) { ["DD.a.b.0"] = 5.1 };
        Assert.False(VetorCaracteristicas.TentarExtrair(ddMuitoLongo, "ab", out _));

        var udForaLimite = new Dictionary<string, double>(paraAb) { ["UD.a.b.0"] = -5.1 };
        Assert.False(VetorCaracteristicas.TentarExtrair(udForaLimite, "ab", out _));

        var naoFinito = new Dictionary<string, double>(paraAb) { ["H.b.0"] = double.NaN };
        Assert.False(VetorCaracteristicas.TentarExtrair(naoFinito, "ab", out _));
    }

    private sealed class ToleranciaComparer(double tolerancia) : IEqualityComparer<double>
    {
        public bool Equals(double x, double y) => Math.Abs(x - y) <= tolerancia;

        public int GetHashCode(double obj) => 0;
    }
}
