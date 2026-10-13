# Cartas - Héroes

| Héroe | Vida | Ataque | Habilidad | Fuerte vs | Débil vs | Dueño |
|---|---|---|---|---|---|---|
| Guerrero | 100 | 25 | Golpe fuerte | Fuego | Agua | P1/P2 según carta |

## Reglas
- #carta un solo uso → roja + bloqueada.
- Spawn en punto libre (`puntosLuchadores`), máximo 3 en campo.
- Al spawnear pasa stats al [[Sistemas|Peleador]].
- Quiz correcta: ataque base * 1.10 este turno. Fallo: * 0.90.

## Peleadores en campo
- Hover: panel auto encima con nombre/vida/ataque/habs.
- Clic: fija panel, bloquea otros hasta elegir habilidad.
- Tras habilidad: gris + `habilidadUsada`, pelea en Fase 5.
