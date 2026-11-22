# S13 · Plan de telemetría (Lab13) — QUIZZERTTACK

> Eventos personalizados declarados en el **Event Manager** ANTES de escribir el código.
> Nombres: snake_case, español, sustantivo + participio. Tipos: String / Integer (Numeric).

| Evento | Cuándo ocurre en el juego | Parámetros (tipo) | Nota |
|---|---|---|---|
| `juego_iniciado` | El jugador ACEPTA el consentimiento (telemetría ON) | — | Primer punto del recorrido |
| `tutorial_completado` | El Player 1 arma su primer escuadrón (5 cartas en fase 1) | `segundos` (Integer) | "Tutorial" = primer arranque listo; mide segundos desde el arranque de la escena |
| `primer_combate_iniciado` | La primera vez que la cuenta lanza un combate | `heroe` (String), `nivel_rival` (Integer) | Una sola vez por cuenta/sesión registrada |
| `combate_iniciado` | Cada combate que se lanza (pvp script o módulo Tienda) | `heroe` (String), `nivel_rival` (Integer) | `heroe` = carta líder (la 1ª del equipo); npc ⇒ `nivel_rival=1` |
| `combate_ganado` | El servidor responde Victoria=true | `heroe` (String), `nivel_rival` (Integer), `turnos` (Integer) | Datos del response del servidor |
| `combate_perdido` | El servidor responde Victoria=false | `heroe` (String), `nivel_rival` (Integer), `turnos` (Integer) | Ídem |
| `tienda_abierta` | Se abre la tienda/oferta (origen de la visita) | `origen` (String) | Valores: `lab12`, `menu`, ... |
| `personaje_comprado` | El servidor confirma la compra | `heroe` (String), `precio` (Integer) | Precio cobrado por el servidor (no el del cliente) |
| `compra_fallida_sin_fondos` | El servidor rechaza la compra por saldo insuficiente | `heroe` (String), `faltante` (Integer) | `faltante = precio − saldo`; nunca datos personales |

## Dónde vive cada punto en el código (Telemetria.cs + llamadores)

1. `juego_iniciado` — `PanelConsentimiento.Aceptar()` (pantalla TVYA al arrancar).
2. `tutorial_completado` — `CartaSeleccion.SpawnsHeroe()` (Player 1, una vez por sesión).
3. `primer_combate_iniciado` + `combate_iniciado` — `ReproductorCombate.PelearAsync()`, `DemoLab12.PelearAsync()` y `PanelRival.Atacar()` (antes del resultado).
4. `combate_ganado` / `combate_perdido` — idem, con `heroe`, `nivel_rival`, `turnos` del response.
5. `tienda_abierta` — `DemoLab12.Correr()` (paso precio/compra).
6. `personaje_comprado` / `compra_fallida_sin_fondos` — `DemoLab12.RegistrarEventoCompra()` (Exito ⇒ comprado; Rechazo ⇒ faltante).

## Verificación (checklist de la guía)
- [ ] Consola del editor: `[DVSC] evento registrado: <nombre> (<param=valor...>)` por cada punto.
- [ ] Event Manager: la columna *Valid Events* sube con cada envío (el servicio aceptó el esquema) y *Invalid* queda en 0.
- [ ] Data Explorer: Usuarios por ExternalUserId propio (`jugador-<n>`), no por conteo de eventos.
- [ ] Embudo: `juego_iniciado → tutorial_completado → primer_combate_iniciado → personaje_comprado` (Apply → Save Funnel).
- [ ] Data Explorer: barras de `combate_ganado` vs `combate_perdido` agrupadas por el parámetro `heroe` → gráfico de columnas, Total → Save.
- [ ] **Cruce héroe × `nivel_rival` antes de proponer cambio de balance** (rúbrica: comparación de héroes con datos observados).
- [ ] Interpretación declara: muestra = 1 instalación, unidad por `ExternalUserId`, retención local, y que **Analytics no es contabilidad** (el oro real vive en Cloud Save protegido server-side; los importes de los eventos son los cobrados por el servidor, sin doble conteo local).

## Entregable
`ApellidoNombre_Lab13.zip` — proyecto instrumentado + esta tabla + capturas (consola con [DVSC], Event Manager, embudo, Data Explorer) + interpretación.
