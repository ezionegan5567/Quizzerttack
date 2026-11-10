# Partida online por turnos (Cloud Code `QuizzerMatch`)

> Relacionado: [[Avances del juego]] · [[Sistemas]] · [[Lab08-Combate]] · [[Lab11-Ranking]] · [[CloudSave-CloudCode]]

**Tema:** dos jugadores conectados juegan la misma partida del GDD, turno a turno, con Cloud Code como única autoridad y push para avisar al rival.
**Fecha:** estado verificado el 2026-11-05 · **Módulo:** `QuizzerMatch` (C# / net6.0) · **Cliente:** `Assets/Scripts/Online/Partida/`

La regla en una frase: **el cliente pide, Cloud Code decide** — el cliente manda intenciones (`PlayMove`) y dibuja la `VistaPartida` que recibe; nunca calcula reglas ni ve la mano del rival.

---

## 0. Dónde está cada cosa

| Pieza | Archivo | Qué hace |
|---|---|---|
| Reglas | `PartidaOnline/Core/Reglas.cs` | Todo el juego (fases, daño, quiz ±25 %, efectos soportados). C# puro. |
| Replay | `PartidaOnline/Core/Motor.cs` + `Registro.cs` | El estado **no** se guarda: se guarda la cabecera + la lista de jugadas y se reproduce en cada llamada. |
| Catálogo / preguntas | `Core/Catalogo.Generado.cs` + `Preguntas.Generado.cs` | Generados desde `DatosFacciones.cs` y `QuizHabilidades.cs` con `python PartidaOnline/Tools/generar_catalogo.py`. No editar a mano. |
| Endpoints | `PartidaOnline/Project/ModuloPartida.cs` | Capa fina: 5 `[CloudCodeFunction]`. La identidad sale de `context.PlayerId`, nunca de un parámetro. |
| Orquestación | `Project/Servicio.cs` | cargar → reproducir → idempotencia → plazo → validar → anotar → guardar → avisar |
| Persistencia | `Project/RepositorioCloudSave.cs` | Cloud Save *custom data* **privado** con write lock (choque = `conflicto`). |
| Push | `Project/NotificadorPush.cs` | `SendPlayerMessageAsync` al rival con cuerpo mínimo `{type, matchId}`. |
| Pruebas | `PartidaOnline/TestProject/` | **61** pruebas (reglas, replay, idempotencia, plazos, información oculta, contrato, partidas completas). |
| Cliente | `Assets/Scripts/Online/Partida/` | `PartidaDto` (contrato), `PartidaOnlineService` (llamadas + requestId + handshake + push), `PanelPartidaOnline` (panel F7). |

## 1. Las 5 funciones y sus parámetros (lo que hay desplegado)

| Función | Parámetros | Devuelve |
|---|---|---|
| `GetProtocolVersion` | *(ninguno)* | `int` — handshake |
| `CreateMatch` | `requestId`, `faccion` | `VistaPartida` |
| `JoinMatch` | `matchId`, `requestId`, `faccion` | `VistaPartida` |
| `GetMatchState` | `matchId` | `VistaPartida` |
| `PlayMove` | `matchId`, `requestId`, `turnoEsperado`, `tipo`, `heroeId`, `casilla`, `peleadorId`, `indiceHabilidad`, `preguntaId`, `opcion` (**10**) | `VistaPartida` |

- Los nombres de argumento del cliente (`PartidaOnlineService`) son **los mismos** que los del servidor: es el error de `req` del Lab 10 y aquí ya está cuidado.
- `tipo` de `PlayMove` (no hay una función por fase): `colocar`, `elegir_habilidad`, `responder_quiz`, `pasar`, `abandonar`. `plazo` y `unirse` **las escribe el servidor**: si un cliente las manda, recibe `invalido`.
- `Resultado` de la vista: `aplicado`, `ok`, `repetido`, `obsoleto`, `no_es_tu_turno`, `fase_incorrecta`, `invalido`, `terminada`, `conflicto`. Las jugadas rechazadas **no** se anotan.

## 2. Despliegue (verificado el 2026-11-05)

```powershell
& ".\ugs\ugs.exe.exe" deploy .\PartidaOnline\QuizzerMatch.sln --dry-run   # dice "Will create"
& ".\ugs\ugs.exe.exe" deploy .\PartidaOnline\QuizzerMatch.sln
& ".\ugs\ugs.exe.exe" cc m get QuizzerMatch        # HasError: false + Endpoints
& ".\ugs\ugs.exe.exe" cc m get-spec QuizzerMatch   # OpenAPI de las 5 rutas
```

- **Trampa resuelta:** sin `PartidaOnline/Project/Properties/PublishProfiles/main.pubxml` el CLI responde `Failed to retrieve main project - Could not find a Publish Profile` y no sube nada. Se copió el perfil de `Tienda` (Release · `linux-x64` · ReadyToRun). Ya está en el repo.
- Se despliega la **`.sln`**, no el `.csproj`. El nombre del módulo sale del proyecto (`QuizzerMatch`), no de la carpeta.
- Estado en el backend: `HasError: false` y las 5 funciones con sus parámetros (arriba). `GetProtocolVersion` **sin** `IExecutionContext` se expone bien, con cero parámetros.

## 3. Cómo probarlo en Unity (panel F7)

1. Plataforma **Standalone** (el símbolo `UGS_ON` sólo está definido ahí; en otra plataforma los scripts compilan a un stub y el panel no aparece, y eso es normal).
2. En la escena, GameObject `Registros` → **Add Component → `PanelPartidaOnline`** (si no está ya). Se abre con **F7**.
3. **Un solo Editor, dos perfiles:** *Iniciar sesión* con `principal` → *Crear partida* (facción F1) → copiar el código de 6 letras → escribir `otro` en *perfil* → *Cambiar perfil* → pegar el código → *Unirse* (F2). Se juega cambiando de perfil.
4. **Dos Editores / ParrelSync:** cada uno con su perfil. Es la única forma de ver el push de verdad.

**Atajos ya ocupados** (no compartir dos demos en la misma tecla: eso fue el `AccessTokenMissing` 51 del Lab 10): F6 Lab 11 · **F7 panel online** · F8 rival · F9 registros · F10 Lab 09 · F11 Lab 10 · F12 Lab 08.

**Qué mirar en la consola:** `[Partida][push] match_updated {"type":"match_updated","matchId":"..."}`. Si el rival se entera en 1–2 s, es push; si tarda ~10 s y **no** hay esa línea, es el sondeo de respaldo (no es lo mismo, y hay que decirlo).

## 4. Diseño que conviene recordar

- **Estado = lista de jugadas.** Una jugada rechazada no se anota; lo guardado sólo crece. Cambiar las reglas o el catálogo obliga a **subir `GameProtocol.Version` en las dos mitades** (`Core/GameProtocol.cs` y `PartidaDto.cs`) o el replay daría otro resultado.
- **Idempotencia:** cada jugada lleva `requestId` y se comprueba **antes** del turno (patrón `TurnMatch`). Reintentar no aplica dos veces.
- **Reloj del servidor:** Cloud Code no tiene temporizadores; el plazo (45 s) se aplica al inicio de cada petición y se anota como jugada `plazo`. 3 plazos seguidos = abandono.
- **Información oculta:** el cliente no recibe la mano del rival (`RivalManoCantidad`), `HabilidadPreparada` del rival (siempre `-1`), la `Semilla` ni la respuesta correcta. Hay una prueba que lo comprueba sobre el JSON.
- **Push liviano:** `{type, matchId}` con `SendPlayerMessageAsync` (nunca `SendProjectMessage`). El cliente no decide nada con el push: al recibirlo llama a `GetMatchState`. Si falla el envío no rompe la jugada (se registra y se sigue).
- **Sondeo de respaldo:** el panel consulta cada ~10 s mientras la partida esté activa.
- **Lo que queda fuera del servidor:** efectos no soportados (22 de las 80 habilidades se rechazan con «aún no está implementada en el servidor»), crítico del GDD, animación de la pelea y las dos reglas que aún no existen (abandono por inactividad en `GetMatchState` y barrido programado de partidas olvidadas).

## 5. Estado

- [x] Cliente compila en Unity con `UGS_ON`: **0 errores, 0 avisos** en `Assets/Scripts/Online/Partida/` (compilación *batchmode* del Editor, 2026-11-05) y los tres `.meta` versionados.
- [x] Módulo **desplegado**: `HasError: false`, 5 funciones con sus parámetros.
- [x] `dotnet test PartidaOnline/TestProject` → **61/61** · `dotnet build Project/QuizzerMatch.csproj` → 0 avisos, 0 errores.
- [ ] **Push con dos Editores** (actividad 8): sin verificar. Es lo único que no se puede hacer con un solo Editor.
- [ ] Actividades 5–7 y 10–13 de `docs/PRUEBAS.md`: handshake, una partida jugada, rechazos, reconexión, plazos, información oculta, trampas.
- [ ] Sustituir el panel IMGUI por la escena real (`GestorTurnos`, `CartaSeleccion`, `Peleador` pintando la `VistaPartidaDto`).
- [ ] Un solo Core de reglas compartido con el juego de Unity (hoy el Core lo comparten el módulo y las pruebas, no el cliente).

## 6. Reporte del issue #2

El detalle por actividad (2 OK · 4 falló y quedó corregido · 8 no pude), con los comandos y las salidas, está en `docs/REPORTE-issue-2.md`. Resumen del mismo día: también se sacaron del repo los 379 archivos de `bin/`/`obj/` y se versionaron los `.csproj`/`.sln` de `Tienda` (commit `282ca3d`).
