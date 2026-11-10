# Lab 12 — LiveOps: evento programado, bienvenida y analítica

**Proyecto:** Quizzerttack · módulo Cloud Code `Tienda` · Remote Config
**Estado:** **terminado y verificado en runtime** (2026-11-09). Módulo desplegado, overrides activos, demo F5 con todas las evidencias. Entregable: `AlexanderVillena_Lab12.zip`.
**Pruebas:** `dotnet test` → **25 correctas, 0 con error** (19 de los labs 10/11 + 6 nuevas del evento).

---

## 1. Qué se agregó (y qué NO se tocó)

| Archivo | Qué hace |
|---|---|
| `Tienda/Evento.cs` | `ConfigEvento`, `ConfigBienvenida`, `ReglasEvento` (reglas puras), `LiveOps.LeerAsync` (la única parte con red) y el catálogo de la tienda. |
| `Tienda/Funciones.cs` | `VistaEvento`, `ReclamarBonusBienvenida`, `ComprarHeroe` + el evento aplicado en `ResolverCombate` y `ResolverCombateProgresion`. |
| `Tienda/Combate.cs` | La bandera `bonoBienvenida` dentro del `perfil` PROTEGIDO (no se creó una séptima llave). |
| `Tienda/Datos.cs` | `EventoActivo`, `EventoNombre` y `FactorDano` en `ResultadoCombate` (para la consola y el informe). |
| `Tienda/S12-campana-evento.rc` | Base que se despliega: `campana_evento` apagado y `bonus_bienvenida` en 0. |
| `Tienda/S12-campana-evento-override.json` | Valor ENCENDIDO para pegar en Game Overrides (paso 5). |
| `Tienda/S12-bienvenida-override.json` | Valor del segmento «nuevos» (paso 8). |
| `Tienda.Pruebas/PruebasEvento.cs` | 6 pruebas: factor de daño, ventana UTC, descuento, botín, bonus y «sin evento». |
| `Assets/Scripts/Online/EventoLiveOps.cs` | Cliente: pregunta `VistaEvento` cada 15 s, dispara `EventoCambio` y calcula la cuenta atrás. |
| `Assets/Scripts/Online/HudEventoLab12.cs` | Cartel en pantalla con nombre, tiempo restante, daño, descuento y precio del Mago. |
| `Assets/Scripts/Online/DemoLab12.cs` | Demo de los pasos 2, 3, 4, 6, 7 y 8 con la tecla **F5**. |
| `Assets/Scripts/Modelos/Estado.cs` | DTOs `VistaEventoDto`, `ResultadoBonoDto`, `ResultadoCompraDto`, `PremioBotinDto`. |

**El módulo pasa de 8 a 11 funciones.** `ComprarHeroe` no existía en este proyecto y el paso 3
(el descuento cobrado por el servidor) no se puede demostrar sin una compra.

---

## 2. Paso 1 — Qué configuración es candidata a cambiar durante un evento

Un evento **cambia NÚMEROS que el servidor aplica**; no toca el estado del jugador ni las
reglas de validación (si el evento pudiera cambiar una validación, el jugador podría
«negociar» las reglas con el cliente).

| Candidato | Dónde vive | Se cambia con el evento |
|---|---|---|
| Multiplicador de daño | `campana_evento` (Remote Config) | Sí · ×1.25 |
| Precios / héroes en venta | catálogo del módulo + `heroesEspeciales` | Sí · −30 % en el Mago |
| Tabla de botín | `progresion.botin` (S10) + `botinExclusivo` | Sí · se SUMA el ítem exclusivo |
| Bonus de bienvenida | `bonus_bienvenida` + override del segmento | Sí · 0 → 150 ORO |
| Estado del jugador (nivel, trofeos, saldo) | Cloud Save protegido | **No** · el evento no reescribe el estado |
| Reglas de validación | código del módulo | **No** · cambian con una versión nueva |

Los dos números del evento viven en **un solo JSON** (`campana_evento`). La base que se
despliega está apagada; el valor encendido vive en el **override** del paso 5.

### Despliegue

```powershell
cd C:\Users\USER\Desktop\Quizzerttack\Tienda
..\ugs\ugs.exe.exe deploy S12-campana-evento.rc    # crea campana_evento y bonus_bienvenida
..\ugs\ugs.exe.exe deploy Tienda.sln               # 11 funciones (suma VistaEvento, ReclamarBonusBienvenida y ComprarHeroe)
```

Si usás el alias de los labs anteriores, es el mismo `ugs deploy ...`.

> **Si `ugs deploy S12-campana-evento.rc` falla** con `GetAllConfigsFromEnvironmentAsync failed`
> (le pasa al service account `lab09` en este proyecto), no es el archivo: falta el rol de
> Remote Config o el servicio no está habilitado en el entorno. Alternativa equivalente y
> válida para el entregable: crear las dos llaves a mano en **Remote Config → Add a key**
> (tipo **JSON**, valores base de abajo) y pulsar **Publish**. El módulo se comporta igual,
> porque lee las llaves por nombre.
>
> ```json
> campana_evento    = {"activo":false,"id":"","nombre":"","inicioUtc":"","finUtc":"","multiplicadorDano":1.0,"descuentoTienda":0.0,"heroesEspeciales":[],"botinExclusivo":[]}
> bonus_bienvenida  = {"moneda":"ORO","cantidad":0}
> ```

---

## 3. Pasos 2 a 4 — Cómo entra el evento en el combate y en la compra

### Paso 2 · Daño (lo aplica el SERVIDOR antes de simular)

En `ResolverCombate` y `ResolverCombateProgresion`:

```csharp
var liveOps = await LiveOps.LeerAsync(context, api, ReglasEvento.EsNuevo(estado.Perfil.NivelCuenta));
double factor = ReglasEvento.FactorDano(liveOps.Evento, DateTime.UtcNow);
foreach (var unidad in propios) unidad.Ataque = ReglasEvento.AtaqueEscalado(unidad.Ataque, factor);
var pelea = Batalla.Similar(propios, rivales, semilla);
```

`ultimo_combate` guarda el equipo **ya escalado**, así que `RepetirCombate` reproduce el
mismo log con evento o sin él. Sin evento el factor es `1.0` y se juega exactamente como
en el Lab 11.

### Paso 3 · Descuento (lo decide el servidor al COBRAR)

```csharp
long precio = ReglasEvento.PrecioConDescuento(evento, item.Id, item.Precio, DateTime.UtcNow);
// Mago: 1200 × (1 − 0.30) = 840
```

- `heroesEspeciales: ["mago"]` solo **lista** ids del catálogo: el catálogo no se duplica.
- Un héroe que no está en la lista conserva su precio.
- El cliente manda **solo el id**; si miente con el precio, el cobro es el del servidor.
- Si no alcanza el oro, el mensaje es el de la guía: «Te faltan 690 ORO: Mago cuesta 840»
  (con 150 de saldo).

### Paso 4 · Botín (se SUMA a la tabla del S10)

```csharp
ReglasProgresion.TirarBotin(ReglasEvento.BotinConEvento(evento, cfg.Botin, ahora), semilla)
```

`BotinConEvento` devuelve una **copia** con los premios base + `botinExclusivo`
(`EMBLEMA_FESTIVAL`, cantidad 1, peso 25). La semilla del combate decide cuál cae; la
tabla publicada en Remote Config no se toca.

---

## 4. Paso 5 — Programar la ventana en el dashboard (esto lo hacés vos)

1. **Productos → Game Overrides → Create Override** (en el panel actual Game Overrides es un
   producto propio dentro del grupo *LiveOps*; no está dentro de Remote Config).
   Antes, en **Remote Config → Config**, las llaves `campana_evento` y `bonus_bienvenida`
   tienen que existir y estar **Published**; si no, el desplegable de Content sale vacío.
2. Nombre: `Lab12 - Festival de la Nube`.
3. **Targeting**: `All Players (Audiences)` (el segmento de nuevos va en el paso 8).
4. **Content → Config Overrides → Add Override**:
   - Key: `campana_evento`
   - Value: el contenido de `Tienda/S12-campana-evento-override.json`
5. **Scheduling → Select a custom date**: inicio y fin **con las MISMAS fechas del JSON**.
   La zona horaria del dashboard está bloqueada en **UTC**.
6. **Enable** el override. Tarda ~1 minuto en propagarse.

Para la clase alcanza una ventana corta: **17:05 → 17:12 UTC** (7 minutos). Copiá esas
fechas exactas al JSON. Con el calendario del dashboard en *Start immediately / Run
indefinitely* el encendido lo decide **solo** el JSON: `inicioUtc` vacío = ya empezó.

> **`finUtc` NO se puede dejar vacío** si querés ver la cuenta atrás: `ReglasEvento.SegundosRestantes`
> devuelve 0 cuando no hay fecha de fin, y el HUD muestra `00:00:00`. Dejalo siempre en el futuro
> mientras dure la demo.
>
> Hay **dos cerrojos** y los dos tienen que estar abiertos: el calendario del override (dashboard)
> decide si el override se aplica; `inicioUtc`/`finUtc` del JSON deciden si el evento está `activo`.

### Por qué la lectura de Remote Config devolvía 400 y después 401

**Primero, el 400.** El cuerpo de `AssignSettingsAsync` es un `UserAssignmentRequest` y el
servidor **exige** que `attributes.unity` y `attributes.app` existan. Si se construye
`RequestAttributes` solo con `user`, la API responde:

```
400 Bad Request
{"error":"Invalid request value at path: com.unity.analytics.remoteconfig.models.
UserAssignmentRequest[\"attributes\"]->UserAssignmentRequestAttributes[\"unity\"]."}
```

El `catch` que tenía el módulo se tragaba el error y el evento quedaba con los valores por
defecto (`activo=false`), como si el override no aplicara. `LiveOps.LeerAsync` arma ahora las
tres ramas —`unity` (con valores del servicio, no del teléfono), `app` (diccionario vacío) y
`user` (`esNuevo`)— y el `catch` guarda el cuerpo real del error en `ConfigLiveOps.Error`, que
se ve en `VistaEvento.Mensaje`. Ese texto es el que hay que mirar cuando algo no se enciende.

**Después, el 401.** Con el cuerpo ya válido la API contestó `ApiException: Unauthorized`. El
endpoint de entrega (`config.unity3d.com/api/v1/settings`) es el mismo que consulta el SDK del
cliente, así que autentica con el **token del jugador**: `context.ServiceToken`
(service-to-service) no le sirve. `LiveOps.LeerAsync` prueba en orden `context.AccessToken`
(jugador) y, si falla, `context.ServiceToken`, y deja el motivo de cada intento en
`ConfigLiveOps.Error`.

---

## 5. Pasos 6 y 7 — El cliente: aparece y desaparece solo

- `EventoLiveOps.Iniciar()` crea su GameObject y pregunta `VistaEvento` **cada 15 s**
  (`SegundosEntreConsulta`). No hay que reiniciar ni publicar una versión.
- `EventoCambio` se dispara **solo** cuando el evento se enciende o se apaga:
  ```
  [Lab12][17:05:08] >> CAMBIÓ: el evento APARECIÓ «Lab12 - Festival de la Nube» (quedan 00:06:50)
  [Lab12][17:12:09] >> CAMBIÓ: el evento DESAPARECIÓ (se vuelve a los números base del S10)
  ```
  La consola de Unity muestra la hora **LOCAL**; el servidor publica en **UTC**.
- `SegundosRestantesAhora()` = segundos que mandó el servidor − `Time.realtimeSinceStartup`
  transcurrido. **Nunca** usa `DateTime.Now` del dispositivo (se podría atrasar el reloj).
- `HudEventoLab12` pinta el cartel: nombre, `quedan hh:mm:ss`, `daño ×1.25`,
  `tienda -30 %` y `Mago 840 (antes 1200)`.

---

## 6. Paso 8 — El segmento de jugadores nuevos

Segundo override: **Productos → Game Overrides → Create Override**

- Nombre: `Lab12 - Bienvenida jugadores nuevos`
- Targeting: JEXL `user.esNuevo == "true"` (atributo de usuario)
- Content → Config Overrides: key `bonus_bienvenida`, value = `S12-bienvenida-override.json`
- **Sin fecha de fin**; **Enable**.

Cómo funciona:

1. El **servidor** calcula `esNuevo = perfil.nivelCuenta <= 1` con el dato PROTEGIDO.
2. Lo manda como atributo en `AssignSettingsAsync` (`RequestAttributes.user["esNuevo"]`).
   El cliente **no manda nada** y no puede decir que es nuevo. La petición viaja con las tres
   ramas (`unity`, `app`, `user`) porque la API rechaza el cuerpo si falta `unity`.
3. `ReclamarBonusBienvenida` entrega el bonus **una sola vez**: la bandera
   `perfil.bonoBienvenida` queda en el perfil protegido.

Verificación con dos cuentas en paralelo:

| Cuenta | Nivel de cuenta | `esNuevo` | Primer reclamo | Segundo reclamo |
|---|---|---|---|---|
| nueva (perfil `principal` recién creado) | 1 | `true` | +150 ORO (500 → 650) | «ya se reclamó» |
| veterana (jugó 2 combates con progresión) | 2 | `false` | 0 ORO | 0 ORO |

Para subir la cuenta a nivel 2: dos victorias con `ResolverCombateProgresion` dan ~62 XP
cada una y el nivel 1→2 cuesta 100 (ver `progresion.niveles.costoBase`).

> En la demo (`DemoLab12`, F5) el reclamo va **antes** de los combates: si se deja al final, la
> cuenta ya subió a nivel 2 con los 6 combates y el segmento deja de aplicar. Es además el orden
> real del juego: el bono de bienvenida se reclama al llegar, no después de pelear.

---

## 7. Paso 9 — Tabla para el informe

| Cambio | Valor base | Valor del evento | Ventana UTC | Métrica para evaluarlo |
|---|---|---|---|---|
| Daño del equipo propio | ×1.0 (ataque de tabla) | ×1.25 (12→15; 30→38 en la tabla actual) | 17:05–17:12 | duración media del combate y tasa de victoria por día |
| Descuento de tienda | 0 % | −30 % (Mago 1 200 → 840) | 17:05–17:12 | conversión de compra del Mago y ORO gastado por jugador |
| Botín | espada / poción / nada | + EMBLEMA_FESTIVAL (peso 25) | 17:05–17:12 | % de combates con el ítem y objetos por jugador |
| Bonus de bienvenida | 0 ORO | 150 ORO solo nivel 1 | sin fecha de fin | retención día 1 de las cuentas nuevas |
| Ventana del evento | — | 7 minutos | 17:05–17:12 | combates por jugador durante el evento |

Los eventos de analítica que alimentan estas métricas son el tema del Lab 13.

---

## 8. Cómo probarlo (orden exacto)

1. `ugs deploy` de las dos configuraciones (sección 2).
2. En Unity: **Play** → **F5** (`DemoLab12`). La consola muestra:
   - vista del evento (activo/apagado, ventana, hora del servidor, segundos restantes);
   - primer intento de compra (precio 840 y «te faltan …»);
   - 6 combates con `factor 1.25` y `RepetirCombate` con el mismo log;
   - compra final del Mago a **840**;
   - botín del combate con progresión;
   - primer y segundo reclamo del bonus.
3. Programá el override del paso 5 con una ventana de 2 minutos y **grabá**: el HUD y la
   consola muestran el cambio sin reiniciar. Ese es el video del entregable.
4. Creá el override del paso 8 y repetí con dos perfiles (`principal` nuevo y otro con
   nivel 2).
5. Capturas: dashboard del override (Schedule en UTC), la lista de overrides activos, la
   consola con «CAMBIÓ: el evento APARECIÓ / DESAPARECIÓ» y el HUD.

---

## 9. Entregable (`ApellidoNombre_Lab12.zip`)

- El proyecto (o al menos `Tienda/` + `Assets/Scripts/Online/Evento*`, `DemoLab12`, `Hud*`).
- `Tienda/S12-campana-evento.rc`.
- `Tienda/S12-campana-evento-override.json` y `Tienda/S12-bienvenida-override.json`.
- La tabla del paso 9 (sección 7 de este archivo).
- El video de la activación automática (HUD + consola, sin reiniciar).

---

## 10. Notas de diseño (para defenderlo)

- **El evento no es estado**: no escribe en Cloud Save. La única escritura nueva es la
  bandera `bonoBienvenida` dentro del `perfil` que ya existía.
- **El cliente no decide**: `VistaEvento` es de solo lectura; el daño, el precio y el botín
  se aplican en el módulo con los datos protegidos.
- **La hora es del servidor**: el contador se ancla a `SegundosRestantes` y avanza con el
  reloj monótono de Unity; si el jugador atrasa el reloj del sistema, no gana tiempo.
- **Repetibilidad**: el equipo escalado se congela en `ultimo_combate`, así que
  `RepetirCombate` devuelve el mismo log con el evento encendido o apagado.
- **Sin RC publicada**: `ConfigEvento()` nace apagado (factor 1.0, bonos 0) y el juego se
  comporta como en el Lab 11.

---

## 11. Conclusiones

### Lo que quedó demostrado

La corrida final (perfil nuevo `lab12nuevo2`) probó el ciclo completo de LiveOps **sin
reiniciar la aplicación y sin publicar una versión nueva**:

| Qué | Evidencia en consola |
|---|---|
| El evento aparece solo | `>> CAMBIÓ: el evento APARECIÓ «Lab12 - Festival de la Nube» (quedan 49:04:36)` |
| Estado leído del servidor | `activo=True` · ventana `11/11/2026 23:59:00 (UTC)` · `hora del servidor 22:54:23Z` |
| HUD | `quedan 49:04:36 · daño x1.25 · tienda -30 %` |
| Segmento de nuevos | `esNuevo=True · bono en la vista=150 ORO` |
| Bono una sola vez | `+150 ORO (saldo 150)` → segunda vez `yaReclamado=True` |
| Descuento cobrado por el servidor | `1200 -> 840 (-30 %)` · con 0 de saldo: `Te faltan 690 ORO` · compra final: `pagó 840 (descuento aplicado=True)` |
| Daño del evento | 6 combates con `evento=True … factor 1.25` |
| Determinismo intacto | `turnos 5 vs 5 · acciones 20 vs 20 · MISMO LOG=True` |
| Botín del evento | `botin=[pocion] · factor 1.25 · evento=True` |
| Vigilancia viva | consultas a las 17:59:27 / :41 / :57 con la cuenta atrás bajando sola |

### Las cinco ideas que me llevo

1. **LiveOps se falla en silencio.** Ningún fallo de este lab se vio como error rojo: el
ejemplo es `activo=False «»`, que no es un bug del cliente sino el **valor base** llegando
porque el override no se aplicó. La regla de diagnóstico quedó clara: si el nombre del
evento viene vacío, el problema está en la entrega de Remote Config, no en el código.

2. **El diagnóstico vale más que el reintento.** Los dos errores reales aparecieron solos
cuando el módulo empezó a **guardar el cuerpo del error** (`ConfigLiveOps.Error` y el
mensaje de `VistaEvento`): el 400 era `attributes.unity` faltante y el 401 era usar
`context.ServiceToken` en un endpoint que exige el token del jugador. Reproducir la llamada
HTTP fuera de Unity (token-exchange + POST a `config.unity3d.com`) permitió distinguir
400 (cuerpo inválido) de 401 (credencial inválida) sin adivinar.

3. **El segmento depende del dato protegido.** `esNuevo` lo calcula el servidor
(`nivelCuenta <= 1`); el cliente solo lo transporta como atributo. Eso es lo que hace que
no se pueda reclamar el bono mintiendo. Y obliga a cuidar el orden de la demo: **dos
combates ya suben la cuenta a nivel 2**, así que el reclamo va primero.

4. **Un evento debe cambiar números, no reglas.** Daño, precio y botín se resuelven dentro
del módulo con el estado protegido; `VistaEvento` es de solo lectura. Por eso el evento se
puede encender y apagar en caliente sin riesgo de que el jugador negocie las reglas.

5. **Dos cerrojos, y hay que abrir los dos.** El calendario del override (dashboard) decide
si el override se aplica; `inicioUtc`/`finUtc` del JSON deciden si el evento está `activo`.
Un evento apagado puede ser cualquiera de los dos, y por eso conviene mirar el mensaje del
servidor antes de tocar nada más.

### Deuda asumida (consciente)

- Las métricas del paso 9 no se instrumentan acá: son el tema del **Lab 13** (analítica).
- `Funciones.cs` todavía lee `tabla_heroes`, `rivales_npc`, `progresion` y `temporada` con el
`ServiceToken` y sin atributos, así que cae a los valores por defecto sin avisar. Se dejó
así a propósito para no tocar lo que ya funcionaba; `Evento.LiveOps.LeerAsync` es el
camino correcto y debería ser el único cuando se unifiquen.
- El estado de los overrides **no** se puede auditar por API con el token del CLI
(el admin de Remote Config responde `401 Untrusted issuer`): se verifica en *Simulation
Experience* y en runtime.
