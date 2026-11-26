# S14 · Tabla de costos y consumo — Quizzerttack

**Consulta de tarifas:** 2026-11-23, lista oficial de [precios de Unity Gaming Services](https://unity.com/es/products/gaming-services/pricing).
**Entorno y medición:** `production`, 2026-11-23, solicitudes REST desde Lima. Son tiempos de respuesta del cliente (incluyen red); el tablero de Cloud Code debe aportar la duración de ejecución facturable.

> Esta hoja separa el conteo del código, los tiempos medidos y los precios. No estima una factura a partir de latencias de red ni atribuye Remote Config a lecturas de Cloud Save.

## 1. Tarifas consultadas

| Servicio | Cuota gratuita mensual | Precio después de la cuota |
|---|---:|---:|
| Cloud Code — invocaciones | 1 000 000 | US$ 1,50 por millón |
| Cloud Code — cómputo | 20 h | US$ 0,00002 por segundo |
| Cloud Save — lecturas | 1 000 000 | US$ 0,009 por 10 000 |
| Cloud Save — escrituras | 1 000 000 | US$ 0,025 por 10 000 |
| Remote Config | Gratis | US$ 0 |
| Authentication | Gratis | US$ 0 |
| Leaderboards | Cuota incluida | Consultar la lista según MAU y volumen |

Las cuotas son mensuales por proyecto. Revisar la lista oficial el día de entrega: Unity puede cambiar precios o cuotas.

## 2. Conteos por operación

Los contadores se toman del código instrumentado. `lecturas` es el total y los campos por servicio permiten separar las unidades: Remote Config no se factura como Cloud Save. `BuscarRival` puede consultar varias franjas y suma una lectura de índice por consulta.

| Operación | Cloud Save (lecturas) | Remote Config (lecturas) | Cloud Save (escrituras) | Cloud Code (invocaciones) |
|---|---:|---:|---:|---:|
| `ComprarHeroe`, jugador nuevo, rechazo por saldo | 2 | 1 | 0 | 1 |
| `ComprarHeroe`, compra aceptada | 2 | 1 | 1 lote | 1 |
| `BuscarRival`, una consulta de índice | 1 por consulta | 0 | 0 | 1 |
| `ResolverCombate`, aceptado | 2 | 2 | 1 lote + 1 publicación de ranking | 1 |
| `ResolverCombate`, rechazo antes de leer estado | 0 | 0 | 0 | 1 |
| `VistaTemporada` | 2 (control y estado) + 1 entrada de Leaderboards | 1 | 0 | 1 |
| `HoraDelServidor` | 0 | 0 | 0 | 1 |

La métrica aún no cubre `VistaTemporada`; su fila describe una lectura del código. La entrada de ranking es una lectura de Leaderboards, no de Cloud Save. Las escrituras de ranking también son operaciones de Leaderboards, separadas de Cloud Save. El campo `escrituras` cuenta las escrituras que el módulo etiqueta y no representa por sí solo una factura completa de todos los servicios.

## 3. Sesión solicitada y medición

Sesión: iniciar/leer estado, comprar el Guerrero, buscar rival, tres combates y abrir la vista de temporada. Son **7 invocaciones**: 1 lectura de estado + 1 compra + 1 búsqueda + 3 combates + 1 vista. La compra puede rechazarse si el jugador no tiene saldo; en ese caso no escribe Cloud Save.

| Medida observada el 2026-11-23 | Tiempo de respuesta |
|---|---:|
| `LeerEstado` | 661 ms primera vez; 381 ms segunda |
| `ComprarHeroe` | 434 ms |
| `BuscarRival` | 342 ms |
| `ResolverCombate` | 517, 479 y 468 ms (promedio 488 ms) |
| `VistaTemporada` | 534 ms |

Los tiempos incluyen red y no son segundos facturables de Cloud Code. El script de verificación no reproduce toda la sesión: crea un jugador nuevo y mide compra, búsqueda, vista de temporada y tres intentos de combate. La lectura `LeerEstado` desde el cliente es una medición aparte.

### Proyección por sesión y umbral gratuito

Para `p` combates aceptados, hay 1 + 1 + 1 + `p` + 1 llamadas para estado, compra, rival, combates y temporada. Añadir una escritura Cloud Save si la compra se acepta; cada combate aceptado aporta las lecturas y escrituras indicadas arriba. Un rechazo temprano no debe contarse como combate aceptado.

Con 1 000 sesiones al mes, las 7 llamadas por sesión suman 7 000 invocaciones de Cloud Code, dentro de la cuota de 1 000 000. Para ese volumen, las lecturas y escrituras de Cloud Save también quedan dentro de la cuota según estos conteos. El costo de cómputo queda **pendiente de duración de ejecución facturable**; no se calcula a partir del RTT. Para un volumen mayor:

| Concepto | Fórmula mensual después de la cuota |
|---|---|
| Invocaciones | `max(0, invocaciones − 1 000 000) × US$ 1,50 / 1 000 000` |
| Cloud Save — lecturas | `max(0, lecturas − 1 000 000) × US$ 0,009 / 10 000` |
| Cloud Save — escrituras | `max(0, escrituras − 1 000 000) × US$ 0,025 / 10 000` |
| Cloud Code — cómputo | `max(0, segundos facturables − 72 000) × US$ 0,00002` |

No se incluyen Analytics, impuestos, almacenamiento, egreso ni otros servicios. Las pruebas y despliegues consumen las cuotas del mismo proyecto.

## 4. Optimizaciones propuestas (estimaciones, no resultados medidos)

| Cambio | Ahorro a medir | Riesgo y comprobación |
|---|---|---|
| Mantener configuración Remote Config en memoria del módulo y pedir el evento junto con la configuración de combate | Hasta 2 consultas de Remote Config por combate, según el flujo actual; no reduce lecturas Cloud Save | Medir `duracionMs` y validar caducidad/actualización. La caché no puede ser fuente autoritativa para compras ni saldo. |
| Leer estado y progresión del cliente en una petición al arrancar | Evita llamadas duplicadas de arranque; el ahorro exacto depende del cliente y requiere medición antes/después | Mantener Cloud Save como autoridad y probar reintentos y cambios concurrentes. |

No se afirma ahorro monetario para Remote Config porque es gratuito. El posible ahorro de tiempo o llamadas del cliente queda pendiente de medición antes/después.

## 5. Registro estructurado

Formato actual:

```text
operacion="ResolverCombate" id="61fa…" resultado="ok" duracionMs=182 lecturas=4 lecturasCloudSave=2 lecturasRemoteConfig=2 escrituras=2
```

`id` es `idOperacion` enviado por el cliente, no el jugador. No registrar tokens, correos ni nombres. Los atributos opacos de contexto del servicio sirven para soporte; no presentarlos como identificadores personales.
