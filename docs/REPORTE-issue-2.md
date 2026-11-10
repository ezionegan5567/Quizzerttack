# Reporte del issue #2 (pendiente tras el PR #1)

> Fecha: 2026-11-05. Proyecto UGS: **Quizzerttack** (`5906c36b-06d8-410b-91be-cf367616022a`).
> Herramientas: Unity `6000.6.0f1` (Standalone), .NET SDK `11.0.100-rc.3`, `ugs` CLI 2.x con *service account*.
> Este archivo es el texto que se pega como comentario en el issue; cada sección es una de las tres actividades pedidas y, al final, la limpieza de `bin/` y `obj/`.

## Resumen en una línea por actividad

| Actividad | Resultado |
|---|---|
| 2. El cliente compila en Unity (Standalone / `UGS_ON`) | **OK** (0 errores, 0 avisos en `Assets/Scripts/Online/Partida/`; Unity generó y ya estaban versionados los `.meta`) |
| 4. Despliegue del módulo `QuizzerMatch` | **Falló primero y quedó corregido**: faltaba el *publish profile*. Después **OK**: 5 funciones, con sus parámetros |
| 8. El push con dos Editores | **No pude hacerla** (necesita dos sesiones de jugador a la vez). Dejé comprobado todo lo que se puede sin eso |
| `bin/` y `obj/` versionados | **OK**: fuera del índice (eran **379** archivos, no 343) y `.gitignore` arreglado |

---

## Actividad 2 — El cliente compila en Unity: OK

Cómo se comprobó (sin depender de mirar la consola a ojo): dos compilaciones desde la línea de comandos del Editor, con el proyecto cerrado.

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe" -batchmode -nographics -quit `
  -projectPath "C:\Users\USER\Desktop\Quizzerttack" -logFile "Logs\batchmode-compile2.log"
```

Resultado:

- `AssetDatabase: script compilation time: 5.465597s` y `Exiting batchmode successfully now!` (código de salida 0).
- **`0` líneas `error CS`** en todo el log y **ningún** aviso (`warning CS`) apunta a `Assets/Scripts/Online/Partida/`. Los 120 avisos que hay son los de siempre, en `Scripts/Combate/` y otros (`FindFirstObjectByType` obsoleto).
- `Library/ScriptAssemblies/Assembly-CSharp.dll` se regeneró en esta compilación y contiene `PartidaOnlineService`, `PanelPartidaOnline`, `SuscribirPushAsync` y `TipoPush`/`MatchUpdated`. Es importante: `PartidaOnlineService` y `PanelPartidaOnline` están **dentro de `#if UGS_ON`**, así que si el símbolo no estuviera definido esos tipos no existirían en el ensamblado. Prueba de que se compiló la rama real con `UGS_ON` activo (define `Standalone: UGS_ON` en `ProjectSettings.asset` y `PLATFORM_STANDALONE_WIN` en el ensamblado).
- Los `.meta` de los tres archivos ya existían y están versionados desde el PR: `PanelPartidaOnline.cs.meta`, `PartidaDto.cs.meta`, `PartidaOnlineService.cs.meta`.

Nada que reportar como error, por tanto. Lo único que **no** se comprobó aquí: que el panel se vea y responda al pulsar F7 (eso es la actividad 5, y hace falta sesión de jugador).

## Actividad 4 — Despliegue del módulo: falló, causa, y ya funciona

### Primero falló

```
ugs deploy PartidaOnline/QuizzerMatch.sln --dry-run
...
This is a Dry Run. The result below is the expected result for this operation.
No content deployed

Will fail to deploy:
    'PartidaOnline\QuizzerMatch.sln' - Status: Failed to retrieve main project -
    [18:04:23.648] Failed to retrieve main project- Could not find a Publish Profile.
```

**Causa:** al módulo le faltaba el perfil de publicación. El CLI de UGS no despliega un `.csproj`/`.sln` sin `Properties/PublishProfiles/*.pubxml` (es el mismo motivo por el que en el Lab 10 el módulo salía llamándose *Project*: el CLI cogía el perfil de la plantilla). `Tienda` lo tiene (`Tienda/Properties/PublishProfiles/main.pubxml`) y `PartidaOnline/Project` no lo tenía.

**Arreglo:** se añadió `PartidaOnline/Project/Properties/PublishProfiles/main.pubxml` con lo mismo que `Tienda` (Release, `linux-x64`, `PublishReadyToRun`, FileSystem). Va en este mismo commit; también quedó documentado en `PartidaOnline/README.md` (*Cómo desplegar*).

### Después: OK

```
ugs deploy PartidaOnline/QuizzerMatch.sln
Deploying files...
Loading cloud-code-modules modules...
Deploying Cloud Code Modules...
Successfully deployed the following files:
    'PartidaOnline\QuizzerMatch.sln'
Created:
    'PartidaOnline\QuizzerMatch.sln'
```

Y lo que hay ahora mismo en el backend (`ugs cc m get QuizzerMatch`): `"HasError": false`, 5 endpoints, con estos parámetros y tipos de retorno:

| Función | Parámetros (nombre : tipo) | Devuelve |
|---|---|---|
| `CreateMatch` | `faccion : String`, `requestId : String` | `QuizzerMatch.VistaPartida` |
| `JoinMatch` | `faccion : String`, `matchId : String`, `requestId : String` | `QuizzerMatch.VistaPartida` |
| `GetMatchState` | `matchId : String` | `QuizzerMatch.VistaPartida` |
| `PlayMove` | `casilla : Int32`, `heroeId : String`, `indiceHabilidad : Int32`, `matchId : String`, `opcion : Int32`, `peleadorId : String`, `preguntaId : Int32`, `requestId : String`, `tipo : String`, `turnoEsperado : Int32` | `QuizzerMatch.VistaPartida` |
| `GetProtocolVersion` | *(ninguno)* | `Int32` |

Dos cosas que el documento dudaba y que quedan respondidas:

1. **`GetProtocolVersion` sin `IExecutionContext` se expone bien**, con cero parámetros (`ugs cc m get-spec` devuelve `/GetProtocolVersion` con `params: type: object` vacío). El miedo a que el servidor no lo derivara era infundado.
2. **`PlayMove` tiene 10 parámetros, no 12** (en `docs/PRUEBAS.md` decía "los 12 parámetros"; ya lo corregí en el documento). Los 10 son los de `ModuloPartida.PlayMove`: `matchId`, `requestId`, `turnoEsperado`, `tipo`, `heroeId`, `casilla`, `peleadorId`, `indiceHabilidad`, `preguntaId`, `opcion`. `IExecutionContext` y `IPushClient` no cuentan: los inyecta el servidor.

Además, con el spec del backend a la vista comprobé a mano lo que ninguna prueba automática cubría: **los nombres de los argumentos coinciden entre el cliente y el servidor** (`PartidaOnlineService`: `faccion`, `matchId`, `requestId`, `turnoEsperado`, `tipo`, `heroeId`, `casilla`, `peleadorId`, `indiceHabilidad`, `preguntaId`, `opcion`). No aparece el error de `req` del Lab 10.

## Actividad 8 — El push con dos Editores: no pude hacerla

**Por qué no pude:** el push sólo se prueba con **dos sesiones de jugador simultáneas** (dos Editores o Editor + build). En una sola máquina sin ParrelSync y sin build Standalone hecho, el panel sólo permite cambiar de perfil, y al cambiar de perfil se pierde la sesión anterior: el que escucha el push es siempre el mismo, así que no hay rival a quien avisar. Un "no pude" es lo que toca aquí.

**Lo que sí dejé comprobado (para que la prueba de ustedes salga a la primera):**

- La suscripción está enganchada al iniciar sesión: `PartidaOnlineService.SuscribirPushAsync()` → `CloudCodeService.Instance.SubscribeToPlayerMessagesAsync(callbacks)`, con `MessageReceived`, `ConnectionStateChanged`, `Kicked` y `Error`.
- El log que hay que buscar en la consola del Editor B es exactamente `[Partida][push] match_updated {"type":"match_updated","matchId":"..."}` (`Debug.Log($"[Partida][push] {@event.MessageType} {@event.Message}")`).
- El cuerpo lo fija el servidor en `NotificadorPush.CrearMensaje`: `JsonConvert.SerializeObject(new { type = TipoPush.MatchUpdated, matchId })`, o sea **sólo** `type` y `matchId`, y se manda con `SendPlayerMessageAsync` al rival (nunca `SendProjectMessage`).
- El código del cliente compila con `UGS_ON` (actividad 2), que era la otra mitad del riesgo: la firma que asume del SDK (`SubscribeToPlayerMessagesAsync(SubscriptionEventCallbacks)`) existe y resuelve.

**Cómo distinguir push de sondeo** (para que el resultado no quede ambiguo): el sondeo de respaldo va cada ~10 s, así que la pregunta es la **latencia**. Si B se actualiza en 1–2 s y en la consola aparece la línea `[Partida][push]`, es push. Si tarda ~10 s y **no** hay línea de push, es el sondeo: eso hay que reportarlo como fallo del push, no como OK.

**Pasos (5 minutos, lo único que falta):** abrir el proyecto dos veces (segundo Editor con ParrelSync, o un build Standalone), plataforma Standalone, `Registros → Add Component → PanelPartidaOnline`, F7 en los dos, perfiles distintos (`principal` en A, `otro` en B); A crea la partida, B se une; cuando le toque a A, A coloca un héroe y **B no toca nada**. Cronometrar cuánto tarda B en verse actualizado.

## Actividad 1 y 3 — de paso, reconfirmadas

```
dotnet test PartidaOnline/TestProject
Correctas! - Con error: 0, Superado: 61, Omitido: 0, Total: 61, Duración: 5 s

dotnet build PartidaOnline/Project/QuizzerMatch.csproj
Compilación correcta.  0 Advertencia(s)  0 Errores
```

---

## `bin/` y `obj/` versionados: arreglado

Conteo real: **379** archivos, no 343 (se quedaba fuera `TestA/`: 36). Reparto: `Tienda.Pruebas/` 217, `Tienda/` 97, `TestA/` 36, `_sonda/` 29.

Hecho:

1. `.gitignore`: se añadieron las salidas de compilación de los cuatro proyectos (`Tienda/bin`, `Tienda/obj`, `Tienda.Pruebas/bin`, `Tienda.Pruebas/obj`, `_sonda/bin`, `_sonda/obj`, `TestA/bin`, `TestA/obj`), además de las de `PartidaOnline` que ya estaban.
2. `git rm -r --cached` sobre esas ocho carpetas: los 379 archivos salen del índice y siguen en disco. `git ls-files` ya no devuelve ninguno con `bin/` u `obj/`.
3. Commit incluido.

**La pregunta del issue ("¿qué necesita alguien que clone el repo para compilar `Tienda` sin tener su máquina?")**, respondida con lo que faltaba: el `.gitignore` de Unity excluía `*.csproj` y `*.sln` en **todo** el repo, así que `Tienda.csproj`, `Tienda.Pruebas.csproj` y las soluciones no estaban versionados (sólo `PartidaOnline` tenía excepción). Ahora hay excepción también para `Tienda`, `Tienda.Pruebas` y `Tienda.sln`: con eso, un clon necesita **sólo** el repo más el SDK de .NET (los paquetes vienen por `PackageReference` desde `Tienda/NuGet.config`, que ya estaba versionado) y puede `dotnet build`/`ugs deploy` sin la máquina original. `PartidaOnline` no necesita `NuGet.config` propio: restaura de nuget.org.

## Lo que NO hice (y por qué)

La sección 3 del issue (*Tarea 4: `defensa`/`nivel` en `Protected`* y *Tarea 1: un solo Core de reglas*) es trabajo de desarrollo, no de reporte. No la toqué: cambiar la clase de acceso de `defensa` a protegida rompe el flujo del Lab 09 (el cliente escribe su propia defensa y el rival la lee) y unificar el Core es una refactorización grande. Queda para cuando se cierren las actividades 5–13.
