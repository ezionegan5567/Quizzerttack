# Procedimiento de reversión — Lab14

Revertir **no es** "volver a desplegar lo de antes" a mano: es importar el respaldo que el
script tomó **antes** de publicar la versión nueva.

```
recursos\respaldos\<ambiente>-<AAAAMMDD-HHMMSS>.ccmzip
```

---

## 1. Qué revierte y qué no

| Revierte | No revierte |
|---|---|
| El **módulo** de Cloud Code (binarios y endpoints) | Los **datos** de los jugadores (Cloud Save) |
| La versión anterior de las funciones | La **configuración remota** (`.rc`, `.lb`, `.ac`) |
| | Game Overrides, esquema de Analytics, índices |

Para la configuración el respaldo es **git**: `git checkout -- Tienda/*.rc` y volver a
desplegar con el script. Los `.ccmzip` no se suben al repositorio.

---

## 2. Comando

```powershell
.\recursos\scripts\desplegar.cmd -Ambiente development -Revertir ..\respaldos\development-20261123-101500.ccmzip
```

o, si la política de ejecución lo permite:

```powershell
.\recursos\scripts\desplegar.ps1 -Ambiente development -Revertir .\recursos\respaldos\development-20261123-101500.ccmzip
```

Fases del modo reversión (son 3, no 5):

| Fase | Qué hace | Tiempo medido (guía) |
|---|---|---|
| 1. Importar | `ugs cc modules import <dir> <archivo>` | ~5,1 s |
| 2. Esperar | 35 s de propagación | 35 s |
| 3. Verificar | jugador anónimo nuevo → `HoraDelServidor` | ~1 s |

Si al verificar responde **404** en `HoraDelServidor`, es normal cuando el respaldo es
anterior al Lab14: esa versión no tenía la función. En ese caso la prueba de que volvió la
versión anterior es que **sigue respondiendo** `VistaTemporada` / `ComprarHeroe` sin la
función nueva, con el mismo mensaje que antes del despliegue.

`-Ambiente production` sigue exigiendo `-Confirmar` también al revertir.

---

## 3. Orden recomendado

1. **Ensayo en `development`**: desplegar, provocar el error deliberado, revertir y verificar.
   Guardar captura de la consola (queda además un `.log` en `recursos/evidencias/`).
2. **Producción**: sólo si el ensayo salió limpio.
3. Anotar en el informe: archivo usado, hora, tiempo de importación y de propagación.

---

## 4. Compatibilidad: el aviso que hay que entender

Revertir el módulo **no** revierte lo que el módulo nuevo ya escribió:

- `control_estado` y los recibos de operaciones idempotentes son datos nuevos.
- Un módulo anterior puede no entender un estado creado por el nuevo.

Por eso **no** se debe "arreglar" un fallo volviendo a un inicializador que importe
`Default` ni borrando llaves a mano: eso destruye datos de jugadores. Si el estado nuevo
rompe la versión vieja, la salida correcta es **corregir hacia adelante** con un despliegue
nuevo, no hacia atrás.

---

## 5. Checklist después de revertir

- [ ] El import terminó sin error y se ve la versión en Cloud Code → Modules.
- [ ] `VistaTemporada` responde con la tabla `trofeos_dvsc` (configuración intacta).
- [ ] Un jugador anónimo nuevo puede autenticarse (Auth no se toca).
- [ ] El último combate de un jugador de prueba sigue resolviendo (el estado es compatible).
- [ ] Se anotó el `.ccmzip` usado en el informe y **no** se subió al repositorio.
- [ ] Si el fallo era de configuración, se revirtió también el `.rc` desde git.

---

## 6. Si el respaldo no existe

- Primer despliegue de un entorno: no hay nada que respaldar (el script lo avisa y sigue).
- Respaldo corrupto o ausente: **no** improvises un "rollback" manual. Vuelve a publicar
  desde git una versión conocida (`git checkout <commit> -- Tienda`) y despliega con el
  script: así queda respaldo y verificación otra vez.
