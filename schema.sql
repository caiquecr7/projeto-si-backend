CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    migration_id character varying(150) NOT NULL,
    product_version character varying(32) NOT NULL,
    CONSTRAINT pk___ef_migrations_history PRIMARY KEY (migration_id)
);

START TRANSACTION;
CREATE TABLE usuarios (
    id uuid NOT NULL,
    nome character varying(50) NOT NULL,
    senha_hash character varying(256) NOT NULL,
    data_mudanca_senha timestamp with time zone NOT NULL,
    biometria_cadastrada_em timestamp with time zone,
    tentativas_falhas integer NOT NULL,
    bloqueado_ate timestamp with time zone,
    criado_em timestamp with time zone NOT NULL,
    CONSTRAINT pk_usuarios PRIMARY KEY (id)
);

CREATE TABLE modelos (
    id bigint GENERATED ALWAYS AS IDENTITY,
    usuario_id uuid NOT NULL,
    versao integer NOT NULL,
    algoritmo character varying(50) NOT NULL,
    caminho_arquivo character varying(500) NOT NULL,
    criado_em timestamp with time zone NOT NULL,
    CONSTRAINT pk_modelos PRIMARY KEY (id),
    CONSTRAINT ck_modelos_versao_positiva CHECK (versao > 0),
    CONSTRAINT fk_modelos_usuarios_usuario_id FOREIGN KEY (usuario_id) REFERENCES usuarios (id) ON DELETE CASCADE
);

CREATE TABLE amostras (
    id bigint GENERATED ALWAYS AS IDENTITY,
    usuario_id uuid NOT NULL,
    origem character varying(20) NOT NULL,
    vetor double precision[] NOT NULL,
    repeticao integer,
    duracao_total_ms double precision NOT NULL,
    decisao character varying(20),
    confianca double precision,
    modelo_id bigint,
    coletada_em timestamp with time zone NOT NULL,
    CONSTRAINT pk_amostras PRIMARY KEY (id),
    CONSTRAINT ck_amostras_login_com_decisao CHECK (origem <> 'Login' OR decisao IS NOT NULL),
    CONSTRAINT ck_amostras_vetor_nao_vazio CHECK (cardinality(vetor) > 0),
    CONSTRAINT fk_amostras_modelos_modelo_id FOREIGN KEY (modelo_id) REFERENCES modelos (id) ON DELETE SET NULL,
    CONSTRAINT fk_amostras_usuarios_usuario_id FOREIGN KEY (usuario_id) REFERENCES usuarios (id) ON DELETE CASCADE
);

CREATE INDEX ix_amostras_modelo_id ON amostras (modelo_id);

CREATE INDEX ix_amostras_usuario_id_origem_coletada_em ON amostras (usuario_id, origem, coletada_em);

CREATE UNIQUE INDEX ix_modelos_usuario_id_versao ON modelos (usuario_id, versao);

CREATE UNIQUE INDEX ix_usuarios_nome ON usuarios (nome);

INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
VALUES ('20261114010509_Inicial', '10.0.4');

COMMIT;

