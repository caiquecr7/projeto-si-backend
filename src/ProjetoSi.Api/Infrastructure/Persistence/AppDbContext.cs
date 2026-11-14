using Microsoft.EntityFrameworkCore;
using ProjetoSi.Api.Domain;

namespace ProjetoSi.Api.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Amostra> Amostras => Set<Amostra>();
    public DbSet<ModeloBiometrico> Modelos => Set<ModeloBiometrico>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
}
