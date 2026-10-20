# Cloud Save + Cloud Code (Lab09)

> Actualizado 2026-10-20 (sesión final). Sección complementaria de [[Sistemas]].

## Estado FINAL — DEMO COMPLETO ✔
Proyecto Unity vinculado: **verdaderos avances de open code** (29f63d5d-2a09-416b-815f-e496fa89a0a9), ambiente `production`, Unity 6000.0.6f1. SDKs: cloudsave 3.5.0, cloudcode 1.1.0, authentication 3.5.1.

Captura validar del F10 (11:55):
- `[SONDA] Cloud Code respondió: {"nombre":"cli","playerId":"uT8..."}` — script JS responde con params declarados.
- `[Inventario] Protegido en el servidor (5 héroes)` en ambos perfiles.
- `[PASO 2 OK] El perfil 'otro' leyó mi defensa` (lectura pública ajena permitida).
- `[VERIFICACIÓN] Escritura ajena rechazada: HTTP 403 | cuerpo={"code":53,"detail":"you are not permitted to access this user's data"}` — **403 code 53 exacto** (el lab).
- `[BuscarRival] Hubo=True franja=4 consultas=2 lecturas=4 rival=pepe` — índice `nivel_1` encontró al rival en ±4.
- `[ResolverCombate] Gano=True contra pepe (pvp) oro=100 trofeos=10` — combate pvp resuelto en el servidor.

## Arquitectura (camino JS que SÍ funciona)
- Cliente: `UgsDefensa` (Cloud Save: login anónimo por perfil, defensa pública dos llaves `defensa`+`nivel`, lectura ajena pública, verificar 403), `UgsRival` (Cloud Code: `CallEndpointAsync<T>` con `Dictionary<string,object>`).
- Cloud Code **scripts JS** (no módulo C# — ver abajo):
  - `Sonda.js` — eco de params `{parametro: "String"}` vía `module.exports.params` (REQUERIDO: sin declarar, llega `{}`).
  - `T9Inventario.js` — `setProtectedItem(projectId, playerId, {key:"inventario", value})` con `DataApi(context)` (serviceToken); stats reales de tabla, id cliente.
  - `T9Buscar.js` — `queryPublicPlayerData` con franja ±2→paso 2→tope; `fields:[{key,value,op,asc}]`, `returnKeys:["defensa","nivel"]`, `limit:20`; excluye `context.playerId`; elige el más cercano; cuenta `Consultas`/`LecturasConsumidas`.
  - `T9Combate.js` — npc (tabla) o pvp: `getPublicItems(rival, ["defensa"])` + `getProtectedItems(rival, ["inventario"])`; validar `heroe_coloso` y héroes declarados vs inventario; poder `#héroes*100 + ataqueProm*10`; +100 oro/+10 trofeos.
- Accesos Cloud Save: defensa = **pública** (actor: cliente, `PublicWriteAccessClassOptions`); inventario = **protegida** (escribe SOLO el servidor via script).
- `context.playerId` reemplaza el playerId que mandaría el cliente (server-authoritative).

## Módulo C# "Tienda" — por qué quedó descartado
- Deploy OK con nombre correcto (mover el `.sln` DENTRO de `Tienda/` + `Properties/PublishProfiles/main.pubxml` único; sin eso el CLI tomaba el `FolderProfile.pubxml` de la plantilla de Unity y el módulo salía llamándose **"Project"** → 404 del cliente).
- Aun con el nombre correcto y compilación sin errores, `get-spec` devolvía `paths: {}` (endpoints no derivados server-side) → el cliente llegaba con 404 NotFound. Reproducido con: módulo mínimo (net9 sin APIs), Tienda completo, y zip `.ccm` R2R hecho a mano (net9 y net6). Es un fallo del pipeline de UGS, no del código.
- Decisión: **migrar la lógica completa a 3 scripts JS** — desplegados y funcionando de una.
- Si el docente requiere módulo C#: probar el botón Deploy del paquete de Editor Cloud Code (pipeline del editor) — pendiente, hoy no se usó.

## Quirks importantes aprendidos (para el docente)
1. Scripts JS: el server FILTRA los params no declarados → declarar `module.exports.params` OBLIGATORIO. Autenticación: `CallEndpointAsync` (JS) también acepta `Dictionary`.
2. Host REST correcto de Cloud Save: **`cloud-save.services.api.unity.com`** (el `-services` no existe: `NameResolutionFailure`); `UnityWebRequest` no resuelve ese dominio aunque la shell sí → usar `HttpClient` de .NET.
3. DNS de Mono cachea negativos si el dominio no estaba en caché al 1er intento (fallback explainable en capturas anteriores).
4. Auth anónima: inicializar UGS **ANTES** de `AuthenticationService.Instance`; login robusto con catch de "already sign"; `SignOut(false)+SwitchProfile`.
5. Cloud Code `CallEndpointAsync<T>` solo acepta `Dictionary<string, object>` (objetos tipados lanza CS1503).
6. índice Cloud Save: created con `ugs cloud-save data index player create --visibility public --body S609-indice-nivel.json` (`{key,asc}`); datos escritos ANTES de crear el índice NO se indexan (solo se reindexan al reescribirse).
7. El token de servicio REST directo a `api.unity.com` da 403 WAF; `api.services.unity.com` no resolevía en shell — todo por CLI/SDK.

## Tabla de consumos (reporte, del F10 de las 11:55)
| # | Operación (paso del lab) | Servicio/canal | Consumos |
|---|---|---|---|
| 1 | Publish defensa 'seijer' (2 llaves, pública) | Cloud Save REST | 1 escritura |
| 2 | Publish inventario 'seijer' (protegida via T9Inventario) | Cloud Code + Cloud Save | 1 escritura protegida |
| 3 | Publish defensa 'pepe' | Cloud Save REST | 1 escritura |
| 4 | Publish inventario 'pepe' | Cloud Code + Cloud Save | 1 escritura protegida |
| 5 | Leer defensa ajena ('otro' lee 'principal', PASO 2) | Cloud Save REST | 1 lectura pública |
| 6 | Intentar escribir ajena (espera 403) | Cloud Save REST | 0 (rechazada, HTTP 403 code 53) |
| 7 | BuscarRival (franja ±2 → sin suficiente, añade ±4) → rival=pepe | Cloud Code → índice `nivel_1` | **2 consultas, 4 lecturas de índice** (2+2 entradas) |
| 8 | ResolverCombate (defensa pública + inventario proteg. del rival) | Cloud Code + Cloud Save | 2 lecturas |
| | **Total demo** | | **4 escrituras REST + 2 protegidas (server), 2 consultas + 6 lecturas** |

> La franja termina en ±4 porque alcanzó `CandidatosSuficientes=2`; con franja máxima ±10 hubieran sido hasta 5 consultas. "Lecturas" = entradas devueltas por cada consulta del índice.

## Conclusiones
1. **Multijugador asíncrono sin servidor dedicado**: cada jugador publica su defensa en Cloud Save (pública); otro jugador la lee cuando quiere y ataca; Cloud Code resuelve el combate server-side con stats reales (anti-cheat).
2. **Seguridad por capas**: escritura-ajena rechazada (403 code 53), inventario protegido escrito sólo por el servidor, y el combate ignora la defensa si declara héroes que no están en el inventario real.
3. **Módulos C# fallaron por pipeline (endpoints no derivados en el servidor), no por código**; los scripts JS replican el flujo 1:1 y son la alternativa estable hoy. La capa `UgsRival` abstrae la diferencia (solo cambió el endpoint y el tipo de llamada).
4. **Consultas del índice son baratas y predecibles**: expandir la franja ±2→±4 costó 2 consultas y 4 entradas leídas; el límite de 20 por consulta y el "suficientes=2" evitan sobreconsultas.
5. **El índice solo indexa datos escritos después de su creación** (error común): creamos el índice primero y las escrituras del F10 ya se indexaron → `rival=pepe` encontrado en 2da franja.
