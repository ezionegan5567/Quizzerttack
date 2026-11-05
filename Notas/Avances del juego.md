# Avances del juego

> Actualizado 2026-11-02 — nota maestra. Detalle en [[Sistemas]], [[Cartas-Heroes]], [[Facciones]], [[Lab08-Combate]], [[Lab10-Progresion]] y [[Lab11-Ranking]].

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

## 1. Proyecto
- **Quizzerttack** — Unity 2D URP, card-battler estilo PvZ Heroes, 2 jugadores local.
- **Escena:** `Assets/Scenes/SampleScene.unity` — 10 cartas (5 P1 + 5 P2), 6 puntos (`puntosLuchadores`, 3 por lado, `indiceCasilla` 0-2), Canvas, Galería, Gestor, Moneda, Quiz, Timer.
- **Scripts:** combate en `Assets/Scripts/Combate/` (10: `MonedaInicial`, `GestorTurnos`, `Temporizador`, `MazoFaccion`, `DatosFacciones`, `CartaSeleccion`, `CartaHover`, `Peleador`, `QuizHabilidades`, `GaleriaArte`) + online en `Assets/Scripts/Online/` (4).
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
- 2026-11-05: **PartidaOnline ya compila en Unity y está desplegado en UGS** — cliente con `UGS_ON` sin errores ni avisos (compilación batchmode del Editor), `ugs deploy PartidaOnline/QuizzerMatch.sln` OK tras añadir `Project/Properties/PublishProfiles/main.pubxml` (sin él el CLI decía *Could not find a Publish Profile*), backend con `HasError: false` y las 5 funciones (`CreateMatch`, `JoinMatch`, `GetMatchState`, `PlayMove` con **10** parámetros, `GetProtocolVersion` sin parámetros); 61/61 pruebas. Falta el push con dos Editores. También se sacaron los 379 archivos de `bin/`+`obj/` del repo y se versionaron los `.csproj`/`.sln` de `Tienda` (ver `docs/REPORTE-issue-2.md`)
