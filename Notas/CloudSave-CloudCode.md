# Cloud Save + Cloud Code (Lab09)

> Actualizado 2026-10-20 (sesión 1). Sección complementaria de [[Sistemas]].

## Estado
- Cliente UGS (`#if UGS_ON`, se activa solo al compilar por `Assets/Editor/ActivarUgs.cs`):
  - `Assets/Scripts/Modelos/Estado.cs` — DTOs: `DefensaPublica`, `HeroeDefensa`, `Llaves` (Defensa/Nivel(Inventario inexistente?...)), `BuscarRival*`, `ResolverCombate*`, `ResultadoCombate`.
  - `Assets/Scripts/Online/UgsDefensa.cs` — Cloud Save: Sign-in anónimo por perfil, `PublicarDefensaAsync` (dos llaves `defensa`+`nivel` con acceso público), `LeerDefensaDeAsync`, `CambiarPerfilAsync`, `VerificarEscrituraAjenaAsync` (petición cruda 403, loguea resultado/error/cuerpo; URL corregida a `/v1/data/projects/{projectId}/players/{id}/items`).
  - `Assets/Scripts/Online/UgsRival.cs` — Cloud Code: `BuscarRivalAsync`, `ResolverCombateAsync`, `PublicarInventarioAsync` (request = Dictionary `{Equipo:[ids]}`).
  - `Assets/Scripts/Online/DemoLab09.cs` — demo F10 con batas `corriendo` (anti doble F10), flujo completo para las capturas.- Proyecto Unity vinculado: **verdaderos avances de open code** (29f63d5d-2a09-416b-815f-e496fa89a0a9), ambiente `production`, Unity 6000.0.6f1.
- SDK versión 3.x instalados: **cloudsave** (3.5.0), **cloudcode** (1.1.0), **authentication** (3.5.1).
- Notas SDK:
  - Tipos de acceso en `Unity.Services.CloudSave.Models.Data.Player` (`SaveOptions(new PublicWriteAccessClassOptions())`, `LoadOptions(new PublicReadAccessClassOptions(playerId))`); por CS0104 calificar completo.
  - Cloud Code `CallModuleEndpointAsync<T>` solo acepta `Dictionary<string, object>` como request (pasar objetos tipados lanza CS1503).
  - Authentication no tiene `State`; inicializar UGS **antes** de tocar `AuthenticationService.Instance`; login robusto con `catch` de "already sign"; perfiles: `SignOut(false)` + `SwitchProfile`.
- Índice de Cloud Save **CREADO y READY**: `ugs cloud-save data index player create --visibility public --body S609-indice-nivel.json` → `Index with ID "nivel_1" created/READY` (los arrays de campos usan `{key, asc}`; los campos data escritos **antes** de crear el índice no se indexarán hasta reescribirse).
- La **consulta** por CLI funciona: `ugs cloud-save data player query --visibility public --body S609-consulta-nivel.json` → sin 403 (pipeline sano; vacío porque lo escrito antes del índice no se indexó hasta que el F10 lo reescriba).
- Probar 403 de Cloud Save pendiente: al ser el acceso **público** la escritura ajena puede requerir token+Oak 403; el esfuerzo real de pruebas se respalda con el log completo.
- **Módulo deploy al 2026-10-20 (06:19) con el nombre correcto "Tienda"** pero problema aún abierto: `get-spec` devuelve `paths: {}` (endpoints vacíos) — ver Debug.

## Flujo del demo (F10 en la escena con el juego montado)
1. Perfil 'principal' (nivel 20, alias 'seijer'): publica defensa + **inventario** (via módulo, clave protegida `inventario`).
2. Perfil 'otro' (nivel 19, alias 'pepe'): publica su defensa+inventario, **lee la de 'principal'** (PASO 2 OK) e intenta escribirla → espera el **403**.
3. Vuelve a 'principal': `BuscarRival` (franja ±2→×2→tope 10) encuentra a 'pepe' → `ResolverCombate`: el servidor arma rival con `defensa` pública + `inventario` protegida; +100 oro/+10 trofeos si ganas, NPC si no hay rival.
- Comparamos consumos de **Consultas/Lecturas de índice** (tabla para el reporte; target: encajonado en `BuscarRivalResult`).

## Módulo Tienda (C#) — estructura + deploy
- `Tienda.sln` (raíz) + `Tienda/`:
  - `Tienda.csproj`: **TargetFramework net9.0**, `PublishReadyToRunComposite true`, paquetes **Com.**-prefix desde **nuget.org**: `Com.Unity.Services.CloudCode.Core` **0.0.5**, `Com.Unity.Services.CloudCode.Apis` **0.0.26** (antes net6.0/0.0.7/0.0.27; el zip BIEN compilaba), `Microsoft.Extensions.Logging.Abstractions` **7.0.1**. `NuGet.config` solo nuget.org.
  - `Datos.cs` (respaldo de DTOs con namespace `Tienda`), `TablaRivales.cs` (stats de los **20 héroes + npcs + `OroPvp=100`/`TrofeosPvp=10` de [[Cartas-Heroes]]**), `Funciones.cs` (patrón `ModuleConfig : ICloudCodeSetup { config.AddGameApiClient(); }`, funciones `(IExecutionContext, IGameApiClient, request)`):
    - `BuscarRival` — franja ±2→paso 2→tope, query índice `nivel_1` (`QueryIndexBody` campo `GE`/`LE`, `fields:[{key,value,op,asc}]` camelCase; devuelve llaves `defensa`+`nivel`). Excluye al propio jugador. `PublicarInventario(Request{Equipo:List<string>})` — SetProtectedItemAsync clave `inventario`.
    - `ResolverCombate` — NPC (tabla), pvp con lectura pública de defensa ajena + **lectura protegida** del rival; conteo oro/trofeos.
- **Problema resuelto del deploy (¡claves!):**
  - Si el `.sln` está en la carpeta **raíz** del proyecto con un solo csproj dentro `Tienda/`, el CLI (v2.0.0) busca el `.pubxml` desde la raíz hacia arriba y **agarra el `FolderProfile.pubxml` de la plantilla** `Library/PackageCache/com.unity.services.cloudcode@.../Template~/Project/...` → el módulo salía con nombre **"Project"** (por eso el 404 del cliente: pedía `Tienda`).
  - Esto no salta con error si hay excesos (`Too many Publish Profiles`); para evitarlo: **mover el sln DENTRO de `Tienda/`** (junto al csproj) y crear `Tienda/Properties/PublishProfiles/main.pubxml` (Release/Any CPU/linux-x64). Un solo `.pubxml` en la jerarquía del sln.
  - Tras arreglar eso: deploy OK de `Tienda.sln` (Updated), `modules list --json` → `Name: Tienda` ✅; zip verificado con `cloud-code modules get Tienda` (signedUrl en 299 s; `Invoke-WebRequest` y `Expand-Archive`).
- Comandos CLI que funcionan (ugs.exe 2.0.0 en `Quizzerttack/ugs/`):
  - `ugs deploy "Tienda\Tienda.sln" --services cloud-code-modules`
  - `ugs cloud-code modules list --json` / `modules get <name>` (signedDownloadURL) / `modules delete <name>`
  - `ugs cloud-save data index player create --visibility public --body S609-indice-nivel.json`
  - `ugs cloud-save data player query --visibility public --body S609-consulta-nivel.json`

## Debug abierto (2026-10-20)
- Estado: deploy correcto del módulo **Tienda** (compila, nombre OK), pero `get-spec`/`get` devuelven **`endpoints: {}`** → el cliente llamaría `module/Tienda/...` y **error 404 “NotFound”** ([LAB09] Falló: NotFound (404)).
- Ya descartado: compilación (DLL del zip contiene `BuscarRival`/`ResolverCombate`/`PublicarInventario`), nombre del módulo (arreglado), cds/API levels (0.0.5/0.0.26 alineados a la plantilla de `Books`).
- Sospecha actual: derivación de endpoints en el lado servidor (algún step del pipeline no está leyendo los atributos `[CloudCodeFunction]`).
- Siguiente prueba (pendiente del usuario): `ugs deploy` del módulo **de nuevo** y probar F10; si persiste: probar `PublicarInventario` por curl REST con autenticación admin; quizás necesite **`modinfo.json`** o depender de la pipeline del **Unity Editor** (botón Deploy del paquete cloudcode).
- Los `slnx` en la raíz (`Quizzerttack.slnx`, `Avancesdeproyectofinalparacloud.slnx`, `verdaderos avances de open code.slnx`) **no confundir** con el `Tienda.sln` clásico: el CLI solo acepta `.sln` al deploy; `dotnet new sln` en .NET 11 crea `.slnx` por defecto → usar `--format sln`.
