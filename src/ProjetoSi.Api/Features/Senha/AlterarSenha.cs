using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ProjetoSi.Api.Common;
using ProjetoSi.Api.Common.Biometria;
using ProjetoSi.Api.Common.Endpoints;
using ProjetoSi.Api.Common.Seguranca;
using ProjetoSi.Api.Domain;
using ProjetoSi.Api.Features.Cadastro;
using ProjetoSi.Api.Infrastructure.Persistence;

namespace ProjetoSi.Api.Features.Senha;

public static class AlterarSenha
{
    public sealed record Request(
        string? Username,
        string? CurrentPassword,
        string? NewPassword,
        IReadOnlyList<AmostraDigitacaoRequest>? Samples);

    public sealed record Response(bool Success, string Message, int SamplesRegistered, double CalculatedAverageHoldMs);

    public sealed class Endpoint : IEndpoint
    {
        public void Mapear(IEndpointRouteBuilder rotas) =>
            rotas.MapPost("/change-password", Executar)
                .WithName(nameof(AlterarSenha))
                .WithTags("Senha");
    }

    public static async Task<Results<Ok<Response>, BadRequest<RespostaOperacao>, JsonHttpResult<RespostaOperacao>>> Executar(
        Request request,
        AppDbContext db,
        VerificadorSenha verificadorSenha,
        IVerificadorBiometrico verificadorBiometrico,
        IOptions<BiometriaOptions> biometria,
        IOptions<SegurancaLoginOptions> seguranca,
        TimeProvider relogio,
        ILogger<Endpoint> logger,
        CancellationToken cancellationToken)
    {
        var quantidadeEsperada = biometria.Value.AmostrasCadastro;

        logger.LogDebug(
            "evento={Evento} fluxo={Fluxo} usernameInformado={UsernameInformado} tamanhoNovaSenha={TamanhoNovaSenha} amostrasRecebidas={AmostrasRecebidas}",
            "backend.change_password.iniciado",
            "change-password",
            !string.IsNullOrWhiteSpace(request.Username),
            request.NewPassword?.Length ?? 0,
            request.Samples?.Count ?? 0);

        if (string.IsNullOrEmpty(request.CurrentPassword))
        {
            return Invalida("A senha atual é obrigatória.");
        }

        var validacao = CadastrarUsuario.Validar(
            new CadastrarUsuario.Request(request.Username, request.NewPassword, request.Samples),
            quantidadeEsperada,
            out var vetores);

        if (validacao is not null)
        {
            logger.LogDebug(
                "evento={Evento} fluxo={Fluxo} motivo={Motivo}",
                "backend.change_password.validacao_falhou",
                "change-password",
                validacao);
            return Invalida(validacao);
        }

        var nome = NomeUsuario.Normalizar(request.Username!);
        var agora = relogio.GetUtcNow();
        var usuario = await db.Usuarios.SingleOrDefaultAsync(item => item.Nome == nome, cancellationToken);

        if (usuario?.EstaBloqueado(agora) == true)
        {
            logger.LogDebug(
                "evento={Evento} fluxo={Fluxo} username={Username} motivo={Motivo}",
                "backend.change_password.rejeitado",
                "change-password",
                nome,
                "conta_bloqueada");
            return NaoAutorizada("Usuário ou senha atuais inválidos.");
        }

        if (!verificadorSenha.Verificar(usuario, request.CurrentPassword))
        {
            if (usuario is not null)
            {
                usuario.RegistrarFalha(agora, seguranca.Value.MaximoTentativas, seguranca.Value.DuracaoBloqueio);
                await db.SaveChangesAsync(cancellationToken);
            }

            logger.LogDebug(
                "evento={Evento} fluxo={Fluxo} username={Username} usuarioEncontrado={UsuarioEncontrado} motivo={Motivo}",
                "backend.change_password.rejeitado",
                "change-password",
                nome,
                usuario is not null,
                "senha_atual_invalida");
            return NaoAutorizada("Usuário ou senha atuais inválidos.");
        }

        if (string.Equals(request.NewPassword, request.CurrentPassword, StringComparison.Ordinal))
        {
            return Invalida("A nova senha deve ser diferente da senha atual.");
        }

        var hashAnterior = usuario.SenhaHash;
        var dataMudancaAnterior = usuario.DataMudancaSenha;
        var biometriaAnterior = usuario.BiometriaCadastradaEm;
        var novaDataMudanca = agora;

        usuario.AlterarSenha(verificadorSenha.GerarHash(request.NewPassword!), novaDataMudanca);
        var amostras = request.Samples!.Zip(vetores, (amostra, vetor) =>
            Amostra.DeCadastro(usuario.Id, vetor, amostra.Repetition, amostra.TotalDurationMs, agora)).ToArray();

        db.Amostras.AddRange(amostras);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            logger.LogDebug(
                "evento={Evento} fluxo={Fluxo} userId={UserId} username={Username}",
                "backend.change_password.conflito_concorrencia",
                "change-password",
                usuario.Id,
                usuario.Nome);
            return Conflito("Outra alteração de senha está em andamento. Tente novamente.");
        }

        long? modeloId;

        try
        {
            modeloId = await verificadorBiometrico.TreinarModeloInicialAsync(usuario, vetores, cancellationToken);

            usuario.ConcluirCadastroBiometrico(novaDataMudanca);
            usuario.RegistrarSucesso();
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception excecao)
        {
            logger.LogDebug(
                "evento={Evento} fluxo={Fluxo} userId={UserId} username={Username} motivo={Motivo}",
                "backend.change_password.treinamento_falhou",
                "change-password",
                usuario.Id,
                usuario.Nome,
                excecao.GetType().Name);

            await ReverterAlteracaoSenhaAsync(
                db,
                verificadorBiometrico,
                usuario,
                amostras,
                hashAnterior,
                dataMudancaAnterior,
                biometriaAnterior,
                novaDataMudanca,
                logger);

            return FalhaServico("Não foi possível concluir a troca de senha; a senha anterior foi restaurada.");
        }

        var mediaHold = Math.Round(vetores.Average(vetor => vetor.MediaHoldMs), 1);

        logger.LogDebug(
            "evento={Evento} fluxo={Fluxo} userId={UserId} username={Username} modelId={ModelId} samplesRegistered={SamplesRegistered} mediaHoldMs={MediaHoldMs}",
            "backend.change_password.concluido",
            "change-password",
            usuario.Id,
            usuario.Nome,
            modeloId,
            vetores.Count,
            mediaHold);

        return TypedResults.Ok(new Response(
            true,
            $"Senha de '{usuario.Nome}' alterada com sucesso.",
            vetores.Count,
            mediaHold));
    }

    private static async Task ReverterAlteracaoSenhaAsync(
        AppDbContext db,
        IVerificadorBiometrico verificadorBiometrico,
        Usuario usuario,
        IReadOnlyList<Amostra> amostras,
        string hashAnterior,
        DateTimeOffset dataMudancaAnterior,
        DateTimeOffset? biometriaAnterior,
        DateTimeOffset novaDataMudanca,
        ILogger logger)
    {
        try
        {
            await verificadorBiometrico.ReverterModelosCriadosAposAsync(
                usuario,
                novaDataMudanca,
                CancellationToken.None);
        }
        catch (Exception excecao)
        {
            logger.LogError(
                excecao,
                "evento={Evento} fluxo={Fluxo} userId={UserId} username={Username}",
                "backend.change_password.reversao_modelos_falhou",
                "change-password",
                usuario.Id,
                usuario.Nome);
        }

        db.Amostras.RemoveRange(amostras);
        usuario.RestaurarCredenciais(hashAnterior, dataMudancaAnterior, biometriaAnterior);

        try
        {
            await db.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception excecao)
        {
            logger.LogError(
                excecao,
                "evento={Evento} fluxo={Fluxo} userId={UserId} username={Username}",
                "backend.change_password.reversao_falhou",
                "change-password",
                usuario.Id,
                usuario.Nome);
        }
    }

    private static BadRequest<RespostaOperacao> Invalida(string mensagem) =>
        TypedResults.BadRequest(RespostaOperacao.Falha(mensagem));

    private static JsonHttpResult<RespostaOperacao> NaoAutorizada(string mensagem) =>
        TypedResults.Json(RespostaOperacao.Falha(mensagem), statusCode: StatusCodes.Status401Unauthorized);

    private static JsonHttpResult<RespostaOperacao> Conflito(string mensagem) =>
        TypedResults.Json(RespostaOperacao.Falha(mensagem), statusCode: StatusCodes.Status409Conflict);

    private static JsonHttpResult<RespostaOperacao> FalhaServico(string mensagem) =>
        TypedResults.Json(RespostaOperacao.Falha(mensagem), statusCode: StatusCodes.Status502BadGateway);
}
