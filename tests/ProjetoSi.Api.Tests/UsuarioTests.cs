using ProjetoSi.Api.Domain;

namespace ProjetoSi.Api.Tests;

public class UsuarioTests
{
    private static readonly DateTimeOffset Agora = new(2026, 11, 13, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Bloqueio = TimeSpan.FromMinutes(15);

    [Fact]
    public void Bloqueia_apos_atingir_maximo_de_tentativas()
    {
        var usuario = Usuario.Criar("ana", "hash", Agora);

        for (var tentativa = 0; tentativa < 4; tentativa++)
        {
            usuario.RegistrarFalha(Agora, 5, Bloqueio);
        }

        Assert.False(usuario.EstaBloqueado(Agora));

        usuario.RegistrarFalha(Agora, 5, Bloqueio);

        Assert.True(usuario.EstaBloqueado(Agora));
        Assert.False(usuario.EstaBloqueado(Agora.Add(Bloqueio)));
    }

    [Fact]
    public void Sucesso_zera_tentativas_e_bloqueio()
    {
        var usuario = Usuario.Criar("ana", "hash", Agora);
        usuario.RegistrarFalha(Agora, 1, Bloqueio);

        usuario.RegistrarSucesso();

        Assert.False(usuario.EstaBloqueado(Agora));
        Assert.Equal(0, usuario.TentativasFalhas);
    }

    [Fact]
    public void Alterar_senha_invalida_biometria_cadastrada()
    {
        var usuario = Usuario.Criar("ana", "hash", Agora);
        usuario.ConcluirCadastroBiometrico(Agora);

        usuario.AlterarSenha("novo-hash", Agora.AddDays(1));

        Assert.False(usuario.PossuiBiometria);
        Assert.Equal(Agora.AddDays(1), usuario.DataMudancaSenha);
    }

    [Theory]
    [InlineData("  Ana.Silva  ", "ana.silva", true)]
    [InlineData("ab", "ab", false)]
    [InlineData("nome com espaco", "nome com espaco", false)]
    public void Normaliza_e_valida_nome(string entrada, string esperado, bool valido)
    {
        var normalizado = NomeUsuario.Normalizar(entrada);

        Assert.Equal(esperado, normalizado);
        Assert.Equal(valido, NomeUsuario.EhValido(normalizado));
    }
}
