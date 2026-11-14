using System.ComponentModel.DataAnnotations;

namespace ProjetoSi.Api.Common.Seguranca;

public sealed class JwtOptions
{
    public const string Secao = "Jwt";

    [Required]
    public string Emissor { get; init; } = string.Empty;

    [Required]
    public string Audiencia { get; init; } = string.Empty;

    [Required, MinLength(32)]
    public string Chave { get; init; } = string.Empty;

    [Range(1, 1440)]
    public int MinutosExpiracao { get; init; } = 60;
}

public sealed class SegurancaLoginOptions
{
    public const string Secao = "SegurancaLogin";

    [Range(1, 100)]
    public int MaximoTentativas { get; init; } = 5;

    [Range(1, 1440)]
    public int MinutosBloqueio { get; init; } = 15;

    public TimeSpan DuracaoBloqueio => TimeSpan.FromMinutes(MinutosBloqueio);
}
