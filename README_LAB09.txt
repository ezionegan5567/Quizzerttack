================================================================
 LAB 09 — QUIZZERTTACK (UGS Cloud Save + Cloud Code) — MANUAL DE INSTALACION
================================================================
Autor: seijer. Módulo Cloud Code: "Tienda" (BuscarRival, ResolverCombate).

PASOS EN ORDEN (los que requiere la guía):

--- PASO 0: activar UGS en el proyecto ---
1. Crea cuenta/proyecto en https://cloud.unity.com (si no lo tienes).
2. En Unity: Edit > Project Settings > Services > link al proyecto y organizacion.
3. En el portal: habilita Cloud Save y Cloud Code en TU proyecto UGS.
4. Package Manager (Window > Package Manager > Unity Registry): instala
   - com.unity.services.cloudsave
   - com.unity.services.cloudcode
   - com.unity.services.authentication
5. Player Settings > Scripting Define Symbols: agrega UGS_ON
   (los scripts UgsDefensa/UgsRival/DemoLab09 estan protegidos con #if UGS_ON
   para que no den errores hasta que termines este paso).

--- PASO 1: historial de acciones del jugador ---
Ya existe la tabla (SistemaPuntos, Botones_avancer) del lab 04 y el cliente
guarda el nivel. El documento DefensaPublica vive en Modelos/Estado.cs.

--- PASO 2: documento de defensa publico ---
En tu escena: GameObject "Registros" > Add Component > DemoLab09
(miAlias seijer, miNivel 20). Ejecuta Play y pulsa F10 (o ContextMenu Correr demo).
Se hace: InitializeAsync + SignInAnonymouslyAsync, SaveAsync con
SaveOptions(new PublicWriteAccessClassOptions()) grabando 2 llaves
(defensa + nivel), lectura ajena con LoadOptions + PublicReadAccessClassOptions,
SwitchProfileAsync("otro") + SignInAnonymouslyAsync para probar sin dos
computadoras, y verificacion 403 code 53 (peticion cruda con tu token).
Para salir sin borrar el perfil: SignOut(false). Para borrar datos locales y
crear uno nuevo: SignOut(true).

--- PASO 3-4: índice en cloud save (requiere UGS CLI + .NET SDK) ---
1. Instala el .NET SDK (dotnet) y el UGS CLI: ugs (Unity Gaming Services CLI).
2. Credenciales: crea un service account en el portal (Project Settings >
   Service accounts) con rol Cloud Code + Cloud Save; exporta:
   set UGS_UCS_USERNAME=ClientID
   set UGS_UCS_PASSWORD=ClientSecret
3. Conecta el CLI: ugs login  /  ugs config set project-id <ID>  y  ugs config set environment production
4. Crea el indice:
   ugs cloud-save data index player create --visibility public --body S609-indice-nivel.json

--- PASO 4: consulta manual (prueba el indice) ---
Edita S609-consulta-nivel.json (franja 18..22 tope 20) y prueba:
   ugs cloud-save data player query --visibility public --body S609-consulta-nivel.json
Si tu version del CLI usa "expression" o "returnKeysExpression", ajusta la llave;
el resultado debe listar jugadores con su "id" y los valores de defensa/nivel.

--- PASO 5: modulo "Tienda" en cloud code ---
Carpeta del modulo: <carpeta del proyecto>/Tienda (Funciones.cs, Datos.cs,
TablaRivales.cs, Tienda.csproj) + solucion Tienda.sln en la raiz.
Construye y despliega:
   ugs deploy Tienda.sln
   (o: ugs cloud-code scripts deploy Tienda --sln Tienda.sln)
La funcion BuscarRival usa franja inicial 2, paso 2, tope 10 (parametrizable),
excluye tu propio playerId ya en el servidor, cuenta Consultas y LecturasConsumidas.

--- PASO 5-6: pruebas ---
   ugs cloud-code scripts run Tienda BuscarRival --body '{"Nivel":20,...}'
(El cliente DemoLab09 hace las mismas llamadas con CloudCodeService.)

--- PASO 7-8-9: casos limite, capturas y capturas de Excepciones ---
Juega el demo de la consola (F10). Registra:
 - Consultas y lecturas consumidas por cada Buscador rival (la funcion devuelve Consultas/Lecturas).
 - Capturas de la consola con 3 cuentas (Switch Profile: perfil "principal" y "otro").
 - El rechazo 403 code 53 al escribir la defensa ajena.
 - Resultado (no excepcion) por defensa vacia (S08).
 - Exception si el rival no existe (S09).
 - Exception por declarar HEROES_COLOSO (protegido: no peleable).
 - Exception por declarar un heroe ajeno a su inventario protegido (S06-S07).

--- ENTREGA ---
ApellidoNombre_Lab09.zip con:
 - Carpeta Tienda/ SIN bin ni obj (module Cloud Code)
 - Solucion Tienda.sln
 - S609-indice-nivel.json y S609-consulta-nivel.json
 - Export del proyecto Unity (comprimido) con los scripts Modulo/Estado/UgsDefensa/UgsRival/DemoLab09
 - Captura de la consola de Unity con el flujo completo (PASO 2 + PASO 3-5 + PASO 6)
 - Tabla Consultas/LecturasConsumidas de cada BuscarRival
 - Conclusiones (6 puntos: uso de datos publicos, acceso con playerId del rival,
   exclusion del pastor en servidor, nivel cercano, pagos y lecturas, control de
   datos protegidos por servidor).

================================================================
ESTADO DE LA IMPLEMENTACION EN ESTE PROYECTO
================================================================
C:raiz\Quizzerttack
  Tienda.sln + Tienda/ (Datos, TablaRivales, Funciones) = modulo cloud code
  S609-indice-nivel.json / S609-consulta-nivel.json  = CLI
  Assets/Scripts/Modelos/Estado.cs                    = DefensaPublica + DTOs
  Assets/Scripts/Online/UgsDefensa.cs                 = Cloud Save (publicar/leer/403/perfiles)
  Assets/Scripts/Online/UgsRival.cs                   = Cloud Code (BuscarRival/ResolverCombate)
  Assets/Scripts/Online/DemoLab09.cs                  = demo de la consola (F10)
  Assets/Scripts/Online/PanelRegistros.cs             = (lab anterior, Firebase, sigue activo)

NOTAS: los nombres exactos de metodos del SDK (CloudSaveApiClient /
QueryPublicPlayerDataAsync / GetProtectedPlayerDataAsync) dependen de la version
de los paquetes: si el build da error "does not contain a definition for...", pega
el error y se ajusta el nombre. El parser usa JSON tolerante (id/data).
