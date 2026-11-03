# Lab 08 — `ResolverCombate`: el combate lo resuelve el servidor

> Relacionado: [[Avances del juego]] · [[Sistemas]] · [[Lab10-Progresion]] · [[CloudSave-CloudCode]]

**Guía de Laboratorio #8 — resuelta**
**Tema:** Bucle de Juego Autoritativo en el Servidor (Cloud Game Loop)
**Alumno:** Alexander Villena · **Fecha:** 2026-11-02 · **Módulo:** Cloud Code `Tienda` (C# / Net6)

Este documento cubre los **10 pasos** de la tarea 8. El módulo `Tienda` ya estaba más
avanzado (Lab 09 y Lab 10), así que la tarea 8 se integró **sin romper nada**: el combate
viejo de poder sigue vivo como `ResolverCombatePvp` y el combate nuevo de la tarea 8
ocupa el nombre `ResolverCombate`.

---

## 0. Las tres capacidades que evalúa la guía

La guía califica tres cosas concretas. Así se cumplen:

| Capacidad pedida | Dónde está | Cómo se demuestra |
|---|---|---|
| **Programa el combate por turnos en C# dentro de una función serverless** | `Tienda/Batalla.cs` (`Batalla.Similar`) llamado desde `Tienda/Funciones.cs` → `[CloudCodeFunction("ResolverCombate")]` | El cliente no manda semilla ni daño: la pelea se ejecuta en Cloud Code y el log se devuelve armado. Prueba: `dotnet test` 7/7. |
| **Simula el enfrentamiento paso a paso y actualiza inventarios y monedas resultantes** | `Batalla.Similar` (bucle `while` por turnos) + `EscribirCombateBatchAsync` (`saldos`, `progreso`, `ultimo_combate` en una sola escritura protegida) | `[Combate] … premio=ORO+150 saldo=550` → el saldo sube solo cuando gana el jugador; un rechazo devuelve `Saldo` intacto. |
| **Parsea el archivo de batalla JSON en el cliente para reproducirlo visualmente** | `Assets/Scripts/Online/ReproductorCombate.cs` (`PelearAsync`, corrutina `Reproducir`, barras de vida) + DTOs en `Assets/Scripts/Modelos/Estado.cs` | Los `[Lab08][Acción] T1 … -18 (queda 132)` se pintan **uno cada 0,6 s** desde el `BattleLog` recibido, sin recalcular nada. |

---

## 1. Qué se hizo (mapa de archivos)

### Servidor (módulo Cloud Code `Tienda/`)

| Archivo | Estado | Qué aporta |
|---|---|---|
| `Tienda/Batalla.cs` | **nuevo** | `UnidadCombate`, `EntradaBatallaLog` y `Batalla.Similar(...)`: la simulación pura y determinista de los pasos 4 y 5. |
| `Tienda/TablaCombate.cs` | **nuevo** | Las dos tablas (`tabla_heroes`, `rivales_npc`) y sus **valores por defecto** en código. |
| `Tienda/S08-rivales-npc.rc` | **nuevo** | El archivo de Remote Config con las dos llaves (paso 1). |
| `Tienda/Funciones.cs` | modificado | `ResolverCombate` (pasos 2-6) y `RepetirCombate` (paso 7) + helpers `Rechazo`, `LeerTablasAsync`, `ArmarUnidades`, `EscribirCombateBatchAsync`. El combate de poder anterior pasa a llamarse `ResolverCombatePvp`. |
| `Tienda/Datos.cs` | modificado | `ResultadoCombate` ahora devuelve `Semilla`, `TurnosJugados`, `EquipoPropio`, `EquipoRival`, `BattleLog`, `Recompensa`, `TrofeosCambio`, `SaldoResultante`, `Repeticion` y `Rechazado`. |
| `Tienda/Combate.cs` | modificado | `UltimoCombateEstado` guarda los equipos congelados y el battle log (para poder repetir la pelea igual). |
| `Tienda.Pruebas/` | **nuevo** | Proyecto de pruebas: **7 pruebas** (6 de la simulación, 1 de la configuración). |

### Cliente (Unity `Assets/Scripts/`)

| Archivo | Estado | Qué aporta |
|---|---|---|
| `Assets/Scripts/Online/ReproductorCombate.cs` | **nuevo** | `PelearAsync(equipo, idRival)` y `RepetirAsync()`: llaman al módulo y **reproducen** el battle log cada 0,6 s. |
| `Assets/Scripts/Online/DemoLab08.cs` | **nuevo** | Corre todo en una pasada: pelea, repetición y **los 5 intentos de trampa** del paso 9. |
| `Assets/Scripts/Modelos/Estado.cs` | modificado | DTOs del cliente: `EntradaBatallaLog`, `UnidadCombate`, `Recompensa` y los campos nuevos de `ResultadoCombate`. |

---

## 2. Los diez pasos, uno por uno

### Paso 1 — la tabla de rivales y el módulo

El `.rc` tiene **dos** llaves:

```json
{ "entries": { "tabla_heroes": { "asesino": { "vida": 80, "ataque": 32, "defensa": 3, "velocidad": 14 }, ... },
               "rivales_npc": [ { "id": "npc_practica_1", "nombre": "Rival de practica",
                                  "equipo": ["escudero","arquero","curandero"], "moneda": 150, "trofeos": 5 }, ... ] } }
```

Comandos y qué esperar:

```powershell
.\ugs\ugs.exe.exe deploy .\Tienda\S08-rivales-npc.rc --dry-run   # qué va a pasar
.\ugs\ugs.exe.exe deploy .\Tienda\S08-rivales-npc.rc             # lo aplica
.\ugs\ugs.exe.exe deploy .\Tienda\Tienda.sln --dry-run           # compila y dice qué módulo cambia
.\ugs\ugs.exe.exe deploy .\Tienda\Tienda.sln                     # despliega el módulo
.\ugs\ugs.exe.exe cloud-code modules get Tienda                  # lista los endpoints
```

> **Ojo (trampa real de este entorno):** el deploy del `.rc` **falla** con
> `Status: Failed to deploy - GetAllConfigsFromEnvironmentAsync failed`. No es el archivo:
> Remote Config no está habilitado / la cuenta de servicio no tiene el rol. Por eso
> `TablaCombate.PorDefecto()` es **espejo exacto** del `.rc`: sin RC publicada el combate
> funciona igual. (Se puede publicar a mano desde el dashboard cuando se pueda.)

Salida real del `modules get Tienda` **después** del despliegue:

```
endpoints:
  BuscarRival:            parameters: req
  DesbloquearNodo:        parameters: idNodo
  PublicarInventario:     parameters: req
  RepetirCombate:         parameters: {}
  ResolverCombate:        parameters: equipo, idRival
  ResolverCombateProgresion: parameters: req
  ResolverCombatePvp:     parameters: req
```

### Paso 2 — la entrada de la función y de dónde sale cada cosa

```csharp
[CloudCodeFunction("ResolverCombate")]
public async Task<ResultadoCombate> ResolverCombate(
    IExecutionContext context, IGameApiClient api, List<string> equipo, string idRival)
```

- `equipo`: 1 a 3 ids. Fuera de rango, **rechazo** (sale de la función sin escribir).
- `idRival`: id de `rivales_npc`. Si no existe, **rechazo**.
- **No hay parámetro** para daño, victoria, semilla ni resultado: lo que el cliente mande
  con esos nombres se descarta antes de entrar (paso 9).

### Paso 3 — de dónde salen las estadísticas (tres lecturas, dos tokens)

1. **Cloud Save protegido, token del servicio** (`LeerEstadoAsync`): el inventario
   responde **QUÉ tienes**. Un héroe que no está en el inventario ⇒ rechazo sin escribir.
2. **Remote Config, token del jugador, UNA sola petición** para `tabla_heroes` +
   `rivales_npc` (`LeerTablasAsync`). La tabla responde **CUÁNTO vale**.
3. Nada de lo que el jugador guarda en su inventario decide el resultado: la vida/ataque
   que se pelean salen de `tabla_heroes` (el inventario solo prueba propiedad).

### Pasos 4 y 5 — el bucle de turnos y el battle log

`Batalla.Similar(propio, rival, semilla)`:

- `var azar = new Random(semilla);` (el azar, **sembrado**).
- `orden = todos.OrderByDescending(u => u.Velocidad)` (orden **estable**).
- `while (turno < MaxTurnos(100) && Viven(nuestros) && Viven(suyos))`.
- `dano = Math.Max(1, atacante.Ataque + azar.Next(-2, 3) - objetivo.Defensa)`.
- Cada golpe añade `{ turno, atacante, objetivo, dano, vidaRestante }`.

Devuelve además `EquipoPropio` / `EquipoRival` con la **vida inicial** (para que el cliente
pinte las barras desde el principio) y `TurnosJugados`, `Semilla` y `Victoria`.

### Paso 6 — el premio y el estado, en una sola escritura

```csharp
estado.Saldos.Monedas     += premio;    // 150 ORO si gana; 0 si pierde
estado.Progreso.Trofeos   += trofeos;   // trofeos del rival
estado.UltimoCombate = new UltimoCombateEstado { Semilla, Victoria, Turnos, Rival,
    UnidadesPropias, UnidadesRivales, BattleLog, SaldoResultante };
await EscribirCombateBatchAsync(...);   // UN SetProtectedItemBatchAsync: saldos + progreso + ultimo_combate
```

- La **semilla nace en el servidor**: `Random.Shared.Next(1, int.MaxValue)`. El cliente no
  la elige ni la conoce de antemano.
- O entran los tres (premio, trofeos, pelea) o no entra ninguno.
- El perdedor **no cobra** y `ultimo_combate` queda igualmente escrito (para poder repetir).

### Paso 7 — repetir el combate: mismo log, sin premiar

```csharp
[CloudCodeFunction("RepetirCombate")]
public async Task<ResultadoCombate> RepetirCombate(IExecutionContext context, IGameApiClient api)
```

Lee `ultimo_combate`, vuelve a simular con **su semilla y sus equipos congelados** y
responde con `Repeticion = true`. **No escribe nada.** Si coincide el log, el determinismo
está demostrado en el servidor (no en el cliente).

### Paso 8 — el cliente: reproducir, no calcular

`ReproductorCombate.PelearAsync(equipo, idRival)`:

1. `CallModuleEndpointAsync<ResultadoCombate>("Tienda", "ResolverCombate", { equipo, idRival })`.
2. Recorre `BattleLog` con `WaitForSeconds(0.6f)` por acción.
3. Pinta la vida **con el `vidaRestante` que llega** (`_vida[objetivo] = vidaRestante`), sin
   restar daño ni recalcular el resultado. Las barras arrancan en la vida de `EquipoPropio`
   / `EquipoRival`.
4. Dispara `AccionReproducida` por golpe y `CombateTerminado` al cerrar.

`RepetirAsync()` hace lo mismo con `RepetirCombate` e imprime si el log salió **idéntico**.

### Paso 9 — los intentos de trampa (y la respuesta literal)

Los hace `DemoLab08.IntentosTramposos()`. Respuestas esperadas del servidor:

| # | Intento | Respuesta del servidor |
|---|---|---|
| 1 | `dano=9999`, `victoria=true`, `semilla=123` en el cuerpo | Los tres campos se ignoran: la pelea ocurre con **la semilla del servidor** y el resultado es el real. |
| 2 | `equipo: ["heroe_coloso"]` | `Rechazado=true` — «heroe_coloso no es peleable.» |
| 3 | El mismo héroe dos veces teniendo uno | `Rechazado=true` — «El equipo no puede llevar el mismo héroe dos veces.» |
| 4 | Cuatro héroes | `Rechazado=true` — «El equipo lleva entre 1 y 3 héroes.» |
| 5 | Rival que no existe | `Rechazado=true` — «No existe el rival <npc_inexistente>.» |
| 6 (extra) | Un héroe que no está en mi inventario | `Rechazado=true` — «asesino no está en tu inventario.» |

Ninguno de los rechazos escribe `saldos`, `progreso` ni `ultimo_combate`. El campo
`Saldo` de la respuesta lo demuestra:

- Intentos **2, 3 y 4** (equipo inválido): el rechazo ocurre **antes** de tocar Cloud Save,
  así que el servidor responde `Saldo=sin cambios` (`SaldoResultante = -1`): no se hizo ni
  la lectura.
- Intentos **5 y 6** (rival que no existe / héroe que no tengo): el rechazo llega después de
  leer el estado, así que devuelve **el mismo saldo** con el que entró (`Saldo=400`, no se
  movió ni un oro).
- Ningún rechazo devuelve `BattleLog`, ni `Semilla`, ni `Recompensa` con cantidad.

### Paso 10 — el paquete

Ver el apartado 7 de este documento (`AlexanderVillena_Lab08.zip`).

---

## 3. Las siete pruebas (`dotnet test`)

```powershell
dotnet build .\Tienda\Tienda.csproj
dotnet test  .\Tienda.Pruebas\Tienda.Pruebas.csproj
```

Salida real:

```
Correctas! - Con error: 0, Superado: 7, Omitido: 0, Total: 7
```

| # | Prueba | Qué demuestra |
|---|---|---|
| 1 | `MismaSemilla_MismoBattleLog` | misma semilla + mismos equipos ⇒ mismo log (determinismo). |
| 2 | `SemillaDistinta_CambiaElLog` | la semilla influye: no es una pelea fija. |
| 3 | `OrdenDeTurno_PorVelocidad` | el más rápido golpea primero. |
| 4 | `DanoMinimoEsUno` | el daño nunca baja de 1 y la vida nunca queda negativa. |
| 5 | `Simular_NoMutaLosEquiposDeEntrada` | se puede repetir la pelea con las mismas unidades. |
| 6 | `TopeDeTurnos_CortaLaPelea` | el bucle se corta a los 100 turnos. |
| 7 | `TablaDeRivales_UsaHeroesDeLaTabla` | `rivales_npc` solo manda héroes de `tabla_heroes`. |

> El proyecto de pruebas apunta a `net8.0` porque en esta máquina no está instalado el
> runtime de `net6.0` (el módulo sigue siendo `net6.0`, que es lo que exige Cloud Code).

---

## 4. Cómo correr la demo en Unity

1. Abre la escena de pruebas (la misma del Lab 10).
2. GameObject **`Registros`** → *Add Component* → **`ReproductorCombate`**
   (opcional: arrastra dos `Slider` a las barras de vida).
3. En el mismo GameObject → *Add Component* → **`DemoLab08`**.
4. Revisa los campos: `equipo` (`guerrero_del_hacha, vampiro, draconiano`) y
   `rivalNpc` (`npc_practica_1`).
5. Play y pulsa **F12** (o el botón que apunte a `DemoLab08.Correr`).

En la consola verás, en orden:

```
[Lab08][Combate] Victoria=True Turnos=… Semilla=… vs Rival de practica (npc_practica_1) acciones=… premio=ORO+150 saldo=150
[Lab08][Acción] T1 escudero → guerrero_del_hacha -… (queda …)        (una cada 0,6 s)
[Lab08][Fin] VICTORIA tras … turnos. Saldo resultante: 150
[Lab08][DETERMINISMO] repetición con semilla …: log idéntico = True (… acciones) — saldo sigue en 150
[Lab08][INTENTO 1 …] servidor → Rechazado=False … Saldo=400 …
[Lab08][INTENTO 2 heroe_coloso] servidor → Rechazado=True … Saldo=sin cambios | "heroe_coloso no es peleable."
…
```

---

## 5. Capturas que pide el informe

1. `dotnet test` con **7 superadas**.
2. `cloud-code modules get Tienda` con `ResolverCombate: parameters: equipo, idRival`
   y `RepetirCombate`.
3. La consola de Unity con la **pelea reproducida** (una línea por acción) y el
   `[Fin]`.
4. La consola con la **repetición idéntica** (`log idéntico = True`).
5. La consola con **los 6 intentos** y su respuesta literal.
6. Opcional: el dashboard → Cloud Save → pestaña **Protected** del jugador con
   `saldos`, `progreso` y `ultimo_combate` escritas por la función.

---

## 6. Trampas conocidas

- **`ugs` no está en el PATH**: es `.\ugs\ugs.exe.exe` (sí, con doble `.exe`).
- **Deploy por `.sln`, nunca por `.csproj`**: el CLI necesita la solución.
- **`--no-prompt` no existe** en este CLI.
- **`Funciones` no puede ser `static`** ni las funciones `[CloudCodeFunction]`: si lo son,
  el módulo despliega con `endpoints: {}` y todo responde 404.
- **El cliente de un módulo C# usa `CallModuleEndpointAsync`**; `CallEndpointAsync` busca en
  los scripts JS y devuelve 404.
- **Los argumentos se mapean por nombre de parámetro**: aquí son `equipo` e `idRival`
  sueltos (no van dentro de `req`).
- **Remote Config**: el deploy del `.rc` falla en este entorno
  (`GetAllConfigsFromEnvironmentAsync failed`); los defaults de `TablaCombate.cs` cubren.
- **`net6.0` no está instalado** en la máquina: el módulo compila igual, pero las pruebas
  se ejecutan en `net8.0`.

---

## 7. Paquete de entrega

```powershell
# desde la raíz del proyecto
Compress-Archive -Path .\AlexanderVillena_Lab08\* -DestinationPath .\AlexanderVillena_Lab08.zip -Force
```

Contenido de `AlexanderVillena_Lab08/`:

```
Tienda/                  (módulo sin bin/ ni obj/)
   Batalla.cs, TablaCombate.cs, Funciones.cs, Datos.cs, Combate.cs,
   Progression.cs, TablaRivales.cs, Tienda.csproj, S08-rivales-npc.rc, S10-progresion.rc, *.js
   Tienda.sln
Tienda.Pruebas/          (las 7 pruebas)
   Tienda.Pruebas.csproj, PruebasCombate.cs
Scripts_Unity/
   Estado.cs, ReproductorCombate.cs, DemoLab08.cs, ProgresionCliente.cs, UgsDefensa.cs
Lab08-Combate.md
```

---

## 8. Conclusiones

1. **El servidor manda y el cliente solo dibuja.** La semilla (`Random.Shared.Next(1, int.MaxValue)`)
nace dentro de `ResolverCombate`; el cliente nunca la envía. Cuando el cliente intentó mandar
`dano=9999`, `victoria=true` y `semilla=123`, la respuesta trajo **otra semilla** (`267978086`)
y un premio calculado en el servidor: los campos del cliente se ignoraron por completo. Un
combate resuelto en el cliente es una sugerencia; uno resuelto en Cloud Code es una verdad.

2. **La simulación es puramente determinista.** `Batalla.Similar` no usa `DateTime`, `Random.Shared`
ni lecturas de red: recibe los dos equipos y una semilla, y devuelve el mismo `BattleLog` siempre.
La prueba `MismaSemilla_MismoBattleLog` lo demuestra y `RepetirCombate` lo confirma en vivo con
`log idéntico = True` sobre 28–32 acciones. Gracias a eso el replay es gratis y auditable: basta
guardar la semilla y los equipos congelados.

3. **Separar QUÉ tengo de CUÁNTO vale es lo que cierra la puerta a la trampa.** El inventario
(Cloud Save **protegido**, token de servicio) solo dice qué héroes posees; las estadísticas salen
siempre de Remote Config (`tabla_heroes`), leída con el token del jugador y en **una sola petición**
junto con `rivales_npc`. Si las stats vinieran del guardado del jugador, comprarlas infinitas
dependería de su buena voluntad.

4. **Una sola escritura concentra el premio, el progreso y el historial.** `saldos`, `progreso` y
`ultimo_combate` viajan en el mismo `SetProtectedItemBatchAsync`: o entran los tres o no entra
ninguno. Evita el estado imposible de “gané oro pero la pelea no quedó registrada”.

5. **Rechazar antes de escribir es una decisión de diseño, no un detalle.** Los rechazos por
entrada inválida (equipo vacío, héroe repetido, 4 héroes, héroe coloso) responden sin tocar
Cloud Save —por eso devuelven `Saldo=sin cambios`—, mientras que los que llegan después de leer
el estado devuelven el saldo real (`Saldo=700`), demostrando que no se movió ni un oro. Rechazar
sin escribir también protege cuotas: un cliente malicioso no puede convertir sus intentos en
lecturas y escrituras de Cloud Save.

6. **Instalar algo nuevo sin romper lo anterior es posible.** El módulo ya tenía el combate de
poder del Lab 09 y la progresión del Lab 10. En lugar de borrar `ResolverCombate`, se renombró a
`ResolverCombatePvp` y el nombre obligatorio pasó al combate nuevo: los endpoints viejos siguen
publicados y las pruebas del laboratorio anterior no se tocaron. La verificación en Unity
(`[UGS] Conectado`, inventario sembrado, pelea, replay y 6 rechazos) cerró el círculo.

---

## 9. Limitaciones y trabajo futuro

**Limitaciones del alcance resuelto**

1. **`S08-rivales-npc.rc` no quedó publicado.** `ugs deploy` responde
   `Failed to deploy - GetAllConfigsFromEnvironmentAsync failed`: es un problema ambiental de
   Remote Config en el proyecto (mismo caso que en el Lab 10), no del archivo. Mitigación:
   `TablaCombate.PorDefecto()` replica exactamente la misma tabla en código y, si la lectura
   de Remote Config falla, el combate sigue funcionando con esos valores. En cuanto Remote
   Config esté operativo, basta con desplegar el `.rc`.
2. **Sin idempotencia en `ResolverCombate`.** Es lo que pide la guía (307: si se pierde la
   respuesta, el jugador simplemente vuelve a pelear), pero implica que un reintento de red
   cuenta como una pelea nueva y vuelve a premiar. En un juego real habría que añadir una
   clave de operación.
3. **El battle log es la única fuente visual.** No hay animación de sprites ni audio: el
   cliente pinta texto y barras de vida cada 0,6 s. La pelea “se lee” más de lo que “se ve”.
4. **Equipos de 1 a 3 héroes y tres rivales de práctica.** Alcanza para validar el bucle
   autoritativo, no para balancear el juego completo.
5. **Una sola pasiva por héroe (ninguna).** La tabla del laboratorio solo modela vida, ataque,
   defensa y velocidad; las habilidades del juego (`DatosFacciones`) no participan en esta
   simulación.

**Trabajo futuro**

- Publicar Remote Config y hacer que `tabla_heroes` se pueda balancear sin desplegar código.
- Idempotencia por `idOperacion` y reintento seguro.
- Emparejar `rivales_npc` con el ranking del Lab 09 para que el rival lo elija el servidor.
- Reproducción con animaciones de sprite usando el mismo `BattleLog` (el contrato no cambia).
- Historial de peleas (`ultimos_combates`) usando la misma escritura en lote.
