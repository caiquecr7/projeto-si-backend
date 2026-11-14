namespace ProjetoSi.Api.Common.Biometria;

public sealed record EventoTeclaRequest(double PressTime, double? ReleaseTime);

public sealed record AmostraDigitacaoRequest(
    int Repetition,
    IReadOnlyList<EventoTeclaRequest>? RawTimings,
    string? TextEntered,
    double TotalDurationMs);
