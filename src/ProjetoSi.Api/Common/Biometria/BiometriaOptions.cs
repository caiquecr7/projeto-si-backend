using System.ComponentModel.DataAnnotations;

namespace ProjetoSi.Api.Common.Biometria;

public sealed class BiometriaOptions
{
    public const string Secao = "Biometria";

    [Range(1, 50)]
    public int AmostrasCadastro { get; init; } = 5;

    [Range(0d, 1d)]
    public double LimiarConfianca { get; init; } = 0.35;

    [Range(0.0001d, 1d)]
    public double DesvioMinimoSegundos { get; init; } = 0.005;
}
