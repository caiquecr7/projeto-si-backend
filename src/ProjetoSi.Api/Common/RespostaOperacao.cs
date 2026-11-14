namespace ProjetoSi.Api.Common;

public sealed record RespostaOperacao(bool Success, string Message)
{
    public static RespostaOperacao Sucesso(string mensagem) => new(true, mensagem);

    public static RespostaOperacao Falha(string mensagem) => new(false, mensagem);
}
