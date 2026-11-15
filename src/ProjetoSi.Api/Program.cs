using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ProjetoSi.Api.Common.Biometria;
using ProjetoSi.Api.Common.Endpoints;
using ProjetoSi.Api.Common.Seguranca;
using ProjetoSi.Api.Domain;
using ProjetoSi.Api.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options => options
    .UseNpgsql(builder.Configuration.GetConnectionString("Postgres"))
    .UseSnakeCaseNamingConvention());

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper)));

builder.Services.AddOptions<BiometriaOptions>().BindConfiguration(BiometriaOptions.Secao).ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddOptions<SegurancaLoginOptions>().BindConfiguration(SegurancaLoginOptions.Secao).ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddOptions<JwtOptions>().BindConfiguration(JwtOptions.Secao).ValidateDataAnnotations().ValidateOnStart();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IPasswordHasher<Usuario>, PasswordHasher<Usuario>>();
builder.Services.AddSingleton<VerificadorSenha>();
builder.Services.AddSingleton<GeradorToken>();
builder.Services.AddHttpClient<IVerificadorBiometrico, VerificadorBiometricoMlApi>((provedor, cliente) =>
{
    var opcoes = provedor.GetRequiredService<IOptions<BiometriaOptions>>().Value;
    cliente.BaseAddress = new Uri(opcoes.MlApiUrl);
});

builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(builder.Configuration.GetSection("Cors:Origens").Get<string[]>() ?? [])
    .AllowAnyHeader()
    .AllowAnyMethod()));

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Configuration.GetValue<bool>("Banco:AplicarMigracoesNaInicializacao"))
{
    await using var escopo = app.Services.CreateAsyncScope();
    await escopo.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseExceptionHandler();
app.UseCors();
app.MapearEndpoints("/api");

app.Run();

public partial class Program;
