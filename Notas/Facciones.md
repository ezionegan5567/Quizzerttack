# Facciones y peleadores (4×5)

> Actualizado 2026-10-13 — verificado contra `DatosFacciones.cs` (stats + `InfoHabilidad`) y `MazoFaccion.cs` (roster). Nombres de facción aún sin definir, se usa F1-F4.

Tinte carta: F1 rojo, F2 azul, F3 verde, F4 amarillo (`CartaSeleccion.TinteFaccion`).

## Facción 1 — F1 (Bufeador, Asesino, Lanza con escudo, Enano, Vampiro)
| # | Peleador | Vida | Ataque | Habilidades (4 c/u) | Estado |
|---|---|---|---|---|---|
| 1 | Bufeador | 90 | 15 | 1 Velocidad aumentada (esquiva equipo 10%, 2T) / 2 Campo curativo (12/turno equipo, 2T) / 3 Aumento ataque (+25% equipo, 2T) / 4 Aumento defensa (+30% equipo, 2T) | listo |
| 2 | Asesino | 80 | 32 | 1 Lluvia de dagas (18 grupo) / 2 Ceguera (50% fallo, 1T) / 3 Cuchillo letal (12 + sangrado 8, 2T) / 4 Cuchillo veneno (10 + veneno 6, 4T) | listo |
| 3 | Lanza con escudo | 150 | 16 | 1 Provocar (taunt 2T) / 2 Endurecimiento (+50% def 1T) / 3 Perforación y devolución (20 + consume stacks +25% atk/golpe recibido, resetea) / 4 Escudo para todos (+25% def equipo 2T) — pasiva `ContadorDevolver` | listo |
| 4 | Enano | 125 | 24 | 1 Golpe martillo (26) / 2 Mejora armas (+20% atk equipo 3T) / 3 Insulto (+20% atk propio 2T, -25% precisión enemiga = Ceguera 25 2T) / 4 Cerveza (cura 15 equipo + esquiva 10% 2T) | listo |
| 5 | Vampiro | 110 | 28 | 1 Mordida (24, cura 25% del daño) / 2 Garra (26) / 3 Intimidar (-25% atk 2T) / 4 Ataque sangre (20 + stacks x5, resetea) — pasiva `ContadorSangre` | listo |

## Facción 2 — F2 (Mago, Cazador, Guerrero hacha, Lancero, Nigromante)
| # | Peleador | Vida | Ataque | Habilidades | Estado |
|---|---|---|---|---|---|
| 1 | Mago (esfera de apoyo) | 90 | 20 | 1 Curación en área (25 equipo) / 2 Campo eléctrico (14 grupo) / 3 Bola de fuego (30, fuego) / 4 Parálisis (2T, no actúa) | listo |
| 2 | Cazador (rifle) | 85 | 30 | 1 Disparo (28) / 2 Ojo de halcón (+30% atk + puntería 2T) / 3 Disparos múltiples (16 grupo) / 4 Baño de pólvora (5 + marca 3T, fuego hace x1.5) | listo |
| 3 | Guerrero (hacha dos manos) | 115 | 30 | 1 Ataque (30) / 2 Grito (+20% atk equipo 2T) / 3 Aguante (cura 25 propio) / 4 Lanza hacha (24) | listo |
| 4 | Lancero | 100 | 24 | 1 Lanza (24) / 2 Impedir (1 bloqueo a aliado, 2T) / 3 Agilidad (esquiva 10% + puntería 2T) / 4 Perforación (16 + sangrado 8 2T) | listo |
| 5 | Nigromante | 90 | 22 | 1 Magia negra (26) / 2 Maldición (8/turno 3T) / 3 Invisible (3T, no elegible) / 4 Magia negra II (22) | listo |

## Facción 3 — F3 (Alquimista, Pistolero, Escudero, Guerrero dos hachas, Hombre lobo)
| # | Peleador | Vida | Ataque | Habilidades | Estado |
|---|---|---|---|---|---|
| 1 | Alquimista | 90 | 18 | 1 Recuperación (cura 30 a aliado) / 2 Veneno (8 + veneno 7 2T) / 3 Invisible (2T) / 4 Viscosidad (Lentitud: -15% atk y sin esquiva 3T) | listo |
| 2 | Pistolero | 85 | 30 | 1 Disparo (28) / 2 Ojo de águila (+20% atk + puntería 2T) / 3 Múltiple (16 grupo) / 4 Incineradora (18 + quemadura 7 2T, fuego) | listo |
| 3 | Escudero | 150 | 14 | 1 Provocar (2T) / 2 Resistencia (+40% def 2T) / 3 Escudo todos (+25% def equipo 2T) / 4 Resistir (inmune 1T) | listo |
| 4 | Guerrero (dos hachas) | 100 | 28 | 1 Hacha (26) / 2 Agilidad (esquiva 10% + puntería 2T) / 3 Lanzar hachas (16 a 2 enemigos, `DanoDos`) / 4 Motivar (cura 12 equipo) | listo |
| 5 | Hombre lobo | 110 | 28 | 1 Mordisco (24) / 2 Garras (16 + sangrado 8 3T) / 3 Aullido (+100% atk propio 2T) / 4 Proteger (recibe daño por aliado, 99T) | listo |

## Facción 4 — F4 (Curandero, Arquero, Muñeco poseído, Espadachín, Draconiano)
| # | Peleador | Vida | Ataque | Habilidades | Estado |
|---|---|---|---|---|---|
| 1 | Curandero | 95 | 16 | 1 Cura área (25 equipo) / 2 Purificar (quita dots/control/debuff atk aliados) / 3 Muro (inmunes equipo 1T) / 4 Incapacitar (no actúa 2T) | listo |
| 2 | Arquero | 85 | 28 | 1 Venenosas (14 + veneno 6 3T) / 2 Lluvia (16 grupo) / 3 Flechas (28) / 4 Respiro (cura 15 propio + 15% atk 2T) | listo |
| 3 | Muñeco poseído | 100 | 20 | 1 Intimidar (-25% atk 2T) / 2 Maldición (8/turno 3T) / 3 Señuelo (refleja 50% 3T) / 4 Confusión (50% fallo 2T) | listo |
| 4 | Espadachín | 105 | 26 | 1 Ataque (26) / 2 Lluvia espadas (15 grupo) / 3 Resistencia (+30% def 2T) / 4 Provocar (2T) | listo |
| 5 | Draconiano | 120 | 30 | 1 Llamarada (16 grupo + quemadura 6 2T, fuego) / 2 Garras (18 + sangrado 8 2T) / 3 Endurecimiento (+50% def 1T) / 4 Doble golpe (20+20) | listo |

## Reglas de motor (`Peleador.cs`)
- Esquiva = suma Velocidad (máx 40%). Puntería la ignora. Viscosidad (`Lentitud`) quita esquiva y baja atk.
- Ceguera/Confusión = % fallo (valor, default 50). Puntería lo quita.
- Defensa suma `BuffDef`, clamp -50 a 75%. Reduce daño directo.
- Dots (`Veneno/Sangrado/Quemadura/Maldición`) tick en `NuevaRonda`. `HotZona` cura en tick.
- Taunt (`Provocar`) redirige carril. Invisible no es elegible ni recibe grupo.
- `Impedir` bloquea 1 golpe. `Proteger` redirige daño a protector. `Reflejo` devuelve 50%. `MarcaPolvora` + fuego = x1.5.
- Quiz: correcta `*1.10`, fallo `*0.90` sobre valor de habilidad.
- Stats: tanques 150 (Lanza/Escudero), DPS frágil 80-85 (Asesino/Cazador/Pistolero/Arquero), medios 100-125, soportes 90-95.
