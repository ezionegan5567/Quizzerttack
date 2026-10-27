# Avances del juego

> Actualizado 2026-10-27 — nota maestra. Detalle en [[Sistemas]], [[Cartas-Heroes]] y [[Facciones]].

## 0. Online (NUEVO 2026-10-19 — Guía Lab #9, multijugador asíncrono)
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
