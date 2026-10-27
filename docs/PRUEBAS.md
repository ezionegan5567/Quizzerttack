# Actividades de prueba: partida online por turnos

Este documento les pide **probar** el módulo `PartidaOnline` (Cloud Code `QuizzerMatch`) y el panel F7, y **reportar** lo que vean. Nadie lo ha desplegado ni probado todavía contra Unity Gaming Services (UGS): sus resultados son la información que falta. Lo que ya está verificado son las pruebas automáticas (actividad 1) y que el módulo compila.

Antes de empezar, lean `PartidaOnline/README.md` (qué hace cada función y por qué).

## Cómo reportar

Abran un *issue* o comenten en el PR con, para cada actividad: **número, qué pasó (OK / falló / no pude hacerla)**, y si falló, lo que pide la columna *Si falla, reporten*. Un "no pude hacerla, me atoré en el paso 3" es un resultado válido y útil. Si algo les parece confuso en estas instrucciones, también es un resultado.

> No arreglen a ciegas un fallo para que "pase". Primero entiendan por qué falla y cuéntenlo: la idea es aprender qué está pasando.

## Lo que necesitan

- Unity con el proyecto abierto y la plataforma activa **Standalone** (el símbolo `UGS_ON` solo está definido ahí; en otra plataforma el panel no compila y esto es normal).
- Paquetes `com.unity.services.cloudcode` ≥ 2.4.0 y `com.unity.services.cloudsave` (ya los tienen).
- El proyecto vinculado a un proyecto UGS con **Cloud Code** y **Cloud Save** activados (como en el Lab 10).
- SDK de .NET para las actividades 1 y 3. Para la 4 en adelante, el CLI `ugs` o el Deployment de Unity.

## Actividades

### 1. Pruebas automáticas (sin Unity ni UGS) — 5 minutos

| | |
|---|---|
| **Pasos** | En la raíz del repo: `dotnet test PartidaOnline/TestProject` |
| **Esperado** | `Correctas! - Con error: 0, Superado: 61, Omitido: 0, Total: 61`. |
| **Si falla, reporten** | El nombre de la prueba que falla y su mensaje completo, y la salida de `dotnet --version`. |

### 2. El cliente compila en Unity

| | |
|---|---|
| **Pasos** | Abrir el proyecto, plataforma Standalone, esperar a que compile. Mirar la consola. |
| **Esperado** | Cero errores en `Assets/Scripts/Online/Partida/`. Unity crea los `.meta` de los archivos nuevos (conviene hacer commit de esos `.meta`). |
| **Si falla, reporten** | El texto exacto de cada error (archivo y línea). **Esta actividad es importante:** el cliente solo se comprobó contra simulaciones de Unity, nunca en Unity. |

### 3. El módulo compila con el Core compartido

| | |
|---|---|
| **Pasos** | `dotnet build PartidaOnline/Project/QuizzerMatch.csproj` |
| **Esperado** | `0 Advertencia(s)`, `0 Errores`. |
| **Si falla, reporten** | Los errores y qué paquete o versión falta. |

### 4. Despliegue del módulo (nadie lo ha hecho todavía)

| | |
|---|---|
| **Pasos** | Seguir *Cómo desplegar* de `PartidaOnline/README.md` (usar la `.sln`, no el `.csproj`; hacer primero `--dry-run`). En el panel de UGS (Cloud Code → Modules) abrir `QuizzerMatch`. |
| **Esperado** | El módulo aparece con **5 funciones**: `CreateMatch`, `JoinMatch`, `GetMatchState`, `PlayMove`, `GetProtocolVersion`. |
| **Si falla, reporten** | El mensaje del despliegue completo. Si falta alguna función o aparece con otros parámetros, cuál. Aquí podría descubrirse que `GetProtocolVersion` (sin contexto) o los 12 parámetros de `PlayMove` no se despachan como esperamos. |

### 5. Handshake de versión

| | |
|---|---|
| **Pasos** | Play → **F7** → *Iniciar sesión*. |
| **Esperado** | El panel muestra `Sesión iniciada como <tu playerId>`. |
| **Si falla, reporten** | El texto que aparece en el panel y los errores de consola. |
| **Variante (provocar el error a propósito)** | Cambien `GameProtocol.Version` a `2` en `PartidaDto.cs`, repitan. Esperado: `ERROR: Versión del juego incompatible (cliente 2, servidor 1)...` y **no** inicia sesión. Vuelvan a poner `1`. |

### 6. Una partida con un solo Editor (dos perfiles)

| | |
|---|---|
| **Pasos** | 1) Iniciar sesión con perfil `principal` → *Crear partida* (facción F1). 2) Copiar el código de 6 letras. 3) En *perfil* escribir `otro` → *Cambiar perfil* → escribir el código → *Unirse* (facción F2). 4) Jugar alternando perfiles. |
| **Esperado** | Tras crear: `estado: esperando_rival`. Tras unirse: `en_juego`, fase `colocar`, y **uno** de los dos tiene `>>> TU TURNO <<<` (lo decide el servidor con la moneda). Cada jugador tiene 5 cartas en la mano; el rival muestra `mano 5`. |
| **Si falla, reporten** | El texto del panel, la consola, y en qué paso. Si el que empieza **siempre** es el mismo en varias partidas nuevas, díganlo (la moneda debería variar). |

### 7. Rechazos que el servidor debe hacer

Con la partida de la actividad 6:

| Qué intentar | Esperado |
|---|---|
| Colocar un héroe cuando **no** es tu turno (cambiar de perfil sin que te toque) | No aparece el botón. Si usan un truco para forzarlo, el mensaje es `[no_es_tu_turno] No es tu turno.` |
| Colocar dos héroes seguidos en tu turno | Tras el primero, el turno pasa al rival (**un héroe por turno**). |
| En fase *habilidades*, elegir una habilidad: aparece una **pregunta** con 4 opciones | La pregunta solo la ve quien eligió. El perfil rival **no** la ve. |
| Responder bien / mal | Bien: `¡Correcto! La habilidad gana +25 %.` Mal: `Incorrecto: la habilidad pierde 25 %.` |
| Elegir una habilidad marcada *(aún no implementada)* | El botón está deshabilitado; si lo fuerzan, `[invalido] ... aún no está implementada en el servidor.` |
| Dejar pasar más de **20 s** respondiendo la pregunta | `Tiempo agotado: la habilidad pierde 25 %.` aunque la opción fuera la correcta. |

**Si falla, reporten:** la acción, el mensaje exacto que apareció y el que esperaban.

### 8. El push (necesita dos Editores)

Un solo Editor no sirve para esto, porque cada perfil se consulta a mano. Usen **dos Editores** (por ejemplo ParrelSync, o un Editor y un build Standalone), cada uno con su perfil.

| | |
|---|---|
| **Pasos** | Editor A crea la partida; Editor B se une. Cuando sea el turno de A, que coloque un héroe. **No toquen nada** en B. |
| **Esperado** | En B, en 1 o 2 segundos, aparece el turno de A reflejado y ahora es `TU TURNO`, sin pulsar *Consultar*. En la consola de B: `[Partida][push] match_updated {"type":"match_updated","matchId":"..."}`. El cuerpo **no** debe contener nada más que `type` y `matchId`. |
| **Si falla, reporten** | Si B se actualiza solo (tarda ~10 s: es el sondeo de respaldo, **no** el push), si aparece algún error de suscripción en la consola, y el texto exacto del log. Esto es lo que más nos interesa de toda la lista. |

### 9. Sondeo de respaldo (~10 s)

| | |
|---|---|
| **Pasos** | En `PanelPartidaOnline.cs`, método `IniciarSesion`, comenten **temporalmente** la línea `await servicio.SuscribirPushAsync();`. Repitan la actividad 8. |
| **Esperado** | Sin push, B se actualiza igual, pero tarda hasta unos **10 segundos**. Esto demuestra por qué el sondeo de respaldo existe. Descomenten la línea al terminar. |
| **Si falla, reporten** | Si B nunca se actualiza solo. |

### 10. Reconexión

| | |
|---|---|
| **Pasos** | Con una partida en curso, anoten el código y **paren el Play**. Vuelvan a darle Play → F7 → *Iniciar sesión* con el **mismo perfil** → escriban el código → **Reanudar**. |
| **Esperado** | Aparece la partida tal como estaba (mismo turno, mismas cartas). |
| **Si falla, reporten** | El mensaje que aparece. Prueben también con el otro perfil. Ojo: si el perfil cambia, el `playerId` cambia y el servidor responde `No participas en esta partida.` (esto es correcto, no es un fallo). |

### 11. Plazo de turno y abandono

| | |
|---|---|
| **Pasos** | Con una partida en curso, **no jueguen** durante 45 s en el perfil al que le toca. Cambien al otro perfil y pulsen *Consultar*. Repitan dejando pasar el plazo **3 veces seguidas** al mismo jugador (el otro perfil sí juega o pasa en sus turnos; el contador es solo del jugador pasivo y se reinicia si este hace cualquier jugada). |
| **Esperado** | Tras el primer plazo, el turno pasa al rival (el servidor lo hace al consultar). Al tercer plazo seguido, la partida termina: `¡GANASTE! (timeouts)` para el otro. |
| **Si falla, reporten** | Cuánto esperaron, qué turno marcaba el panel y si el contador de segundos coincide con el servidor. |

### 12. Información oculta

| | |
|---|---|
| **Pasos** | Con una pregunta pendiente en el perfil A, cambien a B. En `PartidaOnlineService.Llamar`, **temporalmente** añadan `Debug.Log(JsonConvert.SerializeObject(vista));` tras recibir la vista. Miren el JSON que recibe B. |
| **Esperado** | El JSON de B **no** contiene: la mano del rival (solo `RivalManoCantidad`), `HabilidadPreparada` del rival (siempre `-1`), `Semilla`, ni nada llamado `Correcta`. Quiten el log al terminar. |
| **Si falla, reporten** | Qué campo apareció que no debería. Esto sería un fallo de seguridad. |

### 13. Probar a hacer trampa

| | |
|---|---|
| **Pasos** | Desde un script de prueba temporal, llamen con `CloudCodeService.Instance.CallModuleEndpointAsync<VistaPartidaDto>("QuizzerMatch", "PlayMove", ...)` con `tipo = "plazo"` (con todos los argumentos de `PlayMove`: `matchId`, `requestId`, `turnoEsperado`, `tipo`, `heroeId`, `casilla`, `peleadorId`, `indiceHabilidad`, `preguntaId`, `opcion`). Después con `tipo = "inventada"`. Después un `turnoEsperado` viejo con un `tipo = "pasar"` válido. |
| **Esperado** | `plazo` e `inventada`: `Resultado = "invalido"`, `Tipo de jugada no permitido`. Turno viejo: `Resultado = "obsoleto"`. En ningún caso cambia la partida. |
| **Si falla, reporten** | Qué `Resultado` recibieron. Si `plazo` se aceptó, es un fallo grave (un cliente podría vencerle el turno al rival). |

## Qué esperamos aprender de esto

1. Si el cliente y el módulo se **entienden** en UGS real (actividades 2, 4, 5).
2. Si el **push** llega y con qué latencia (8 y 9): la única parte que no se pudo comprobar en local.
3. Si las **reglas** se sienten bien jugando (6 y 7): qué es confuso, qué falta.
4. Cualquier cosa que el servidor **acepte y no debería** (12 y 13).
