namespace ProjetoSi.Api.Common.Biometria;

public sealed record AmostraDigitacaoRequest(
    int Repetition,
    IReadOnlyDictionary<string, double>? Features,
    string? TextEntered,
    double TotalDurationMs);
