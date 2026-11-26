# Lab14 — Qué tienes que hacer tú (paso a paso)

El código ya está listo: módulo instrumentado, 72 pruebas, `desplegar.ps1`,
`verificar.ps1`, manifiesto, reversión y tabla de costos. Lo que falta necesita tu cuenta
de Unity (yo no puedo crear entornos ni tocar roles).

---

## Antes de empezar: 2 tareas de dashboard (una sola vez)

### Tarea A — Crear el entorno `development`

1. Abre <https://cloud.unity.com> → proyecto **Quizzerttack** (el del juego).
2. **Settings → Environments → Add environment**.
3. Nombre: `development` (minúsculas, exacto).
4. Crear.

**Captura:** `01-entorno-development.png` (que se vea el nombre y el id).

> Si el botón no aparece, tu usuario no es Owner del proyecto: pide que te lo den.

### Tarea B — Dar permisos a la service account

1. **Project Settings → Service Accounts** → abre la cuenta que usa el CLI
   (la que tiene key id `3b92f6d1-…`).
2. Añade estos roles de proyecto:

   | Rol | Para qué |
   |---|---|
   | `Remote Config Admin` | desplegar los `.rc` (hoy da **403**) |
   | `Cloud Code Editor` | publicar el módulo |
   | `Leaderboards Admin` | la tabla `trofeos_dvsc` |
   | `Project Resource Policy Editor` | el `.ac` de la política |
   | `Unity Environments Admin` | crear/ver entornos desde el CLI |

3. Guardar.

**Captura:** `02-roles-service-account.png`.

> Sin `Remote Config Admin` el script se detiene en la fase 3 y **no** toca el módulo:
> es la guarda funcionando, no un fallo del script.

---

## Paso 1 — Abrir la consola en el proyecto

```powershell
cd C:\Users\USER\Desktop\Quizzerttack
$env:UGS_SERVICE_ACCOUNT_KEY_ID     = "<tu key id>"
$env:UGS_SERVICE_ACCOUNT_SECRET_KEY = "<tu secret>"
```

Las credenciales **no** se escriben en ningún archivo. El proyecto se toma solo de
`ProjectSettings.asset`.

---

## Paso 2 — Ensayo sin tocar nada (`-Simular`)

```powershell
.\recursos\scripts\desplegar.cmd -Ambiente development -Simular
```

Qué verás: fase 1 (72 pruebas), fase 2 (respaldo `.ccmzip`), fase 3 y 4 en `--dry-run`, y
la fase 5 omitida. **No** crea jugadores ni cambia el backend.

**Captura:** `03-simulacion.png` (consola completa con el resumen de tiempos).

### Paso 2b — Provocar un fallo local (evidencia obligatoria)

1. Abre `Tienda.Pruebas\CatalogoPruebas.cs` y cambia, **sólo para la prueba**:
   `Assert.Equal(1200, mago.Precio);` → `Assert.Equal(9999, mago.Precio);`
2. Ejecuta: `.\recursos\scripts\desplegar.cmd -Ambiente development -Simular`
3. Debe fallar en la **fase 1** y escribir `No se continuo con las fases siguientes.`
   (1 prueba en rojo, 71 en verde; **no** se llega a respaldar ni a desplegar).
4. Devuelve la línea a `1200`.

**Captura:** `03b-fallo-local.png`.

> Esto demuestra la guarda: un fallo local detiene el despliegue **antes** de tocar el
> backend, no después.

---

## Paso 3 — Despliegue real a `development`

```powershell
.\recursos\scripts\desplegar.cmd -Ambiente development -VerificarEconomia
```

Espera ~80 s (45 son de propagación). Al final debe decir **VERIFICACION OK** con
`entorno=development, marca=dvsc-20261105`.

**Captura:** `04-despliegue-development.png` (consola con las 5 fases y los tiempos).

> Se guarda además un `.log` en `recursos\evidencias\`.

---

## Paso 4 — Evidencia del log estructurado

1. En la captura del paso 3, copia el `idOperacion` que imprime la verificación.
2. Dashboard → **Cloud Code → Tienda → Logs** → entorno `development`.
3. Busca ese id (o `operacion=`).

Debes ver una línea como:

```
operacion="ComprarHeroe" id="001e01…" resultado="rechazada" duracionMs=… lecturas=3 escrituras=0
```

**Captura:** `05-log-estructurado.png` (que se lea la línea completa).

Comprueba también que **no** aparece ningún token ni correo en esas líneas.

---

## Paso 5 — Error deliberado desplegado (traza en los logs)

```powershell
.\recursos\scripts\verificar.cmd -Proyecto 5906c36b-06d8-410b-91be-cf367616022a -Ambiente development -ProvocarError
```

El jugador recibe un **422** con un mensaje corto; la traza queda en el servidor.

1. Dashboard → **Cloud Code → Tienda → Logs** → `development`.
2. Busca `Fallo en ForzarErrorPrueba`.
3. Abre la entrada: tipo de excepción, mensaje y **traza con archivo y línea**.

**Captura:** `06-error-logs.png`.

> Aquí se ve por qué el `catch` registra `{Traza}` con `exception.StackTrace`: el dashboard
> **no** guarda la traza de una excepción no capturada.

---

## Paso 6 — Reversión

Primero mira qué respaldos tienes:

```powershell
Get-ChildItem .\recursos\respaldos
```

Luego revierte al anterior (usa 35 s de espera, como en la guía):

```powershell
.\recursos\scripts\desplegar.cmd -Ambiente development -Revertir .\recursos\respaldos\development-AAAAMMDD-HHMMSS.ccmzip -EsperaSegundos 35
```

**Captura:** `07-reversion.png` (import + espera + verificación).

> El README de reversión (`recursos/REVERSION.md`) explica qué **no** revierte: datos,
> políticas y configuración remota.

---

## Paso 7 — Producción (sólo si el ensayo salió limpio)

```powershell
.\recursos\scripts\desplegar.cmd -Ambiente production -Simular
.\recursos\scripts\desplegar.cmd -Ambiente production -Confirmar -VerificarEconomia
```

**Captura:** `08-produccion.png`.

---

## Paso 8 — Armar el ZIP

Cuando tengas las capturas, avísame y armo `AlexanderVillena_Lab14.zip` con:

```
AlexanderVillena_Lab14/
  Tienda/                      (módulo + .rc + .lb + .ac)
  Tienda.Pruebas/              (72 pruebas)
  recursos/
    scripts/desplegar.ps1  verificar.ps1  *.cmd
    MANIFIESTO.md  REVERSION.md
    respaldos/*.ccmzip         (opcional: pesa ~1,3 MB)
    evidencias/*.log
  planillas/S14-tabla-costos.md
  capturas/01…08.png
```

---

## Si algo falla

| Síntoma | Causa | Solución |
|---|---|---|
| `403` en la fase 3 | falta `Remote Config Admin` | Tarea B |
| `el entorno 'development' no existe` | falta crearlo | Tarea A |
| `La ejecución de scripts está deshabilitada` | política de PowerShell | usar `desplegar.cmd` (ya usa `-ExecutionPolicy Bypass`) |
| `HoraDelServidor 404` tras desplegar | propagación | el script reintenta; si sigue, revisa que la fase 4 terminó en verde |
| `ComprarHeroe` responde "Te faltan 1200 ORO" | el jugador nuevo no tiene saldo | es lo esperado: prueba de que el cobro lo decide el servidor |

## Estado y lo que falta para integrar a CI

Estado local al 2026-11-23: el registro del ensayo muestra **72/72 pruebas** y un fallo
inducido de **71/72** que se detuvo antes de desplegar. Se ejecutó `-Simular` contra
`production`; el CLI señaló que Remote Config no se podía desplegar y la guarda paró antes
del módulo. No se hizo un despliegue real. El respaldo local está en
`recursos/respaldos/production-20261123-201725.ccmzip`. Los logs están en
`recursos/evidencias/`. La prueba de precio quedó restaurada a `1200`.

Para completar el ensayo en nube faltan, en este orden:

1. Crear `development` en Unity Cloud y conceder a la service account los roles de la
   tabla de arriba, en especial `Remote Config Admin`.
2. Repetir `-Simular` en `development`, desplegar allí, comprobar el log estructurado y
   provocar el error deliberado para capturar la traza.
3. Revertir desde el `.ccmzip` de ese despliegue y guardar evidencia. Evaluar producción
   sólo después de que el ensayo de `development` termine correctamente.
4. Guardar las capturas numeradas en `recursos/evidencias/` y armar el ZIP final para
   subirlo a Canvas.

Para integrarlo a un pipeline CI todavía harían falta: guardar las credenciales de la
service account como secretos protegidos del repositorio; disparar pruebas y simulación
por rama o solicitud de cambio; publicar `development` al integrar a la rama acordada;
guardar logs y respaldos como artefactos con retención; y exigir aprobación manual,
protección de rama y `-Confirmar` antes de producción. No copiar claves al repositorio ni
imprimirlas en los logs.
