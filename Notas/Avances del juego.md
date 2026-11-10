# Avances del juego

> Actualizado 2026-11-07 — nota maestra. Detalle en [[Sistemas]], [[Cartas-Heroes]], [[Facciones]], [[PartidaOnline]], [[Lab01-Autenticacion]], [[Lab08-Combate]], [[Lab10-Progresion]], [[Lab11-Ranking]] y [[Lab12-Evento]].

## LiveOps: evento programado (NUEVO 2026-11-07 — Guía Lab #12)

Detalle completo en [[Lab12-Evento]].
- **Un evento cambia NÚMEROS que aplica el servidor**, no estado ni validaciones: `campana_evento` (Remote Config) trae `activo`, ventana UTC, `multiplicadorDano` ×1.25, `descuentoTienda` −30 % en `heroesEspeciales` y `botinExclusivo` (se SUMA a la tabla del S10).
- **Módulo `Tienda` → 11 funciones**: se suman `VistaEvento` (vista con reloj del servidor), `ReclamarBonusBienvenida` (una vez, bandera en el `perfil` protegido) y `ComprarHeroe` (el precio lo decide el servidor: Mago 1 200 → 840 con el evento).
- **Cliente**: `EventoLiveOps` pregunta cada 15 s, dispara `EventoCambio` al encenderse/apagarse y calcula la cuenta atrás con `Time.realtimeSinceStartup` (nunca el reloj del dispositivo); `HudEventoLab12` pinta nombre, `quedan hh:mm:ss`, daño, descuento y precio del Mago. `DemoLab12` se dispara con **F5**.
- **Segmento de nuevos**: `esNuevo = perfil.nivelCuenta <= 1` lo calcula el servidor y viaja como atributo de usuario (`user.esNuevo`); override `bonus_bienvenida` con 150 ORO sin fecha de fin.
- **Pruebas:** `Tienda.Pruebas/` → **25/25** (6 nuevas: factor de daño, ventana UTC, descuento, botín que suma sin mutar la base, bonus único y defaults sin evento).
- **Estado:** falta desplegar (`S12-campana-evento.rc` + `Tienda.sln`) y capturar el video de la activación automática con el HUD.

## 0. Tabla de trofeos en el servidor (NUEVO 2026-11-02 — Guía Lab #11)

Detalle completo en [[Lab11-Ranking]].
- **Tabla `trofeos_dvsc`** (`Tienda/trofeos_dvsc.lb`): `desc`, `keepLatest`, reset mensual con `Archive: true`, tiers por rango (2 Oro / 3-9 Plata / 10+ Bronce).
- **El cliente NO escribe**: los trofeos los mueve `ResolverCombate` con `ReglasProgresion.CambioDeTrofeos` (ganar suma los del rival, perder resta la mitad con suelo 0) y el **servidor** publica el **TOTAL** con `context.ServiceToken` (`Tienda/Ranking.cs`) **después** de guardar. Si la tabla falla, el combate ya está a salvo y el siguiente envío del total la corrige (`keepLatest`).
- **Cliente (`RankingCliente`)**: solo consulta — top 10 (`GetScoresAsync`), mi puesto (`GetPlayerScoreAsync`, 404 = «sin clasificar», no es error) y vecinos (`GetPlayerRangeAsync`). Siempre muestra `Rank + 1` porque el servicio cuenta desde 0.
- **Nombre una sola vez** (`PonerNombreUnaVezAsync`): cada `UpdatePlayerNameAsync` añade otro sufijo (`Ana#60257`) y crearía jugadores distintos.
- **Anti-trampa**: `S11-politica-ranking.ac` (`DenyPlayerWriteLeaderboards`, Principal `Player`, Action `Write`, `urn:ugs:leaderboards:/**`) hace que el `AddPlayerScoreAsync(9999)` del cliente devuelva *Access has been restricted*; se revierte con `ugs access delete-project-policy-statements S11-quitar-politica.json`. Con el `urn:ugs:leaderboards:/*` de la guía el `Deny` **no muerde** (sigue entrando el puntaje del cliente).
- **Temporada server-side**: `[CloudCodeFunction("VistaTemporada")]` devuelve cierre (versiones archivadas de la tabla, con Remote Config `temporada` como respaldo), puesto, tier, segundos restantes y premio en gemas.
- **Montaje:** `DemoLab11` en el GameObject `Registros`, se dispara con **F6** (4 perfiles: bruno, carla, ana, dana). F7-F12 ya están ocupadas: F7 panel online, F8 rival, F9 registros, F10 Lab 09, F11 Lab 10, F12 Lab 08 — compartir F11 con el demo del Lab 10 hacía que ambos corrieran a la vez y el token se perdiera (`AccessTokenMissing` 51).
- **Pruebas:** `Tienda.Pruebas/` → **19/19** (6 nuevas, incluida la de **10.000 combates** sin bajar de cero).
- **Estado:** juego **migrado al proyecto UGS `Quizzerttack`** (`5906c36b-06d8-410b-91be-cf367616022a`): `ugs config set project-id` + `cloudProjectId` + `projectName`, módulo `Tienda` redesplegado allí (`Created`, con `VistaTemporada`), `trofeos_dvsc.lb` desplegado y verificado con `ugs leaderboards get` (`Desc` · `KeepLatest` · `0 0 1 * *` desde 2026-12-01 · `archive: true` · tiers `Rank` Oro 2 / Plata 9 / Bronce). Roles de la cuenta de servicio `lab09` para ese proyecto: Leaderboards Admin, Cloud Code, Cloud Save, Unity Environments, Project y Player Resource Policy Editor.
- **Verificado en escena (2026-11-02):** tres corridas de la demo (entonces en F11; ahora F6) con 4 perfiles reales → tabla con **5 entradas y 30 trofeos por jugador** (`Alexander 9999` es la trampa de la corrida sin política): top 10, `[MÍO]` (Rank 0 → puesto 1), `[VECINOS]` ±5, `[RESUMEN]` y `[TEMPORADA]` (`cierra 2026-12-01T05:00:00Z · faltan 28d · tier Oro · Corona de oro +20 GEMAS`). Tras la política amplia, la escritura del cliente **ya no entra** (`updatedTime` del 9999 sin cambios). `S11-temporada.rc` no publica (`GetAllConfigsFromEnvironmentAsync failed`) → el módulo usa sus valores por defecto. **Falta:** capturas en el zip y el PDF de la guía.

## 0.1 Combate autoritativo en el servidor (2026-11-02 — Guía Lab #8)

Detalle completo en [[Lab08-Combate]].
- **Módulo `Tienda`** (el mismo del Lab 9/10): `ResolverCombate(equipo, idRival)` y `RepetirCombate()`. El combate de poder anterior **no se borró**: se conserva como `ResolverCombatePvp`.
- **Servidor:** `Tienda/Batalla.cs` (`Batalla.Similar`: tope 100 turnos, orden por `Velocidad`, daño `Math.Max(1, atk + azar.Next(-2,3) - def)`), `TablaCombate.cs` (valores por defecto) y `S08-rivales-npc.rc` (`tabla_heroes` + `rivales_npc`).
- **Tres lecturas, dos tokens:** el inventario en Cloud Save **protegido** (token de servicio) dice **QUÉ** tengo; `tabla_heroes` + `rivales_npc` en **una sola** petición de Remote Config (token del jugador) dicen **CUÁNTO** vale.
- **Una sola escritura:** `saldos` + `progreso` + `ultimo_combate` en el mismo `SetProtectedItemBatchAsync`. La semilla nace en el servidor (`Random.Shared.Next`), el cliente nunca la elige.
- **Cliente:** `ReproductorCombate.PelearAsync/RepetirAsync` reproduce el `BattleLog` **una acción cada 0,6 s** (barras de vida desde los equipos que entraron, sin recalcular). `DemoLab08` (F12) corre pelea + repetición + 6 intentos de trampa.
- **Montaje:** `ReproductorCombate` + `DemoLab08` en el GameObject `Registros` de SampleScene. Se dispara con F12.
- **Pruebas:** `Tienda.Pruebas/` → **7/7** (6 de simulación pura, sin nube).
- **Verificado en escena (2026-11-02):** semilla `2018759276`, 8 turnos, 32 acciones, `premio=ORO+150 saldo=550`; repetición con `log idéntico = True`; INTENTO 1 (`dano=9999` + `victoria=true` + `semilla=123`) fue **ignorado** → respondió su propia semilla y `Saldo=700`; intentos 2-6 rechazados con el mensaje literal (`Saldo=sin cambios` / `Saldo=700`, sin mover un oro).
- **Las 3 capacidades del lab: ✔ verificadas** (combate por turnos en función serverless, simulación paso a paso con premio y progreso, parseo del JSON de batalla en el cliente para reproducirlo).
- **Pendiente del lab:** documento Guía #8 en PDF con las capturas (entrega 2026-11-02 23:59). Publicar `S08-rivales-npc.rc` falla con `GetAllConfigsFromEnvironmentAsync failed` (problema ambiental de Remote Config) → el combate no depende de eso: `TablaCombate.PorDefecto()` replica la misma tabla.

## 0.2 Online (2026-10-19 — Guía Lab #9, multijugador asíncrono)
- **Carpeta nueva:** `Assets/Scripts/Online/` (`RegistroDefensivo`, `ClienteNube`, `SimuladorBatalla`, `PanelRegistros`). Los 10 de combate se movieron a `Assets/Scripts/Combate/` (antes quedaban en la carpeta renombrada `Assets/ScriptsVillena/`).
- **Firebase RTDB** `https://quizzerttack-default-rtdb.firebaseio.com` (us-central1, modo prueba 30 días, reglas: lectura pública + escritura solo en `registros/$id` con validate).
- **Montaje:** GameObject vacío `Registros` en SampleScene + `PanelRegistros` (miNombre) + `ClienteNube` (urlBase). Se abre con F9.
- **Input System:** proyecto en Input System package → F9 via `Keyboard.current.f9Key` (#if ENABLE_INPUT_SYSTEM).
- **Probado en escena:** Publicar escuadrón ("Publicado como seijer, 5 peleadores" → nodo en Firebase Datos), Actualizar (fila en panel), Simular (8 rondas, "Tu escuadrón GANA, vivos 4/5" — Vampiro Mordida, Lanza devolución/perforación correctos), Combatir (escuadrón cargado como P2, ReiniciarContraRegistro, Fase 1 con P1array=5 P2array=5).
- **Las 3 capacidades del lab: ✔ verificadas.**
- **Pendientes del lab:** demo cruzado con compañero (su registro en la base), capturas finales, documento Guía #9 (portada, desarrollo con capturas, arquitectura, limitaciones, conclusiones).

## 0.3 Partida online por turnos (NUEVO 2026-11-05)

Detalle completo en [[PartidaOnline]].
- **Qué es:** dos jugadores **conectados** juegan la misma partida del GDD turno a turno (`moneda → colocar → habilidades con quiz → pelea → ronda`). No es el combate asíncrono del Lab 08: aquí cada jugada pasa por el servidor y el rival se entera al momento.
- **Módulo `QuizzerMatch`** (`PartidaOnline/`): 5 funciones — `CreateMatch`, `JoinMatch`, `GetMatchState`, `PlayMove` (10 parámetros; las fases son el `tipo`, no funciones distintas) y `GetProtocolVersion` (handshake, **sin** `IExecutionContext`). El estado es una **lista de jugadas** que se reproduce con `Motor.Reproducir`; idempotencia por `requestId` antes del turno; write lock en Cloud Save; el plazo de 45 s se anota como jugada `plazo`; 3 plazos seguidos = abandono.
- **Cliente** `Assets/Scripts/Online/Partida/` (todo bajo `#if UGS_ON` salvo el DTO): `PartidaDto` (espejo del contrato), `PartidaOnlineService` (llamadas + custodia del `requestId` + handshake + suscripción al push) y `PanelPartidaOnline` (panel **F7**, con sondeo de respaldo cada ~10 s).
- **Push:** `{type: "match_updated", matchId}` sólo al rival con `SendPlayerMessageAsync`; el cliente no decide nada con él, al recibirlo llama a `GetMatchState`. Log a buscar: `[Partida][push] match_updated {...}`.
- **Códigos de partida:** 6 letras derivadas del `requestId` de `CreateMatch` (reintentar no crea otra partida). Se entra con código, no hay matchmaker.
- **Verificado (2026-11-05):** el cliente **compila en Unity** con `UGS_ON` sin errores ni avisos (compilación *batchmode*, y `Assembly-CSharp.dll` contiene `PanelPartidaOnline`, que sólo existe dentro del `#if`), el módulo **está desplegado** (`HasError: false` y las 5 funciones con sus parámetros), y `dotnet test PartidaOnline/TestProject` da **61/61**. Hizo falta añadir `PartidaOnline/Project/Properties/PublishProfiles/main.pubxml`: sin él el CLI respondía *Could not find a Publish Profile* y no subía nada.
- **Falta:** el **push con dos Editores** (actividad 8: es lo único que un solo Editor no puede probar), las actividades 5–7 y 10–13 de `docs/PRUEBAS.md` (handshake, partida jugada, rechazos, reconexión, plazos, información oculta, trampas), y sustituir el panel IMGUI por la escena real (`GestorTurnos`, `CartaSeleccion` y `Peleador` pintando la `VistaPartidaDto`).
- **Cuidado con el replay:** las reglas son parte del contrato. Si se cambian las reglas o el catálogo hay que **subir `GameProtocol.Version` en las dos mitades** (`Core/GameProtocol.cs` y `PartidaDto.cs`); si no, una partida guardada daría otro resultado.
- **Efectos:** 58 de las 80 habilidades están soportadas; las otras 22 se rechazan con un mensaje claro («aún no está implementada en el servidor»). Los efectos *secundarios* no soportados se ignoran sin avisar.

## 0.4 Autenticación y arranque de servicios (NUEVO 2026-11-07 — Guía Lab #1)

Detalle completo en [[Lab01-Autenticacion]].
- **Escena aparte**: `Assets/Scenes/Lab01-Autenticacion.unity` (cámara + `EventSystem` + GameObject `AuthLab01`). No toca `campo de batalla` ni el menú; está al final de Build Settings.
- **Scripts nuevos**: `Assets/Scripts/Online/Auth/ServiciosBootstrap.cs` (arranque, handshake, las tres autenticaciones) y `PanelAuthLab01.cs` (menú de acceso tipo juego armado por código: *Jugar como invitado* / *Crear cuenta* / *Ya tengo cuenta*, lobby de sesión con *Guardar progreso* para unificar al invitado, y una pantalla de herramientas con los pasos 6A/6B).
- **Versiones anotadas**: editor **6000.6.0f1**, `com.unity.services.core` **1.18.0**, `com.unity.services.authentication` **3.7.4** (estos dos sí coinciden con la guía; el editor no). Ahora los dos paquetes están **explícitos** en `Packages/manifest.json` (antes solo llegaban como dependencia transitiva).
- **Lo que enseña el paso 6**: `InitializeAsync` **no valida** el Project ID, siempre devuelve `Initialized`; el fallo (404 `RESOURCE_NOT_FOUND`, o el fallo de transporte sin internet) aparece en `SignInAnonymouslyAsync` y con el **mensaje del `catch` vacío** — el detalle está en las líneas `[Authentication]` de la consola.
- **Unificar usuarios** = `AddUsernamePasswordAsync` (no existe `LinkWithUsernamePassword` en 3.7.4): el invitado recibe usuario/clave **sin cambiar de Player ID**. Registrarse desde *Crear cuenta*, en cambio, crea **otro** jugador.
- **Estado**: código compilado contra los ensamblados reales (0 errores, 0 avisos). **Falta** la evidencia de ejecución: capturas del panel, ficha del dashboard (paso 7), texto de consola de 6A/6B, hoja del diagrama y el PDF/zip del lab.

## 1. Proyecto
- **Quizzerttack** — Unity 2D URP, card-battler estilo PvZ Heroes, 2 jugadores (local y, desde 2026-11-05, también online por turnos con Cloud Code).
- **Escena:** `Assets/Scenes/SampleScene.unity` — 10 cartas (5 P1 + 5 P2), 6 puntos (`puntosLuchadores`, 3 por lado, `indiceCasilla` 0-2), Canvas, Galería, Gestor, Moneda, Quiz, Timer.
- **Scripts:** combate en `Assets/Scripts/Combate/` (10: `MonedaInicial`, `GestorTurnos`, `Temporizador`, `MazoFaccion`, `DatosFacciones`, `CartaSeleccion`, `CartaHover`, `Peleador`, `QuizHabilidades`, `GaleriaArte`) + online en `Assets/Scripts/Online/` (Firebase, Labs 08–11: `UgsRival`, `UgsDefensa`, `ProgresionCliente`, `RankingCliente`, `ReproductorCombate`, `SimuladorBatalla`, paneles y demos) + **partida online por turnos** en `Assets/Scripts/Online/Partida/` (3: `PartidaDto`, `PartidaOnlineService`, `PanelPartidaOnline`).
- **Prefabs:** `Assets/prefab/Triangle` x2 (placeholder héroe). **Arte:** `Assets/Dibujo y artes/` + `GaleriaArte` (20 carta + 20 campo P1 + 20 campo P2).

## 2. Loop verificado
- **Fase 0 Moneda:** gira 2.5s a 720°/s, texto quién primero/segundo, espera 1.5s, oculta y `IniciarJuego(primero)`. Cartas apagadas, timer pausado.
- **Fase 1:** coloca primero. **Fase 2:** coloca segundo. Pasar o timer 30s avanza. Fase 2→3 apaga cartas.
- **Fase 3/4:** habilidades (hover → clic fija → quiz). Solo PREPARA (`indice + mult *1.10 / *0.90`), no daña.
- **Fase 5:** PELEA AUTOMÁTICA orden P1→P2 por carril. Solo `HabilidadLista && PuedePelear`. Taunt > mismo carril > primero vivo. Invisible no elegible. Timer pausado. Pasar = verificar.
- **Verificación:** pierde quien queda sin cartas sin usar Y sin peleadores vivos. Si ambos siguen → `NuevaRonda` (tick dots/Hot + baja duraciones, restaura ataque/color/habilidad, vida se mantiene). Empate si ninguno sigue.
- **Timer:** 30s, reinicia cada fase, `TiempoAgotado → PasarTurno`. Sorting: quiz 200 > panel hab/carta 100 > barra vida 50.

## 3. Cartas (resumen [[Cartas-Heroes]])
- Mazo `F1-F4/Random` en `Awake`, 5 cartas por jugador con nombre+stats+dueño+facción.
- Tinte: F1 rojo, F2 azul, F3 verde, F4 amarillo. Sprite auto vía `GaleriaArte.Buscar` (en mano igual para ambos; en campo según jugador).
- Un uso → roja + bloqueada. Spawn en primer punto libre (`childCount==0` + sin Peleador en 0.4). `indiceCasilla=i`.
- Hover sube 100, panel vida/ataque/4 habs desde `DatosFacciones`. Sin fortalezas/debilidades (legacy `Fuego/Agua` no se usa).
- Clic válido solo en fase 1/2 + tu turno + tu facción (`PuedeUsarCarta`).

## 4. Facciones 4×5 (resumen [[Facciones]])
- **F1:** Bufeador 90/15, Asesino 80/32, Lanza 150/16 (pasiva Devolver), Enano 125/24, Vampiro 110/28 (pasiva Sangre).
- **F2:** Mago 90/20, Cazador 85/30, Hacha 115/30, Lancero 100/24, Nigromante 90/22.
- **F3:** Alquimista 90/18, Pistolero 85/30, Escudero 150/14, DosHachas 100/28 (`DanoDos`), Lobo 110/28 (Proteger).
- **F4:** Cura 95/16 (Purificar), Arquero 85/28, Muñeco 100/20 (Reflejo 50%), Espadachín 105/26, Draco 120/30 (Doble 20+20).
- Motor: esquiva Velocidad máx 40% (Puntería ignora, Lentitud quita), Ceguera/Confusión %fallo, Defensa clamp -50/75, dots tick en ronda, Impedir 1 bloqueo, Proteger redirige, MarcaPólvora+fuego x1.5, Mordida roba 25%.

## 5. Quiz / Galería
- `QuizHabilidades`: 40 preguntas en `banco` (pregunta + 4 resp + índice), random, panel 400x350.
- `GaleriaArte`: objeto "Galeria" con 20 carta + 20 campo P1 (izquierda) + 20 campo P2 (derecha). `Buscar(nombre, enCampo, jugador)` con fallback: campo P2 → campo P1 → carta → `Resources/Arte/`.

## 6. Estado actual (verificado código y escena)
- [x] Combate: hover, moneda, 5 fases, quiz, timer, pelea carril, verificación/nueva ronda
- [x] Panel cierra al responder, P2 seleccionable, punto libre, dueño auto, facción fija
- [x] 20x4 habilidades + motor, stats, esquiva, barra vida, tinte, galería, 40 quiz
- [x] Online: publicar/listar/simular/combatir vs Firebase (2026-10-19)
- [x] Autoritativo: `ResolverCombate`/`RepetirCombate` en Cloud Code + replay del battle log en el cliente (2026-11-02)
- [x] Ranking: tabla `trofeos_dvsc` publicada por el servidor + top/puesto/vecinos en el cliente + política `.ac` (2026-11-02)
- [x] Partida online por turnos: módulo `QuizzerMatch` **desplegado** (5 funciones) + cliente compilando en Unity con `UGS_ON` + panel F7 con sondeo de respaldo (2026-11-05, ver [[PartidaOnline]])
- [x] LiveOps del Lab 12: evento `campana_evento` leído desde Remote Config cada 15 s (aparece y desaparece sin reiniciar), bonus de bienvenida por segmento, descuento de tienda cobrado por el servidor y botín exclusivo (2026-11-09, ver [[Lab12-Evento]])
- [ ] Push de la partida online con **dos Editores** y actividades 5–13 de `docs/PRUEBAS.md`
- [ ] Fase 5 con animación (ahora instantánea)
- [ ] Sonido seleccionar / quiz correcta
- [ ] Pantalla fin con botón reinicio (ahora solo texto)
- [ ] Nombres oficiales F1-F4

## 7. Historial
- Probé hover de carta
- Moneda inicial, 5 fases, quiz de habilidades, timer por fase, pelea por carril + verificación y nueva ronda
- Fixes: panel se cierra al responder, P2 seleccionable, cartas no se enciman (punto libre), dueño auto
- 20 peleadores con 4 habilidades en DatosFacciones + motor de efectos
- Stats equilibradas, esquiva por velocidad/agilidad, barra de vida encima
- MazoFaccion F1-F4/Random, facción fija, hover sin fortalezas, 10 cartas configuradas
- 2026-10-27: arte de campo por jugador — GaleriaArte con `XxxCampo` (P1, lado izquierdo) y `XxxCampoP2` (P2, lado derecho); `CartaSeleccion` pasa `jugadorDueno` al spawnear y el lado decide la imagen (fallback P2→P1→carta)
- 2026-11-02: **Lab #8 cerrado** — combate resuelto en el servidor: `Batalla.Similar` determinista (semilla del servidor), lectura protegida del inventario + Remote Config en una sola petición, premio y estado en una sola escritura, `RepetirCombate` con `log idéntico = True`, y 6 intentos de trampa rechazados sin escribir (ver [[Lab08-Combate]])
- 2026-11-02: **Lab #11 implementado** — tabla `trofeos_dvsc` (desc + keepLatest + reset mensual con archivo + tiers), trofeos sumados/restados en el servidor con suelo 0 (`CambioDeTrofeos`, 10.000 combates en prueba), publicación del **total** con token de servicio después de guardar, cliente de solo lectura (top 10 / mi puesto / vecinos / temporada) y política `.ac` que rechaza la escritura del jugador (ver [[Lab11-Ranking]])
- 2026-11-05: **PartidaOnline ya compila en Unity y está desplegado en UGS** — cliente con `UGS_ON` sin errores ni avisos (compilación batchmode del Editor), `ugs deploy PartidaOnline/QuizzerMatch.sln` OK tras añadir `Project/Properties/PublishProfiles/main.pubxml` (sin él el CLI decía *Could not find a Publish Profile*), backend con `HasError: false` y las 5 funciones (`CreateMatch`, `JoinMatch`, `GetMatchState`, `PlayMove` con **10** parámetros, `GetProtocolVersion` sin parámetros); 61/61 pruebas. Falta el push con dos Editores. También se sacaron los 379 archivos de `bin/`+`obj/` del repo y se versionaron los `.csproj`/`.sln` de `Tienda` (ver [[PartidaOnline]] y `docs/REPORTE-issue-2.md`)
- 2026-11-09: **Lab #12 terminado y verificado en runtime** — el módulo `Tienda` pasó de 8 a 11 funciones (`VistaEvento`, `ReclamarBonusBienvenida`, `ComprarHeroe`); en la corrida final el evento apareció solo (`>> CAMBIÓ: el evento APARECIÓ`, quedan 49:04:36), el daño se aplicó x1.25 en los 6 combates, el Mago se cobró a 840 (‑30 %) y el bono de bienvenida dio +150 ORO una sola vez (segundo reclamo `yaReclamado=True`), todo leído desde override del dashboard. Los dos bugs reales fueron de entrega de Remote Config: 400 por `attributes.unity` faltante y 401 por usar el `ServiceToken` en vez del token del jugador. También se blindó `UgsDefensa.LimpiarPerfil` para que un nombre de perfil con espacios o acentos no rompa el arranque (2026-11-09, ver [[Lab12-Evento]])
