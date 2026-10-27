# PartidaOnline — partida por turnos entre dos jugadores, con Cloud Code como autoridad

Módulo Cloud Code **`QuizzerMatch`** + cliente Unity para jugar la partida de Quizzerttack (moneda → colocar → habilidades con quiz → pelea → nueva ronda) **online y por turnos**, con el servidor como única autoridad y **push messages** de Cloud Code para avisar al rival.

> **Estado: desarrollado y probado en local, NO desplegado ni probado todavía contra UGS real.** Lo que sí está verificado: el módulo compila contra los paquetes reales de Cloud Code y las 61 pruebas pasan (reglas, replay, servicio, idempotencia, write lock, plazos, información oculta, contrato cliente/servidor y partidas completas simuladas). Lo que falta verificar con Unity y UGS: despliegue, push y panel. Ver *Qué falta*.
>
> **Sigue el patrón de TTTXO** (arquitectura de referencia del curso): `CreateMatch` / `JoinMatch` / `PlayMove` / `GetMatchState`, estado como lista de jugadas que se reproduce, push `{type, matchId}` con sondeo de respaldo y `GameProtocol.Version`. Una diferencia importante: el Core se comparte entre el módulo y las pruebas, **pero no con el juego de Unity** (ver *Diferencias con TTTXO*).

## Por qué existe

Los Labs 09 y 10 resuelven un combate **asíncrono** contra una defensa guardada, con otra fórmula de combate que no usa el juego. Esto es otra cosa: **dos jugadores conectados juegan la misma partida del GDD**, turno a turno, y cada jugada pasa por el servidor.

| Principio | Cómo se cumple |
|---|---|
| El cliente pide, Cloud Code decide | Toda regla vive en `Core/Reglas.cs` (C# puro). El cliente sólo envía intenciones y dibuja la `VistaPartida` que recibe. |
| Información oculta | El cliente nunca recibe la mano del rival, sus habilidades preparadas, la semilla ni la respuesta correcta de las preguntas (hay una prueba que lo comprueba sobre el JSON). |
| Aleatoriedad del servidor | La moneda, la facción *Random*, la pregunta del quiz y el desempate salen de una semilla secreta y reproducible guardada en el servidor. |
| Estado = lista de jugadas | Lo guardado es la cabecera (jugadores, semilla) y la lista de jugadas, **solo de agregar**. El estado se deduce en cada llamada reproduciéndola con un motor nuevo (`Motor.Reproducir`). Una jugada rechazada no se anota. |
| Idempotencia | Cada jugada lleva un `requestId`; un reintento no se aplica dos veces (se busca en el registro). Se comprueba **antes** que el turno (patrón de `TurnMatch` del curso). |
| Concurrencia | Cloud Save *custom data* privado con write lock; si dos jugadas chocan, una recibe `conflicto`. |
| Reloj del servidor | Cloud Code no tiene temporizadores: el plazo se aplica al principio de cada petición y **se anota como una jugada más** (`plazo`, del servidor) para que el replay lo reproduzca. 3 plazos vencidos seguidos = abandono. |
| Reconexión | El estado completo vive en la nube: volver a llamar a `GetMatchState` con el código basta. |
| Versión del contrato | `GameProtocol.Version` en cliente y servidor; una prueba falla si dejan de coincidir los campos de las vistas, las constantes o la versión. |

## Estructura

```
PartidaOnline/
├── QuizzerMatch.sln
├── Core/                         C# PURO (sin Unity ni Cloud Code): el módulo y las pruebas lo COMPILAN
│   ├── Reglas.cs                 las reglas del juego  <-- aquí está el juego
│   ├── Motor.cs                  Aplicar(jugada) y Reproducir(registro): el replay
│   ├── Registro.cs               Jugada, TipoJugada y RegistroPartida (lo que se guarda)
│   ├── Estado.cs / Vista.cs      el estado en memoria / lo que ve cada jugador
│   ├── GameProtocol.cs           versión del contrato y tipo del push
│   ├── Modelos.cs                héroes, habilidades, preguntas
│   ├── Catalogo.Generado.cs      GENERADO desde DatosFacciones.cs / MazoFaccion.cs
│   └── Preguntas.Generado.cs     GENERADO desde QuizHabilidades.cs
├── Project/                      módulo Cloud Code (net6.0, se llamará "QuizzerMatch"); incluye Core con <Compile Include>
│   ├── ModuloPartida.cs          las funciones [CloudCodeFunction] (capa fina)
│   ├── Servicio.cs               orquesta cada petición: cargar → reproducir → idempotencia → plazo → validar → anotar → guardar → avisar
│   ├── RepositorioCloudSave.cs   el registro en Cloud Save (custom data privado)
│   ├── NotificadorPush.cs        push {type, matchId} al rival con IPushClient
│   └── ModuleSetup.cs
├── TestProject/                  61 pruebas NUnit (repositorio y push falsos en memoria; compila también el DTO del cliente)
└── Tools/generar_catalogo.py     regenera los archivos *.Generado.cs (en Core/) desde el código de Unity
Assets/Scripts/Online/Partida/    cliente Unity (todo bajo #if UGS_ON salvo los DTO)
├── PartidaDto.cs                 espejo de VistaPartida y de GameProtocol (contrato; lo comprueba ContratoTests)
├── PartidaOnlineService.cs       llamadas + custodia del requestId + handshake de versión + suscripción al push
└── PanelPartidaOnline.cs         panel de prueba (F7), con sondeo de respaldo cada ~10 s
```

**Si cambias héroes, habilidades o preguntas en Unity**, ejecuta desde la raíz del repo `python PartidaOnline/Tools/generar_catalogo.py` y vuelve a desplegar. No edites los `*.Generado.cs`.

## Mensajes

Las jugadas del cliente son **llamadas normales** a funciones del módulo (`CloudCodeService.Instance.CallModuleEndpointAsync("QuizzerMatch", función, args)`). Los **push** sólo van del servidor al cliente.

Los nombres son los del patrón TTTXO. Las fases de la partida no son funciones distintas: son el `tipo` de la jugada de **`PlayMove`**.

| Función | Argumentos | Qué hace y valida el servidor |
|---|---|---|
| `GetProtocolVersion` | — | Handshake: devuelve `GameProtocol.Version`. El cliente se niega a jugar si no coincide con la suya. |
| `CreateMatch` | `requestId`, `faccion` | Facción válida (F1–F4/Random). El código (`matchId`) se deriva del `requestId`: reintentar no crea otra partida (idempotente, como el `handoffId` de TTTXO). |
| `JoinMatch` | `matchId`, `requestId`, `faccion` | Existe, espera rival, no es la tuya. El servidor lanza la **moneda** y abre la ronda 1. |
| `GetMatchState` | `matchId` | Participas. Aplica el plazo vencido (y lo anota) antes de responder. Sirve para reconectar. |
| `PlayMove` | `matchId`, `requestId`, `turnoEsperado`, `tipo`, `heroeId`, `casilla`, `peleadorId`, `indiceHabilidad`, `preguntaId`, `opcion` | Participas, **idempotencia**, tipo permitido, plazo, turno esperado y las reglas del `tipo`. Los parámetros que el `tipo` no usa se ignoran. |

Tipos de `PlayMove` (`TipoJugada`): `colocar` (tu turno, fase *colocar*, héroe en tu mano, máx. 3 en el campo, casilla libre, **uno por turno**), `elegir_habilidad` (peleador tuyo y vivo, habilidad soportada y no usada en el ciclo; devuelve la **pregunta** sin respuesta), `responder_quiz` (es tu pregunta pendiente, opción en rango, **a tiempo**; acierto +25 %, fallo o tarde −25 %), `pasar` (tu turno y sin pregunta pendiente) y `abandonar` (no exige turno; el rival gana). `plazo` y `unirse` existen en el registro pero **las escribe el servidor**: un cliente que las envíe por `PlayMove` recibe `invalido` (hay prueba).

Todas devuelven una **`VistaPartida`** con `Protocolo` y `Resultado`: `aplicado`, `ok`, `repetido`, `obsoleto`, `no_es_tu_turno`, `fase_incorrecta`, `invalido`, `terminada` o `conflicto`. Las jugadas rechazadas viajan en `Resultado`, no como excepción, y **no se anotan en el registro**; las excepciones son para lo excepcional (código inexistente, no participas, partida de otra versión del protocolo).

**Push:** un solo `MessageType`, `match_updated`, con el cuerpo mínimo `{type, matchId}`, enviado sólo al rival. Al recibirlo, el cliente llama a `GetMatchState`. El push **no lleva estado ni información oculta** y **no se fía de él para decidir nada**. **Sondeo de respaldo:** el panel consulta además cada ~10 s (`IntervaloSondeoSeg`) mientras la partida esté activa, por si se pierde un push.

Argumentos por **nombre**: los de `ModuloPartida.cs` y los de `PartidaOnlineService.cs` deben llamarse igual (es el error de `req` que ya les pasó en el Lab 10). Esto **no** lo comprueba ninguna prueba (el servicio del cliente necesita UGS); sí se comprueban los campos de las vistas, las constantes y la versión.

## Reglas implementadas (y lo que no)

Orden de una ronda (GDD y `GestorTurnos`): colocar (inicial) → colocar (segundo) → habilidades (inicial) → habilidades (segundo) → pelea automática → ronda siguiente. Se salta el turno del jugador que no puede hacer nada. Fin: pierde quien se queda sin peleadores y sin cartas; empate si ambos; límite de 30 rondas.

**Efectos soportados:** `Dano`, `DanoTodos`, `DanoDos`, `Curar`, `CurarZona`, `BuffAtk(Zona)`, `BuffDef(Zona)`, `Intimidar`. Son **58 de las 80 habilidades**: las que tienen un efecto *principal* soportado. Las otras 22 (provocar, velocidad, maldición, invisibilidad, ceguera, parálisis, reflejo…) existen en el catálogo pero **se rechazan con un mensaje claro** al elegirlas (`aún no está implementada en el servidor`). **Ojo con los efectos secundarios:** una habilidad soportada puede traer un efecto secundario que aún no existe (p. ej. el sangrado de *Cuchillo letal* o el veneno de *Cuchillo veneno*); en ese caso se aplica el daño y el efecto secundario **se ignora sin avisar**. Para añadir un efecto: implementarlo en `Reglas.AplicarEfecto`, añadirlo a `EfectosSoportados` y escribir su prueba.

Tampoco están todavía: esquiva por velocidad, robo de vida (Vampiro), pasivas, el **fallo crítico** del GDD ni la animación (la pelea se resuelve entera y se devuelve el `RegistroPelea`).

**Decisiones de diseño que el equipo debe confirmar** (todas en `Config`, en `Reglas.cs`):

| Regla | Valor del servidor | Código actual de Unity |
|---|---|---|
| Segundos por turno | 45 (GDD) | 30 |
| Quiz acertado / fallado | +25 % / −25 % (GDD) | +10 % / −10 % |
| Ataque por ronda | +10 % acumulado (GDD) | no existe |
| Habilidad ya usada | no se repite hasta agotar el repertorio (GDD) | sin comprobar |
| Habilidades del rival | ocultas hasta la pelea | visibles en local |
| Orden de la pelea | primero el anfitrión (como `GestorTurnos`) | igual |

> El orden de la pelea (siempre el anfitrión primero) hace que casi no haya empates y da ventaja al anfitrión. Es heredado del código actual; conviene decidir si se alterna por ronda.

## Cómo ejecutar las pruebas

```bash
dotnet test PartidaOnline/TestProject
```

Necesita el SDK de .NET (las pruebas corren en net8.0; el módulo compila a net6.0 como `Tienda` y `TurnMatch`). No necesita Unity ni UGS.

## Cómo desplegar (SIN PROBAR)

Mismo procedimiento que `Tienda` (ver `Notas/Lab10-Progresion.md`: usar la **`.sln`** y no el `.csproj`; sin `static` en las funciones):

```bash
ugs deploy PartidaOnline/QuizzerMatch.sln
```

Antes: activar Cloud Code y Cloud Save en el proyecto UGS, y comprobar con `--dry-run`. El paquete `com.unity.services.cloudcode` del cliente debe ser ≥ 2.4.0 (ya tienen 2.10.4). Atención: la documentación de Cloud Code 3.0.0 es inconsistente sobre el runtime de .NET (hasta 9 en una página, 10 en otra); comprueben qué admite su entorno antes de cambiar de net6.0.

## Cómo probarlo en Unity (SIN PROBAR)

1. Desplegar el módulo.
2. En la escena, en el GameObject `Registros`: **Add Component → `PanelPartidaOnline`**. Pulsar **F7**.
3. **Un solo Editor, los dos lados:** *Iniciar sesión* con el perfil `principal` → *Crear partida*; copiar el código; en el campo *perfil* escribir `otro` → *Cambiar perfil* → *Unirse* con el código. Cambiar de perfil para jugar cada lado.
4. **Dos Editores (o ParrelSync):** cada uno con su perfil; así se ve el push de verdad (el rival ve su turno sin pulsar *Consultar*).

## Diferencias con la arquitectura de referencia del curso (TTTXO)

Ya sigue el patrón: el cliente envía intenciones, Cloud Code valida con un Core en C# puro que el módulo compila (`<Compile Include>`), `PlayMove` único con tipos de jugada, estado como lista de jugadas que se reproduce, push liviano `{type, matchId}` más sondeo de respaldo, `GameProtocol.Version` con una prueba que compara los contratos, y `context.PlayerId` como única identidad. Lo que **todavía no** es igual:

1. **El Core no lo comparte el juego de Unity.** En TTTXO el cliente (modo local, IA, vista previa) y el servidor ejecutan el mismo código. Aquí el Core lo comparten el módulo y las pruebas, pero `Peleador`, `GestorTurnos` y `CartaSeleccion` siguen con sus propias reglas mezcladas con `MonoBehaviour`. Compartirlo exige refactorizar el juego (y llevar el Core a un sitio que Unity compile: C# 9, sin `record` ni espacios de nombres de archivo). Es el trabajo de los alumnos y la prueba de que entienden las reglas.
2. **Dos reglas de servidor ausentes:** abandono por inactividad en `GetMatchState` y el barrido programado de partidas olvidadas (`SweepAbandonedMatches`); tampoco caducan las salas que esperan rival.
3. **Sin `GetGameConfig`:** los valores (`Config`) están en el código y no llegan por Remote Config.
4. **Sin Matchmaker:** el emparejamiento es por código de sala, no por ticket.
5. **No engancha los premios ni el ranking** al fin de partida (ver *Qué falta*).

**Cuidado con el replay:** las reglas forman parte del contrato. Si cambias las reglas o el catálogo, **sube `GameProtocol.Version` en las dos mitades**: una partida guardada con otra versión se rechaza con un mensaje claro (si no, el replay daría otro resultado). Una partida cuyo registro ya no se puede reproducir lanza una excepción en lugar de inventarse el estado.

## Qué falta (honestamente)

1. **Verificar contra UGS real**: despliegue, que `IPushClient` se inyecte como parámetro, que el push llegue, que `GetProtocolVersion` (sin `IExecutionContext`) y los 12 parámetros primitivos de `PlayMove` se despachen bien, el tipo exacto de los eventos `ConnectionStateChanged`/`Error` del SDK (el cliente asume lo de la documentación), que el tamaño del registro de una partida larga quepa en un item de Cloud Save, y que el cliente compile en Unity (aquí sólo se comprobó contra stubs).
2. **Sustituir el panel IMGUI por la escena real**: hacer que `GestorTurnos`, `CartaSeleccion` y `Peleador` *pinten* la `VistaPartidaDto` en vez de decidir fases y peleas en local. Es el trabajo de integración y conviene que lo hagan ustedes.
3. **Implementar los efectos que faltan** (22 habilidades) y decidir los valores de la tabla de arriba.
4. **Unificar backends**: hoy conviven Firebase, los scripts `T9*`, el módulo `Tienda` y este. Cuando la partida online funcione, el fin de partida puede llamar a la progresión del Lab 10 (con `IdOperacion` = código + jugador) para premiar una sola vez.
5. **Emparejamiento**: hoy es por código. Se puede reutilizar `BuscarRival` por nivel para invitar.
6. **Limpieza de partidas**: nada borra las partidas terminadas de Cloud Save.
