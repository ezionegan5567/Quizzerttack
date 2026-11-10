// ============ LAB01: ACCESO A LA CUENTA (UGS) ============
// Flujo tipo juego online, en la escena "Lab01-Autenticacion":
//
//   PANTALLA DE INICIO
//     - JUGAR COMO INVITADO   -> SignInAnonymouslyAsync            (paso 7 de la guía)
//     - CREAR CUENTA          -> SignUpWithUsernamePasswordAsync
//     - YA TENGO CUENTA       -> SignInWithUsernamePasswordAsync
//
//   MI CUENTA (lobby)
//     - si entró como invitado: "GUARDAR PROGRESO" -> AddUsernamePasswordAsync
//       (unificar usuarios: el invitado recibe usuario/clave SIN cambiar de Player ID)
//     - CERRAR SESIÓN
//
//   HERRAMIENTAS DEL LAB (discreto, para el informe): estado de servicios, registro,
//   y los dos fallos del paso 6 (Project ID inválido / sin internet).
//
// Todo el UI se construye por código: la escena solo lleva este componente en "AuthLab01".

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using Unity.Services.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PanelAuthLab01 : MonoBehaviour
{
    [Header("Config")]
    public string entorno = "production";
    [Tooltip("Solo se usa para rellenar el campo de usuario en la demo.")]
    public string usuarioEjemplo = "";
    [Tooltip("Informativo: la clave no se rellena nunca por seguridad.")]
    public string claveEjemplo = "";

    [Header("Navegación")]
    [Tooltip("Escena que se carga al pulsar JUGAR (debe estar en Build Settings).")]
    public string escenaSiguiente = "Main Menu";

    // ---- paleta ----
    static readonly Color FondoJuego = new Color(0.05f, 0.06f, 0.11f, 1f);
    static readonly Color FondoTarjeta = new Color(0.08f, 0.10f, 0.17f, 0.98f);
    static readonly Color FondoCaja = new Color(0.02f, 0.03f, 0.06f, 0.95f);
    static readonly Color Dorado = new Color(1f, 0.85f, 0.35f, 1f);
    static readonly Color TextoClaro = new Color(0.90f, 0.93f, 0.98f, 1f);
    static readonly Color TextoGris = new Color(0.62f, 0.68f, 0.78f, 1f);
    static readonly Color Verde = new Color(0.18f, 0.60f, 0.42f, 1f);
    static readonly Color Azul = new Color(0.20f, 0.45f, 0.78f, 1f);
    static readonly Color Morado = new Color(0.44f, 0.32f, 0.66f, 1f);
    static readonly Color Rojo = new Color(0.60f, 0.26f, 0.26f, 1f);
    static readonly Color Gris = new Color(0.30f, 0.33f, 0.38f, 1f);
    static readonly Color Mostaza = new Color(0.52f, 0.42f, 0.15f, 1f);
    static readonly Color VerdeOk = new Color(0.45f, 0.85f, 0.55f, 1f);
    static readonly Color RojoError = new Color(1f, 0.52f, 0.45f, 1f);

    // ---- pantallas ----
    GameObject pInicio, pCrear, pEntrar, pSesion, pHerramientas;

    // ---- textos ----
    TextMeshProUGUI txtEstadoInicio, txtMensajeInicio;
    TextMeshProUGUI txtTituloCrear, txtAyudaCrear, txtMensajeCrear;
    TextMeshProUGUI txtMensajeEntrar;
    TextMeshProUGUI txtNombreSesion, txtEstadoSesion, txtMensajeSesion, txtAyudaVincular;
    TextMeshProUGUI txtEstadoHerramientas, txtLogHerramientas;
    Button btnPrimarioCrear, btnVincular;

    // ---- campos ----
    TMP_InputField crearUsuario, crearClave, crearClave2, entrarUsuario, entrarClave;

    readonly List<string> lineas = new List<string>();
    bool ocupado;
    bool modoVincular;

    // ================================================================
    // ciclo de vida
    // ================================================================
    void Start()
    {
        AsegurarEventSystem();
        CrearUI();
        ServiciosBootstrap.AlRegistrar += AnadirLinea;
        Mostrar(pInicio);
        Refrescar();
        AnadirLinea("[Lab01] Elegí una opción: invitado, crear cuenta o entrar.");
    }

    void OnDestroy()
    {
        ServiciosBootstrap.AlRegistrar -= AnadirLinea;
    }

    void Update()
    {
        // F9 abre/cierra las herramientas del lab desde cualquier pantalla (no ensucia el menú).
        if (AtajoHerramientas()) AlternarHerramientas();

        if (Time.frameCount % 20 == 0 && pSesion != null && pSesion.activeSelf) RefrescarSesion();
    }

    static bool AtajoHerramientas()
    {
#if ENABLE_INPUT_SYSTEM
        var teclado = UnityEngine.InputSystem.Keyboard.current;
        return teclado != null && teclado.f9Key.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.F9);
#else
        return false;
#endif
    }

    void AlternarHerramientas()
    {
        if (pHerramientas == null) return;
        if (pHerramientas.activeSelf) Volver();
        else OnHerramientas();
    }

    // ================================================================
    // navegación
    // ================================================================
    void Mostrar(GameObject pantalla)
    {
        pInicio.SetActive(pantalla == pInicio);
        pCrear.SetActive(pantalla == pCrear);
        pEntrar.SetActive(pantalla == pEntrar);
        pSesion.SetActive(pantalla == pSesion);
        pHerramientas.SetActive(pantalla == pHerramientas);
    }

    void Volver()
    {
        Mostrar(ServiciosBootstrap.HaySesion ? pSesion : pInicio);
    }

    // ================================================================
    // acciones
    // ================================================================
    public void OnInvitado()
    {
        Mensaje("Entrando como invitado...", false);
        Ejecutar("Jugar como invitado", () => ServiciosBootstrap.LoginAnonimoAsync(), () =>
        {
            Mensaje(ServiciosBootstrap.EsInvitado
                ? "Listo. Estás jugando como invitado."
                : "Sesión recuperada: " + ServiciosBootstrap.UsuarioActual, false);
            IrASesion();
        });
    }

    public void OnAbrirCrear()
    {
        modoVincular = false;
        txtTituloCrear.text = "CREAR CUENTA";
        txtAyudaCrear.text = "Elige un usuario y una contraseña para tu cuenta nueva.";
        CambiarTextoBoton(btnPrimarioCrear, "CREAR CUENTA");
        if (crearUsuario != null && string.IsNullOrEmpty(crearUsuario.text))
            crearUsuario.text = usuarioEjemplo;
        Mensaje("", false);
        Mostrar(pCrear);
    }

    public void OnAbrirEntrar()
    {
        if (entrarUsuario != null && string.IsNullOrEmpty(entrarUsuario.text))
            entrarUsuario.text = usuarioEjemplo;
        Mensaje("", false);
        Mostrar(pEntrar);
    }

    // se abre desde "MI CUENTA" cuando el jugador es invitado: unificar usuarios
    public void OnAbrirVincular()
    {
        if (!ServiciosBootstrap.HaySesion)
        {
            Mensaje("Primero entrá como invitado.", true);
            return;
        }

        modoVincular = true;
        txtTituloCrear.text = "GUARDAR TU PROGRESO";
        txtAyudaCrear.text = "Tu invitado recibe usuario y contraseña SIN cambiar de Player ID: no perdés nada de lo jugado.";
        CambiarTextoBoton(btnPrimarioCrear, "GUARDAR PROGRESO");
        if (crearUsuario != null && string.IsNullOrEmpty(crearUsuario.text))
            crearUsuario.text = usuarioEjemplo;
        Mensaje("", false);
        Mostrar(pCrear);
    }

    public void OnCrear()
    {
        string usuario = crearUsuario != null ? crearUsuario.text.Trim() : "";
        string clave = crearClave != null ? crearClave.text : "";
        string clave2 = crearClave2 != null ? crearClave2.text : "";

        if (clave != clave2)
        {
            Mensaje("Las dos contraseñas no coinciden.", true);
            return;
        }

        if (modoVincular)
        {
            Ejecutar("Guardar progreso (unificar)", () => ServiciosBootstrap.UnificarConUsuarioAsync(usuario, clave), () =>
            {
                Mensaje("Progreso guardado. Ya podés entrar con tu usuario desde cualquier lado.", false);
                IrASesion();
            });
        }
        else
        {
            Ejecutar("Crear cuenta", () => ServiciosBootstrap.CrearCuentaAsync(usuario, clave), () =>
            {
                Mensaje("Cuenta creada. ¡Bienvenido!", false);
                IrASesion();
            });
        }
    }

    public void OnEntrar()
    {
        string usuario = entrarUsuario != null ? entrarUsuario.text.Trim() : "";
        string clave = entrarClave != null ? entrarClave.text : "";
        Ejecutar("Iniciar sesión", () => ServiciosBootstrap.IniciarSesionAsync(usuario, clave), () =>
        {
            Mensaje("Sesión iniciada.", false);
            IrASesion();
        });
    }

    // Botón JUGAR: sale de la escena del lab y entra al juego con la sesión ya abierta.
    public void OnJugar()
    {
        if (!ServiciosBootstrap.HaySesion)
        {
            Mensaje("Entrá primero como invitado o con tu cuenta.", true);
            return;
        }

        if (string.IsNullOrEmpty(escenaSiguiente))
        {
            Mensaje("Falta configurar la escena siguiente en el componente.", true);
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(escenaSiguiente))
        {
            Mensaje($"La escena '{escenaSiguiente}' no está en Build Settings.", true);
            return;
        }

        AnadirLinea($"[Lab01] JUGAR -> escena '{escenaSiguiente}' con el Player ID {ServiciosBootstrap.PlayerIdActual}.");
        SceneManager.LoadScene(escenaSiguiente);
    }

    public void OnCerrarSesion()
    {
        Ejecutar("Cerrar sesión", () =>
        {
            ServiciosBootstrap.CerrarSesion();
            return Task.CompletedTask;
        }, () =>
        {
            Mensaje("Sesión cerrada.", false);
            Mostrar(pInicio);
        });
    }

    public void OnHerramientas()
    {
        RefrescarHerramientas();
        Mostrar(pHerramientas);
    }

    public void OnVolverDeHerramientas()
    {
        Volver();
    }

    // ---- paso 6 del informe ----
    public void OnFalloProjectId()
    {
        AnadirLinea("-- PASO 6A | Project ID inválido --");
        AnadirLinea("Edit -> Project Settings -> Services: dejá un Project ID inventado, salí y volvé a Play.");
        AnadirLinea("El paso 5 dirá 'Servicios listos. Estado: Initialized' (NO valida el ID) y el fallo aparece al entrar como invitado:");
        AnadirLinea("RequestFailedException con mensaje VACÍO + línea [Authentication] 404 RESOURCE_NOT_FOUND.");
        AnadirLinea($"Intento con el Project ID actual ({Application.cloudProjectId}):");
        Ejecutar("Paso 6A | intento actual", () => ServiciosBootstrap.LoginAnonimoAsync());
    }

    public void OnFalloSinRed()
    {
        AnadirLinea("-- PASO 6B | sin internet --");
        AnadirLinea("Desconectá el WiFi/Ethernet y pulsá este botón (o entrá como invitado): el catch llega VACÍO");
        AnadirLinea("y la diferencia está en la consola: acá la petición no sale, no hay 404 del servicio.");
        Ejecutar("Paso 6B | intento actual", () => ServiciosBootstrap.LoginAnonimoAsync());
    }

    public void OnCopiar()
    {
        GUIUtility.systemCopyBuffer = "===== LAB01 | ESTADO =====\n" + ServiciosBootstrap.ResumenEstado() +
                                      "\n\n===== LAB01 | REGISTRO =====\n" + string.Join("\n", lineas);
        AnadirLinea("[Lab01] Estado + registro copiados al portapapeles.");
    }

    public void OnLimpiar()
    {
        lineas.Clear();
        if (txtLogHerramientas != null) txtLogHerramientas.text = "";
        AnadirLinea("[Lab01] Registro limpio.");
    }

    // ================================================================
    // ejecución con estado ocupado + mensajes
    // ================================================================
    async void Ejecutar(string titulo, Func<Task> accion, Action alTerminar = null)
    {
        if (ocupado)
        {
            Mensaje("Esperá: ya hay una operación en curso.", true);
            return;
        }

        ocupado = true;
        AnadirLinea("> " + titulo);
        try
        {
            await accion();
            if (alTerminar != null) alTerminar();
        }
        catch (Exception e)
        {
            string detalle = ServiciosBootstrap.DetallarExcepcion(e);
            AnadirLinea("[X] " + titulo + " falló:\n" + detalle);
            Debug.LogError("[DVSC] " + titulo + " falló:\n" + detalle);
            Mensaje(MensajeCorto(e), true);
        }
        finally
        {
            ocupado = false;
            Refrescar();
        }
    }

    static string MensajeCorto(Exception e)
    {
        if (e is ArgumentException || e is InvalidOperationException) return e.Message;

        // Lo primero: lo que dijo el servicio en la consola ([Authentication] -> "detail").
        string servicio = ServiciosBootstrap.ExplicacionServicio();
        if (!string.IsNullOrEmpty(servicio)) return servicio;

        var rfe = e as RequestFailedException;
        if (rfe != null && rfe.ErrorCode == Unity.Services.Authentication.AuthenticationErrorCodes.InvalidParameters)
            return "El servicio rechazó los datos. Abrí F9 -> herramientas y copiá el registro para ver el motivo exacto.";

        return "No se pudo completar. El detalle está en la consola (líneas [Authentication]).";
    }

    void Mensaje(string texto, bool error)
    {
        Color c = error ? RojoError : VerdeOk;
        if (txtMensajeInicio != null) { txtMensajeInicio.text = texto; txtMensajeInicio.color = c; }
        if (txtMensajeCrear != null) { txtMensajeCrear.text = texto; txtMensajeCrear.color = c; }
        if (txtMensajeEntrar != null) { txtMensajeEntrar.text = texto; txtMensajeEntrar.color = c; }
        if (txtMensajeSesion != null) { txtMensajeSesion.text = texto; txtMensajeSesion.color = c; }
    }

    void IrASesion()
    {
        RefrescarSesion();
        Mostrar(pSesion);
    }

    // ================================================================
    // refresco de datos
    // ================================================================
    void Refrescar()
    {
        if (txtEstadoInicio != null) txtEstadoInicio.text = ServiciosBootstrap.ResumenServicios();
        RefrescarSesion();
        RefrescarHerramientas();
    }

    void RefrescarSesion()
    {
        if (txtEstadoSesion == null) return;

        bool hay = ServiciosBootstrap.HaySesion;
        string usuario = ServiciosBootstrap.UsuarioActual;
        bool invitado = ServiciosBootstrap.EsInvitado;

        txtEstadoSesion.text = ServiciosBootstrap.ResumenSesion();
        txtNombreSesion.text = !hay ? "SIN SESIÓN"
            : invitado ? "Invitado"
            : "Hola, " + usuario;

        if (btnVincular != null) btnVincular.gameObject.SetActive(invitado);
        if (txtAyudaVincular != null)
        {
            txtAyudaVincular.gameObject.SetActive(invitado);
            txtAyudaVincular.text = invitado
                ? "Estás como invitado: si cambiás de equipo o borrás datos, perdés la partida. Guardá tu progreso."
                : "Tu cuenta ya tiene usuario y contraseña: podés entrar desde cualquier PC.";
        }
    }

    void RefrescarHerramientas()
    {
        if (txtEstadoHerramientas != null) txtEstadoHerramientas.text = ServiciosBootstrap.ResumenEstado();
    }

    void AnadirLinea(string mensaje)
    {
        if (string.IsNullOrEmpty(mensaje)) return;
        lineas.Add(mensaje);
        while (lineas.Count > 8) lineas.RemoveAt(0);
        if (txtLogHerramientas != null) txtLogHerramientas.text = string.Join("\n", lineas);
    }

    // ================================================================
    // construcción del UI
    // ================================================================
    void AsegurarEventSystem()
    {
        if (EventSystem.current != null) return;
        var es = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
        es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
        es.AddComponent<StandaloneInputModule>();
#endif
    }

    void CrearUI()
    {
        var canvasGo = new GameObject("CanvasLab01", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        // fondo a pantalla completa
        var fondo = new GameObject("Fondo", typeof(RectTransform), typeof(Image));
        fondo.transform.SetParent(canvas.transform, false);
        fondo.GetComponent<Image>().color = FondoJuego;
        Estirar((RectTransform)fondo.transform);

        // tarjeta centrada
        var tarjeta = new GameObject("Tarjeta", typeof(RectTransform), typeof(Image));
        tarjeta.transform.SetParent(canvas.transform, false);
        tarjeta.GetComponent<Image>().color = FondoTarjeta;
        var rtTarjeta = (RectTransform)tarjeta.transform;
        rtTarjeta.sizeDelta = new Vector2(660f, 620f);
        rtTarjeta.anchoredPosition = Vector2.zero;

        pInicio = NuevaPantalla(tarjeta.transform, "PantallaInicio");
        pCrear = NuevaPantalla(tarjeta.transform, "PantallaCrear");
        pEntrar = NuevaPantalla(tarjeta.transform, "PantallaEntrar");
        pSesion = NuevaPantalla(tarjeta.transform, "PantallaSesion");
        pHerramientas = NuevaPantalla(tarjeta.transform, "PantallaHerramientas");

        CrearInicio();
        CrearPantallaCrear();
        CrearPantallaEntrar();
        CrearPantallaSesion();
        CrearPantallaHerramientas();
    }

    void CrearInicio()
    {
        Transform t = pInicio.transform;

        Texto(t, "Titulo", "QUIZZERTTACK", new Vector2(0f, 232f), new Vector2(560f, 56f), 44f, Dorado, TextAlignmentOptions.Center, true);
        Texto(t, "Subtitulo", "CARD BATTLER  ·  ACCESO A LA CUENTA", new Vector2(0f, 190f), new Vector2(560f, 22f), 13f, TextoGris, TextAlignmentOptions.Center);

        Boton(t, "BtnInvitado", "JUGAR COMO INVITADO", new Vector2(0f, 112f), new Vector2(520f, 58f), Verde, OnInvitado);
        Boton(t, "BtnCrear", "CREAR CUENTA", new Vector2(0f, 42f), new Vector2(520f, 58f), Azul, OnAbrirCrear);
        Boton(t, "BtnEntrar", "YA TENGO CUENTA", new Vector2(0f, -28f), new Vector2(520f, 58f), Morado, OnAbrirEntrar);

        Texto(t, "Nota", "Si entrás como invitado podés guardar el progreso después, sin perder la partida.",
            new Vector2(0f, -80f), new Vector2(520f, 20f), 11f, TextoGris, TextAlignmentOptions.Center);

        // Herramientas del lab: sin botón en el menú, se abren con F9 (y también desde "Mi cuenta").
        Texto(t, "PistaF9", "F9 = herramientas del lab (para el informe)",
            new Vector2(0f, -112f), new Vector2(520f, 16f), 9f, new Color(0.42f, 0.46f, 0.54f, 1f), TextAlignmentOptions.Center);

        txtMensajeInicio = Texto(t, "Mensaje", "", new Vector2(0f, -152f), new Vector2(560f, 40f), 12f, VerdeOk, TextAlignmentOptions.Center);

        txtEstadoInicio = Texto(t, "Estado", "", new Vector2(0f, -232f), new Vector2(560f, 44f), 10f, TextoGris, TextAlignmentOptions.Top);
    }

    void CrearPantallaCrear()
    {
        Transform t = pCrear.transform;

        txtTituloCrear = Texto(t, "Titulo", "CREAR CUENTA", new Vector2(0f, 238f), new Vector2(560f, 34f), 26f, Dorado, TextAlignmentOptions.Center, true);
        txtAyudaCrear = Texto(t, "Ayuda", "", new Vector2(0f, 200f), new Vector2(540f, 40f), 12f, TextoGris, TextAlignmentOptions.Top);

        Etiqueta(t, "Usuario", new Vector2(0f, 158f));
        crearUsuario = Campo(t, "CampoUsuario", new Vector2(0f, 132f), new Vector2(520f, 42f), "tu.usuario", false);

        Etiqueta(t, "Contraseña", new Vector2(0f, 96f));
        crearClave = Campo(t, "CampoClave", new Vector2(0f, 70f), new Vector2(520f, 42f), "mínimo 8 caracteres", true);

        Etiqueta(t, "Repetir contraseña", new Vector2(0f, 34f));
        crearClave2 = Campo(t, "CampoClave2", new Vector2(0f, 8f), new Vector2(520f, 42f), "la misma contraseña", true);

        Texto(t, "Reglas", "Usuario: 3-20 caracteres (letras, números, . - @ _).   Contraseña: 8-30 con mayúscula, minúscula, número y símbolo.",
            new Vector2(0f, -30f), new Vector2(540f, 34f), 10f, TextoGris, TextAlignmentOptions.Top);

        txtMensajeCrear = Texto(t, "Mensaje", "", new Vector2(0f, -88f), new Vector2(540f, 60f), 12f, RojoError, TextAlignmentOptions.Top);

        btnPrimarioCrear = Boton(t, "BtnCrear", "CREAR CUENTA", new Vector2(-135f, -150f), new Vector2(250f, 48f), Verde, OnCrear);
        Boton(t, "BtnVolver", "VOLVER", new Vector2(135f, -150f), new Vector2(250f, 48f), Gris, Volver);

        Texto(t, "Pie", "Este es el paso 6 del código: lo que falle queda explicado en Herramientas del lab.",
            new Vector2(0f, -235f), new Vector2(540f, 20f), 10f, TextoGris, TextAlignmentOptions.Center);
    }

    void CrearPantallaEntrar()
    {
        Transform t = pEntrar.transform;

        Texto(t, "Titulo", "INICIAR SESIÓN", new Vector2(0f, 238f), new Vector2(560f, 34f), 26f, Dorado, TextAlignmentOptions.Center, true);
        Texto(t, "Ayuda", "Entrá con el usuario y la contraseña de tu cuenta.",
            new Vector2(0f, 200f), new Vector2(540f, 24f), 12f, TextoGris, TextAlignmentOptions.Center);

        Etiqueta(t, "Usuario", new Vector2(0f, 150f));
        entrarUsuario = Campo(t, "CampoUsuario", new Vector2(0f, 124f), new Vector2(520f, 42f), "tu.usuario", false);

        Etiqueta(t, "Contraseña", new Vector2(0f, 88f));
        entrarClave = Campo(t, "CampoClave", new Vector2(0f, 62f), new Vector2(520f, 42f), "tu contraseña", true);

        txtMensajeEntrar = Texto(t, "Mensaje", "", new Vector2(0f, 16f), new Vector2(540f, 40f), 12f, RojoError, TextAlignmentOptions.Top);

        Boton(t, "BtnEntrar", "ENTRAR", new Vector2(-135f, -60f), new Vector2(250f, 48f), Morado, OnEntrar);
        Boton(t, "BtnVolver", "VOLVER", new Vector2(135f, -60f), new Vector2(250f, 48f), Gris, Volver);

        Texto(t, "Pie", "Si todavía no tenés cuenta, volvé y elegí Invitado o Crear cuenta.",
            new Vector2(0f, -140f), new Vector2(540f, 20f), 10f, TextoGris, TextAlignmentOptions.Center);
    }

    void CrearPantallaSesion()
    {
        Transform t = pSesion.transform;

        txtNombreSesion = Texto(t, "Titulo", "", new Vector2(0f, 248f), new Vector2(560f, 40f), 30f, Dorado, TextAlignmentOptions.Center, true);

        // Con la sesión abierta, este es el botón que sale del lab y entra al juego.
        Boton(t, "BtnJugar", "JUGAR (" + escenaSiguiente + ")", new Vector2(0f, 192f), new Vector2(520f, 56f), Verde, OnJugar);

        var marco = new GameObject("MarcoEstado", typeof(RectTransform), typeof(Image));
        marco.transform.SetParent(t, false);
        marco.GetComponent<Image>().color = FondoCaja;
        var rt = (RectTransform)marco.transform;
        rt.sizeDelta = new Vector2(540f, 104f);
        rt.anchoredPosition = new Vector2(0f, 110f);
        txtEstadoSesion = Texto(marco.transform, "Estado", "", Vector2.zero, new Vector2(516f, 92f), 11f, TextoClaro, TextAlignmentOptions.TopLeft);

        btnVincular = Boton(t, "BtnVincular", "GUARDAR PROGRESO (usuario y contraseña)", new Vector2(0f, 34f), new Vector2(520f, 46f), Azul, OnAbrirVincular);

        txtAyudaVincular = Texto(t, "AyudaVincular", "", new Vector2(0f, -2f), new Vector2(520f, 26f), 10f, TextoGris, TextAlignmentOptions.Top);

        Boton(t, "BtnSalir", "CERRAR SESIÓN", new Vector2(0f, -50f), new Vector2(520f, 44f), Gris, OnCerrarSesion);

        txtMensajeSesion = Texto(t, "Mensaje", "", new Vector2(0f, -96f), new Vector2(540f, 40f), 12f, VerdeOk, TextAlignmentOptions.Top);

        Boton(t, "BtnHerramientas", "Herramientas del lab", new Vector2(0f, -142f), new Vector2(220f, 26f), Gris, OnHerramientas);
    }

    void CrearPantallaHerramientas()
    {
        Transform t = pHerramientas.transform;

        Texto(t, "Titulo", "HERRAMIENTAS DEL LAB 01", new Vector2(0f, 262f), new Vector2(560f, 30f), 22f, Dorado, TextAlignmentOptions.Center, true);

        var marcoEstado = new GameObject("MarcoEstado", typeof(RectTransform), typeof(Image));
        marcoEstado.transform.SetParent(t, false);
        marcoEstado.GetComponent<Image>().color = FondoCaja;
        var rt1 = (RectTransform)marcoEstado.transform;
        rt1.sizeDelta = new Vector2(560f, 120f);
        rt1.anchoredPosition = new Vector2(0f, 182f);
        txtEstadoHerramientas = Texto(marcoEstado.transform, "Estado", "", Vector2.zero, new Vector2(540f, 108f), 10f, TextoClaro, TextAlignmentOptions.TopLeft);

        var marcoLog = new GameObject("MarcoLog", typeof(RectTransform), typeof(Image));
        marcoLog.transform.SetParent(t, false);
        marcoLog.GetComponent<Image>().color = FondoCaja;
        var rt2 = (RectTransform)marcoLog.transform;
        rt2.sizeDelta = new Vector2(560f, 130f);
        rt2.anchoredPosition = new Vector2(0f, 48f);
        txtLogHerramientas = Texto(marcoLog.transform, "Log", "", Vector2.zero, new Vector2(540f, 118f), 10f, TextoClaro, TextAlignmentOptions.TopLeft);

        Boton(t, "BtnInicializar", "1 | Inicializar servicios", new Vector2(0f, -40f), new Vector2(280f, 32f), Azul,
            () => Ejecutar("Inicializar servicios", () => ServiciosBootstrap.InicializarAsync(entorno)));

        Boton(t, "Btn6A", "8 | Paso 6A | Project ID", new Vector2(-142f, -84f), new Vector2(276f, 32f), Mostaza, OnFalloProjectId);
        Boton(t, "Btn6B", "9 | Paso 6B | sin internet", new Vector2(142f, -84f), new Vector2(276f, 32f), Rojo, OnFalloSinRed);

        Boton(t, "BtnCopiar", "Copiar estado + registro", new Vector2(-142f, -128f), new Vector2(276f, 30f), Azul, OnCopiar);
        Boton(t, "BtnLimpiar", "Limpiar", new Vector2(142f, -128f), new Vector2(276f, 30f), Gris, OnLimpiar);

        Boton(t, "BtnVolver", "VOLVER", new Vector2(0f, -186f), new Vector2(250f, 42f), Verde, OnVolverDeHerramientas);
    }

    // ================================================================
    // helpers de UI
    // ================================================================
    GameObject NuevaPantalla(Transform padre, string nombre)
    {
        var go = new GameObject(nombre, typeof(RectTransform));
        go.transform.SetParent(padre, false);
        Estirar((RectTransform)go.transform);
        return go;
    }

    TextMeshProUGUI Texto(Transform padre, string nombre, string contenido, Vector2 pos, Vector2 size, float fuente,
        Color color, TextAlignmentOptions alineacion, bool negrita = false)
    {
        var go = new GameObject(nombre, typeof(RectTransform));
        go.transform.SetParent(padre, false);

        var rt = (RectTransform)go.transform;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = contenido;
        tmp.fontSize = fuente;
        tmp.color = color;
        tmp.alignment = alineacion;
        tmp.raycastTarget = false;
        if (negrita) tmp.fontStyle = FontStyles.Bold;
        Fuente(tmp);
        return tmp;
    }

    void Etiqueta(Transform padre, string texto, Vector2 pos)
    {
        Texto(padre, "Etiqueta" + texto, texto.ToUpperInvariant(), pos, new Vector2(520f, 16f), 10f, TextoGris, TextAlignmentOptions.Left);
    }

    Button Boton(Transform padre, string nombre, string etiqueta, Vector2 pos, Vector2 size, Color color, Action accion)
    {
        var go = new GameObject(nombre, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(padre, false);

        var rt = (RectTransform)go.transform;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        var img = go.GetComponent<Image>();
        img.color = color;

        var boton = go.GetComponent<Button>();
        boton.targetGraphic = img;
        boton.onClick.AddListener(() => accion());

        var texto = Texto(go.transform, "Texto", etiqueta, Vector2.zero, Vector2.zero, 14f, Color.white, TextAlignmentOptions.Center, true);
        Estirar(texto.rectTransform, new Vector2(6f, 2f));
        return boton;
    }

    static void CambiarTextoBoton(Button boton, string etiqueta)
    {
        if (boton == null) return;
        var texto = boton.GetComponentInChildren<TextMeshProUGUI>();
        if (texto != null) texto.text = etiqueta;
    }

    TMP_InputField Campo(Transform padre, string nombre, Vector2 pos, Vector2 size, string marcador, bool secreto)
    {
        // Se crea inactivo: así TMP_InputField no arranca antes de tener viewport y texto asignados.
        var go = new GameObject(nombre, typeof(RectTransform), typeof(Image));
        go.SetActive(false);
        go.transform.SetParent(padre, false);

        var rt = (RectTransform)go.transform;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        go.GetComponent<Image>().color = new Color(0.12f, 0.15f, 0.22f, 1f);

        var area = new GameObject("TextArea", typeof(RectTransform), typeof(RectMask2D));
        area.transform.SetParent(go.transform, false);
        var rtArea = (RectTransform)area.transform;
        rtArea.anchorMin = Vector2.zero;
        rtArea.anchorMax = Vector2.one;
        rtArea.offsetMin = new Vector2(10f, 4f);
        rtArea.offsetMax = new Vector2(-10f, -4f);

        var marcadorTxt = Texto(area.transform, "Placeholder", marcador, Vector2.zero, Vector2.zero, 13f,
            new Color(1f, 1f, 1f, 0.32f), TextAlignmentOptions.Left);
        Estirar(marcadorTxt.rectTransform);

        var textoTxt = Texto(area.transform, "Text", "", Vector2.zero, Vector2.zero, 14f,
            Color.white, TextAlignmentOptions.Left);
        Estirar(textoTxt.rectTransform);

        var campo = go.AddComponent<TMP_InputField>();
        campo.textViewport = rtArea;
        campo.textComponent = textoTxt;
        campo.placeholder = marcadorTxt;
        campo.lineType = TMP_InputField.LineType.SingleLine;
        campo.caretWidth = 2;
        campo.customCaretColor = true;
        campo.caretColor = new Color(0.35f, 0.95f, 0.85f, 1f);
        campo.selectionColor = new Color(0.20f, 0.45f, 0.70f, 0.60f);

        if (secreto)
        {
            campo.contentType = TMP_InputField.ContentType.Password;
            campo.asteriskChar = '*';
            campo.characterLimit = 30;
        }
        else
        {
            campo.contentType = TMP_InputField.ContentType.Standard;
            campo.characterLimit = 20;
        }

        go.SetActive(true);
        return campo;
    }

    static void Estirar(RectTransform rt, Vector2 relleno = default(Vector2))
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = relleno;
        rt.offsetMax = -relleno;
    }

    static void Fuente(TextMeshProUGUI tmp)
    {
        // A veces la fuente por defecto de TMP todavía no está cargada al crear el texto por código.
        if (tmp.font != null) return;
        tmp.font = TMP_Settings.defaultFontAsset;
        if (tmp.font != null) return;
        var fuentes = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
        if (fuentes != null && fuentes.Length > 0) tmp.font = fuentes[0];
    }
}
