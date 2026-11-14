using ProjetoSi.Api.Common.Biometria;

namespace ProjetoSi.Api.Tests;

public class ManhattanEscalonadoTests
{
    private static readonly double[][] Perfil =
    [
        [0.10, 0.30],
        [0.12, 0.32],
        [0.11, 0.28],
        [0.09, 0.31],
        [0.10, 0.29]
    ];

    [Fact]
    public void Tentativa_igual_a_media_tem_distancia_zero_e_confianca_maxima()
    {
        var distancia = ManhattanEscalonado.DistanciaMedia(Perfil, [0.104, 0.30], 0.005);

        Assert.Equal(0, distancia, 9);
        Assert.Equal(1, ManhattanEscalonado.Confianca(distancia), 9);
    }

    [Fact]
    public void Tentativa_distante_tem_confianca_menor_que_tentativa_proxima()
    {
        var proxima = ManhattanEscalonado.Confianca(ManhattanEscalonado.DistanciaMedia(Perfil, [0.11, 0.31], 0.005));
        var distante = ManhattanEscalonado.Confianca(ManhattanEscalonado.DistanciaMedia(Perfil, [0.25, 0.60], 0.005));

        Assert.True(proxima > 0.35);
        Assert.True(distante < 0.1);
    }

    [Fact]
    public void Desvio_minimo_evita_divisao_por_zero()
    {
        double[][] perfilConstante = [[0.1], [0.1], [0.1]];

        var distancia = ManhattanEscalonado.DistanciaMedia(perfilConstante, [0.11], 0.005);

        Assert.Equal(2, distancia, 9);
    }

    [Fact]
    public void Dimensoes_diferentes_lancam_excecao()
    {
        Assert.Throws<ArgumentException>(() => ManhattanEscalonado.DistanciaMedia(Perfil, [0.1], 0.005));
    }
}
