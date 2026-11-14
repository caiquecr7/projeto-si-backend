using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using ProjetoSi.Api.Infrastructure.Persistence;

#nullable disable

namespace ProjetoSi.Api.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(AppDbContext))]
    partial class AppDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "10.0.4")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("ProjetoSi.Api.Domain.Amostra", b =>
                {
                    b.Property<long>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bigint")
                        .HasColumnName("id");

                    NpgsqlPropertyBuilderExtensions.UseIdentityAlwaysColumn(b.Property<long>("Id"));

                    b.Property<DateTimeOffset>("ColetadaEm")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("coletada_em");

                    b.Property<double?>("Confianca")
                        .HasColumnType("double precision")
                        .HasColumnName("confianca");

                    b.Property<string>("Decisao")
                        .HasMaxLength(20)
                        .HasColumnType("character varying(20)")
                        .HasColumnName("decisao");

                    b.Property<double>("DuracaoTotalMs")
                        .HasColumnType("double precision")
                        .HasColumnName("duracao_total_ms");

                    b.Property<long?>("ModeloId")
                        .HasColumnType("bigint")
                        .HasColumnName("modelo_id");

                    b.Property<string>("Origem")
                        .IsRequired()
                        .HasMaxLength(20)
                        .HasColumnType("character varying(20)")
                        .HasColumnName("origem");

                    b.Property<int?>("Repeticao")
                        .HasColumnType("integer")
                        .HasColumnName("repeticao");

                    b.Property<Guid>("UsuarioId")
                        .HasColumnType("uuid")
                        .HasColumnName("usuario_id");

                    b.PrimitiveCollection<double[]>("Vetor")
                        .IsRequired()
                        .HasColumnType("double precision[]")
                        .HasColumnName("vetor");

                    b.HasKey("Id")
                        .HasName("pk_amostras");

                    b.HasIndex("ModeloId")
                        .HasDatabaseName("ix_amostras_modelo_id");

                    b.HasIndex("UsuarioId", "Origem", "ColetadaEm")
                        .HasDatabaseName("ix_amostras_usuario_id_origem_coletada_em");

                    b.ToTable("amostras", null, t =>
                        {
                            t.HasCheckConstraint("ck_amostras_login_com_decisao", "origem <> 'Login' OR decisao IS NOT NULL");

                            t.HasCheckConstraint("ck_amostras_vetor_nao_vazio", "cardinality(vetor) > 0");
                        });
                });

            modelBuilder.Entity("ProjetoSi.Api.Domain.ModeloBiometrico", b =>
                {
                    b.Property<long>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bigint")
                        .HasColumnName("id");

                    NpgsqlPropertyBuilderExtensions.UseIdentityAlwaysColumn(b.Property<long>("Id"));

                    b.Property<string>("Algoritmo")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("character varying(50)")
                        .HasColumnName("algoritmo");

                    b.Property<string>("CaminhoArquivo")
                        .IsRequired()
                        .HasMaxLength(500)
                        .HasColumnType("character varying(500)")
                        .HasColumnName("caminho_arquivo");

                    b.Property<DateTimeOffset>("CriadoEm")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("criado_em");

                    b.Property<Guid>("UsuarioId")
                        .HasColumnType("uuid")
                        .HasColumnName("usuario_id");

                    b.Property<int>("Versao")
                        .HasColumnType("integer")
                        .HasColumnName("versao");

                    b.HasKey("Id")
                        .HasName("pk_modelos");

                    b.HasIndex("UsuarioId", "Versao")
                        .IsUnique()
                        .HasDatabaseName("ix_modelos_usuario_id_versao");

                    b.ToTable("modelos", null, t =>
                        {
                            t.HasCheckConstraint("ck_modelos_versao_positiva", "versao > 0");
                        });
                });

            modelBuilder.Entity("ProjetoSi.Api.Domain.Usuario", b =>
                {
                    b.Property<Guid>("Id")
                        .HasColumnType("uuid")
                        .HasColumnName("id");

                    b.Property<DateTimeOffset?>("BiometriaCadastradaEm")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("biometria_cadastrada_em");

                    b.Property<DateTimeOffset?>("BloqueadoAte")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("bloqueado_ate");

                    b.Property<DateTimeOffset>("CriadoEm")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("criado_em");

                    b.Property<DateTimeOffset>("DataMudancaSenha")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("data_mudanca_senha");

                    b.Property<string>("Nome")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("character varying(50)")
                        .HasColumnName("nome");

                    b.Property<string>("SenhaHash")
                        .IsRequired()
                        .HasMaxLength(256)
                        .HasColumnType("character varying(256)")
                        .HasColumnName("senha_hash");

                    b.Property<int>("TentativasFalhas")
                        .HasColumnType("integer")
                        .HasColumnName("tentativas_falhas");

                    b.Property<uint>("Versao")
                        .IsConcurrencyToken()
                        .ValueGeneratedOnAddOrUpdate()
                        .HasColumnType("xid")
                        .HasColumnName("xmin");

                    b.HasKey("Id")
                        .HasName("pk_usuarios");

                    b.HasIndex("Nome")
                        .IsUnique()
                        .HasDatabaseName("ix_usuarios_nome");

                    b.ToTable("usuarios", (string)null);
                });

            modelBuilder.Entity("ProjetoSi.Api.Domain.Amostra", b =>
                {
                    b.HasOne("ProjetoSi.Api.Domain.ModeloBiometrico", null)
                        .WithMany()
                        .HasForeignKey("ModeloId")
                        .OnDelete(DeleteBehavior.SetNull)
                        .HasConstraintName("fk_amostras_modelos_modelo_id");

                    b.HasOne("ProjetoSi.Api.Domain.Usuario", null)
                        .WithMany()
                        .HasForeignKey("UsuarioId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired()
                        .HasConstraintName("fk_amostras_usuarios_usuario_id");
                });

            modelBuilder.Entity("ProjetoSi.Api.Domain.ModeloBiometrico", b =>
                {
                    b.HasOne("ProjetoSi.Api.Domain.Usuario", null)
                        .WithMany()
                        .HasForeignKey("UsuarioId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired()
                        .HasConstraintName("fk_modelos_usuarios_usuario_id");
                });
#pragma warning restore 612, 618
        }
    }
}
