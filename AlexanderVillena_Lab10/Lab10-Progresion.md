# Progresión post-combate (Lab10)

> Creado 2026-10-27 (sesión de avance). **Actualizado 2026-10-26: Lab 10 COMPLETADO y verificado en runtime.** Complementa [[CloudSave-CloudCode]] (Lab09) y [[Sistemas]].
> Módulo Cloud Code **C#** `Tienda` (net6.0). Proyecto UGS: `verdaderos avances de open code` → `29f63d5d-2a09-416b-815f-e496fa89a0a9`, ambiente `production`.

## Resumen de qué es el Lab10
Después de un combate resuelto en el servidor, el jugador **progresa**: gana experiencia, sube de nivel (héroes y cuenta), recibe botín con *loot* determinista, gana oro/trofeos, y puede **desbloquear nodos** de un árbol de progresión pagando con moneda. Todo debe ser **determinista** (misma semilla → mismo resultado), **idempotente** (reintentos con el mismo `idOperación` no duplican premios) y **escrito de forma atómica** (las 6 claves protegidas en un solo batch).

Importante: el Lab09 corre por **scripts JS** (`T9*.js`). El Lab10 vive en el **módulo C#** `Tienda/` (los `[CloudCodeFunction]` de `Funciones.cs`). Son dos caminos paralelos; no se pisan.

## Estado FINAL de esta sesión ✅ (Lab 10 COMPLETADO Y VERIFICADO)
- Módulo C# `Tienda` **desplegado y funcionando** (5 endpoints registrados, `hasError: false`).
- **Cliente Unity compilado a 0 errores** y PROBADO con Play + F11 → resultado exitoso.
- **Demostración en consola (2026-10-26 23:34):**
  ```
  [Lab10][Inventario] Servidor sembró el inventario protegido: inventario listo: 3 héroes
  [Lab10][Combate] Repetida=False Gano=True xp=+51 nivel=2 botin=[nada] oro=+50 trofeos=+2
  [Lab10][Combate] Repetida=True  Gano=True xp=+51 nivel=2 botin=[nada] oro=+0  trofeos=+0 — sin volver a premiar
  [Lab10][IDEMPOTENCIA] 1º Repetida=False | 2º Repetida=True (el saldo NO volvió a moverse)
  [Lab10][Nodo] TALENTO_ATAQUE_1: Exito=True saldo=0 desbloqueados=[TALENTO_ATAQUE_1] — por 100 oro
  [Lab10][Progreso] Mis 6 llaves protegidas:
     · inventario = {"heroes":[{guerrero_del_hacha,nivel1,vida115},{vampiro,...},{draconiano,...}]}
     · operaciones = {"ids":["7e8f2eef-...","d5056e95-..."]}
     · perfil = {"experiencia":2,"nivelCuenta":2}
     · progreso = {"desbloqueos":["TALENTO_ATAQUE_1"],"trofeos":4}
     · saldos = {"monedas":0}
     · ultimo_combate = {"botin":["nada"],"equipos":[...],"log":"T1: guerrero_del_hacha golpea..."}
  ```
- **Zip de entrega generado:** `AlexanderVillena_Lab10.zip` (33 KB) en la raíz del proyecto.
- **Paso 7** ✔ (6 llaves protegidas leídas y capturadas en consola con JSON real).
- **Paso 8** ✔ (idempotencia demostrada: `Repetida=True`, saldo intacto).
- **Paso 9** ✔ (desbloqueo de nodo con precio: `Exito=True`, `por 100 oro`).
- **Paso 10** ✔ (zip generado con estructura correcta).

## Archivos creados / modificados
| Archivo | Qué aporta |
|---|---|
| `Tienda/Progression.cs` | **Nuevo.** Reglas puras (sin nube): experiencia, curva de niveles, botín determinista por semilla, validación de nodos. Clases de config `ConfigProgresion`/`ConfigExperiencia`/`ConfigNiveles`/`ConfigBotin`/`PremioBotin`/`ConfigNodo`/`ResultadoNodo`. |
| `Tienda/Combate.cs` | **Nuevo.** `Simulador` determinista (`new Random(semilla)`), DTOs de estado (`EstadoJugador` + 6 sub-estados), `LlavesProgresion`, `ResultadoSimulacion`. |
| `Tienda/Funciones.cs` | **Modificado.** Nuevas `[CloudCodeFunction]`: `ResolverCombateProgresion` y `DesbloquearNodo` + helpers (lectura/escritura en batch, idempotencia, lectura Remote Config con *fallback*). **No se tocó** `ResolverCombate` (Lab09 intacto). |
| `Tienda/Datos.cs` | **Modificado.** `ResolverCombateRequest` += `Semilla`, `IdOperacion`. `ResultadoCombate` += `Xp`, `Nivel`, `Subieron`, `Botin`, `Repetida`. |
| `Tienda/S10-progresion.rc` | **Nuevo.** Archivo Remote Config con la clave `progresion` (JSON). Aun no publicado (ver *Remote Config*). |

## Reglas implementadas (`Progression.cs`) — con números verificados
- **Experiencia de combate:** `ExperienciaDeCombate(victoria, turnos) = base + PorTurno·min(turnos, TopeTurnos)`, base = `Victoria`(50) o `Derrota`(15). Ej.: victoria 11 turnos → 50 + 1·11 = **61**; derrota 12 → 15 + 12 = **27**.
- **Curva de niveles:** `CostoParaSubir(nivel) = CostoBase · nivel²` (CostoBase=100). `AplicarNiveles` sube niveles en bucle gastando XP, tope `Maximo`(10). Ej verificado: nivel1 con 90 XP + 600 ganada → **nivel 3, 190 XP restantes, 2 subidas**.
- **Botín determinista:** `TirarBotin(botin, semilla)` siembra `new Random(semilla ^ 0x5EED)`, tirada ponderada por `Peso`. Verificado con muchas semillas ≈ **40% / 40% / 20%** (espada / poción / nada).
- **Nodos:** `PorQueNoSePuede(nodo, desbloqueos, nivelCuenta, saldo)` valida en orden **prerequisitos → ya desbloqueado → nivel mínimo → precio**; devuelve `null` si se puede desbloquear.

## Simulación determinista (`Combate.cs`)
- `Simulador.Simular(propios, rivales, semilla, maxTurnos=30)`: stats escalados por nivel (`·(1 + 0.10·(nivel-1))`), bucle por turnos, objetivo vivo aleatorio **acotado por la semilla**, produce log línea a línea (`"T1: guerrero_del_hacha golpea a bufeador (-30)"`).
- **Verificado:** misma semilla → **mismo log exacto**; débiles vs fuertes → `vidaFinal=[0]` (caído). Esto garantiza que el botín y la experiencia sean reproducibles.

## Integración en el servidor (`Funciones.cs`)
- **`ResolverCombateProgresion(context, api, req)`** — flujo (paso 6 de la guía):
  1. Lee config (Remote Config con *fallback* a defaults) y las 6 claves protegidas del jugador.
  2. **Idempotencia:** si `req.IdOperacion` ya está en `operaciones.Ids` → devuelve `RespuestaRepetida` (mismo resultado, **no vuelve a dar premios**), `Repetida=true`.
  3. Arma propios (desde `inventario`) y rivales (npc o pvp según `req`), simula con `req.Semilla`.
  4. XP de héroes (saltando caídos si `!CaidosGanan`), nivel de cuenta, botín si victoria, economía (oro/trofeos).
  5. Registra `idOperación` (cola de ~20), guarda `ultimo_combate`, aplica inventario.
  6. **Escribe las 6 claves en UN solo batch** (`SetProtectedItemBatchAsync`) → atomicidad.
- **`DesbloquearNodo(context, api, idNodo)`** — valida con `PorQueNoSePuede`; si procede descuenta `CuestaMoneda` de `saldos`, añade el nodo a `progreso.Desbloqueos` y re-escribe el batch.
- **6 claves protegidas** (`LlavesProgresion`): `inventario`, `operaciones`, `perfil`, `progreso`, `saldos`, `ultimo_combate`.
- **`LeerConfiguracionAsync`**: prueba `api.RemoteConfigSettings.AssignSettingsAsync`; si falla (RC no publicado / sin red), usa `new ConfigProgresion()` (los defaults del profesor). **Por eso el Lab10 funciona aunque no publiques el `.rc`.**

## Cómo se desplegó (lo que se ejecutó en esta sesión) ✅
Comando **correcto** (usa la **solución `.sln`**, NO el `.csproj`):
```powershell
cd C:\Users\USER\Desktop\Quizzerttack
.\ugs\ugs.exe.exe deploy .\Tienda\Tienda.sln
```
Resultado: `Successfully deployed ... 'Tienda.sln'` y luego `modules get Tienda` → `dateModified: 2026-10-27`, `hasError: false`.

### Trampas que costaron descubrir (dejarlas por escrito)
1. **`ugs deploy <...>.csproj` → "No content deployed".** El comando `deploy` de módulos C# necesita la **`.sln`**, no el `.csproj`. Con la `.sln` sí dice *"Loading cloud-code-modules… Will update"*.
2. **`--no-prompt` no existe** en `deploy` → se interpreta como una **ruta** y da `Path ...\--no-prompt could not be found`. El `deploy` **no pide confirmación**, así que no hace falta bandera.
3. **`endpoints: {}` NO era normal: era el síntoma de un bug.** El cliente devolvía **`NotFound (404) ErrorCode=9002`** al llamar cualquier función del módulo C#. Causa raíz: **Cloud Code NO soporta métodos `static` marcados con `[CloudCodeFunction]`** (doc oficial: *"Cloud Code doesn't support static methods for Cloud Code functions"*). `Funciones` era `public static class` con los 5 métodos `static` → el servicio no los registró como endpoints → `endpoints: {}` → 404. **Arreglo:** quitar `static` de la clase y de los 5 `[CloudCodeFunction]` (`public class Funciones` + `public async Task<...>`); los helpers internos sí pueden quedar `static`. Tras redeployar, `get-spec` ya lista `/BuscarRival`, `/PublicarInventario`, `/ResolverCombate`, `/ResolverCombateProgresion`, `/DesbloquearNodo`. `TestA` tenía el mismo defecto (`static class Prueba`).
4. Antes de desplegar real, usar **`--dry-run`** (no toca nada) para validar el empaquetado.
5. **Llamar un MÓDULO C# ≠ llamar un script JS.** En el cliente, `CallEndpointAsync(fn, args)` enruta al endpoint de **SCRIPTS** (`RunScriptAsync`); para funciones de un **módulo** hay que usar **`CallModuleEndpointAsync<T>("Tienda", fn, args)`** (→ `RunModuleAsync`). Usar el primero daba **`NotFound (404) ErrorCode=9002`** aunque el endpoint existiera. (Por eso Lab09, que son scripts JS `T9*`, sí funcionaba con `CallEndpointAsync`.)
6. **Los args de una función de módulo se mapean POR NOMBRE DE PARÁMETRO.** La firma `PublicarInventario(ctx, api, ResolverCombateRequest req)` tiene un parámetro llamado **`req`**, así que hay que mandar `{"req": {"Equipo": [...]}}`, NO el cuerpo plano. Mandándolo plano llegaba `req` vacío → "inventario listo: 0 héroes" y un **`422 ScriptError (ErrorCode=9009)`** en `ResolverCombateProgresion` (NRE al leer `req.IdOperacion`). Ver nombres exactos con `ugs cloud-code modules get Tienda` → bloque `parameters:` (`req`, `idNodo`). `DesbloquearNodo` ya va bien porque su parámetro es `idNodo` y se manda `{"idNodo": ...}`. Además el servidor ahora tolera `req` nulo (`req = req ?? new ...`).
7. **Leer datos PROTEGIDOS desde el cliente.** `CloudSaveService...Player.LoadAsync(keys)` por defecto usa `LoadOptions()` → clase `Default` (**privada**). Pero las 6 llaves las escribió el servidor como **PROTECTED** (`SetProtectedItemAsync`), así que el cliente las veía todas como "(aún sin datos)" aunque existieran. **Arreglo:** leer con `new LoadOptions(new ProtectedReadAccessClassOptions())` (namespace `Unity.Services.CloudSave.Models.Data.Player`; `PlayerId => null` = el propio jugador).
8. **Botín y nodos vacíos sin Remote Config.** Como el `.rc` no se pudo publicar, el módulo caía al default de `ConfigProgresion`, que traía `Botin.Premios` y `Nodos` **vacíos** → `botin=[]` y `"El nodo no existe"`. **Arreglo:** los defaults de `Progression.cs` ahora **replican el `.rc`** (premios espada/pocion/nada 40-40-20; nodos TALENTO_ATAQUE_1/2, TALENTO_DEFENSA_1, MAESTRIA), así el Lab 10 funciona completo **sin depender de Remote Config**. Si algún día se publica el `.rc`, su JSON sobreescribe estos defaults (mismos valores).

## Remote Config (`S10-progresion.rc`) — por qué NO se publicó aún
`.\ugs\ugs.exe.exe deploy .\Tienda\S10-progresion.rc --dry-run` responde **"Will fail to deploy"**, **incluso con la plantilla `.rc` que genera el propio `ugs`**. Conclusión: no es un error del archivo, es del **entorno** (Remote Config probablemente sin habilitar en el proyecto, o la Service Account sin rol de Remote Config).
- **Impacto:** ninguno funcional, porque `ResolverCombateProgresion` usa *defaults* si no lee RC.
- **Para publicarlo después:** habilitar *Remote Config* en el portal UGS y/o dar a la Service Account rol *Cloud Code / Remote Config Admin*, y repetir el `deploy` del `.rc`.

## Cliente Unity (escrito ✅, compila a 0 errores)
Archivos nuevos en `Assets/Scripts/Online/` (todo bajo `#if UGS_ON`, que ya está definido):
- **`ProgresionCliente.cs`** — helper estático (mismo estilo que `UgsRival`), con:
  - `PublicarInventarioServidorAsync(ids)` → llama la función C# `PublicarInventario` (el servidor siembra la llave protegida `inventario` en formato `{heroes:[{id,vida,ataque,nivel,experiencia}]}`).
  - `ResolverCombateProgresionAsync(rival, equipo, semilla, idOperacion)` → llama `ResolverCombateProgresion`; manda `Rival/Equipo/Semilla/IdOperacion` y loguea `Repetida/xp/nivel/subieron/botin/oro/trofeos`.
  - `DesbloquearNodoAsync(idNodo)` → llama `DesbloquearNodo` (manda `{idNodo}`).
  - `MiProgresoAsync()` → lee con `CloudSaveService...Player.LoadAsync(6 claves, new LoadOptions(new ProtectedReadAccessClassOptions()))` y **imprime las 6 llaves protegidas** (evidencia del paso 7 desde el cliente). OJO: sin `ProtectedReadAccessClassOptions` leería la clase Default (privada) y no vería nada.
- **`DemoLab10.cs`** — MonoBehaviour orquestador. `Correr()` (público, para un Botón onClick) o **tecla F11**. Flujo: login anónimo → sembrar inventario → combate (GUID) → **reintento con la MISMA idOperación** (demuestra `Repetida=true` y saldo sin moverse) → desbloquear nodo → leer las 6 llaves. Incluye `DetalleExcepcion` (patrón `CloudCodeException.Reason/.ErrorCode`).
- **`Estado.cs`** (modificado) — `ResultadoCombate` += `Xp, Nivel, Subieron, Botin, Repetida`; nueva clase `ResultadoNodo`; `Llaves` += `perfil, progreso, saldos, operaciones, ultimo_combate`.

### Cómo usarlo (TU parte, sin tocar código)
1. En Unity, abre la escena de juego y selecciona el GameObject **`Registros`**.
2. **Add Component → `DemoLab10`**. (En el Inspector puedes cambiar `equipo`, `rivalNpc`, `semilla`, `nodo`; por defecto ya vienen válidos.)
3. Pulsa **Play** y luego **F11** (o engancha un Botón cuyo onClick apunte a `DemoLab10.Correr Lab10`).
4. Lee la consola: verás la batalla con `Repetida=False`, el reintento con `Repetida=True` (mismo xp/botin, saldo intacto), el resultado del nodo, y las 6 llaves protegidas impresas.

## Entrega (paso 10) — ZIP generado ✅
Archivo: **`AlexanderVillena_Lab10.zip`** (33 KB) en `C:\Users\USER\Desktop\Quizzerttack\`
```
AlexanderVillena_Lab10/
├── Tienda/                ← módulo Cloud Code (sin bin/ni obj/)
│   ├── Funciones.cs, Datos.cs, Combate.cs, Progression.cs, TablaRivales.cs
│   ├── Tienda.csproj, Tienda.sln, NuGet.config
│   ├── S10-progresion.rc
│   └── T9*.js, Sonda.js  (scripts Lab09, van juntos por contexto)
├── Tienda.sln
├── Scripts_Unity/
│   ├── Estado.cs          (DTOs compartidos)
│   ├── ProgresionCliente.cs
│   └── DemoLab10.cs
└── Lab10-Progresion.md    (esta nota)
```
**Para subir a Canvas:** adjuntar el zip + la captura de pantalla de la consola (PNG).

## Qué quedó pendiente de la GUIA (si aplica)
- **Captura del Dashboard** (Cloud Save → Datos del jugador): puede que el Dashboard no liste jugadores con datos protegidos escritos por Cloud Code. La evidencia válida es la consola de Unity (ya capturada).
- **Remote Config `.rc`**: no se pudo publicar (limitación del entorno / rol de SA). No afecta al Lab porque los defaults de código replican el `.rc`.
- **Conclusiones** (si la guía las pide como documento aparte): redactar en un PDF/DOCX aparte.

## Nota de verificación (cómo se comprobó sin nube)
Como no hay runtime net6 local (solo 8/9/10/11), las verificaciones se hicieron con **consolas temporales apuntando a `net10.0`** referenciando la librería `net6.0` del módulo (roll-forward). Las consolas auxiliares (`_verif`, `_probe`, `_v2`) se **eliminaron** tras validar. Para confirmar las firmas exactas de la API de Cloud Code (batch / Remote Config) se usó una **sonda por reflexión** sobre los DLL del paquete antes de escribir la integración — por eso la integración compiló a la primera.

## Conclusiones (6 puntos)

1. **Progresión autoritativa en el servidor (anti-cheat).** Toda la lógica de experiencia, subida de nivel, botín, oro, trofeos y desbloqueo de nodos se ejecuta dentro de `[CloudCodeFunction]` en el módulo C# desplegado. El cliente **nunca calcula premios**: solo envía la intención (pelear contra X con semilla Y) y recibe el resultado. Un atacante que manipule el cliente no puede otorgarse XP ni items porque el servidor es la única fuente de verdad que escribe las llaves protegidas.

2. **Determinismo con semilla (reproducibilidad y fairness).** `Simulador.Simular(propios, rivales, semilla)` usa `new Random(semilla)` para que la MISMA semilla produzca el MISMO battle log, mismo ganador y mismo botín (`new Random(semilla ^ 0x5EED)` para no alterar la corriente de la simulación). Esto permite: (a) depurar cualquier combate solo con la semilla, (b) mostrar el log al jugador sabiendo que es idéntico al que computó el servidor, y (c) cumplir el requisito de la guía de que "dos llamadas con la misma semilla dan el mismo resultado".

3. **Idempotencia mediante `idOperación` (GUID).** Antes de premiar, la función busca `req.IdOperacion` en la cola `operaciones.Ids` (últimas ~20). Si ya está, devuelve `RespuestaRepetida` con `Repetida=true` y **no mueve ningún saldo**. Esto protege contra reintentos de red, doble-click o timeouts del SDK: el jugador recibe sus premios exactamente una vez por batalla, sin importar cuántas veces llegue la petición al servidor. La demo F11 lo demuestra al correr dos veces con el mismo GUID y mostrar `oro=+0 trofeos=+0` en la segunda.

4. **Escritura atómica en batch (consistencia).** Las 6 llaves protegidas (`inventario`, `perfil`, `progreso`, `saldos`, `operaciones`, `ultimo_combate`) se escriben con **un solo `SetProtectedItemBatchAsync`**. Si alguna validación falla, la función lanza excepción ANTES del batch → ninguna llave queda modificada (todo-o-nada). Esto evita estados corruptos donde el jugador tenga XP pero no el log, o botín pero no el inventario actualizado.

5. **Datos protegidos: solo Cloud Code escribe, el cliente lee con opciones explícitas.** Las 6 llaves usan `SetProtectedItemAsync` (token de servicio del módulo), que es inaccesible para escritura desde el cliente. Para leerlas, el cliente DEBE usar `LoadOptions(new ProtectedReadAccessClassOptions())` — el `LoadAsync` por defecto lee la clase `Default` (privada) y no las encuentra. Esto separa responsabilidades: el servidor controla la integridad, el cliente solo consume.

6. **Remote Config con fallback (resiliencia y desopliegue).** `LeerConfiguracionAsync` intenta leer la llave `progresion` de Remote Config vía `IGameApiClient`. Si falla (RC no publicado, sin red, rol insuficiente), cae a `new ConfigProgresion()` cuyos defaults replican el `.rc`. El Lab funciona completo sin necesidad de publicar el archivo `.rc`, pero en producción bastaría publicarlo para ajustar XP, precios y botín **sin redeployar el módulo** → separa reglas de negocio del código desplegable.
