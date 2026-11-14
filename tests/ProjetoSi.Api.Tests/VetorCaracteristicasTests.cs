using ProjetoSi.Api.Common.Biometria;

namespace ProjetoSi.Api.Tests;

public class VetorCaracteristicasTests
{
    [Fact]
    public void Extrai_vetor_no_formato_cmu_intercalado()
    {
        EventoTeclaRequest[] eventos =
        [
            new(1000, 1100),
            new(1300, 1350),
            new(1400, 1500)
        ];

        var extraiu = VetorCaracteristicas.TentarExtrair(eventos, out var vetor);

        Assert.True(extraiu);
        Assert.Equal(3, vetor!.QuantidadeTeclas);
        Assert.Equal(VetorCaracteristicas.TamanhoPara(3), vetor.Valores.Length);
        Assert.Equal([0.1, 0.3, 0.2, 0.05, 0.1, 0.05, 0.1], vetor.Valores, new ToleranciaComparer(1e-9));
        Assert.Equal(250d / 3, vetor.MediaHoldMs, 6);
    }

    [Fact]
    public void Ordena_teclas_pelo_instante_de_pressao()
    {
        EventoTeclaRequest[] eventos = [new(1300, 1350), new(1000, 1100)];

        VetorCaracteristicas.TentarExtrair(eventos, out var vetor);

        Assert.Equal([0.1, 0.3, 0.2, 0.05], vetor!.Valores, new ToleranciaComparer(1e-9));
    }

    [Fact]
    public void Preserva_teclas_repetidas_que_colidiriam_em_um_dicionario_por_nome()
    {
        EventoTeclaRequest[] eventos = [new(0, 80), new(200, 290), new(400, 470), new(600, 690)];

        VetorCaracteristicas.TentarExtrair(eventos, out var vetor);

        Assert.Equal(10, vetor!.Valores.Length);
    }

    [Fact]
    public void Aceita_up_down_negativo_quando_teclas_se_sobrepoem()
    {
        EventoTeclaRequest[] eventos = [new(0, 200), new(100, 250)];

        VetorCaracteristicas.TentarExtrair(eventos, out var vetor);

        Assert.Equal(-0.1, vetor!.Valores[2], 9);
    }

    public static TheoryData<EventoTeclaRequest[]?> EventosInvalidos => new()
    {
        null,
        Array.Empty<EventoTeclaRequest>(),
        new EventoTeclaRequest[] { new(0, 10) },
        new EventoTeclaRequest[] { new(0, 10), new(100, 50) },
        new EventoTeclaRequest[] { new(0, double.NaN), new(100, 150) }
    };

    [Theory]
    [MemberData(nameof(EventosInvalidos))]
    public void Rejeita_eventos_invalidos(EventoTeclaRequest[]? eventos)
    {
        Assert.False(VetorCaracteristicas.TentarExtrair(eventos, out _));
    }

    private sealed class ToleranciaComparer(double tolerancia) : IEqualityComparer<double>
    {
        public bool Equals(double x, double y) => Math.Abs(x - y) <= tolerancia;

        public int GetHashCode(double obj) => 0;
    }
}
