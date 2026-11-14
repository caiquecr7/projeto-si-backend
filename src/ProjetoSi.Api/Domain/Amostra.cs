using ProjetoSi.Api.Common.Biometria;

namespace ProjetoSi.Api.Domain;

public sealed class Amostra
{
    private Amostra()
    {
    }

    public long Id { get; private set; }
    public Guid UsuarioId { get; private set; }
    public OrigemAmostra Origem { get; private set; }
    public double[] Vetor { get; private set; } = [];
    public int? Repeticao { get; private set; }
    public double DuracaoTotalMs { get; private set; }
    public DecisaoBiometrica? Decisao { get; private set; }
    public double? Confianca { get; private set; }
    public long? ModeloId { get; private set; }
    public DateTimeOffset ColetadaEm { get; private set; }

    public static Amostra DeCadastro(Guid usuarioId, VetorCaracteristicas vetor, int repeticao, double duracaoTotalMs, DateTimeOffset agora) => new()
    {
        UsuarioId = usuarioId,
        Origem = OrigemAmostra.Cadastro,
        Vetor = vetor.Valores,
        Repeticao = repeticao,
        DuracaoTotalMs = duracaoTotalMs,
        ColetadaEm = agora
    };

    public static Amostra DeLogin(Guid usuarioId, VetorCaracteristicas vetor, double duracaoTotalMs, ResultadoVerificacao resultado, DateTimeOffset agora) => new()
    {
        UsuarioId = usuarioId,
        Origem = OrigemAmostra.Login,
        Vetor = vetor.Valores,
        DuracaoTotalMs = duracaoTotalMs,
        Decisao = resultado.Decisao,
        Confianca = resultado.Confianca,
        ModeloId = resultado.ModeloId,
        ColetadaEm = agora
    };
}
