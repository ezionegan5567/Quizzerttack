# Lab 08 — `ResolverCombate`: el combate lo resuelve el servidor

Este documento cubre los **10 pasos** de la tarea 8. El módulo `Tienda` ya estaba más
avanzado (Lab 09 y Lab 10), así que la tarea 8 se integró **sin romper nada**: el combate
viejo de poder sigue vivo como `ResolverCombatePvp` y el combate nuevo de la tarea 8
ocupa el nombre `ResolverCombate`.

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

Ninguno de los rechazos escribe `saldos`, `progreso` ni `ultimo_combate`: `SaldoResultante`
no se mueve entre intentos.

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
[Lab08][INTENTO 1 …] servidor → Rechazado=False …
[Lab08][INTENTO 2 heroe_coloso] servidor → Rechazado=True | "heroe_coloso no es peleable."
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
