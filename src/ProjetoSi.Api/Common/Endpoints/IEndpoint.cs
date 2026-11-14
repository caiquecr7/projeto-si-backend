using System.Reflection;

namespace ProjetoSi.Api.Common.Endpoints;

public interface IEndpoint
{
    void Mapear(IEndpointRouteBuilder rotas);
}

public static class EndpointExtensions
{
    public static WebApplication MapearEndpoints(this WebApplication app, string prefixo)
    {
        var grupo = app.MapGroup(prefixo);

        var endpoints = Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(tipo => tipo is { IsAbstract: false, IsInterface: false } && tipo.IsAssignableTo(typeof(IEndpoint)))
            .Select(tipo => (IEndpoint)Activator.CreateInstance(tipo)!);

        foreach (var endpoint in endpoints)
        {
            endpoint.Mapear(grupo);
        }

        return app;
    }
}
