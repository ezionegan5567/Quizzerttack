# =============================================================================
#  DIAGNOSTICO · PARTIDA ONLINE POR TURNOS (modulo QuizzerMatch)
# =============================================================================
#  Entra como DOS jugadores anonimos nuevos y juega una partida real:
#    1. handshake GetProtocolVersion
#    2. CreateMatch (A) / JoinMatch (B)
#    3. unos turnos de colocar / elegir habilidad / responder quiz / pasar
#  No imprime tokens. Uso:
#    .\probar_partida_online.ps1
#    .\probar_partida_online.ps1 -Ambiente development
# =============================================================================
#Requires -Version 5.1
[CmdletBinding()]
param(
    [string]$Proyecto = "5906c36b-06d8-410b-91be-cf367616022a",
    [ValidateSet("development", "production")]
    [string]$Ambiente = "production",
    [int]$Jugadas = 10
)

$ErrorActionPreference = "Stop"
$Base = "https://cloud-code.services.api.unity.com/v1/projects/$Proyecto/modules"
$AuthUrl = "https://player-auth.services.api.unity.com/v1/authentication/anonymous"

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
    param([string]$Modulo, [string]$Funcion, [string]$Token, [hashtable]$Parametros = @{})
    $uri = "$Base/$Modulo/$Funcion"
    $cuerpo = @{ params = $Parametros } | ConvertTo-Json -Depth 10 -Compress
    $cab = @{ "Authorization" = "Bearer $Token"; "Content-Type" = "application/json" }
    $reloj = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        $r = Invoke-RestMethod -Method Post -Uri $uri -Headers $cab -Body $cuerpo -TimeoutSec 90
        $reloj.Stop()
        return @{ Ok = $true; Salida = $r.output; Ms = $reloj.ElapsedMilliseconds; Error = "" }
    } catch {
        $reloj.Stop()
        $texto = ""
        try { $texto = $_.ErrorDetails.Message } catch { }
        if (-not $texto) { try { $texto = $_.Exception.Message } catch { } }
        return @{ Ok = $false; Salida = $null; Ms = $reloj.ElapsedMilliseconds; Error = $texto }
    }
}

function Nueva-Peticion { return [guid]::NewGuid().ToString("N") }

function Estado-Vista {
    param($V)
    $preg = Campo $V "pregunta"
    return ("estado={0} fase={1} turno={2} ronda={3} esTuTurno={4} campo={5} mano={6} pregunta={7} resultado={8}" -f `
        (Campo $V "estado"), (Campo $V "fase"), (Campo $V "numeroTurno"), (Campo $V "ronda"), `
        (Campo $V "esTuTurno"), (@(Campo $V "miCampo").Count), (@(Campo $V "miMano").Count), `
        ($(if ($null -ne $preg) { "si" } else { "no" })), (Campo $V "resultado"))
}

Linea ""
Linea "PARTIDA ONLINE POR TURNOS · $Ambiente" "Cyan"
Linea "Proyecto: $Proyecto" "DarkGray"
Linea ""
Linea "1) Jugadores anonimos nuevos" "Yellow"
$A = Nuevo-Jugador "A"
$B = Nuevo-Jugador "B"

Linea ""
Linea "2) Handshake del protocolo" "Yellow"
$v = Llamar "QuizzerMatch" "GetProtocolVersion" $A.Token
if (-not $v.Ok) { throw ("GetProtocolVersion fallo: " + $v.Error) }
Linea ("  servidor = {0} (cliente espera 2) · {1} ms" -f $v.Salida, $v.Ms) "Green"

Linea ""
Linea "3) Crear y unirse" "Yellow"
$creada = Llamar "QuizzerMatch" "CreateMatch" $A.Token @{ requestId = (Nueva-Peticion); faccion = "F1" }
if (-not $creada.Ok) { throw ("CreateMatch fallo: " + $creada.Error) }
$codigo = [string](Campo $creada.Salida "codigo")
Linea ("  A crea la partida {0} · {1}" -f $codigo, (Estado-Vista $creada.Salida)) "Green"

$union = Llamar "QuizzerMatch" "JoinMatch" $B.Token @{ matchId = $codigo; requestId = (Nueva-Peticion); faccion = "F2" }
if (-not $union.Ok) { throw ("JoinMatch fallo: " + $union.Error) }
Linea ("  B se une · {0}" -f (Estado-Vista $union.Salida)) "Green"

Linea ""
Linea "4) Turnos reales (cada jugada la valida el servidor)" "Yellow"
$vistas = @{}
$vistas[$A.Id] = Llamar "QuizzerMatch" "GetMatchState" $A.Token @{ matchId = $codigo }
$vistas[$B.Id] = Llamar "QuizzerMatch" "GetMatchState" $B.Token @{ matchId = $codigo }

$eventos = 0
for ($i = 1; $i -le $Jugadas; $i++) {
    # Elegir de quien es el turno segun la vista de cada uno.
    $quien = $null
    foreach ($j in @($A, $B)) {
        $vv = $vistas[$j.Id]
        if ($vv.Ok -and (Campo $vv.Salida "esTuTurno") -eq $true) { $quien = $j; break }
    }
    if ($null -eq $quien) { Linea "  (nadie tiene el turno: fin o espera)" "DarkYellow"; break }

    $actual = Llamar "QuizzerMatch" "GetMatchState" $quien.Token @{ matchId = $codigo }
    if (-not $actual.Ok) { Linea ("  GetMatchState fallo: " + $actual.Error) "Red"; break }
    $vista = $actual.Salida
    $estado = [string](Campo $vista "estado")
    if ($estado -eq "terminada") { Linea "  partida terminada" "DarkYellow"; break }

    $fase = [string](Campo $vista "fase")
    $turno = [int](Campo $vista "numeroTurno")
    $params = @{
        matchId = $codigo; requestId = (Nueva-Peticion); turnoEsperado = $turno
        tipo = "pasar"; heroeId = ""; casilla = -1; peleadorId = ""; indiceHabilidad = 0; preguntaId = 0; opcion = 0
    }

    $pregunta = Campo $vista "pregunta"
    $mano = @(Campo $vista "miMano")
    $campo = @(Campo $vista "miCampo")

    if ($null -ne $pregunta) {
        $params.tipo = "responder_quiz"
        $params.preguntaId = [int](Campo $pregunta "preguntaId")
        $params.opcion = 0
        $detalle = "responde quiz " + $params.preguntaId
    } elseif ($fase -eq "colocar" -and $mano.Count -gt 0 -and $campo.Count -lt 3) {
        $params.tipo = "colocar"
        $params.heroeId = [string]$mano[0]
        $params.casilla = -1
        $detalle = "coloca " + $params.heroeId
    } elseif ($fase -eq "habilidades") {
        $elegido = $null
        foreach ($p in $campo) {
            $elegibles = @(Campo $p "habilidadesElegibles")
            if ($elegibles.Count -gt 0) { $elegido = $p; break }
        }
        if ($null -ne $elegido) {
            $params.tipo = "elegir_habilidad"
            $params.peleadorId = [string](Campo $elegido "heroeId")
            $params.indiceHabilidad = [int](@(Campo $elegido "habilidadesElegibles")[0])
            $detalle = "elige habilidad de " + $params.peleadorId
        } else {
            $detalle = "pasa turno"
        }
    } else {
        $detalle = "pasa turno"
    }

    $r = Llamar "QuizzerMatch" "PlayMove" $quien.Token $params
    if (-not $r.Ok) {
        Linea ("  [{0}] {1} -> ERROR {2}" -f $i, $detalle, $r.Error) "Red"
        break
    }
    $vistas[$quien.Id] = $r
    # El rival no recibe la respuesta: su vista se refresca para ver el turno nuevo.
    $otro = if ($quien.Id -eq $A.Id) { $B } else { $A }
    $vistas[$otro.Id] = Llamar "QuizzerMatch" "GetMatchState" $otro.Token @{ matchId = $codigo }
    $eventos++
    Linea ("  [{0}] {1}: {2} · {3} ms · {4}" -f $i, $quien.Nombre, $detalle, $r.Ms, (Estado-Vista $r.Salida)) "Green"
    if ([string](Campo $r.Salida "estado") -eq "terminada") {
        Linea ("  FIN: ganador={0} motivo={1}" -f (Campo $r.Salida "ganadorId"), (Campo $r.Salida "motivoFin")) "Cyan"
        break
    }
}

Linea ""
Linea "5) Lo que ve cada jugador" "Yellow"
foreach ($j in @($A, $B)) {
    $vv = Llamar "QuizzerMatch" "GetMatchState" $j.Token @{ matchId = $codigo }
    if ($vv.Ok) { Linea ("  {0}: {1}" -f $j.Nombre, (Estado-Vista $vv.Salida)) "Green" }
    else { Linea ("  {0}: ERROR {1}" -f $j.Nombre, $vv.Error) "Red" }
}

Linea ""
if ($eventos -ge 3) { Linea ("RESULTADO: la partida online por turnos RESPONDE ({0} jugadas aplicadas)." -f $eventos) "Green" }
else { Linea ("RESULTADO: solo {0} jugadas aplicadas; revisar el detalle." -f $eventos) "Red" }
Linea ""
