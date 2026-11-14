namespace ProjetoSi.Api.Domain;

public sealed class Usuario
{
    private Usuario()
    {
    }

    public Guid Id { get; private set; }
    public string Nome { get; private set; } = string.Empty;
    public string SenhaHash { get; private set; } = string.Empty;
    public DateTimeOffset DataMudancaSenha { get; private set; }
    public DateTimeOffset? BiometriaCadastradaEm { get; private set; }
    public int TentativasFalhas { get; private set; }
    public DateTimeOffset? BloqueadoAte { get; private set; }
    public DateTimeOffset CriadoEm { get; private set; }
    public uint Versao { get; private set; }

    public bool PossuiBiometria => BiometriaCadastradaEm is not null;

    public static Usuario Criar(string nome, string senhaHash, DateTimeOffset agora) => new()
    {
        Id = Guid.CreateVersion7(agora),
        Nome = nome,
        SenhaHash = senhaHash,
        DataMudancaSenha = agora,
        CriadoEm = agora
    };

    public void AlterarSenha(string senhaHash, DateTimeOffset agora)
    {
        SenhaHash = senhaHash;
        DataMudancaSenha = agora;
        BiometriaCadastradaEm = null;
    }

    public void AtualizarHashSenha(string senhaHash) => SenhaHash = senhaHash;

    public void ConcluirCadastroBiometrico(DateTimeOffset agora) => BiometriaCadastradaEm = agora;

    public bool EstaBloqueado(DateTimeOffset agora) => BloqueadoAte > agora;

    public void RegistrarFalha(DateTimeOffset agora, int maximoTentativas, TimeSpan duracaoBloqueio)
    {
        TentativasFalhas++;

        if (TentativasFalhas < maximoTentativas)
        {
            return;
        }

        BloqueadoAte = agora.Add(duracaoBloqueio);
        TentativasFalhas = 0;
    }

    public void RegistrarSucesso()
    {
        TentativasFalhas = 0;
        BloqueadoAte = null;
    }
}
