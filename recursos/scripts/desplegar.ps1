<#
=============================================================================
 LAB14 · DESPLIEGUE POR FASES (CI/CD)  ·  Quizzerttack
=============================================================================
 Un solo comando publica el módulo de Cloud Code y la configuración remota,
 en ESTE orden y con guardas:

   1. PRUEBAS        dotnet test. Si algo falla, NO se despliega nada.
   2. RESPALDO       ugs cc modules export -> recursos\respaldos\<ambiente>-<fecha>.ccmzip
   3. CONFIGURACIÓN  ugs deploy de los .rc + .lb + .ac  (ANTES que el módulo)
   4. MÓDULO         dotnet build + ugs deploy Tienda.sln
   5. VERIFICACIÓN   45 s de propagación + jugador anónimo nuevo -> HoraDelServidor

 Uso:
   .\desplegar.ps1 -Ambiente development -Simular          # ensayo: no cambia nada
   .\desplegar.ps1 -Ambiente development                   # despliegue real
   .\desplegar.ps1 -Ambiente development -VerificarEconomia
   .\desplegar.ps1 -Ambiente production -Confirmar         # producción exige -Confirmar
   .\desplegar.ps1 -Ambiente development -Revertir ..\respaldos\development-20261123-101500.ccmzip

 Credenciales: SOLO por variables de entorno (nunca en el archivo):
   UGS_SERVICE_ACCOUNT_KEY_ID
   UGS_SERVICE_ACCOUNT_SECRET_KEY
 El id del proyecto se lee del proyecto de Unity (ProjectSettings.asset) para no
 desplegar al proyecto equivocado; también se puede pasar con -Proyecto o UGS_PROJECT_ID.
=============================================================================
#>
#Requires -Version 5.1
[CmdletBinding()]
param(
    [ValidateSet("development", "production")]
    [string]$Ambiente = "development",

    # Producción exige una confirmación explícita.
    [switch]$Confirmar,

    # Ensayo: pruebas + dry-run de configuración y módulo. No crea jugadores
    # ni ejecuta la verificación económica.
    [switch]$Simular,

    # Verificación del circuito económico con un jugador anónimo nuevo.
    [switch]$VerificarEconomia,

    # Revertir: importa un respaldo .ccmzip y verifica que responde la versión anterior.
    [string]$Revertir,

    # Opcional: id del proyecto UGS (por defecto, el del proyecto de Unity).
    [string]$Proyecto,

    # Segundos de propagación antes de verificar (45 por defecto; 35 al revertir).
    [int]$EsperaSegundos = 45
)

$ErrorActionPreference = "Continue"

# ---------------------------------------------------------------------------
# Rutas y utilidades
# ---------------------------------------------------------------------------
$Raiz       = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent   # ...\Quizzerttack
$Tienda     = Join-Path $Raiz "Tienda"
$Respaldos  = Join-Path $Raiz "recursos\respaldos"
$Evidencias = Join-Path $Raiz "recursos\evidencias"
$Verificar  = Join-Path $PSScriptRoot "verificar.ps1"
$script:Tiempos = New-Object System.Collections.ArrayList
$script:Ugs = "ugs"

function Linea { param([string]$Texto = "", [string]$Color = "Gray") Write-Host $Texto -ForegroundColor $Color }

function Titulo {
    param([string]$Texto)
    Linea ""
    Linea ("=" * 74) "DarkCyan"
    Linea ("  " + $Texto) "Cyan"
    Linea ("=" * 74) "DarkCyan"
}

function Fase-Inicio {
    param([int]$Numero, [int]$Total, [string]$Nombre)
    Linea ""
    Linea ("[$Numero/$Total] $Nombre") "Yellow"
    return [System.Diagnostics.Stopwatch]::StartNew()
}

function Fase-Fin {
    param([string]$Nombre, $Reloj, [string]$Nota = "")
    $Reloj.Stop()
    $seg = [math]::Round($Reloj.Elapsed.TotalSeconds, 1)
    [void]$script:Tiempos.Add([pscustomobject]@{ Fase = $Nombre; Segundos = $seg })
    $texto = "      -> ${Nombre}: $seg s"
    if ($Nota -ne "") { $texto += "  ($Nota)" }
    Linea $texto "Green"
}

# Ejecuta el CLI y devuelve el código de salida. La salida se muestra y se guarda.
function Invocar-Ugs {
    param([string[]]$Argumentos, [switch]$PermitirFallo)
    $salida = & $script:Ugs @Argumentos 2>&1
    $codigo = $LASTEXITCODE
    $texto = @()
    foreach ($l in $salida) {
        $s = [string]$l
        $texto += $s
        if ($s.Trim() -ne "") { Linea ("   " + $s) "DarkGray" }
    }
    $script:UltimaSalida = ($texto -join "`n")
    if ($codigo -ne 0 -and -not $PermitirFallo) {
        throw "El CLI fallo (codigo $codigo): ugs $($Argumentos -join ' ')"
    }
    return $codigo
}

function Invocar-Dotnet {
    param([string[]]$Argumentos)
    $salida = & dotnet @Argumentos 2>&1
    $codigo = $LASTEXITCODE
    foreach ($l in $salida) {
        $s = [string]$l
        if ($s.Trim() -ne "") { Linea ("   " + $s) "DarkGray" }
    }
    if ($codigo -ne 0) { throw "dotnet fallo (codigo $codigo): dotnet $($Argumentos -join ' ')" }
    return $salida
}

# Lee el proyecto de Unity vinculado (fuente de verdad del destino).
function Leer-ProyectoUnity {
    $asset = Join-Path $Raiz "ProjectSettings\ProjectSettings.asset"
    if (-not (Test-Path $asset)) { return $null }
    $linea = Select-String -Path $asset -Pattern "^\s*cloudProjectId:\s*([0-9a-f\-]{36})" | Select-Object -First 1
    if ($linea -and $linea.Matches.Count -gt 0) { return $linea.Matches[0].Groups[1].Value }
    return $null
}

# Nombre de entorno -> id (lo necesita la verificación REST).
function Resolver-Entorno {
    param([string]$Nombre)
    $json = & $script:Ugs env list -p $Proyecto --json 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "No se pudieron listar los entornos. Revisa las credenciales y los roles de la service account.`n$($json -join "`n")"
    }
    $entornos = ($json -join "`n") | ConvertFrom-Json
    foreach ($e in @($entornos)) {
        if ($e.name -eq $Nombre) { return $e.id }
    }
    return $null
}

function Ver-Resumen {
    Linea ""
    Linea ("RESUMEN (segundos)") "Cyan"
    $total = 0
    foreach ($t in $script:Tiempos) {
        $total += $t.Segundos
        Linea ("   {0,-34} {1,7}" -f $t.Fase, $t.Segundos) "White"
    }
    Linea ("   {0,-34} {1,7}" -f "TOTAL", ([math]::Round($total, 1))) "Cyan"
    Linea ""
}

# ---------------------------------------------------------------------------
# Arranque
# ---------------------------------------------------------------------------
try {
    if (-not (Test-Path $Respaldos))  { New-Item -ItemType Directory -Force -Path $Respaldos | Out-Null }
    if (-not (Test-Path $Evidencias)) { New-Item -ItemType Directory -Force -Path $Evidencias | Out-Null }

    # Consola del CLI (tolera tildes en Windows PowerShell 5.1)
    try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch { }

    # El CLI instalado en el repo tiene prioridad sobre el del PATH.
    $ugsLocal = Join-Path $Raiz "ugs\ugs.exe"
    if (Test-Path $ugsLocal) { $script:Ugs = $ugsLocal }

    Titulo "LAB14 · DESPLIEGUE POR FASES  ·  Quizzerttack"
    Linea ("  Ambiente : " + $Ambiente) "White"
    if ($Simular) { Linea "  Modo     : SIMULACION (--dry-run, no cambia nada)" "Magenta" }
    else          { Linea "  Modo     : REAL" "White" }

    # ---- Guardas de credenciales (nunca se imprimen los valores) ----
    $falta = @()
    if ([string]::IsNullOrWhiteSpace($env:UGS_SERVICE_ACCOUNT_KEY_ID))     { $falta += "UGS_SERVICE_ACCOUNT_KEY_ID" }
    if ([string]::IsNullOrWhiteSpace($env:UGS_SERVICE_ACCOUNT_SECRET_KEY)) { $falta += "UGS_SERVICE_ACCOUNT_SECRET_KEY" }
    if ($falta.Count -gt 0) {
        Linea ""
        Linea ("ERROR: faltan variables de entorno: " + ($falta -join ", ")) "Red"
        Linea "  Las credenciales NO se guardan en el script. Abre una consola y define:" "Yellow"
        Linea '    $env:UGS_SERVICE_ACCOUNT_KEY_ID     = "<key id>"' "Yellow"
        Linea '    $env:UGS_SERVICE_ACCOUNT_SECRET_KEY = "<secret>"' "Yellow"
        exit 2
    }

    # ---- Proyecto destino ----
    $proyectoUnity = Leer-ProyectoUnity
    if ([string]::IsNullOrWhiteSpace($Proyecto)) {
        # El proyecto de Unity manda: evita desplegar al proyecto equivocado por una
        # variable de entorno vieja.
        if ($proyectoUnity) { $Proyecto = $proyectoUnity }
        elseif (-not [string]::IsNullOrWhiteSpace($env:UGS_PROJECT_ID)) { $Proyecto = $env:UGS_PROJECT_ID }
        else { throw "No se pudo determinar el proyecto: usa -Proyecto <id> o define UGS_PROJECT_ID." }
    }
    if ($proyectoUnity -and $Proyecto -ne $proyectoUnity) {
        Linea ""
        Linea "AVISO: el proyecto indicado no es el del proyecto de Unity." "Yellow"
        Linea ("  indicado : " + $Proyecto) "Yellow"
        Linea ("  Unity    : " + $proyectoUnity) "Yellow"
        Linea "  Se usara el indicado; si es un error, omite -Proyecto o corrige UGS_PROJECT_ID." "Yellow"
    }
    $Proyecto = $Proyecto.Trim()
    Linea ("  Proyecto : " + $Proyecto) "White"

    # ---- Producción exige confirmación (salvo en simulación) ----
    if ($Ambiente -eq "production" -and -not $Confirmar -and -not $Simular) {
        Linea ""
        Linea "ERROR: desplegar a PRODUCTION exige -Confirmar." "Red"
        Linea "  Ensaya primero:  .\desplegar.ps1 -Ambiente production -Simular" "Yellow"
        exit 2
    }

    # ---- Entorno destino ----
    $entornoId = Resolver-Entorno $Ambiente
    if (-not $entornoId) {
        Linea ""
        Linea ("ERROR: el entorno '" + $Ambiente + "' no existe en este proyecto.") "Red"
        Linea "  Crearlo UNA vez en el dashboard: Settings -> Environments -> Add environment" "Yellow"
        Linea "  (o con el CLI, si la cuenta tiene el rol Unity Environments Admin: ugs env add $Ambiente)" "Yellow"
        exit 2
    }
    Linea ("  Entorno  : " + $Ambiente + "  (" + $entornoId + ")") "White"

    # Transcripcion de la ejecucion como evidencia (sin credenciales).
    $sello = Get-Date -Format "yyyyMMdd-HHmmss"
    $log = Join-Path $Evidencias ("despliegue-" + $Ambiente + "-" + $sello + ".log")
    try { Start-Transcript -Path $log -Force | Out-Null } catch { }

    # =======================================================================
    # MODO REVERSIÓN
    # =======================================================================
    if (-not [string]::IsNullOrWhiteSpace($Revertir)) {
        Titulo ("REVERSION  ->  " + $Ambiente)
        if (-not (Test-Path $Revertir)) { throw "No existe el respaldo: $Revertir" }
        if ([System.IO.Path]::GetExtension($Revertir) -ne ".ccmzip") { throw "El respaldo debe ser un .ccmzip" }
        $zip = (Resolve-Path $Revertir).Path
        $dir = Split-Path $zip -Parent
        $file = Split-Path $zip -Leaf
        Linea ("  Respaldo : " + $file) "White"
        Linea "  Revertir el modulo NO revierte datos, politicas ni configuracion remota." "Yellow"

        $r = Fase-Inicio 1 3 "IMPORTAR RESPALDO"
        $argsImp = @("cc", "modules", "import", $dir, $file, "-p", $Proyecto, "-e", $Ambiente)
        if ($Simular) { $argsImp += "--dry-run" }
        Invocar-Ugs $argsImp | Out-Null
        Fase-Fin "Importar respaldo" $r

        if ($Simular) {
            Linea "   (simulacion: no se importo nada ni se verifico)" "DarkGray"
            Ver-Resumen
            try { Stop-Transcript | Out-Null } catch { }
            exit 0
        }

        $r = Fase-Inicio 2 3 ("ESPERA DE PROPAGACION (" + $EsperaSegundos + " s)")
        Start-Sleep -Seconds $EsperaSegundos
        Fase-Fin "Espera" $r

        $r = Fase-Inicio 3 3 "VERIFICACION"
        $codigo = 0
        if (Test-Path $Verificar) {
            $parametrosVerif = @{
                Proyecto = $Proyecto
                Ambiente = $Ambiente
                EntornoId = $entornoId
                EsperarSegundos = 0
                Reintentos = 3
            }
            & $Verificar @parametrosVerif
            $codigo = $LASTEXITCODE
        }
        Fase-Fin "Verificacion" $r
        Ver-Resumen
        try { Stop-Transcript | Out-Null } catch { }
        if ($codigo -ne 0) { exit 1 }
        Linea "REVERSION COMPLETA: responde la version anterior del modulo." "Green"
        exit 0
    }

    $total = 5

    # =======================================================================
    # FASE 1: PRUEBAS (si fallan, no se despliega nada)
    # =======================================================================
    $r = Fase-Inicio 1 $total "PRUEBAS LOCALES (sin nube)"
    $pruebas = Join-Path $Raiz "Tienda.Pruebas\Tienda.Pruebas.csproj"
    if (-not (Test-Path $pruebas)) { throw "No existe el proyecto de pruebas: $pruebas" }
    $salida = Invocar-Dotnet @("test", $pruebas, "--nologo", "-v", "q")
    $resumen = ($salida | Where-Object { [string]$_ -match "Superado|Passed!" } | Select-Object -Last 1)
    Fase-Fin "Pruebas" $r ([string]$resumen).Trim()

    # =======================================================================
    # FASE 2: RESPALDO DEL MÓDULO PUBLICADO
    # =======================================================================
    $r = Fase-Inicio 2 $total "RESPALDO DEL MODULO PUBLICADO"
    $existe = $false
    & $script:Ugs cc modules get Tienda -p $Proyecto -e $Ambiente -j *> $null
    if ($LASTEXITCODE -eq 0) { $existe = $true }

    $archivoRespaldo = $null
    if ($existe) {
        $archivoRespaldo = $Ambiente + "-" + (Get-Date -Format "yyyyMMdd-HHmmss") + ".ccmzip"
        $codigo = Invocar-Ugs @("cc", "modules", "export", $Respaldos, $archivoRespaldo, "-p", $Proyecto, "-e", $Ambiente) -PermitirFallo
        if ($codigo -ne 0) { throw "No se pudo respaldar el modulo. No se despliega sin respaldo." }
        $rutaZip = Join-Path $Respaldos $archivoRespaldo
        $mb = [math]::Round((Get-Item $rutaZip).Length / 1MB, 2)
        Fase-Fin "Respaldo" $r ($archivoRespaldo + ", " + $mb + " MB")
    } else {
        Linea "   (el modulo Tienda aun no existe en este entorno: no hay nada que respaldar)" "DarkGray"
        Fase-Fin "Respaldo" $r "primer despliegue"
    }

    # =======================================================================
    # FASE 3: CONFIGURACIÓN REMOTA (SIEMPRE ANTES QUE EL MÓDULO)
    # =======================================================================
    $r = Fase-Inicio 3 $total "CONFIGURACION REMOTA (.rc + .lb + .ac)"
    $rcs = @(Get-ChildItem -Path $Tienda -Filter "*.rc" -File | Sort-Object Name)
    $lbs = @(Get-ChildItem -Path $Tienda -Filter "*.lb" -File | Sort-Object Name)
    $acs = @(Get-ChildItem -Path $Tienda -Filter "*.ac" -File | Sort-Object Name)
    if ($rcs.Count -eq 0) { throw "No hay archivos .rc en $Tienda" }

    Linea ("   llaves: " + (($rcs | ForEach-Object { $_.Name }) -join ", ")) "DarkGray"
    foreach ($rc in $rcs) {
        try {
            $jsonRc = Get-Content $rc.FullName -Raw | ConvertFrom-Json
            $claves = @($jsonRc.entries.PSObject.Properties | ForEach-Object { $_.Name })
            Linea ("      " + $rc.Name + " -> " + ($claves -join ", ")) "DarkGray"
        } catch {
            Linea ("      " + $rc.Name + " -> (no se pudo leer el JSON)") "DarkYellow"
        }
    }
    if ($lbs.Count -gt 0) { Linea ("   tablas: " + (($lbs | ForEach-Object { $_.Name }) -join ", ")) "DarkGray" }
    if ($acs.Count -gt 0) { Linea ("   politicas: " + (($acs | ForEach-Object { $_.Name }) -join ", ")) "DarkGray" }

    $comun = @("-p", $Proyecto, "-e", $Ambiente)
    if ($Simular) { $comun += "--dry-run" }

    $rutasRc = @($rcs | ForEach-Object { $_.FullName })
    $codigo = Invocar-Ugs (@("deploy") + $rutasRc + @("-s", "remote-config") + $comun) -PermitirFallo
    if ($codigo -ne 0) {
        Linea ""
        Linea "   El despliegue de Remote Config fallo. Causas habituales:" "Red"
        Linea "     - la service account no tiene el rol 'Remote Config Admin' (403 Forbidden)" "Yellow"
        Linea '     - un .rc con valores JSON sin declarar en "types"' "Yellow"
        throw "No se despliega el modulo si la configuracion no sube."
    }

    foreach ($lb in $lbs) {
        $codigo = Invocar-Ugs (@("deploy", $lb.FullName, "-s", "leaderboards") + $comun) -PermitirFallo
        if ($codigo -ne 0) { throw ("No se pudo desplegar la tabla " + $lb.Name) }
    }
    foreach ($ac in $acs) {
        $codigo = Invocar-Ugs (@("deploy", $ac.FullName, "-s", "access") + $comun) -PermitirFallo
        if ($codigo -ne 0) { throw ("No se pudo desplegar la politica " + $ac.Name) }
    }
    Fase-Fin "Configuracion" $r ($rcs.Count.ToString() + " .rc, " + $lbs.Count + " .lb, " + $acs.Count + " .ac")

    # =======================================================================
    # FASE 4: MÓDULO (build + deploy)
    # =======================================================================
    $r = Fase-Inicio 4 $total "MODULO DE CLOUD CODE"
    $sln = Join-Path $Tienda "Tienda.sln"
    Invocar-Dotnet @("build", $sln, "-c", "Release", "--nologo", "-v", "q") | Out-Null
    $codigo = Invocar-Ugs (@("deploy", $sln, "-s", "cloud-code-modules", "-p", $Proyecto, "-e", $Ambiente) + $(if ($Simular) { @("--dry-run") } else { @() })) -PermitirFallo
    if ($codigo -ne 0) {
        Linea ""
        Linea "   El modulo no se pudo publicar. Si fue 403, falta el rol 'Cloud Code Editor'." "Red"
        throw "Fallo el despliegue del modulo."
    }
    Fase-Fin "Modulo" $r "Tienda"

    # =======================================================================
    # FASE 5: ESPERA + VERIFICACIÓN
    # =======================================================================
    $r = Fase-Inicio 5 $total "ESPERA + VERIFICACION"
    if ($Simular) {
        Linea "   (simulacion: no se crea ningun jugador ni se ejecuta la verificacion)" "DarkGray"
        Fase-Fin "Verificacion" $r "omitida"
        Ver-Resumen
        Linea "SIMULACION TERMINADA: no se cambio nada en el backend." "Magenta"
        try { Stop-Transcript | Out-Null } catch { }
        exit 0
    }

    Linea ("   esperando " + $EsperaSegundos + " s a que propague la version...") "DarkGray"
    Start-Sleep -Seconds $EsperaSegundos

    $parametrosVerif = @{
        Proyecto = $Proyecto
        Ambiente = $Ambiente
        EntornoId = $entornoId
        EsperarSegundos = 0
    }
    if ($VerificarEconomia) { $parametrosVerif.VerificarEconomia = $true }
    & $Verificar @parametrosVerif
    $codigo = $LASTEXITCODE
    if ($codigo -ne 0) { throw "La verificacion fallo: el entorno no responde como se esperaba." }
    Fase-Fin "Verificacion" $r "jugador anonimo nuevo"

    Ver-Resumen
    try { Stop-Transcript | Out-Null } catch { }
    Linea ("DESPLIEGUE COMPLETO en " + $Ambiente + ".") "Green"
    exit 0
}
catch {
    Linea ""
    Linea ("ERROR: " + $_.Exception.Message) "Red"
    if ($script:Tiempos.Count -gt 0) { Ver-Resumen }
    try { Stop-Transcript | Out-Null } catch { }
    Linea "No se continuo con las fases siguientes." "Red"
    exit 1
}
