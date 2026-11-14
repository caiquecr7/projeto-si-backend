namespace ProjetoSi.Api.Domain;

public sealed class ModeloBiometrico
{
    private ModeloBiometrico()
    {
    }

    public long Id { get; private set; }
    public Guid UsuarioId { get; private set; }
    public int Versao { get; private set; }
    public string Algoritmo { get; private set; } = string.Empty;
    public string CaminhoArquivo { get; private set; } = string.Empty;
    public DateTimeOffset CriadoEm { get; private set; }

    public static ModeloBiometrico Criar(Guid usuarioId, int versao, string algoritmo, string caminhoArquivo, DateTimeOffset agora) => new()
    {
        UsuarioId = usuarioId,
        Versao = versao,
        Algoritmo = algoritmo,
        CaminhoArquivo = caminhoArquivo,
        CriadoEm = agora
    };
}
