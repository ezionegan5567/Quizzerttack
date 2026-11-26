# Manifiesto de recursos — Lab14 (Quizzerttack)

Qué hay desplegado, **quién lo crea** y **dónde se comprueba**. La regla del laboratorio:
lo que se puede versionar se despliega con el script; lo que sólo vive en el dashboard se
documenta aquí para no perderlo.

- Proyecto UGS: `5906c36b-06d8-410b-91be-cf367616022a` (el `cloudProjectId` del proyecto de Unity)
- Entornos: `production` (`79b555af-a333-4e71-8583-f72459784a0e`) y `development` (se crea una vez)
- Script: `recursos/scripts/desplegar.ps1` (lanzador: `desplegar.cmd`)
- Verificación: `recursos/scripts/verificar.ps1` (lanzador: `verificar.cmd`)

---

## 1. Lo que despliega el script (configuración como código)

| Recurso | Archivo | Servicio CLI | Contenido |
|---|---|---|---|
| Reglas de combate y tienda | `Tienda/S08-rivales-npc.rc` | `remote-config` | `tabla_heroes` (20 héroes) y `rivales_npc` (3 rivales de práctica/jefe) |
| Progresión | `Tienda/S10-progresion.rc` | `remote-config` | XP, niveles, botín y nodos del tallo |
| Temporada | `Tienda/S11-temporada.rc` | `remote-config` | tabla `trofeos_dvsc`, cierre y premios por tier |
| Evento y bienvenida | `Tienda/S12-campana-evento.rc` | `remote-config` | `campana_evento` (apagado por defecto) y `bonus_bienvenida` |
| Clasificación | `Tienda/trofeos_dvsc.lb` | `leaderboards` | tabla de trofeos (orden descendente) |
| Política de acceso | `Tienda/S11-politica-ranking.ac` | `access` | `DenyPlayerWriteLeaderboards`: el jugador no escribe la tabla; sólo el módulo |
| Módulo de Cloud Code | `Tienda/Tienda.sln` | `cloud-code-modules` | 11 funciones: tienda, combate, progresión, temporada y las dos del Lab14 |

Comando base (lo ejecuta el script, fase por fase):

```
ugs deploy <archivos .rc> -s remote-config      -p <proyecto> -e <entorno>
ugs deploy trofeos_dvsc.lb -s leaderboards      -p <proyecto> -e <entorno>
ugs deploy S11-politica-ranking.ac -s access    -p <proyecto> -e <entorno>
ugs deploy Tienda/Tienda.sln -s cloud-code-modules -p <proyecto> -e <entorno>
```

> El orden importa: la configuración sube **antes** que el módulo. Si el módulo nuevo
> llegara primero, durante unos segundos leería llaves que todavía no existen.
>
> Son **6 llaves de Remote Config en 4 archivos** (`tabla_heroes`, `rivales_npc`,
> `progresion`, `temporada`, `campana_evento`, `bonus_bienvenida`) más la tabla y la
> política: es el equivalente al "seis .rc" del kit del laboratorio.

---

## 2. Lo que queda MANUAL (vive en el dashboard, no en el repositorio)

| Recurso | Dónde | Por qué es manual | Cuándo se hizo |
|---|---|---|---|
| Entorno `development` | Settings → Environments → Add environment | Se crea una sola vez por proyecto (la service account necesitaría `Unity Environments Admin`) | Pendiente (lo crea el dueño) |
| Service account y sus roles | Project Settings → Service Accounts | Las credenciales **no** se guardan en archivos: sólo en variables de entorno | Ya existe; faltan roles |
| Game Override del evento S12 | Remote Config → Overrides | El `.ugo` del segmento `nuevo` y la ventana del evento se activan a mano; apagarlo es intencional | Lab12 |
| Esquema de eventos de Analytics S13 | Analytics → Event Manager | El esquema no se despliega por CLI; se declara evento por evento | Lab13 |
| Índices públicos de Cloud Save | Cloud Save → Indexes | `nivel_1` y `trofeos_-1` se crean con `ugs cloud-save data index player create --fields '<json>' --visibility public` | Lab09 (recreados) |
| Datos de jugadores | Cloud Save (por jugador) | **Nunca** se despliegan: se crean al jugar | Continuo |

### Roles que necesita la cuenta que despliega

| Servicio | Rol de proyecto |
|---|---|
| Remote Config | `Remote Config Admin` |
| Cloud Code | `Cloud Code Editor` |
| Leaderboards | `Leaderboards Admin` |
| Access (políticas) | `Project Resource Policy Editor` |
| Entornos | `Unity Environments Admin` |

Si falta `Remote Config Admin`, `ugs deploy` devuelve **403** en la fase 3 y el script
aborta **antes** de tocar el módulo (probado: es el fallo que se ve hoy).

---

## 3. Funciones del módulo (contrato con el cliente)

| Función | Qué hace | Lee | Escribe |
|---|---|---|---|
| `BuscarRival` | Busca un rival publicado por franja de nivel | índice público | — |
| `PublicarInventario` | Publica la defensa (nivel + equipo) | estado protegido | índice público |
| `ResolverCombate` | Combate PvE contra un NPC de la tabla | estado, RC | lote protegido + ranking |
| `RepetirCombate` | Repite el último combate con la misma semilla | estado | — |
| `ResolverCombatePvp` | Combate contra la defensa de otro jugador | estado, índice | lote + ranking |
| `ResolverCombateProgresion` | Combate + XP + botín + niveles | estado, RC | lote + ranking |
| `DesbloquearNodo` | Compra un nodo del tallo | estado | lote protegido |
| `VistaEvento` | Estado del evento (hora del servidor) | config | — |
| `ReclamarBonusBienvenida` | Entrega el bonus **una sola vez** | estado, config | lote protegido |
| `ComprarHeroe` | Cobra y añade el héroe (precio del servidor) | estado, config | lote protegido |
| `VistaTemporada` | Cierre, puesto, tier y premio | estado, config | — |
| `HoraDelServidor` | **Lab14**: verificación del despliegue (entorno + marca del kit) | — | — |
| `ForzarErrorPrueba` | **Lab14**: error deliberado para leer la traza en los logs | — | — |

`ComprarHeroe`, `ResolverCombate` y `BuscarRival` escriben una línea de métricas por
operación: id opaco enviado por el cliente, resultado, duración, total de lecturas,
lecturas separadas de Cloud Save y Remote Config, y escrituras etiquetadas por el módulo.
`VistaTemporada` aún no tiene esa instrumentación; sus lecturas de Leaderboards se deben
contrastar con los logs del servicio.

---

## 4. Las cinco fases del script

| Fase | Qué ejecuta | Qué garantiza | Tiempo medido (2026-11-23) |
|---|---|---|---|
| 1. Pruebas | `dotnet test Tienda.Pruebas` | 72 pruebas puras; si falla **no se despliega nada** | 4,7 s |
| 2. Respaldo | `ugs cc modules export` | Un `.ccmzip` fechado antes de sobrescribir | 4,8 s |
| 3. Configuración | `ugs deploy` de `.rc` + `.lb` + `.ac` | La configuración sube **antes** que el módulo | bloqueada: la cuenta carece de `Remote Config Admin` (log 2026-11-23) |
| 4. Módulo | `dotnet build` + `ugs deploy Tienda.sln` | Publica la versión nueva | no ejecutada: la guarda detuvo el flujo en fase 3 |
| 5. Verificación | espera 45 s + jugador anónimo nuevo | Responde el entorno correcto y la marca del kit | no ejecutada |

Estado local al 2026-11-23: 72/72 pruebas pasaron en el ensayo registrado; el ensayo de
fallo inducido detuvo el flujo con 71/72 antes del respaldo/despliegue. Existe un respaldo
local de `production` (`recursos/respaldos/production-20261123-201725.ccmzip`, 1,28 MB).
Los intentos se hicieron en `-Simular`; no hay publicación confirmada. No se halló
evidencia de que el entorno `development` exista. Ver `recursos/evidencias/` para los logs.

Guardas del script:

- `-Ambiente` sólo acepta `development` o `production` (`ValidateSet`).
- `production` exige `-Confirmar`; si falta, termina sin desplegar.
- `-Simular` usa `--dry-run`: enseña qué cambiaría sin crear jugadores ni verificar la economía.
- Credenciales **sólo** por `UGS_SERVICE_ACCOUNT_KEY_ID` / `UGS_SERVICE_ACCOUNT_SECRET_KEY`.
- El proyecto se lee de `ProjectSettings.asset` (o `-Proyecto`), no está escrito en el script.

---

## 5. Lo que el script NO hace

- No crea ni borra entornos.
- No toca Game Overrides ni el esquema de Analytics.
- No borra datos de jugadores (`--reconcile` no se usa).
- No revierte configuración: para eso está git (ver `REVERSION.md`).
- No sube respaldos al repositorio: los `.ccmzip` son binarios de ~1,3 MB.
