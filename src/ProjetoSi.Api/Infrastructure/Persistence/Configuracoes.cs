using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjetoSi.Api.Domain;

namespace ProjetoSi.Api.Infrastructure.Persistence;

public sealed class UsuarioConfiguracao : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> builder)
    {
        builder.ToTable("usuarios");
        builder.HasKey(usuario => usuario.Id);
        builder.Property(usuario => usuario.Id).ValueGeneratedNever();
        builder.Property(usuario => usuario.Nome).HasMaxLength(NomeUsuario.TamanhoMaximo).IsRequired();
        builder.HasIndex(usuario => usuario.Nome).IsUnique();
        builder.Property(usuario => usuario.SenhaHash).HasMaxLength(256).IsRequired();
        builder.Property(usuario => usuario.Versao).IsRowVersion();
    }
}

public sealed class AmostraConfiguracao : IEntityTypeConfiguration<Amostra>
{
    public void Configure(EntityTypeBuilder<Amostra> builder)
    {
        builder.ToTable("amostras", tabela =>
        {
            tabela.HasCheckConstraint("ck_amostras_vetor_nao_vazio", "cardinality(vetor) > 0");
            tabela.HasCheckConstraint("ck_amostras_login_com_decisao", "origem <> 'Login' OR decisao IS NOT NULL");
        });
        builder.HasKey(amostra => amostra.Id);
        builder.Property(amostra => amostra.Id).UseIdentityAlwaysColumn();
        builder.Property(amostra => amostra.Origem).HasConversion<string>().HasMaxLength(20);
        builder.Property(amostra => amostra.Decisao).HasConversion<string>().HasMaxLength(20);
        builder.Property(amostra => amostra.Vetor).HasColumnType("double precision[]").IsRequired();
        builder.HasOne<Usuario>().WithMany().HasForeignKey(amostra => amostra.UsuarioId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ModeloBiometrico>().WithMany().HasForeignKey(amostra => amostra.ModeloId).OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(amostra => new { amostra.UsuarioId, amostra.Origem, amostra.ColetadaEm });
    }
}

public sealed class ModeloBiometricoConfiguracao : IEntityTypeConfiguration<ModeloBiometrico>
{
    public void Configure(EntityTypeBuilder<ModeloBiometrico> builder)
    {
        builder.ToTable("modelos", tabela => tabela.HasCheckConstraint("ck_modelos_versao_positiva", "versao > 0"));
        builder.HasKey(modelo => modelo.Id);
        builder.Property(modelo => modelo.Id).UseIdentityAlwaysColumn();
        builder.Property(modelo => modelo.Algoritmo).HasMaxLength(50).IsRequired();
        builder.Property(modelo => modelo.CaminhoArquivo).HasMaxLength(500).IsRequired();
        builder.HasOne<Usuario>().WithMany().HasForeignKey(modelo => modelo.UsuarioId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(modelo => new { modelo.UsuarioId, modelo.Versao }).IsUnique();
    }
}
