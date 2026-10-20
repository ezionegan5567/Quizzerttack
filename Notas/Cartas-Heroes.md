# Cartas - Héroes

> Actualizado 2026-10-13 — verificado contra `CartaSeleccion.cs`, `CartaHover.cs`, `MazoFaccion.cs`, `DatosFacciones.cs`.

## Mazo por facción (`MazoFaccion.cs`)
- Se elige en Inspector: `F1, F2, F3, F4 o Random` por jugador (`faccionP1`, `faccionP2`).
- En `Awake()` llena las 5 cartas con nombre + stats de `DatosFacciones.GetStats()` + dueño + facción.
- Roster fijo:
  - **F1:** Bufeador, Asesino, Lanza con escudo, Enano, Vampiro
  - **F2:** Mago, Cazador, Guerrero del hacha, Lancero, Nigromante
  - **F3:** Alquimista, Pistolero, Escudero, Guerrero dos hachas, Hombre lobo
  - **F4:** Curandero, Arquero, Muñeco poseído, Espadachín, Draconiano
- `GestorTurnos.faccionP1 / faccionP2` valida facción (`PuedeUsarCarta`). Si la carta no es de tu facción no spawnea.

## Carta (`CartaSeleccion.cs`)
- Campos: `nombreHeroe, vida, ataque, jugadorDueno (1/2), faccion (F1-F4)`.
- Tinte auto por facción (`TinteFaccion`): F1 rojo, F2 azul, F3 verde, F4 amarillo.
- Sprite auto: `imagenPeleador` → `GaleriaArte.Buscar(nombre)` → sprite del `prefabHeroe`.
- Un solo uso → roja (`colorOscurecida`) + `usada=true` + bloqueada. `ReiniciarCarta()` la restaura (lo usa Mazo al repartir).
- Clic (`OnPointerDown`) solo si: no usada + `Button.interactable` + fase 1/2 + es tu turno + es tu facción.
- Spawn: primer punto libre de `puntosLuchadores` (libre = `childCount==0` y sin `Peleador` en radio 0.4). Si no hay punto libre: "No hay mas posiciones disponibles".
  - Instancia `prefabHeroe` como hijo del punto, `heroe.name = nombreHeroe`.
  - Pasa stats al [[Sistemas|Peleador]]: dueño, nombre, vida, ataque, facción, `indiceCasilla = i` (0,1,2).
  - Arte en campo: `GaleriaArte.Buscar(nombre, enCampo:true)`, escala por `tamanoPeleador`.

## Hover (`CartaHover.cs`)
- Hover sube 100 (`distanciaSubir`), `velocidad 10`, trae al frente (`SetAsLastSibling`).
- Panel `sortingOrder 100`. Bloqueado si carta usada o `Button` apagado.
- Muestra: `Vida / Ataque / Habs` sincronizadas desde `DatosFacciones.Get(nombreHeroe)` (4 nombres). Sin fortalezas/debilidades.
- **Legacy:** `Peleador.fortalezas/debilidades` ("Fuego/Agua") ya no se usan. No mostrar en UI.

## Quiz y combate
- Quiz correcta: habilidad vale `*1.10` este turno. Fallo: `*0.90` (`Peleador.porcentajeAcierto/Fallo`).
- Fase 3/4 solo PREPARA (guarda `indiceHabilidad + mult`). Fase 5 ejecuta en orden P1→P2 por carril.
- Ver [[Sistemas]] para fases y [[Facciones]] para los 20 peleadores.
