# Lab 11 — Tabla de trofeos (Leaderboards)

> Relacionado: [[Avances del juego]] · [[Lab08-Combate]] · [[Lab10-Progresion]] · [[CloudSave-CloudCode]] · [[Sistemas]]

**Guía de Laboratorio #11 — resuelta**
**Tema:** Tabla de clasificación global (UGS Leaderboards) gobernada por el servidor
**Alumno:** Alexander Villena · **Fecha:** 2026-11-02 · **Módulo:** Cloud Code `Tienda` (C# / Net6)

La regla del laboratorio en una frase: **el cliente consulta, el servidor escribe**. Los trofeos
suman o restan en el servidor (`ResolverCombate`), el servidor publica el **total** en la tabla con
su token de servicio, y el cliente solo pide el top, su puesto y sus vecinos. La escritura directa
desde el cliente se cierra con una política `.ac`.

---

## 0. Las tres cosas que evalúa la guía

| Capacidad pedida | Dónde está | Cómo se demuestra |
|---|---|---|
| **Configurar la tabla (`trofeos_dvsc`: Desc, keepLatest, reset mensual con archivo, tiers)** | `Tienda/trofeos_dvsc.lb` | `ugs deploy trofeos_dvsc.lb` y `ugs leaderboards get trofeos_dvsc` devuelven la misma configuración; el dashboard pinta tier Oro/Plata/Bronce por puesto. |
| **Publicar desde el servidor y leer desde el cliente** | `Tienda/Ranking.cs` → `PublicarTrofeosAsync` (token de **servicio**), invocado desde `ResolverCombate`; `Assets/Scripts/Online/RankingCliente.cs` (solo `GetScoresAsync` / `GetPlayerScoreAsync` / `GetPlayerRangeAsync`) | `[Lab11][PERFIL ana] VICTORIA · trofeos +10 · puesto en la tabla 3` (servidor) y `[Lab11][TOP] 1. Ana 40 (Oro)` (cliente). El cliente **nunca** llama a `AddLeaderboardPlayerScoreAsync`… salvo en la trampa del paso 8, que la política rechaza. |
| **Cerrar la trampa y mostrar la temporada** | `Tienda/S11-politica-ranking.ac` + `S11-quitar-politica.json`; `Ranking.LeerCierreAsync` / `LeerTemporadaAsync` + `[CloudCodeFunction("VistaTemporada")]` | `[Lab11][TRAMPA] el servicio RECHAZÓ el puntaje del cliente: … Access has been restricted` y `[Lab11][TEMPORADA] cierra 2026-12-01T05:00:00Z … faltan 28d … premio Corona de oro (+20 GEMAS)`. |

---

## 1. Qué se hizo (mapa de archivos)

### Servidor (módulo Cloud Code `Tienda/`)

| Archivo | Estado | Qué aporta |
|---|---|---|
| `Tienda/trofeos_dvsc.lb` | **nuevo** | La configuración de la tabla: `SortOrder desc`, `UpdateType keepLatest`, `ResetConfig` mensual con `Archive: true`, `TieringConfig` por rango (Oro 2.0 / Plata 9.0 / Bronce). |
| `Tienda/Ranking.cs` | **nuevo** | `PublicarTrofeosAsync` (token de servicio, devuelve `Rank + 1`), `LeerEntradaAsync`, `LeerCierreAsync` (versiones de la tabla, si no hay cae a Remote Config), `LeerTemporadaAsync` y los DTO `ConfigTemporada` / `ResultadoTemporada`. |
| `Tienda/S11-politica-ranking.ac` | **nuevo** | Política de Access Control: `DenyPlayerWriteLeaderboards` (Principal `Player`, Action `Write`, Resource `urn:ugs:leaderboards:/**`). |
| `Tienda/S11-quitar-politica.json` | **nuevo** | `{"statementIds":["DenyPlayerWriteLeaderboards"]}` para revertir la política con `ugs access delete-project-policy-statements`. |
| `Tienda/S11-temporada.rc` | **nuevo** | Remote Config `temporada`: tabla, fecha de cierre y premios por tier (20/10/5 gemas). |
| `Tienda/Progression.cs` | modificado | `ReglasProgresion.CambioDeTrofeos` (suma al ganar, resta la mitad al perder, suelo 0), `TierPorPuesto` y `PremioDe`. |
| `Tienda/Funciones.cs` | modificado | `ResolverCombate` aplica la regla de trofeos y **publica el total** después de guardar; endpoint nuevo `VistaTemporada`. |
| `Tienda/Datos.cs` | modificado | `ResultadoCombate` devuelve `PuestoEnRanking` y `TablaRanking`. |
| `Tienda.Pruebas/PruebasTrofeos.cs` | **nuevo** | 6 pruebas nuevas (incluye la de **10.000 combates**), total del proyecto **19/19**. |

### Cliente (Unity `Assets/Scripts/`)

| Archivo | Estado | Qué aporta |
|---|---|---|
| `Assets/Scripts/Online/RankingCliente.cs` | **nuevo** | `RefrescarTopAsync` (top 10), `MiPuestoAsync` (404 = «sin clasificar»), `VecinosAsync` (±5), `PonerNombreUnaVezAsync` y `VistaTemporadaAsync`. |
| `Assets/Scripts/Online/DemoLab11.cs` | **nuevo** | F6: cuatro perfiles pelean uno tras otro, consulta de las tres vistas, trampa del paso 8 y vista de temporada. |
| `Assets/Scripts/Modelos/Estado.cs` | modificado | `EntradaTablaRanking`, `PremioTemporadaDto`, `VistaTemporadaDto`. |
| `Packages/manifest.json` | modificado | `com.unity.services.leaderboards: 2.3.4`. |

---

## 2. Los diez pasos, uno por uno

### Paso 1 — la tabla `trofeos_dvsc`

Se crea con el archivo de configuración (recomendado, queda versionado en el repo):

```bash
ugs deploy trofeos_dvsc.lb
ugs leaderboards get trofeos_dvsc      # se lee de vuelta para comprobar
```

La configuración exacta (`Tienda/trofeos_dvsc.lb`):

```json
{ "Name": "Trofeos del curso", "SortOrder": "desc", "UpdateType": "keepLatest",
  "ResetConfig": { "Start": "2026-12-01T05:00:00Z", "Schedule": "0 0 1 * *", "Archive": true },
  "TieringConfig": { "Strategy": "rank",
    "Tiers": [ { "Id": "Oro", "Cutoff": 2.0 }, { "Id": "Plata", "Cutoff": 9.0 }, { "Id": "Bronce" } ] } }
```

- **`desc`**: más trofeos = puesto más alto (el servicio ordena; el cliente no ordena nada).
- **`keepLatest`**: se guarda el último valor enviado, no el mejor histórico → publicar el **total**
  hace que un envío perdido se corrija solo en el siguiente combate.
- **`ResetConfig`**: `Start` **tiene que ser una fecha futura** y el `Schedule` es cron: `0 0 1 * *`
  = el día 1 de cada mes a las 00:00 UTC. `Archive: true` guarda la temporada anterior, que es lo
  que permite leer el cierre real.
- **`Name`**: sin paréntesis ni caracteres especiales (`name contains invalid characters`).
- **`TieringConfig`**: los 2 primeros son Oro, del 3 al 9 Plata, del 10 en adelante Bronce.

> **Dos rechazos reales del servicio** (el primero, al desplegar la versión inicial):
>
> ```
> {"code":1004,"errors":[
>   {"field":"name","messages":["name contains invalid characters"]},
>   {"field":"resetConfig.start","messages":["scheduled reset start provided must be after the current time"]}]}
> ```
>
> Se corrigió el nombre (sin `(Quizzerttack)`) y se movió `Start` al **1 de diciembre de 2026**,
> que es además el cierre que anuncia el Remote Config (`temporada.cierre`).

**Lectura de vuelta** (`ugs leaderboards get trofeos_dvsc`) — lo que quedó guardado en el servicio:

```
sortOrder: Desc            updateType: KeepLatest
resetConfig:
  start: 2026-12-01T05:00:00Z
  schedule: 0 0 1 * *
  archive: true
tieringConfig:
  strategy: Rank
  tiers: Oro 2 · Plata 9 · Bronce
```

> **Lo que NO hay que hacer:** crear la tabla a mano en el dashboard. Ahí los tiers se piden como
> *score* y quedaron como `strategy: Score`, `cobre cutoff 49996`, `plata 5555`, con reset
> `@every 504h` y `archive: false`. El deploy del `.lb` fue lo que dejó la configuración correcta:
> el archivo versionado es la fuente de verdad, el dashboard es solo una vista.

> **Cuidado con el proyecto:** esta organización tiene **dos** proyectos UGS. El juego (Unity,
> `ProjectSettings.asset` → `cloudProjectId`) y el CLI apuntaban a `verdaderos avances de open code`,
> mientras la tabla se estaba creando en `Quizzerttack`. Todo daba `403` porque el rol estaba en un
> proyecto y la llamada iba al otro. Ahora los tres apuntan a **Quizzerttack**
> (`5906c36b-06d8-410b-91be-cf367616022a`): `ugs config set project-id`, `cloudProjectId` del editor y
> los roles de la cuenta de servicio.

### Paso 2 — el paquete y el nombre, una sola vez

`Packages/manifest.json` ya incluye `com.unity.services.leaderboards: 2.3.4` (compatible con el
`core` 1.18.0 y `auth` 3.7.4 que ya usa el proyecto). El nombre se pone **una sola vez**:

```csharp
if (string.IsNullOrEmpty(auth.PlayerName)) await auth.UpdatePlayerNameAsync(nombre);
```

`UpdatePlayerNameAsync("Ana")` guarda `Ana#60257`; llamarlo otra vez cambia el sufijo y la tabla
mostraría **otro jugador**. Por eso `RankingCliente.PonerNombreUnaVezAsync` sale sin hacer nada si
ya hay nombre (lo escribe en consola: `ya tiene nombre (Ana#60257): NO se vuelve a poner`).

### Paso 3 — publicar el total desde el servidor

En `ResolverCombate`, **después** de `EscribirCombateBatchAsync` (paso 6 del Lab 08):

```csharp
await EscribirCombateBatchAsync(context, api, estado);                       // 1) el combate a salvo
int puesto = await Ranking.PublicarTrofeosAsync(api, context, context.PlayerId,
                                                estado.Progreso.Trofeos);     // 2) el total a la tabla
```

y dentro de `Ranking.PublicarTrofeosAsync`:

```csharp
var resp = await api.Leaderboards.AddLeaderboardPlayerScoreAsync(
    context, context.ServiceToken, Guid.Parse(context.ProjectId), Tabla, playerId,
    new AddLeaderboardScore(trofeos, null, null), default);
return resp.Data.Rank + 1;   // el servicio cuenta desde 0; aquí se muestra 1..N
```

Tres decisiones que importan:

1. **Token de servicio, nunca el del jugador.** El cliente no puede escribir; el servidor sí.
2. **Se publica el TOTAL, no la diferencia.** Con `keepLatest`, si la publicación falla el combate
   ya está guardado y el siguiente combate manda el total correcto: se autocorrige.
3. **El orden manda**: primero se guarda, después se publica. Un fallo de la tabla devuelve puesto
   `0` («no publicado») y **jamás** pierde el combate.

### Paso 4 — la regla de trofeos (con suelo en cero)

```csharp
public static int CambioDeTrofeos(int trofeosActuales, bool victoria, int trofeosDelRival)
{
    var rival = Math.Max(0, trofeosDelRival);
    if (victoria) return rival;                                   // ganas: sumas los del rival
    var mitad = rival / 2;
    return -Math.Min(Math.Max(0, trofeosActuales), mitad);        // pierdes: restas la mitad, no más de lo que tienes
}
```

Ejemplo del enunciado: **5 trofeos y derrota contra uno de 20** → intenta restar 10, pero solo hay
5 → `-5` → queda **0**. Nunca negativo (eso lo garantiza además el `Math.Max(0, …)` del total).

### Paso 5 — el top 10

```csharp
var top = await LeaderboardsService.Instance.GetScoresAsync(
    Tabla, new GetScoresOptions { Offset = 0, Limit = 10 });
```

El cliente **no ordena ni pagina a mano**: el servicio devuelve la página ordenada y con `Total`.
`Convertir` traduce `Rank` (0..N) a **puesto** (1..N).

### Paso 6 — mi puesto y mis vecinos

```csharp
var mio     = await LeaderboardsService.Instance.GetPlayerScoreAsync(Tabla);
var vecinos = await LeaderboardsService.Instance.GetPlayerRangeAsync(
                  Tabla, new GetPlayerRangeOptions { RangeLimit = 5 });
```

- Si el jugador **todavía no tiene puntaje**, el servicio responde **404**: eso *no* es un error,
  la interfaz dice «sin clasificar» (`MiPuestoAsync` devuelve `null` y no pinta excepción).
- En los extremos de la tabla el rango devuelve menos filas: **no está garantizado** que haya ±5.

### Paso 7 — cuatro jugadores distintos

Una cuenta sola, cuatro identidades anónimas (`bruno`, `carla`, `ana`, `dana`): el cliente cambia de
perfil, pone el nombre la primera vez, publica el inventario protegido y pelea. Después se vuelve al
perfil principal y se consulta la misma tabla. El dashboard (`Leaderboards → trofeos_dvsc`) debe
mostrar **las mismas filas y el mismo orden**, con el Rank del servicio empezando en 0.

### Paso 8 — la trampa y la política `.ac`

`DemoLab11` intenta lo que haría un tramposo:

```csharp
await LeaderboardsService.Instance.AddPlayerScoreAsync("trofeos_dvsc", 9999);
```

- **Sin política**: el servicio lo **acepta** (`puesto 1` con 9999) → la tabla queda inservible.
  Evidencia en el servidor: `ugs leaderboards scores get trofeos_dvsc <principal>` devolvía
  `score: 9999 · rank: 0 · tier: Oro` para el jugador principal.
- **Con `S11-politica-ranking.ac` desplegada** (`ugs deploy S11-politica-ranking.ac`): el servicio
  responde `Access has been restricted` y **no** entra nada; el servidor sigue publicando sin
  problema porque usa el token de servicio (la política solo afecta a `Player`).

> **Lo que costó que la política mordiera** (tres intentos, todos comprobados contra el servidor):
>
> | Intento | `Resource` | Resultado |
> |---|---|---|
> | 1 | `urn:ugs:leaderboards:/*` — el que trae la guía | el `Deny` se crea, pero la escritura del cliente **sigue entrando** (la tabla quedó con `9999`) |
> | 2 | `urn:ugs:leaderboards:/v1/projects/*/leaderboards/*/scores/players/*` — la ruta literal del SDK | sigue entrando; la corrida llegó 1-2 min después del despliegue, así que no se puede separar «ruta mala» de «propagación pendiente» |
> | 3 | `urn:ugs:leaderboards:/**` — todo el servicio, solo `Write` | **rechaza**: tras la corrida el `updatedTime` del `9999` siguió en `2026-11-03T04:51:43Z` |
>
> Regla práctica: un `Deny` de cliente para Leaderboards se escribe con el URN completo del servicio
> y `/**` (`urn:ugs:leaderboards:/**`), se espera unos minutos o se reinicia Play (token nuevo) y se
> comprueba en el servidor, no en la consola. `Action: Write` solo cubre POST/PUT/PATCH/DELETE, así
> que las lecturas (`GetScoresAsync`, `GetPlayerScoreAsync`, `GetPlayerRangeAsync`) siguen intactas, y
> el servidor publica con el token de servicio.
>
> Dato de apoyo: el CLI tiene **dos** capas (`access player-policy`, por jugador/baneo, y
> `access project-policy`, por proyecto). El `Deny` global va en la de **proyecto**; la de jugador
> estaba vacía (`[]`). Y la selección de reglas documentada es «gana la más específica», por eso no
> conviene dejar reglas amplias sueltas del mismo recurso.
- **Revertir** (por si hay que repetir la demo):
  `ugs access delete-project-policy-statements S11-quitar-politica.json`.

### Paso 9 — la temporada con el reloj del servidor

```csharp
var vista = await CloudCodeService.Instance.CallModuleEndpointAsync<VistaTemporadaDto>(
    "Tienda", "VistaTemporada", new Dictionary<string, object>());
```

El módulo calcula: **cierre** (de las versiones archivadas de la tabla; si la tabla aún no tiene
versiones, cae a la fecha de Remote Config y lo dice en `FuenteCierre`), **puesto**, **tier**,
**segundos restantes** y **premio** (de `S11-temporada.rc`). El cliente solo pinta: no tiene el
reloj bueno (se puede adelantar en el dispositivo).

### Paso 10 — el paquete

`ApellidoNombre_Lab11.zip` con el proyecto, `trofeos_dvsc.lb`, `S11-politica-ranking.ac` y las
capturas (tabla global con varios jugadores + trampa rechazada). Ver sección 7.

---

## 3. Las pruebas (`dotnet test`)

```
dotnet test Tienda.Pruebas\Tienda.Pruebas.csproj
Correctas! - Con error: 0, Superado: 19, Total: 19
```

Las 6 nuevas (`Tienda.Pruebas/PruebasTrofeos.cs`):

| Prueba | Qué fija |
|---|---|
| `AlGanar_SumanLosTrofeosDelRival` | ganar suma los trofeos del rival. |
| `AlPerder_RestaLaMitad_PeroNuncaMasDeLoQueHay` | 5 trofeos vs rival de 20 → `-5` → total 0. |
| `AlPerder_ConCeroTrofeos_ElCambioEsCero` | con 0 no se resta nada (ni negativo). |
| `MilDiezMilCombates_ElTotalNuncaBajaDeCero` | **10.000 combates aleatorios** (semilla fija `11_011`): el total nunca baja de 0. |
| `TierPorPuesto_CoincideConLaTabla` | 0→«» · 1-2 Oro · 3-9 Plata · 10+ Bronce. |
| `PremioDe_SaleDeLaConfiguracion` | el premio sale de Remote Config, no del código del cliente. |

---

## 4. Cómo correr la demo en Unity

1. Unity → Package Manager muestra **Leaderboards 2.3.4** (ya está en el manifest).
2. GameObject `Registros` (el mismo de los otros labs) → añadir el componente **`DemoLab11`**.
3. Campos: `perfiles` = bruno, carla, ana, dana · `equipo` = guerrero_del_hacha, vampiro, draconiano
   · `rivalNpc` = `npc_practica_2` · `perfilPrincipal` = `principal`.
4. Play → **F6**. Consola (filtro `Lab11`): peleas de los cuatro perfiles, luego `[TOP]`,
   `[MÍO]`, `[VECINOS]`, `[TRAMPA]` y `[TEMPORADA]`.
5. Para la trampa en los dos estados: corre la demo **sin** la política (se acepta) y otra vez **con**
   `ugs deploy S11-politica-ranking.ac` (se rechaza). Captura las dos.

Orden de despliegue completo:

```bash
ugs deploy trofeos_dvsc.lb
ugs deploy S11-temporada.rc
ugs deploy S11-politica-ranking.ac
ugs deploy Tienda.sln          # ya desplegado: ResolverCombate, RepetirCombate, VistaTemporada…
ugs leaderboards get trofeos_dvsc
```

## 5. Capturas que pide el informe

1. `ugs leaderboards get trofeos_dvsc` (configuración: desc, keepLatest, reset, tiers).
2. Consola de la demo (F6) con los cuatro perfiles y su `puesto en la tabla`.
3. `[Lab11][TOP]` con varias filas (el mismo orden que el dashboard).
4. `[Lab11][MÍO]` / `[Lab11][VECINOS]`.
5. `[Lab11][TRAMPA]` **rechazada** con la política desplegada.
6. Dashboard `Leaderboards → trofeos_dvsc` con la tabla global.
7. `[Lab11][TEMPORADA]` (cierre, puesto, tier, premio, segundos restantes).

### Datos reales de la última corrida (proyecto `Quizzerttack`)

```
Total: 5 entradas · tabla trofeos_dvsc · producción
Alexander#64941  9999  Rank 0  Oro      ← trampa de la corrida SIN política (queda como evidencia)
Dana#56853        30  Rank 1  Oro
Carla#80924       30  Rank 2  Oro
Bruno#58496       30  Rank 3  Plata
Ana#22311         30  Rank 4  Plata

[Lab11][TEMPORADA] cierra 2026-12-01T05:00:00Z (fuente: rc) · faltan 28d 0h 8m
  · trofeos 0 · puesto 1 · tier Oro · premio Corona de oro (+20 GEMAS)
```

Lectura: los cuatro perfiles pelearon **tres veces** cada uno (10 → 20 → 30 trofeos) y el servidor
publicó el total en cada combate; el cliente nunca sumó nada. El corte de tiers se aplica sobre el
rango base 0, así que con `Oro 2.0` / `Plata 9.0` el servicio da **Oro a los puestos 1-3** y Plata
del 4 al 10 (el cliente no opina: pinta el `Tier` que devuelve el servicio).

## 6. Problemas conocidos

| Síntoma | Causa | Solución |
|---|---|---|
| `Code: 53 · Status: 403 · Not authorized` en `ugs leaderboards …` | (a) la cuenta de servicio no tiene **Leaderboards Admin** para el proyecto, o (b) el CLI apunta a **otro** proyecto (`ugs config get project-id`) | dar el rol en *Service accounts → Manage project roles* **del proyecto correcto** y alinear `project-id` con el `cloudProjectId` de Unity. |
| `Error calling: GetEnvironments` (403) | falta `Unity Environments Admin` en ese proyecto | añadirlo junto a los demás roles de la cuenta de servicio. |
| `Validation error 1004: name contains invalid characters` | el `Name` del `.lb` tenía paréntesis | dejar el nombre simple (`Trofeos del curso`). |
| `Validation error 1004: resetConfig.start … must be after the current time` | `Start` en el pasado | poner el próximo cierre real (hoy: `2026-12-01T05:00:00Z`). |
| `Access has been restricted` también en el servidor | se publicó con el token del jugador | publicar siempre con `context.ServiceToken` (como `Ranking.PublicarTrofeosAsync`). |
| El jugador aparece dos veces en la tabla | se llamó `UpdatePlayerNameAsync` más de una vez (cada llamada = otro sufijo) | poner el nombre una sola vez (`PonerNombreUnaVezAsync`). |
| «Deserialization failed» al leer versiones de la tabla | `HttpClient` + modelo tipado cuando la tabla aún no tiene versiones archivadas | leer con `JToken` y si no hay versiones caer a Remote Config (`LeerCierreAsync`). |
| Puesto `0` en el resultado del combate | la publicación en la tabla falló (tabla no desplegada / sin rol) | el combate ya está guardado; se corrige en el siguiente combate (se publica el total). |
| La trampa sigue **aceptándose** aunque la política esté desplegada | el `Resource` no coincide con la ruta real del servicio: `urn:ugs:leaderboards:/*` (guía) deja pasar, y la ruta literal del SDK tampoco frenó | usar `urn:ugs:leaderboards:/**` con `Action: Write`, esperar unos minutos / reiniciar Play y comprobar en el servidor (`updatedTime`) en vez de fiarse de la consola. |
| `[Lab10] Falló en el PASO 2 … Reason=AccessTokenMissing ErrorCode=51` al pulsar F11 | **`DemoLab11` compartía la tecla F11 con `DemoLab10`** (el demo del Lab 10 la usa desde su entrega): al pulsarla arrancaban los dos, `DemoLab11` cambiaba de perfil (`bruno`) mientras `DemoLab10` estaba a mitad de su llamada y el token del perfil anterior desaparecía → `51`. Con F7-F12 todas ocupadas (F7 panel online, F8 rival, F9 registros, F10 Lab 09, F11 Lab 10, F12 Lab 08), el Lab 11 se movió a **F6** | un solo dueño por tecla: `DemoLab11` ahora se dispara con **F6** y `DemoLab10` conserva F11 (como se entregó). |

## 7. Paquete de entrega

```
AlexanderVillena_Lab11.zip
├── Lab11-Ranking.md              (este documento)
├── Tienda.sln
├── Tienda/                       (Ranking.cs, Funciones.cs, Progression.cs, Datos.cs, Combate.cs,
│                                  Batalla.cs, TablaCombate.cs, trofeos_dvsc.lb,
│                                  S11-politica-ranking.ac, S11-quitar-politica.json,
│                                  S11-temporada.rc, S08-rivales-npc.rc, S10-progresion.rc, *.js)
├── Tienda.Pruebas/               (PruebasCombate.cs, PruebasTrofeos.cs, csproj)
└── Scripts_Unity/                (RankingCliente.cs, DemoLab11.cs, UgsDefensa.cs,
                                   ProgresionCliente.cs, ReproductorCombate.cs, DemoLab08.cs,
                                   Estado.cs)  ← copia de Assets/Scripts/... para revisión
```

## 8. Conclusiones

1. **La tabla no es un contador del cliente.** Sumar o restar trofeos en Unity habría sido una línea
   de código y una puerta abierta: el cliente puede decir 9999. La regla vive en
   `ReglasProgresion.CambioDeTrofeos`, se ejecuta en el servidor y se prueba con 10.000 combates.
2. **Publicar el total (y no la diferencia) es lo que hace el sistema tolerante a fallos.** Con
   `keepLatest`, un envío perdido no deja la tabla desfasada para siempre: el siguiente combate
   empuja el valor correcto. Es la diferencia entre «un error» y «un error que se arregla solo».
3. **El orden guardar → publicar no es casual.** El combate es la verdad (oro, progreso, semilla);
   la tabla es una vista. Si la tabla cae, el jugador no pierde nada y el puesto se informa como 0.
4. **`keepLatest` + total** también impide el clásico «doble envío = doble premio»: reenviar el
   mismo total no infla nada.
5. **La política `.ac` cierra la puerta sin tocar el gameplay, pero hay que saber escribirla.**
   Una sola declaración (`Principal: Player`, `Action: Write`, `Resource: urn:ugs:leaderboards:/**`)
   bloquea la escritura del cliente y deja intacto el camino del servidor. En el proceso quedaron dos
   lecciones caras: el URN de una línea (`/*`) y la ruta literal del SDK **no** bastaron para que el
   `Deny` mordiera, y la prueba válida no es la consola del cliente sino el `updatedTime` de la entrada
   en el servidor. Un `Deny` mal escrito no falla: **parece** que protege y no protege nada.
6. **El nombre se pone una vez.** Es un detalle que parece cosmético y rompe la tabla entera: cada
   `UpdatePlayerNameAsync` crea una identidad nueva con otro sufijo y el ranking se llena de
   «Ana#60257», «Ana#60714»… La solución es una condición de tres líneas.
7. **La temporada se calcula donde está el reloj bueno.** El cierre, el puesto, el tier y el premio
   salen del servidor (versiones archivadas + Remote Config); el cliente pinta. Un cliente con el
   reloj adelantado no puede «ganar» la temporada.

## 9. Limitaciones y trabajo futuro

- **Migración de proyecto**: el juego se movió de `verdaderos avances de open code` a `Quizzerttack`
  (`5906c36b-…`) y allí el módulo `Tienda` se desplegó **de cero** (`Created`): los datos de los
  labs 8/9/10 del proyecto viejo no viajan, se regeneran jugando (`PublicarInventario`, `ResolverCombate`).
- **Remote Config sin publicar**: `S11-temporada.rc` falla con `GetAllConfigsFromEnvironmentAsync failed`
  (problema del entorno, no del archivo). `Ranking.LeerTemporadaAsync` cae a los valores por defecto
  del código (mismo contenido que el `.rc`), así que la vista de temporada funciona igual.
- **Premio de temporada**: `VistaTemporada` **calcula** el premio (tier + gemas) pero no lo **entrega**;
  entregarlo requiere un cierre programado (Cloud Code con trigger / job) que lea el top, pague y
  archive. Es el siguiente paso natural.
- **Un solo tipo de puntaje**: la tabla guarda trofeos; faltan tablas por facción o por mazo
  (Leaderboards admite varias tablas, el coste es configuración + lectura).
- **Sin `metadata`**: no se guarda el mazo ni el nivel junto al puntaje; el dashboard solo muestra
  el número.
- **Vecinos en los extremos**: con pocos jugadores el rango ±5 devuelve menos filas; la interfaz
  debe tolerarlo (hoy lo hace, pero no está maquetado).
- **Anti-trampa acotada**: la política impide escribir desde el cliente, pero no detecta granjas de
  cuentas que peleen «legalmente» muchas veces. Un límite de combates por día en el módulo lo
  reduciría.
