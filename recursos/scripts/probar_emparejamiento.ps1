# =============================================================================
#  DIAGNOSTICO · BUSCAR RIVAL Y REANUDAR (modulo QuizzerMatch, protocolo 3)
# =============================================================================
#  Entra como TRES jugadores anonimos nuevos y comprueba contra el modulo desplegado:
#    1. handshake GetProtocolVersion (debe ser 3)
#    2. A busca rival -> espera en su sala; B busca -> cae en la sala de A (en juego)
#    3. A reanuda con GetActiveMatch (sin escribir el codigo)
#    4. A no puede crear otra partida mientras juega ("una partida a la vez")
#    5. C busca y cancela -> su sala queda cancelada y sin partida activa
#    6. limpieza: A abandona; ya no tiene partida activa
#  No imprime tokens. Usa las mismas funciones que probar_partida_online.ps1.
#  Por defecto va contra "development": crea jugadores anonimos nuevos en cada ejecucion.
#  Uso:
#    .\probar_emparejamiento.ps1
#    .\probar_emparejamiento.ps1 -Ambiente production
# =============================================================================
#Requires -Version 5.1
[CmdletBinding()]
param(
    [string]$Proyecto = "5906c36b-06d8-410b-91be-cf367616022a",
    [ValidateSet("development", "production")]
    [string]$Ambiente = "development",
    [int]$ProtocoloEsperado = 3
)

$ErrorActionPreference = "Stop"
$Base = "https://cloud-code.services.api.unity.com/v1/projects/$Proyecto/modules"
$AuthUrl = "https://player-auth.services.api.unity.com/v1/authentication/anonymous"
$script:Fallos = 0

function Linea { param([string]$Texto = "", [string]$Color = "Gray") Write-Host $Texto -ForegroundColor $Color }

function Campo {
    param($Objeto, [string]$Nombre)
    if ($null -eq $Objeto) { return $null }
    foreach ($p in $Objeto.PSObject.Properties) { if ($p.Name -ieq $Nombre) { return $p.Value } }
    return $null
}

function Nuevo-Jugador {
    param([string]$Etiqueta)
    $cab = @{ "ProjectId" = $Proyecto; "UnityEnvironment" = $Ambiente; "Content-Type" = "application/json" }
    $r = Invoke-RestMethod -Method Post -Uri $AuthUrl -Headers $cab -Body "{}" -TimeoutSec 60
    if ([string]::IsNullOrWhiteSpace($r.idToken)) { throw "Sin idToken para $Etiqueta" }
    $corto = [string]$r.userId
    if ($corto.Length -gt 8) { $corto = $corto.Substring(0, 8) + "..." }
    Linea ("  $Etiqueta -> $corto") "DarkGray"
    return @{ Nombre = $Etiqueta; Token = $r.idToken; Id = [string]$r.userId }
}

function Llamar {
    param([string]$Funcion, [string]$Token, [hashtable]$Parametros = @{})
    $uri = "$Base/QuizzerMatch/$Funcion"
    $cuerpo = @{ params = $Parametros } | ConvertTo-Json -Depth 10 -Compress
    $cab = @{ "Authorization" = "Bearer $Token"; "Content-Type" = "application/json" }
    try {
        $r = Invoke-RestMethod -Method Post -Uri $uri -Headers $cab -Body $cuerpo -TimeoutSec 90
        return @{ Ok = $true; Salida = $r.output; Error = "" }
    } catch {
        $texto = ""
        try { $texto = $_.ErrorDetails.Message } catch { }
        if (-not $texto) { try { $texto = $_.Exception.Message } catch { } }
        return @{ Ok = $false; Salida = $null; Error = $texto }
    }
}

function Nueva-Peticion { return [guid]::NewGuid().ToString("N") }

function Comprobar {
    param([bool]$Condicion, [string]$Que, [string]$Detalle = "")
    if ($Condicion) { Linea ("  OK    " + $Que) "Green" }
    else { $script:Fallos++; Linea ("  FALLO " + $Que + $(if ($Detalle) { "  ->  " + $Detalle } else { "" })) "Red" }
}

function Resumen { param($V) return ("codigo={0} estado={1} resultado={2} mensaje='{3}'" -f `
    (Campo $V "codigo"), (Campo $V "estado"), (Campo $V "resultado"), (Campo $V "mensaje")) }

Linea ""
Linea "BUSCAR RIVAL Y REANUDAR · $Ambiente" "Cyan"
Linea "Proyecto: $Proyecto" "DarkGray"

Linea ""
Linea "0) Jugadores anonimos nuevos" "Yellow"
$A = Nuevo-Jugador "A"
$B = Nuevo-Jugador "B"
$C = Nuevo-Jugador "C"

Linea ""
Linea "1) Handshake del protocolo" "Yellow"
$v = Llamar "GetProtocolVersion" $A.Token
if (-not $v.Ok) { throw ("GetProtocolVersion fallo: " + $v.Error) }
Comprobar ([int]$v.Salida -eq $ProtocoloEsperado) ("el servidor habla el protocolo $ProtocoloEsperado") `
    ("el servidor dice {0}: despliega QuizzerMatch otra vez" -f $v.Salida)
if ([int]$v.Salida -ne $ProtocoloEsperado) { Linea ""; Linea "No sigo: el modulo desplegado es de otra version." "Red"; exit 1 }

Linea ""
Linea "2) Buscar rival" "Yellow"
$a = Llamar "QuickMatch" $A.Token @{ requestId = (Nueva-Peticion); faccion = "F1" }
if (-not $a.Ok) { throw ("QuickMatch (A) fallo: " + $a.Error) }
$codigo = [string](Campo $a.Salida "codigo")
Linea ("  A: " + (Resumen $a.Salida)) "DarkGray"
Comprobar ((Campo $a.Salida "estado") -eq "esperando_rival") "A queda esperando en su sala"

$b = Llamar "QuickMatch" $B.Token @{ requestId = (Nueva-Peticion); faccion = "F2" }
if (-not $b.Ok) { throw ("QuickMatch (B) fallo: " + $b.Error) }
Linea ("  B: " + (Resumen $b.Salida)) "DarkGray"
Comprobar ([string](Campo $b.Salida "codigo") -eq $codigo) "B cae en la sala de A" ("B esta en " + (Campo $b.Salida "codigo"))
Comprobar ((Campo $b.Salida "estado") -eq "en_juego") "la partida empieza"

Linea ""
Linea "3) Reanudar sin el codigo" "Yellow"
$r = Llamar "GetActiveMatch" $A.Token
Comprobar ($r.Ok -and [string](Campo $r.Salida "codigo") -eq $codigo) "GetActiveMatch devuelve la partida de A" $r.Error
Comprobar ($r.Ok -and @(Campo $r.Salida "miMano").Count -eq 5) "con la vista completa (5 cartas en la mano)"

Linea ""
Linea "4) Una partida a la vez" "Yellow"
$otra = Llamar "CreateMatch" $A.Token @{ requestId = (Nueva-Peticion); faccion = "F1" }
Comprobar ((-not $otra.Ok) -and ($otra.Error -match "partida en curso")) "A no puede crear otra mientras juega" `
    $(if ($otra.Ok) { "se creo " + (Campo $otra.Salida "codigo") } else { $otra.Error })
$otraVez = Llamar "QuickMatch" $A.Token @{ requestId = (Nueva-Peticion); faccion = "F1" }
Comprobar ($otraVez.Ok -and [string](Campo $otraVez.Salida "codigo") -eq $codigo) "Buscar rival de nuevo reanuda la misma partida"

Linea ""
Linea "5) Buscar y cancelar" "Yellow"
$c = Llamar "QuickMatch" $C.Token @{ requestId = (Nueva-Peticion); faccion = "F3" }
Comprobar ($c.Ok -and (Campo $c.Salida "estado") -eq "esperando_rival") "C queda esperando (A y B ya juegan)"
$cc = Llamar "CancelQuickMatch" $C.Token
Comprobar ($cc.Ok -and (Campo $cc.Salida "motivoFin") -eq "cancelada") "cancelar deja la sala de C cancelada" $cc.Error
$ca = Llamar "GetActiveMatch" $C.Token
Comprobar ($ca.Ok -and [string](Campo $ca.Salida "codigo") -eq "") "C ya no tiene partida activa"

Linea ""
Linea "6) Limpieza" "Yellow"
$fin = Llamar "PlayMove" $A.Token @{ matchId = $codigo; requestId = (Nueva-Peticion); turnoEsperado = -1; tipo = "abandonar"
    heroeId = ""; casilla = -1; peleadorId = ""; indiceHabilidad = 0; preguntaId = 0; opcion = 0 }
Comprobar ($fin.Ok -and (Campo $fin.Salida "estado") -eq "terminada") "A abandona y la partida termina" $fin.Error
$fa = Llamar "GetActiveMatch" $A.Token
Comprobar ($fa.Ok -and [string](Campo $fa.Salida "codigo") -eq "") "A ya no tiene partida activa"

Linea ""
if ($script:Fallos -eq 0) { Linea "RESULTADO: buscar rival, reanudar y cancelar FUNCIONAN contra el modulo desplegado." "Green"; exit 0 }
Linea ("RESULTADO: {0} comprobacion(es) fallaron; copien esta salida en el PR." -f $script:Fallos) "Red"
exit 1
