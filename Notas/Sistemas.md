# Sistemas del juego

> Actualizado 2026-10-27 — verificado contra `Assets/Scripts/`.

## Organización de scripts (carpetas separadas)
- `Assets/Scripts/Combate/`: los 10 del juego de cartas (Moneda, Gestor, Temporizador, MazoFaccion, DatosFacciones, CartaSeleccion, CartaHover, Peleador, QuizHabilidades, GaleriaArte).
- `Assets/Scripts/Online/`: capacidades de base de datos compartida (RegistroDefensivo, ClienteNube, SimuladorBatalla, PanelRegistros). Ver sección de abajo.

## Online (Scripts/Online) — base de datos compartida
- `RegistroDefensivo.cs`: registro de escuadrón defensivo (id, jugador, facción, fecha, 5 peleadores con stats). No es MonoBehaviour. `ToJson` / `ListaDesdeJson` (parsea el mapa `{id:{...}}` de Firebase cortando por balance de llaves) / `DesdeCartas` (lee tus 5 CartaSeleccion) / `Sanear`.
- `ClienteNube.cs`: habla con Firebase RTDB por REST (no SDK). `urlBase` en Inspector (PASO 4 del plan). `PUT registros/{id}.json` para publicar mi escuadrón; `GET registros.json` para descargar la lista. id = nombre saneado en minúsculas → republicar sobrescribe tu propio registro.
- `SimuladorBatalla.cs`: batalla automática REUSANDO el motor real de Peleador. Raíz temporal "BatallaSimulada" que se destruye. Peleadores con `esSimulacion=true`: sin panel/barra (`Peleador.Start` corta si esSimulacion), aislados de los peleadores reales (filtro en EnemigoEnCarril, EquipoAliado/Enemigo, HayTaunt, HayEnemigoEnCarril). `PrepararSimulacion(idx)` marca habilidad sin quiz. `MejorIndiceSimulacion` elige daño > área > dúples > cura. Hasta 40 rondas (atacan → tick dots → reinicio). Mi lado: cartas P1 de escena (o roster F1 default).
- `PanelRegistros.cs`: panel UI (sortingOrder 250), se abre con F9. Botones: Actualizar registros / Publicar mi escuadrón; por cada registro: Simular → `SimuladorBatalla.Simular` con resultado en texto; Combatir → carga el escuadrón como P2 real (llena `cartasPlayer2` + facción + `ReiniciarContraRegistro`). Montaje: GameObject vacío "Registros" + este script (ClienteNube se añade solo si falta).
- `GestorTurnos.ReiniciarContraRegistro()`: destruye peleadores, resetea cartas, fase 1.
- `CartaSeleccion.RefrescarSprite()`: re-lee sprite tras cambiar nombre (lo usa Combatir).
- Nota: probado 2026-10-19 — publicar, listar, simular y combatir funcionando con Firebase (`https://quizzerttack-default-rtdb.firebaseio.com`, us-central1, reglas solo en nodo `registros/*`). Sin URL configurada el panel avisa y no crashea. F9 usa `Keyboard.current.f9Key` si el proyecto está en Input System (legacy `Input.GetKeyDown` solo en modo Both/Old).

## Estructura gameplay (estilo PvZ Heroes)
- **Moneda inicial** (`MonedaInicial.cs`): fase 0, cartas apagadas y timer pausado. Gira `duracionGiro 2.5s` a `720°/s`, texto quién primero/segundo, espera `pausaResultado 1.5s`, luego oculta texto + moneda y llama `IniciarJuego(primero)`. Auto-crea `TextoMoneda` si falta.
- **Fase 1**: coloca el primero (cartas). Botón Pasar o timer 30s → Fase 2.
- **Fase 2**: coloca el segundo. Pasar/timer → Fase 3 (cartas apagadas con `SetCartas(...,false)`).
- **Fase 3**: habilidades del primero (hover panel, clic fija, quiz ±10%): solo PREPARA (`multGuardado + indice`), no daña.
- **Fase 4**: habilidades del segundo (igual). Pasar → Fase 5.
- **Fase 5**: PELEA AUTOMÁTICA con lo preparado. Orden: P1 por `indiceCasilla`, luego P2 (`Sort` por `jugadorDueno` + `indiceCasilla`). Solo ejecuta si `HabilidadLista() && PuedePelear()` (vivo + sin Parálisis/Incapacitar). Carril/taunt/invisible respetados (`EnemigoEnCarril`: taunt > mismo carril > primero vivo). Pasar = verificar. Timer pausado en Fase 5.
- **Verificación** (`VerificarFin`): pierde quien quede sin cartas sin usar Y sin peleadores vivos (5 cartas por jugador). Gana quien aún tenga cartas o luchadores. Empate si ninguno sigue. Si ambos siguen → nueva ronda Fase 1 (vida se mantiene, ataque/color/habilidad se restauran con `TickRonda() + ReiniciarPeleador()`, estáticos limpiados).
- **Timer** (`Temporizador.cs` 30s `tiempoInicial`): pausado en moneda y Fase 5, se reinicia cada fase (`IniciarTemporizador`), al agotarse `PasarTurno()`. Formato `tiempo: mm:ss`.

## Scripts en Assets/Scripts/Combate/
- `MonedaInicial.cs`: moneda UI dentro del Canvas + texto, se ocultan al elegir. Tiempos editables en Inspector.
- `GestorTurnos.cs`: fases 0-5, `jugadorInicial`, `IniciarJuego()`, `PasarTurno()`, `IniciarPelea()`, `VerificarFin()`, `NuevaRonda()` (tick efectos + reinicio), `TerminarJuego()` (texto turno + resultado, apaga timer y botón). Dueño auto por array + nombre (`AsignarDuenos`), conteo escena (`ContarCartasEscena`). `faccionP1/faccionP2` (default F1/F2) + `PuedeUsarCarta()`.
- `DatosFacciones.cs`: base de datos 20 peleadores x4 habilidades (`InfoHabilidad`: efecto, objetivo, valor, duración, efecto2, robo, fuego) + pasivas (`ContadorDevolver` en Lanza, `ContadorSangre` en Vampiro) + stats. `Get()`, `GetStats()`, `Clave()`, `SpriteDe()` (carga `Resources/Arte/`).
- `MazoFaccion.cs`: `F1-F4/Random` por grupo (`faccionP1/faccionP2`, `cartasP1/cartasP2`), `Resolver()` + `CargarMazo()` en `Awake`. Llena las 5 cartas (nombre, stats, dueño, facción).
- `CartaSeleccion.cs`: clic spawnea en primer punto libre (hijo del punto, `indiceCasilla=i`), roja + bloqueada, valida `Button.interactable` + fase 1/2 + turno + facción. Punto ocupado = `childCount>0` o `OverlapCircle 0.4` con Peleador. Tinte F1-F4, sprite auto vía Galería (arte de campo según `jugadorDueno`: P1 izquierda / P2 derecha).
- `CartaHover.cs`: panel `sortingOrder 100`, sube 100, trae al frente. Muestra vida/ataque + 4 Habs desde `DatosFacciones`. Sin fortalezas/debilidades; bloqueado si usada o apagada.
- `Peleador.cs`: motor de efectos + esquiva por Velocidad (máx 40%, Puntería la ignora, Lentitud la quita) + barra de vida encima (WorldSpace, sorting 50, verde/amarillo/rojo). Stats y 4 Habs desde `DatosFacciones` (también en Inspector con `OnValidate`). Panel auto sorting 100 (240x170 +34 por hab), hover/clic fija y bloquea otros (`seleccionadoActual`), gris al preparar, quiz ±10%, pelea Fase 5. `TickRonda()` aplica dots/Hot y baja duraciones.
- `QuizHabilidades.cs`: banco editable (40 preguntas en código), panel sorting 200 (400x350), pregunta random, 4 botones, callback `bool correcta`. Sin banco → auto-correcta.
- `Temporizador.cs`: 30s, `gestor` auto (`FindFirstObjectByType`), `TiempoAgotado()` → `PasarTurno()`, `Iniciar/Pausar/Reanudar`.
- `GaleriaArte.cs`: singleton `Instance` en objeto "Galeria" escena. 20 sprites carta + 20 campo P1 "izquierda" (`XxxCampo`) + 20 campo P2 "derecha" (`XxxCampoP2`). `Get(nombre, enCampo, jugador)` + `Buscar(nombre, enCampo, jugador)` con fallback: campo P2 vacío → campo P1 → carta → `Resources/Arte/`. El jugador que invoca decide el lado (1=izq, 2=der).

## Datos
- Ver [[Cartas-Heroes]] para cartas y [[Facciones]] para los 20 (stats y efectos verificados).
- Stats: tanques 150 (Lanza/Escudero), DPS frágil 80-85 (Asesino/Cazador/Pistolero/Arquero), medios 100-125, soportes 90-95.
- Quiz banco: 40 preguntas en `QuizHabilidades.banco` (editar en Inspector, campos `pregunta/respuestas[4]/indiceCorrecta`).
- Casillas: `indiceCasilla` 0-2 por lado (3 puntos por jugador en orden izq→der). Carril espejo: `indiceCasilla` igual en ambos lados pelea entre sí, si no hay → primero vivo, taunt roba todo.
- Sorting Canvas: quiz 200 > panel habilidad/carta 100 > barra vida 50.

## Pendientes
- [ ] Fase 5 con animación de pelea (ahora instantánea en `IniciarPelea`)
- [ ] Sonido al seleccionar / quiz correcta
- [ ] Pantalla fin de juego con botón reinicio (ahora solo `textoTurno/textoResultado`)
- [x] Panel habilidad se cierra al responder quiz
- [x] P2 seleccionable cuando moneda lo elige
- [x] 10 cartas con datos F1/F2 puestos en escena + MazoFaccion
- [x] Facción fija por jugador (`faccionP1/faccionP2` + `PuedeUsarCarta`)
- [x] 20 peleadores con 4 habilidades en DatosFacciones + motor de efectos
- [x] Barra de vida + esquiva por velocidad + tinte por facción + Galería arte
