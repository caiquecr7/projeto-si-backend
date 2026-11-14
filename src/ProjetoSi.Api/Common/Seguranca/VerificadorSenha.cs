using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Identity;
using ProjetoSi.Api.Domain;

namespace ProjetoSi.Api.Common.Seguranca;

public sealed class VerificadorSenha
{
    private readonly IPasswordHasher<Usuario> hasher;
    private readonly Lazy<string> hashFicticio;

    public VerificadorSenha(IPasswordHasher<Usuario> hasher)
    {
        this.hasher = hasher;
        hashFicticio = new Lazy<string>(() => GerarHash(Guid.NewGuid().ToString("N")));
    }

    public string GerarHash(string senha) => hasher.HashPassword(null!, senha);

    public bool Verificar([NotNullWhen(true)] Usuario? usuario, string senha)
    {
        if (usuario is null)
        {
            hasher.VerifyHashedPassword(null!, hashFicticio.Value, senha);
            return false;
        }

        var resultado = hasher.VerifyHashedPassword(usuario, usuario.SenhaHash, senha);

        if (resultado == PasswordVerificationResult.SuccessRehashNeeded)
        {
            usuario.AtualizarHashSenha(GerarHash(senha));
        }

        return resultado != PasswordVerificationResult.Failed;
    }
}
