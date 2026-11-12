<#
.SYNOPSIS
  Copia para src/Web/wwwroot/assets apenas os arquivos do tema Velzon usados pelo Web.

.DESCRIPTION
  O Velzon é um tema comercial (Themesbrand) e NÃO deve ser publicado em repositório público.
  Por isso src/Web/wwwroot/assets está no .gitignore: cada desenvolvedor com licença roda este script.

.PARAMETER Origem
  Pasta do projeto Velzon licenciado (a que contém wwwroot/assets).
#>
param(
    [Parameter(Mandatory = $true)][string]$Origem
)

$ErrorActionPreference = "Stop"
$src = Join-Path $Origem "wwwroot\assets"
$dst = Join-Path $PSScriptRoot "..\src\Web\wwwroot\assets"

if (-not (Test-Path $src)) { throw "Não encontrei '$src'. Informe a pasta do projeto Velzon em -Origem." }

function Copiar($relativo, [switch]$Pasta) {
    $de = Join-Path $src $relativo
    $para = Join-Path $dst $relativo
    New-Item -ItemType Directory -Force -Path (Split-Path $para) | Out-Null
    if ($Pasta) { Copy-Item $de $para -Recurse -Force } else { Copy-Item $de $para -Force }
}

# CSS (versões minificadas) e fontes de ícones
foreach ($f in "bootstrap.min.css", "icons.min.css", "app.min.css", "custom.min.css") { Copiar "css\$f" }
Copiar "fonts" -Pasta

# Imagens usadas pelo layout e pela tela de login
foreach ($f in "favicon.ico", "logo-sm.png", "logo-dark.png", "logo-light.png", "auth-one-bg.jpg", "users\avatar-1.jpg") { Copiar "images\$f" }

# JavaScript do tema e bibliotecas mínimas
foreach ($f in "layout.js", "app.js", "plugins.js") { Copiar "js\$f" }
foreach ($l in "bootstrap", "simplebar", "node-waves", "feather-icons") { Copiar "libs\$l" -Pasta }
Copiar "lang" -Pasta

Write-Host "Assets copiados para $dst"
