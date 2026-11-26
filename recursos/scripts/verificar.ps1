<#
=============================================================================
 LAB14 · VERIFICACION DEL DESPLIEGUE
=============================================================================
 Entra como un JUGADOR ANONIMO NUEVO al entorno indicado y comprueba que
 responde la version desplegada:

   1. Autenticacion anonima (sin usuario ni contrasena).
   2. HoraDelServidor  -> entorno esperado + marca del kit ("dvsc-20261105").
   3. -VerificarEconomia -> circuito economico: comprar un heroe que no alcanza,
      buscar rival y ver la temporada. Mide milisegundos por llamada.
   4. -ProvocarError -> invoca ForzarErrorPrueba (error deliberado) y deja la
      evidencia para leer la traza en Cloud Code -> Logs.

 No imprime tokens. Uso:

   .\verificar.ps1 -Proyecto <id> -Ambiente development
   .\verificar.ps1 -Proyecto <id> -Ambiente development -VerificarEconomia
   .\verificar.ps1 -Proyecto <id> -Ambiente development -ProvocarError
=============================================================================
#>
#Requires -Version 5.1
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Proyecto,

    [ValidateSet("development", "production")]
    [string]$Ambiente = "development",

    # Si no se pasa, se resuelve con `ugs env list`.
    [string]$EntornoId,

    [int]$EsperarSegundos = 0,

    # Reintentos de la verificacion (la propagacion del modulo puede tardar).
    [int]$Reintentos = 1,
    [int]$IntervaloSegundos = 10,

    # Circuito economico con el jugador anonimo nuevo.
    [switch]$VerificarEconomia,

    # Error deliberado: deja la traza en los logs del servidor.
    [switch]$ProvocarError
)

$ErrorActionPreference = "Continue"
$script:Fallos = New-Object System.Collections.ArrayList
$script:Tiempos = New-Object System.Collections.ArrayList

$Raiz = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$script:Ugs = Join-Path $Raiz "ugs\ugs.exe"
if (-not (Test-Path $script:Ugs)) { $script:Ugs = "ugs" }

function Linea { param([string]$Texto = "", [string]$Color = "Gray") Write-Host $Texto -ForegroundColor $Color }

function Fallo {
    param([string]$Texto)
    [void]$script:Fallos.Add($Texto)
    Linea ("   x " + $Texto) "Red"
}

function Bien { param([string]$Texto) Linea ("   ok " + $Texto) "Green" }

# Lee una propiedad sin depender de mayusculas (el API devuelve camelCase).
function Campo {
    param($Objeto, [string]$Nombre)
    if ($null -eq $Objeto) { return $null }
    foreach ($p in $Objeto.PSObject.Properties) {
        if ($p.Name -ieq $Nombre) { return $p.Value }
    }
    return $null
}

function Leer-CuerpoError {
    param($Error)
    try { if ($Error.ErrorDetails -and $Error.ErrorDetails.Message) { return $Error.ErrorDetails.Message } } catch { }
    try {
        $resp = $Error.Exception.Response
        if ($resp) {
            $stream = $resp.GetResponseStream()
            if ($stream) {
                $lector = New-Object System.IO.StreamReader($stream)
                return $lector.ReadToEnd()
            }
        }
    } catch { }
    return ""
}

function Estado-Http {
    param($Error)
    try { return [int]$Error.Exception.Response.StatusCode } catch { return 0 }
}

# Llama a una funcion del modulo Tienda con el token del jugador.
function Invocar-Funcion {
    param(
        [string]$Funcion,
        [hashtable]$Parametros,
        [string]$Token,
        [string]$Etiqueta = ""
    )
    $uri = "https://cloud-code.services.api.unity.com/v1/projects/$Proyecto/modules/Tienda/$Funcion"
    $cuerpo = @{ params = $Parametros } | ConvertTo-Json -Depth 8 -Compress
    $cabeceras = @{ "Authorization" = "Bearer $Token"; "Content-Type" = "application/json" }
    $reloj = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        $respuesta = Invoke-RestMethod -Method Post -Uri $uri -Headers $cabeceras -Body $cuerpo -TimeoutSec 120
        $reloj.Stop()
        $ms = $reloj.ElapsedMilliseconds
        if ($Etiqueta -ne "") { [void]$script:Tiempos.Add([pscustomobject]@{ Llamada = $Etiqueta; Ms = $ms }) }
        return @{ Ok = $true; Salida = $respuesta.output; Ms = $ms; Estado = 200; Cuerpo = "" }
    } catch {
        $reloj.Stop()
        $ms = $reloj.ElapsedMilliseconds
        if ($Etiqueta -ne "") { [void]$script:Tiempos.Add([pscustomobject]@{ Llamada = ($Etiqueta + " (error)"); Ms = $ms }) }
        return @{
            Ok = $false; Salida = $null; Ms = $ms
            Estado = (Estado-Http $_)
            Cuerpo = (Leer-CuerpoError $_)
        }
    }
}

try {
    if ($EsperarSegundos -gt 0) {
        Linea ("Esperando " + $EsperarSegundos + " s a que propague el despliegue...") "DarkGray"
        Start-Sleep -Seconds $EsperarSegundos
    }

    # ---- Entorno ----
    if ([string]::IsNullOrWhiteSpace($EntornoId)) {
        $json = & $script:Ugs env list -p $Proyecto --json 2>&1
        if ($LASTEXITCODE -ne 0) { throw "No se pudieron listar los entornos del proyecto." }
        $entornos = ($json -join "`n") | ConvertFrom-Json
        foreach ($e in @($entornos)) { if ($e.name -eq $Ambiente) { $EntornoId = $e.id } }
    }
    if ([string]::IsNullOrWhiteSpace($EntornoId)) { throw ("El entorno '" + $Ambiente + "' no existe en el proyecto.") }

    Linea ""
    Linea ("VERIFICACION  ·  " + $Ambiente + "  (" + $EntornoId + ")") "Cyan"
    Linea ("Proyecto: " + $Proyecto) "DarkGray"

    # ---- 1. Jugador anonimo NUEVO ----
    $cabecerasAuth = @{ "ProjectId" = $Proyecto; "UnityEnvironment" = $Ambiente; "Content-Type" = "application/json" }
    $auth = Invoke-RestMethod -Method Post -Uri "https://player-auth.services.api.unity.com/v1/authentication/anonymous" `
        -Headers $cabecerasAuth -Body "{}" -TimeoutSec 60
    $token = $auth.idToken
    $jugador = [string]$auth.userId
    if ($jugador.Length -gt 8) { $jugador = $jugador.Substring(0, 8) + "..." }
    if ([string]::IsNullOrWhiteSpace($token)) { throw "La autenticacion anonima no devolvio idToken." }
    Bien ("jugador anonimo nuevo: " + $jugador)

    # ---- 2. HoraDelServidor (con reintentos) ----
    $intento = 0
    $verificado = $false
    while ($intento -lt $Reintentos -and -not $verificado) {
        $intento++
        $r = Invocar-Funcion -Funcion "HoraDelServidor" -Token $token `
            -Parametros @{ entornoEsperado = $Ambiente } -Etiqueta "HoraDelServidor"
        if ($r.Ok) {
            $marca = [string](Campo $r.Salida "marca")
            $entorno = [string](Campo $r.Salida "entorno")
            $ahora = [string](Campo $r.Salida "ahoraUtc")
            if ($marca -eq "dvsc-20261105" -and $entorno -eq $Ambiente) {
                Bien ("HoraDelServidor responde: entorno=" + $entorno + ", marca=" + $marca + " (" + $r.Ms + " ms)")
                Linea ("      hora del servidor: " + $ahora) "DarkGray"
                $verificado = $true
            } else {
                Fallo ("HoraDelServidor respondio otra cosa: entorno='" + $entorno + "' marca='" + $marca + "'")
            }
        } elseif ($r.Estado -eq 404) {
            Linea ("   ... intento " + $intento + "/" + $Reintentos + ": el modulo aun no expone HoraDelServidor (404)") "Yellow"
        } else {
            Fallo ("HoraDelServidor fallo (HTTP " + $r.Estado + "): " + $r.Cuerpo)
        }
        if (-not $verificado -and $intento -lt $Reintentos) { Start-Sleep -Seconds $IntervaloSegundos }
    }
    if (-not $verificado) {
        Fallo "La version desplegada no respondio como se esperaba (HoraDelServidor)."
    }

    # ---- 3. Circuito economico (opcional) ----
    if ($VerificarEconomia) {
        Linea ""
        Linea "CIRCUITO ECONOMICO (jugador nuevo, sin datos previos)" "Cyan"

        # idOperacion: el GUID que viaja en la llamada y aparece en los logs.
        $idOperacion = [guid]::NewGuid().ToString("N")
        Linea ("  idOperacion: " + $idOperacion) "DarkGray"

        $compra = Invocar-Funcion -Funcion "ComprarHeroe" -Token $token `
            -Parametros @{ idHeroe = "mago"; idOperacion = $idOperacion } -Etiqueta "ComprarHeroe"
        if ($compra.Ok) {
            $exito = [string](Campo $compra.Salida "exito")
            $mensaje = [string](Campo $compra.Salida "mensaje")
            if ($exito -eq "True" -or $exito -eq "true") {
                Bien ("ComprarHeroe cobro en el servidor: " + $mensaje + " (" + $compra.Ms + " ms)")
            } elseif ($mensaje -match "Te faltan") {
                Bien ("ComprarHeroe rechazo sin fondos (sin escrituras): " + $mensaje + " (" + $compra.Ms + " ms)")
            } else {
                Fallo ("ComprarHeroe respondio algo inesperado: " + $mensaje)
            }
        } else {
            Fallo ("ComprarHeroe fallo (HTTP " + $compra.Estado + "): " + $compra.Cuerpo)
        }

        $rival = Invocar-Funcion -Funcion "BuscarRival" -Token $token `
            -Parametros @{ req = @{ nivel = 1; franjaMaxima = 10; candidatosSuficientes = 2; playerId = "" } } -Etiqueta "BuscarRival"
        if ($rival.Ok) {
            $mensaje = [string](Campo $rival.Salida "mensaje")
            Bien ("BuscarRival consulto el indice publico: " + $mensaje + " (" + $rival.Ms + " ms)")
        } else {
            Fallo ("BuscarRival fallo (HTTP " + $rival.Estado + "): " + $rival.Cuerpo)
        }

        $temporada = Invocar-Funcion -Funcion "VistaTemporada" -Token $token `
            -Parametros @{} -Etiqueta "VistaTemporada"
        if ($temporada.Ok) {
            $tabla = [string](Campo $temporada.Salida "tabla")
            $cierre = [string](Campo $temporada.Salida "cierreUtc")
            Bien ("VistaTemporada responde: tabla=" + $tabla + ", cierre=" + $cierre + " (" + $temporada.Ms + " ms)")
        } else {
            Fallo ("VistaTemporada fallo (HTTP " + $temporada.Estado + "): " + $temporada.Cuerpo)
        }

        # Tres combates: el jugador nuevo no tiene ese heroe, pero la llamada ya
        # lee el estado protegido y la configuracion (es lo que se quiere medir).
        for ($i = 1; $i -le 3; $i++) {
            $combate = Invocar-Funcion -Funcion "ResolverCombate" -Token $token `
                -Parametros @{ equipo = @("escudero"); idRival = "npc_practica_1"; idOperacion = [guid]::NewGuid().ToString("N") } `
                -Etiqueta ("ResolverCombate #" + $i)
            if ($combate.Ok) {
                $mensaje = [string](Campo $combate.Salida "mensaje")
                $victoria = [string](Campo $combate.Salida "victoria")
                Bien ("ResolverCombate #" + $i + ": victoria=" + $victoria + " (" + $combate.Ms + " ms)")
            } elseif ($combate.Estado -eq 400 -or $combate.Estado -eq 422) {
                Linea ("   ... ResolverCombate #" + $i + " rechazado por el servidor (esperado en jugador nuevo), " + $combate.Ms + " ms") "DarkGray"
            } else {
                Fallo ("ResolverCombate #" + $i + " fallo (HTTP " + $combate.Estado + "): " + $combate.Cuerpo)
            }
        }

        Linea ""
        Linea "  Busca en Cloud Code -> Logs (entorno $Ambiente) este idOperacion" "Yellow"
        Linea ('  para ver la linea: operacion="ComprarHeroe" id="' + $idOperacion + '" resultado="..." lecturas=... escrituras=...') "Yellow"
    }

    # ---- 4. Error deliberado (opcional) ----
    if ($ProvocarError) {
        Linea ""
        Linea "ERROR DELIBERADO (evidencia de traza en los logs)" "Cyan"
        $idErr = [guid]::NewGuid().ToString("N")
        $r = Invocar-Funcion -Funcion "ForzarErrorPrueba" -Token $token `
            -Parametros @{ motivo = "lab14" } -Etiqueta "ForzarErrorPrueba"
        if ($r.Ok) {
            Fallo "ForzarErrorPrueba NO fallo; se esperaba un error deliberado."
        } elseif ($r.Estado -eq 404) {
            Fallo "ForzarErrorPrueba no existe en la version desplegada (404)."
        } elseif ($r.Estado -ge 400) {
            $detalle = ""
            try {
                $obj = $r.Cuerpo | ConvertFrom-Json
                $detalle = [string](Campo $obj "detail")
                if ([string]::IsNullOrWhiteSpace($detalle)) { $detalle = [string](Campo $obj "title") }
            } catch { $detalle = $r.Cuerpo }
            Bien ("el servidor devolvio HTTP " + $r.Estado + " (error deliberado) en " + $r.Ms + " ms")
            Linea ("      respuesta al cliente: " + $detalle) "DarkGray"
            Linea ""
            Linea "  AHORA (captura para el informe):" "Yellow"
            Linea "    1. Dashboard -> Cloud Code -> Tienda -> Logs (entorno $Ambiente)" "Yellow"
            Linea "    2. Busca 'Fallo en ForzarErrorPrueba'" "Yellow"
            Linea "    3. Abre la entrada: se ve el tipo, el mensaje y la traza con archivo y linea" "Yellow"
            Linea '       (la linea lleva: catch + _logger.LogError(exception, "Fallo en ...: {Traza}", exception.StackTrace))' "DarkGray"
        }
    }

    # ---- Resumen ----
    if ($script:Tiempos.Count -gt 0) {
        Linea ""
        Linea "  Llamadas medidas (incluyen red)" "Cyan"
        foreach ($t in $script:Tiempos) { Linea ("     {0,-32} {1,6} ms" -f $t.Llamada, $t.Ms) "White" }
    }

    if ($script:Fallos.Count -gt 0) {
        Linea ""
        Linea ("VERIFICACION CON FALLOS (" + $script:Fallos.Count + ")") "Red"
        exit 1
    }
    Linea ""
    Linea "VERIFICACION OK" "Green"
    exit 0
}
catch {
    Linea ""
    Linea ("ERROR: " + $_.Exception.Message) "Red"
    exit 1
}
