[CmdletBinding()]
param(
    [string]$ConfigPath,
    [string]$SchemaPath,
    [string]$PrecheckPath,
    [string]$SqlitePath,
    [string]$MySqlHost,
    [int]$MySqlPort,
    [string]$MySqlDatabase,
    [string]$MySqlUsername,
    [string]$MySqlPassword,
    [switch]$SkipSchemaInitialization,
    [switch]$SkipPrecheck,
    [switch]$SkipClearTarget,
    [switch]$SkipValidation,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

if (-not $ConfigPath) {
    $ConfigPath = Join-Path $PSScriptRoot "..\SwitchYard.WebApi\SwitchYard.Service\appsettings.json"
}
if (-not $SchemaPath) {
    $SchemaPath = Join-Path $PSScriptRoot "..\SwitchYard.WebApi\SwitchYard.Service\Database\mysql-schema.sql"
}
if (-not $PrecheckPath) {
    $PrecheckPath = Join-Path $PSScriptRoot "..\SwitchYard.WebApi\SwitchYard.Service\Database\sqlite-precheck.sql"
}

$projectPath = Join-Path $PSScriptRoot "..\tools\SQLiteToMySqlMigrator\SQLiteToMySqlMigrator.csproj"
if (-not (Test-Path -LiteralPath $projectPath)) {
    throw "Migrator project not found: $projectPath"
}

$argumentList = @(
    "run",
    "--project", $projectPath,
    "--configuration", "Release",
    "--"
)

function Add-ArgumentPair {
    param(
        [string]$Name,
        [string]$Value
    )

    if ($null -ne $Value -and $Value -ne "") {
        $script:argumentList += $Name
        $script:argumentList += $Value
    }
}

Add-ArgumentPair "--config-path" $ConfigPath
Add-ArgumentPair "--schema-path" $SchemaPath
Add-ArgumentPair "--precheck-path" $PrecheckPath
Add-ArgumentPair "--sqlite-path" $SqlitePath
Add-ArgumentPair "--mysql-host" $MySqlHost
if ($MySqlPort) {
    Add-ArgumentPair "--mysql-port" $MySqlPort.ToString()
}
Add-ArgumentPair "--mysql-database" $MySqlDatabase
Add-ArgumentPair "--mysql-username" $MySqlUsername
Add-ArgumentPair "--mysql-password" $MySqlPassword

if ($SkipSchemaInitialization) { $argumentList += "--skip-schema-initialization" }
if ($SkipPrecheck) { $argumentList += "--skip-precheck" }
if ($SkipClearTarget) { $argumentList += "--skip-clear-target" }
if ($SkipValidation) { $argumentList += "--skip-validation" }
if ($DryRun) { $argumentList += "--dry-run" }

& dotnet @argumentList
exit $LASTEXITCODE
