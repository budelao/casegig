$ErrorActionPreference = "Stop"

param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("mysql", "aurora-mysql", "postgres", "aurora-postgres")]
    [string]$Engine,

    [Parameter(Mandatory = $true)]
    [string]$Host,

    [Parameter(Mandatory = $false)]
    [int]$Port,

    [Parameter(Mandatory = $true)]
    [string]$Database,

    [Parameter(Mandatory = $true)]
    [string]$Username,

    [Parameter(Mandatory = $true)]
    [string]$Password,

    [Parameter(Mandatory = $false)]
    [string]$Schema = "public",

    [Parameter(Mandatory = $false)]
    [switch]$SkipSeed
)

function Get-MySqlPort([int]$value) {
    if ($value -gt 0) { return $value }
    return 3306
}

function Get-PostgresPort([int]$value) {
    if ($value -gt 0) { return $value }
    return 5432
}

function Get-MySqlInitSql([string]$databaseName, [bool]$includeSeed) {
    $seedSql = ""
    if ($includeSeed) {
        $seedSql = @"
INSERT INTO `Clientes` (`IdCliente`, `Nome`, `Cpf`, `SaldoDisponivel`, `RowVersion`) VALUES
('11111111-1111-1111-1111-111111111111', 'João Silva', '11111111111', 10000.00, 1),
('22222222-2222-2222-2222-222222222222', 'Maria Souza', '22222222222', 100.00, 1);

INSERT INTO `Fundos` (`IdFundo`, `Nome`, `HorarioCorte`, `ValorCota`, `ValorMinimoAporte`, `ValorMinimoPermanencia`, `StatusCaptacao`, `RowVersion`) VALUES
('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 'Fundo Renda Fixa', '14:00:00', 10.000000, 100.00, 50.00, 'ABERTO', 1),
('bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', 'Fundo Ações Fechado', '14:00:00', 20.000000, 200.00, 100.00, 'FECHADO', 1);

INSERT INTO `Posicoes` (`IdCliente`, `IdFundo`, `QuantidadeCotas`, `RowVersion`) VALUES
('11111111-1111-1111-1111-111111111111', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 500.000000000000000000, 1),
('22222222-2222-2222-2222-222222222222', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 5.000000000000000000, 1);
"@
    }

    return @"
CREATE DATABASE IF NOT EXISTS \`$databaseName\` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
USE \`$databaseName\`;

CREATE TABLE IF NOT EXISTS `Clientes` (
  `IdCliente` char(36) COLLATE ascii_general_ci NOT NULL,
  `Nome` varchar(200) NOT NULL,
  `Cpf` varchar(11) NOT NULL,
  `SaldoDisponivel` decimal(18,2) NOT NULL,
  `RowVersion` bigint NOT NULL,
  PRIMARY KEY (`IdCliente`)
) CHARACTER SET=utf8mb4;

CREATE TABLE IF NOT EXISTS `Fundos` (
  `IdFundo` char(36) COLLATE ascii_general_ci NOT NULL,
  `Nome` varchar(200) NOT NULL,
  `HorarioCorte` time(0) NOT NULL,
  `ValorCota` decimal(18,6) NOT NULL,
  `ValorMinimoAporte` decimal(18,2) NOT NULL,
  `ValorMinimoPermanencia` decimal(18,2) NOT NULL,
  `StatusCaptacao` varchar(10) NOT NULL,
  `RowVersion` bigint NOT NULL,
  PRIMARY KEY (`IdFundo`)
) CHARACTER SET=utf8mb4;

CREATE TABLE IF NOT EXISTS `Ordens` (
  `IdOrdem` char(36) COLLATE ascii_general_ci NOT NULL,
  `IdCliente` char(36) COLLATE ascii_general_ci NOT NULL,
  `IdFundo` char(36) COLLATE ascii_general_ci NOT NULL,
  `TipoOperacao` varchar(10) NOT NULL,
  `QuantidadeCotas` decimal(38,18) NOT NULL,
  `DataCriacao` datetime(6) NOT NULL,
  `DataAgendamento` datetime(6) NULL,
  `DataProcessamento` datetime(6) NULL,
  `Status` varchar(20) NOT NULL,
  `RowVersion` bigint NOT NULL,
  `IdempotencyKey` varchar(200) NULL,
  `IdempotencyOperation` varchar(80) NULL,
  `IdempotencyRequestHash` varchar(64) NULL,
  PRIMARY KEY (`IdOrdem`),
  CONSTRAINT `FK_Ordens_Clientes_IdCliente` FOREIGN KEY (`IdCliente`) REFERENCES `Clientes` (`IdCliente`) ON DELETE CASCADE,
  CONSTRAINT `FK_Ordens_Fundos_IdFundo` FOREIGN KEY (`IdFundo`) REFERENCES `Fundos` (`IdFundo`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE IF NOT EXISTS `Posicoes` (
  `IdCliente` char(36) COLLATE ascii_general_ci NOT NULL,
  `IdFundo` char(36) COLLATE ascii_general_ci NOT NULL,
  `QuantidadeCotas` decimal(38,18) NOT NULL,
  `RowVersion` bigint NOT NULL,
  PRIMARY KEY (`IdCliente`,`IdFundo`),
  CONSTRAINT `FK_Posicoes_Clientes_IdCliente` FOREIGN KEY (`IdCliente`) REFERENCES `Clientes` (`IdCliente`) ON DELETE CASCADE,
  CONSTRAINT `FK_Posicoes_Fundos_IdFundo` FOREIGN KEY (`IdFundo`) REFERENCES `Fundos` (`IdFundo`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE INDEX `IX_Ordens_IdFundo` ON `Ordens` (`IdFundo`);
CREATE UNIQUE INDEX `IX_Ordens_IdCliente_IdempotencyOperation_IdempotencyKey` ON `Ordens` (`IdCliente`, `IdempotencyOperation`, `IdempotencyKey`);
CREATE INDEX `IX_Posicoes_IdFundo` ON `Posicoes` (`IdFundo`);

$seedSql
"@
}

function Get-PostgresInitSql([string]$databaseName, [string]$schemaName, [bool]$includeSeed) {
    $schema = $schemaName
    $seedSql = ""
    if ($includeSeed) {
        $seedSql = @"
INSERT INTO "$schema"."Clientes" ("IdCliente", "Nome", "Cpf", "SaldoDisponivel", "RowVersion") VALUES
('11111111-1111-1111-1111-111111111111', 'João Silva', '11111111111', 10000.00, 1),
('22222222-2222-2222-2222-222222222222', 'Maria Souza', '22222222222', 100.00, 1);

INSERT INTO "$schema"."Fundos" ("IdFundo", "Nome", "HorarioCorte", "ValorCota", "ValorMinimoAporte", "ValorMinimoPermanencia", "StatusCaptacao", "RowVersion") VALUES
('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 'Fundo Renda Fixa', '14:00:00', 10.000000, 100.00, 50.00, 'ABERTO', 1),
('bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', 'Fundo Ações Fechado', '14:00:00', 20.000000, 200.00, 100.00, 'FECHADO', 1);

INSERT INTO "$schema"."Posicoes" ("IdCliente", "IdFundo", "QuantidadeCotas", "RowVersion") VALUES
('11111111-1111-1111-1111-111111111111', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 500.000000000000000000, 1),
('22222222-2222-2222-2222-222222222222', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 5.000000000000000000, 1);
"@
    }

    return @"
SELECT format('CREATE DATABASE %I', '$databaseName')
WHERE NOT EXISTS (SELECT 1 FROM pg_database WHERE datname = '$databaseName')\\gexec

\\c "$databaseName"

CREATE SCHEMA IF NOT EXISTS "$schema";

CREATE TABLE IF NOT EXISTS "$schema"."Clientes" (
  "IdCliente" uuid PRIMARY KEY,
  "Nome" varchar(200) NOT NULL,
  "Cpf" varchar(11) NOT NULL,
  "SaldoDisponivel" numeric(18,2) NOT NULL,
  "RowVersion" bigint NOT NULL
);

CREATE TABLE IF NOT EXISTS "$schema"."Fundos" (
  "IdFundo" uuid PRIMARY KEY,
  "Nome" varchar(200) NOT NULL,
  "HorarioCorte" time NOT NULL,
  "ValorCota" numeric(18,6) NOT NULL,
  "ValorMinimoAporte" numeric(18,2) NOT NULL,
  "ValorMinimoPermanencia" numeric(18,2) NOT NULL,
  "StatusCaptacao" varchar(10) NOT NULL,
  "RowVersion" bigint NOT NULL
);

CREATE TABLE IF NOT EXISTS "$schema"."Ordens" (
  "IdOrdem" uuid PRIMARY KEY,
  "IdCliente" uuid NOT NULL REFERENCES "$schema"."Clientes" ("IdCliente") ON DELETE CASCADE,
  "IdFundo" uuid NOT NULL REFERENCES "$schema"."Fundos" ("IdFundo") ON DELETE CASCADE,
  "TipoOperacao" varchar(10) NOT NULL,
  "QuantidadeCotas" numeric(38,18) NOT NULL,
  "DataCriacao" timestamp NOT NULL,
  "DataAgendamento" timestamp NULL,
  "DataProcessamento" timestamp NULL,
  "Status" varchar(20) NOT NULL,
  "RowVersion" bigint NOT NULL,
  "IdempotencyKey" varchar(200) NULL,
  "IdempotencyOperation" varchar(80) NULL,
  "IdempotencyRequestHash" varchar(64) NULL
);

CREATE TABLE IF NOT EXISTS "$schema"."Posicoes" (
  "IdCliente" uuid NOT NULL REFERENCES "$schema"."Clientes" ("IdCliente") ON DELETE CASCADE,
  "IdFundo" uuid NOT NULL REFERENCES "$schema"."Fundos" ("IdFundo") ON DELETE CASCADE,
  "QuantidadeCotas" numeric(38,18) NOT NULL,
  "RowVersion" bigint NOT NULL,
  PRIMARY KEY ("IdCliente", "IdFundo")
);

CREATE INDEX IF NOT EXISTS "IX_Ordens_IdFundo" ON "$schema"."Ordens" ("IdFundo");
CREATE INDEX IF NOT EXISTS "IX_Posicoes_IdFundo" ON "$schema"."Posicoes" ("IdFundo");
CREATE UNIQUE INDEX IF NOT EXISTS "IX_Ordens_IdCliente_IdempotencyOperation_IdempotencyKey" ON "$schema"."Ordens" ("IdCliente", "IdempotencyOperation", "IdempotencyKey");

$seedSql
"@
}

$includeSeed = -not $SkipSeed.IsPresent

if ($Engine -in @("mysql", "aurora-mysql")) {
    $mysqlPort = Get-MySqlPort $Port
    $mysqlCmd = Get-Command "mysql" -ErrorAction SilentlyContinue
    if (-not $mysqlCmd) {
        throw "mysql CLI não encontrado no PATH. Instale o cliente MySQL (mysql.exe) ou execute em um ambiente que possua o client."
    }

    $sql = Get-MySqlInitSql -databaseName $Database -includeSeed:$includeSeed
    $temp = [System.IO.Path]::GetTempFileName()
    [System.IO.File]::WriteAllText($temp, $sql, [System.Text.Encoding]::UTF8)
    try {
        Get-Content -Raw $temp | & $mysqlCmd.Source "-h" $Host "-P" $mysqlPort "-u" $Username ("--password=$Password") | Out-Host
    }
    finally {
        Remove-Item $temp -Force
    }
    exit 0
}

if ($Engine -in @("postgres", "aurora-postgres")) {
    $pgPort = Get-PostgresPort $Port
    $psqlCmd = Get-Command "psql" -ErrorAction SilentlyContinue
    if (-not $psqlCmd) {
        throw "psql CLI não encontrado no PATH. Instale o cliente PostgreSQL (psql.exe) ou execute em um ambiente que possua o client."
    }

    $sql = Get-PostgresInitSql -databaseName $Database -schemaName $Schema -includeSeed:$includeSeed
    $temp = [System.IO.Path]::GetTempFileName()
    [System.IO.File]::WriteAllText($temp, $sql, [System.Text.Encoding]::UTF8)

    $env:PGPASSWORD = $Password
    try {
        & $psqlCmd.Source "-h" $Host "-p" $pgPort "-U" $Username "-d" "postgres" "-f" $temp | Out-Host
    }
    finally {
        Remove-Item $temp -Force
        Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue
    }

    exit 0
}

throw "Engine inválido: $Engine"
