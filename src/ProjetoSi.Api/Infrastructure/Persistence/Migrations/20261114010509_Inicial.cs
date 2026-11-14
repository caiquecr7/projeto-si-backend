using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ProjetoSi.Api.Infrastructure.Persistence.Migrations
{
    public partial class Inicial : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "usuarios",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    senha_hash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    data_mudanca_senha = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    biometria_cadastrada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    tentativas_falhas = table.Column<int>(type: "integer", nullable: false),
                    bloqueado_ate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_usuarios", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "modelos",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    versao = table.Column<int>(type: "integer", nullable: false),
                    algoritmo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    caminho_arquivo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_modelos", x => x.id);
                    table.CheckConstraint("ck_modelos_versao_positiva", "versao > 0");
                    table.ForeignKey(
                        name: "fk_modelos_usuarios_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "amostras",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    origem = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    vetor = table.Column<double[]>(type: "double precision[]", nullable: false),
                    repeticao = table.Column<int>(type: "integer", nullable: true),
                    duracao_total_ms = table.Column<double>(type: "double precision", nullable: false),
                    decisao = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    confianca = table.Column<double>(type: "double precision", nullable: true),
                    modelo_id = table.Column<long>(type: "bigint", nullable: true),
                    coletada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_amostras", x => x.id);
                    table.CheckConstraint("ck_amostras_login_com_decisao", "origem <> 'Login' OR decisao IS NOT NULL");
                    table.CheckConstraint("ck_amostras_vetor_nao_vazio", "cardinality(vetor) > 0");
                    table.ForeignKey(
                        name: "fk_amostras_modelos_modelo_id",
                        column: x => x.modelo_id,
                        principalTable: "modelos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_amostras_usuarios_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_amostras_modelo_id",
                table: "amostras",
                column: "modelo_id");

            migrationBuilder.CreateIndex(
                name: "ix_amostras_usuario_id_origem_coletada_em",
                table: "amostras",
                columns: new[] { "usuario_id", "origem", "coletada_em" });

            migrationBuilder.CreateIndex(
                name: "ix_modelos_usuario_id_versao",
                table: "modelos",
                columns: new[] { "usuario_id", "versao" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_usuarios_nome",
                table: "usuarios",
                column: "nome",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "amostras");

            migrationBuilder.DropTable(
                name: "modelos");

            migrationBuilder.DropTable(
                name: "usuarios");
        }
    }
}
