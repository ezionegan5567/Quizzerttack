# Sistemas del juego

## Estructura gameplay (estilo PvZ Heroes)
- **Moneda inicial** (`MonedaInicial.cs`): fase 0, cartas apagadas y timer pausado. Gira 2.5s, texto quién primero/segundo, luego texto y moneda desaparecen y llama `IniciarJuego(primero)`.
- **Fase 1**: coloca el primero (cartas). Botón Pasar o timer 30s → Fase 2.
- **Fase 2**: coloca el segundo. Pasar/timer → Fase 3 (cartas apagadas).
- **Fase 3**: habilidades del primero (hover panel, clic fija, quiz ±10%): solo PREPARA, no daña.
- **Fase 4**: habilidades del segundo (igual). Pasar → Fase 5.
- **Fase 5**: PELEA AUTOMÁTICA con lo preparado (P1 por carril, luego P2; carril/taunt/invisible respetados). Pasar = verificar.
- **Verificación**: pierde quien quede sin cartas sin usar Y sin guerreros vivos (5 cartas por jugador). Gana quien aún tenga cartas o luchadores. Si ambos siguen → nueva ronda Fase 1 (vida se mantiene, ataque/color/habilidad se restauran).
- **Timer** (`Temporizador.cs` 30s): pausado en moneda, se reinicia cada fase, al agotarse `PasarTurno()`.

## Scripts en Assets/Scripts/
- `MonedaInicial.cs`: moneda UI dentro del Canvas + texto, se ocultan al elegir.
- `GestorTurnos.cs`: fases 0-5, `jugadorInicial`, `IniciarJuego()`, `PasarTurno()`, `IniciarPelea()`, `VerificarFin()`, `NuevaRonda()` (tick efectos + reinicio), `TerminarJuego()`. Dueño auto por array + nombre, activado por escena, conteo escena.
- `DatosFacciones.cs`: base de datos 20 peleadores x4 habilidades (efecto, objetivo, valor, duración, robo, fuego) + pasivas + stats equilibradas.
- `MazoFaccion.cs`: `F1-F4/Random` por grupo, llena las 5 cartas (nombre, stats, facción) en `Awake`.
- `CartaSeleccion.cs`: clic spawnea en primer punto libre (hijo del punto, `indiceCasilla`), roja + bloqueada, valida `Button.interactable` + fase 1/2 + turno + facción (`Faccion P1/P2` en Gestor).
- `CartaHover.cs`: sin fortalezas/debilidades; nombre `X (F1 - P1)` + stats y 4 Habs sincronizadas; hover sube 100, sorting 100, bloqueado si usada o apagada.
- `Peleador.cs`: motor de efectos + esquiva por Velocidad (máx 40%, Puntería la ignora, Viscosidad la quita) + barra de vida encima (verde/amarillo/rojo). Stats y 4 Habs desde `DatosFacciones` (también en Inspector con `OnValidate`).
- `QuizHabilidades.cs`: banco editable, panel sorting 200.
- `Temporizador.cs`: 30s, `gestor` auto, `TiempoAgotado()` → `PasarTurno()`.

## Datos
- Ver [[Cartas-Heroes]] para stats y [[Facciones]] para los 20 (stats equilibradas puestas).
- Stats: tanques 150 (Lanza/Escudero), DPS frágil 80-85 (Asesino/Cazador/Pistolero/Arquero), medios 100-125, soportes 90.
- Quiz banco: 5 preguntas ejemplo en `QuizHabilidades.banco` (cambiar en Inspector).
- Casillas: P1 = 1-2-3, P2 = 4-5-6. Cada carta con sus 3 puntos en orden izq→der.

## Pendientes
- [ ] Fase 5 con animación de pelea
- [ ] Sonido al seleccionar / quiz correcta
- [ ] Pantalla fin de juego con botón reinicio
- [x] Panel habilidad se cierra al responder quiz
- [x] P2 seleccionable cuando moneda lo elige
- [x] 10 cartas con datos F1/F2 puestos en escena + MazoFaccion
- [x] Facción fija por jugador
