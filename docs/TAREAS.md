# Tareas para seguir desarrollando la partida online

El módulo `PartidaOnline` es un **modelo para entender y ampliar**, no una caja negra. Estas tareas son el trabajo que sigue, ordenado de más a menos importante. Cada una dice **qué lograr, dónde mirar, cómo empezar, cómo saber que está bien y qué se aprende**. Elijan una, léanla con calma y, si algo no se entiende, pregunten antes de copiar.

Antes de cualquiera: ejecuten `dotnet test PartidaOnline/TestProject` y lean `PartidaOnline/README.md`. Además, hagan primero las actividades de `docs/PRUEBAS.md`: lo que descubran ahí puede cambiar el orden de estas tareas.

> Regla de oro: **el cliente nunca decide nada del juego.** Cada vez que una tarea les tiente a "calcular algo en el cliente porque es más fácil", esa es la señal de que va en `PartidaOnline/Core`.

---

## Tarea 1 (la más importante): que el juego use el mismo Core que el servidor

**Qué lograr.** Hoy hay **dos copias de las reglas**: las de `Peleador.cs` y `GestorTurnos.cs` (en Unity) y las de `PartidaOnline/Core/Reglas.cs` (en el servidor). Si cambian una y no la otra, el juego local y el online se comportan distinto. El objetivo es que ambos ejecuten **el mismo código**.

**Dónde mirar.** `Assets/Scripts/Combate/Peleador.cs` (~1.000 líneas: daño, buffs, habilidades) y `GestorTurnos.cs` (orden de fases). Compárenlas con `Core/Reglas.cs`: `ResolverPelea`, `AplicarEfecto`, `Golpear`.

**Cómo empezar.**
1. Escojan **una sola** regla, por ejemplo el cálculo de daño con defensa (`Golpear` en el Core). Encuentren dónde hace lo mismo `Peleador.cs`.
2. Comparen los resultados con el mismo ejemplo a mano. ¿Dan lo mismo? Las diferencias que encuentren son el hallazgo (anótenlas).
3. Decidan cuál es la regla correcta (¿qué dice el GDD?) y corrijan la otra.
4. Para que Unity use el Core, este debe compilar en Unity: **C# 9, sin `record` y sin `namespace X;`** de archivo (el Core usa C# más moderno). Piensen cómo adaptarlo o cómo compilar el mismo archivo desde las dos partes.
5. Hagan que `Peleador` llame al Core en vez de calcular por su cuenta, regla por regla, con el juego funcionando después de cada paso.

**Cómo saber que está bien.** Las 61 pruebas siguen pasando y el combate local da los mismos números que antes (para la misma situación).

**Qué se aprende.** Por qué las reglas deben ser código puro sin `MonoBehaviour`, por qué importa tener una sola fuente de verdad, y cómo refactorizar sin romper nada.

---

## Tarea 2: implementar las habilidades que faltan (22 de 80)

**Qué lograr.** Hoy una habilidad cuyo efecto principal no está soportado se rechaza con `aún no está implementada en el servidor`. Implementen una familia de efectos (por ejemplo `Provocar`, o `Velocidad`).

**Dónde mirar.** `Core/Reglas.cs`: `EfectosSoportados` (la lista), `AplicarEfecto` (el `switch` con cada efecto) y los efectos ya hechos como `Intimidar`, que es el más simple. Las habilidades pendientes están en `Core/Catalogo.Generado.cs` (no lo editen: se genera con `python PartidaOnline/Tools/generar_catalogo.py`).

**Cómo empezar.**
1. Escojan un efecto y lean cómo lo hace hoy `Peleador.cs` en Unity.
2. Escriban **primero la prueba** en `TestProject/PartidaTests.cs` (clase `PeleaTests`: monten el estado con `Mesa.Nueva()` y `Mesa.ListoParaPelear`, como las pruebas que ya existen). Debe fallar.
3. Impleméntenlo en `AplicarEfecto` y añádanlo a `EfectosSoportados`. La prueba debe pasar.
4. Ojo con los **efectos secundarios** (veneno, sangrado): una habilidad soportada puede traer uno que todavía se ignora sin avisar.
5. Como cambian las reglas, **suban `GameProtocol.Version` en las dos mitades** (`Core/GameProtocol.cs` y `PartidaDto.cs`): una prueba falla si solo suben una.

**Cómo saber que está bien.** La prueba nueva pasa, el resto sigue pasando y en el panel F7 la habilidad ya no aparece como *(aún no implementada)*.

**Qué se aprende.** Desarrollo guiado por pruebas, y por qué cambiar reglas obliga a subir la versión del protocolo.

---

## Tarea 3: pintar la partida del servidor en la escena real

**Qué lograr.** El panel F7 es de prueba. La escena real (`GestorTurnos`, `CartaSeleccion`, `Peleador`) debe **dibujar** lo que dice `VistaPartidaDto` en lugar de decidir fases y peleas en local.

**Dónde mirar.** `Assets/Scripts/Online/Partida/PanelPartidaOnline.cs` (método `DibujarPartida`: cómo se lee la vista) y `PartidaOnlineService.cs`.

**Cómo empezar.** Un modo "online" que, en vez de `GestorTurnos` decidiendo, solo reaccione al evento `servicio.VistaRecibida`. Empiecen por lo más pequeño: que al recibir una vista se coloquen los héroes en el campo, sin animaciones. La pelea llega ya resuelta en `RegistroPelea`: solo hay que animarla.

**Cómo saber que está bien.** Dos Editores juegan una partida completa en la escena real, y el cliente no calcula daño, turno ni resultado de ninguna pregunta.

**Qué se aprende.** Qué significa "el cliente solo dibuja", y cómo separar la presentación de las reglas.

---

## Tarea 4: cerrar los huecos de datos que escribe el cliente

**Qué lograr.** En Cloud Save, lo que escribe el jugador con su propio token puede falsificarse. Las claves **Default** y **Public** las puede escribir el jugador; solo **Protected** (el jugador la lee pero no la escribe) y **Private** (ni siquiera la lee) son seguras para lo que decide el servidor.

**Dónde mirar.**
- `UgsDefensa.PublicarDefensaAsync`: el cliente escribe `defensa` y `nivel` en una clase pública escribible (`PublicWriteAccessClassOptions`). Cualquiera puede declararse el nivel que quiera.
- `Tienda/T9Ranking.js`: el ranking se calcula sobre el dato `trofeos`, que el servidor escribe con `setPublicItem`, pero esa clase la puede escribir el propio jugador.
- `T9Inventario`: acepta cualquier lista de ids del cliente.

**Cómo empezar.**
1. Comprueben el problema ustedes mismos: con el token de un jugador de prueba, intenten escribir `trofeos` por la API REST de Cloud Save (en `Assets/Scripts/Online/UgsDefensa.cs` ya hay una prueba así, que escribe `defensa` con el valor `"hack"` por REST). ¿Funciona?
2. Decidan para cada dato **quién debe escribirlo** (¿el cliente o el servidor?) y en qué clase de acceso debe vivir.
3. Muevan primero `nivel`: debe salir de `perfil.nivelCuenta` (que ya está en Protected, Lab 10) y escribirlo solo Cloud Code.
4. Pongan un comentario `ACCESS CLASS` junto a cada escritura a Cloud Save para poder buscarla con grep.

**Cómo saber que está bien.** Un jugador con su propio token ya no puede cambiar su nivel ni sus trofeos con una llamada REST (da error de permisos).

**Qué se aprende.** El modelo de permisos de Cloud Save, y por qué "el servidor lo valida" no sirve si el cliente puede escribir el dato directamente.

---

## Tarea 5: abandono por inactividad y partidas olvidadas

**Qué lograr.** Hoy, si un jugador cierra el juego, el rival solo gana cuando vencen **3 plazos de 45 s** seguidos. Añadan el **abandono por inactividad**: si un jugador consulta la partida y el rival lleva más de N minutos sin actividad, gana por abandono.

**Dónde mirar.** `Project/Servicio.cs`, `GetMatchStateAsync`, y cómo el plazo se anota como una jugada (`TipoJugada.Plazo`). El tiempo debe medirlo **el reloj del servidor**, nunca el del cliente.

**Cómo empezar.** Piensen primero: ¿hace falta guardar la hora de la última actividad? ¿Se puede deducir del registro (cada jugada ya lleva `HoraUtc`)? Después escriban la prueba en `PlazosYAbandonoTests` moviendo `e.Ahora` (el reloj de la prueba es controlable).

**Cómo saber que está bien.** La prueba pasa, y el replay sigue siendo determinista (`ReplayTests`).

**Qué se aprende.** Cómo resolver tiempo sin cron en un servidor sin estado, y por qué el reloj no puede ser el del cliente.

---

## Tarea 6: decidir los valores del juego

El servidor usa los valores del GDD y el código de Unity usa otros. **Es una decisión de diseño del equipo**, no técnica:

| Regla | Servidor (GDD) | Unity hoy |
|---|---|---|
| Segundos por turno | 45 | 30 |
| Quiz acertado / fallado | +25 % / −25 % | +10 % / −10 % |
| Ataque por ronda | +10 % acumulado | no existe |

Los valores están en `Config` (`Core/Reglas.cs`). Decídanlo, cámbienlo en un solo sitio y súbanle la versión al protocolo. Además: hoy en cada pelea ataca **siempre primero el anfitrión**, lo que le da ventaja. ¿Quieren alternarlo por ronda?

---

## Tarea 7 (más adelante): configuración y emparejamiento

- **Configuración desde el servidor:** que los valores de `Config` lleguen por Remote Config a través de una función de Cloud Code, para que el cliente no los lea directamente.
- **Matchmaker:** hoy se juega por código de sala; el Matchmaker empareja por nivel automáticamente.
- **Premios:** al terminar la partida, llamar a la progresión del Lab 10 con un `IdOperacion` derivado del código de partida y el jugador, para premiar **una sola vez**.
