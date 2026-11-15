using ProjetoSi.Api.Domain;

namespace ProjetoSi.Api.Common.Biometria;

public interface IVerificadorBiometrico
{
    Task<long?> TreinarModeloInicialAsync(Usuario usuario, IReadOnlyList<VetorCaracteristicas> vetores, CancellationToken cancellationToken);

    Task<ResultadoVerificacao> VerificarAsync(Usuario usuario, VetorCaracteristicas tentativa, CancellationToken cancellationToken);

    Task ReverterModelosCriadosAposAsync(Usuario usuario, DateTimeOffset criadoApos, CancellationToken cancellationToken);
}

public sealed record ResultadoVerificacao(
    DecisaoBiometrica Decisao,
    double Confianca,
    double Limiar,
    double MediaHoldPerfilMs,
    long? ModeloId,
    bool DeveGravarParaRetreino = false);
