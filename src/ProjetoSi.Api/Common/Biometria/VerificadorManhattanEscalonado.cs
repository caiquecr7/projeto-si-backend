using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ProjetoSi.Api.Domain;
using ProjetoSi.Api.Infrastructure.Persistence;

namespace ProjetoSi.Api.Common.Biometria;

public sealed class VerificadorManhattanEscalonado(AppDbContext db, IOptions<BiometriaOptions> opcoes) : IVerificadorBiometrico
{
    public async Task<ResultadoVerificacao> VerificarAsync(Usuario usuario, VetorCaracteristicas tentativa, CancellationToken cancellationToken)
    {
        var configuracao = opcoes.Value;

        var perfil = await db.Amostras
            .AsNoTracking()
            .Where(amostra => amostra.UsuarioId == usuario.Id
                && amostra.Origem == OrigemAmostra.Cadastro
                && amostra.ColetadaEm >= usuario.DataMudancaSenha)
            .Select(amostra => amostra.Vetor)
            .ToListAsync(cancellationToken);

        if (perfil.Count == 0)
        {
            return new ResultadoVerificacao(DecisaoBiometrica.Impostor, 0, configuracao.LimiarConfianca, 0, null);
        }

        var mediaHoldPerfil = perfil.Average(VetorCaracteristicas.CalcularMediaHoldMs);

        if (perfil.Any(vetor => vetor.Length != tentativa.Valores.Length))
        {
            return new ResultadoVerificacao(DecisaoBiometrica.Impostor, 0, configuracao.LimiarConfianca, mediaHoldPerfil, null);
        }

        var distancia = ManhattanEscalonado.DistanciaMedia(perfil, tentativa.Valores, configuracao.DesvioMinimoSegundos);
        var confianca = ManhattanEscalonado.Confianca(distancia);
        var decisao = confianca >= configuracao.LimiarConfianca ? DecisaoBiometrica.Genuine : DecisaoBiometrica.Impostor;

        return new ResultadoVerificacao(decisao, confianca, configuracao.LimiarConfianca, mediaHoldPerfil, null);
    }
}
