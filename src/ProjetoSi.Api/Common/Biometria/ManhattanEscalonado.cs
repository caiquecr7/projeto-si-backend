namespace ProjetoSi.Api.Common.Biometria;

public static class ManhattanEscalonado
{
    public static double DistanciaMedia(IReadOnlyList<double[]> perfil, double[] tentativa, double desvioMinimo)
    {
        if (perfil.Count == 0 || perfil.Any(vetor => vetor.Length != tentativa.Length))
        {
            throw new ArgumentException("Perfil e tentativa precisam ter a mesma dimensão.", nameof(perfil));
        }

        var soma = 0d;

        for (var dimensao = 0; dimensao < tentativa.Length; dimensao++)
        {
            var media = perfil.Average(vetor => vetor[dimensao]);
            var desvio = Math.Max(perfil.Average(vetor => Math.Abs(vetor[dimensao] - media)), desvioMinimo);
            soma += Math.Abs(tentativa[dimensao] - media) / desvio;
        }

        return soma / tentativa.Length;
    }

    public static double Confianca(double distanciaMedia) => 1d / (1d + distanciaMedia);
}
