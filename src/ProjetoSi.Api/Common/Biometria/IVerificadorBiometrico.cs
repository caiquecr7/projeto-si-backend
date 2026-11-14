using ProjetoSi.Api.Domain;

namespace ProjetoSi.Api.Common.Biometria;

public interface IVerificadorBiometrico
{
    Task<ResultadoVerificacao> VerificarAsync(Usuario usuario, VetorCaracteristicas tentativa, CancellationToken cancellationToken);
}

public sealed record ResultadoVerificacao(
    DecisaoBiometrica Decisao,
    double Confianca,
    double Limiar,
    double MediaHoldPerfilMs,
    long? ModeloId);
