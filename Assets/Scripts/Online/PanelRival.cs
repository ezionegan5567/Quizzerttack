// ============ LAB09 → MODO ONLINE REAL (requiere UGS_ON) ============
// Panel del jugador (F8) que integra el laboratorio al juego:
//   1. Guardar defensa en la nube (+ inventario protegido automático).
//   2. Buscar rival por nivel (índice nivel_1, franjas ±2→tope).
//   3. Atacar al rival encontrado (pvp resuelto en Cloud Code).
//   4. Si no hay rivales: atacar NPC de práctica (fallback).
// Montaje: en el GameObject "Registros" (o cualquiera) → Add Component → PanelRival.
// La sesión de UGS se inicia sola la primera vez que usa un botón.

#if UGS_ON
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

public class PanelRival : MonoBehaviour
{
    [Header("Datos de cuenta (edita aquí)")]
    public string miAlias = "Jugador1";
    public int miNivel = 20;
    public int franjaMaxima = 10;

    [Header("Tecla")]
    public KeyCode teclaAbrir = KeyCode.F8;

    [Header("Auto (no tocar)")]
    public GameObject panelUI;
    GameObject cartelOcupado; // privado: ya no aparece en el Inspector → sin UnassignedReferenceException
    public TextMeshProUGUI textoEstado;
    public TextMeshProUGUI textoResultado;
    public Button botonAtacar;

    GestorTurnos gestor;
    bool ocupado;
    string rivalElegido = "";   // playerId del último BuscarRival
    string nombreRival = "";
    List<string> equipoMio = new List<string>();

    void Start()
    {
        gestor = Object.FindFirstObjectByType<GestorTurnos>();
        if (panelUI == null) CrearUI();
    }

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
    void Update()
    {
        var kb = Keyboard.current;
        if (kb != null && kb.f8Key.wasPressedThisFrame) Toggle();
    }
#else
    void Update()
    {
        if (Input.GetKeyDown(teclaAbrir)) Toggle();
    }
#endif

    public void Toggle()
    {
        if (panelUI == null) return;
        bool abrir = !panelUI.activeSelf;
        panelUI.SetActive(abrir);
        panelUI.transform.SetAsLastSibling();
        if (abrir)
        {
            textoEstado.text = ocupado ? "Trabajando en la nube..." : "Conecta y guarda tu defensa para jugar online.";
            textoResultado.text = "1) GUARDAR DEFENSA  2) BUSCAR RIVAL  3) ATACAR.\n4) NPC de práctica si no hay rivales.";
            panelUI.transform.SetAsLastSibling();
        }
    }

    // ---------- 1. GUARDAR DEFENSA ----------
    public async void GuardarDefensa()
    {
        if (ocupado) return;
        gestor = Object.FindFirstObjectByType<GestorTurnos>();
        if (gestor == null || gestor.cartasPlayer1 == null)
        {
            textoEstado.text = "No encuentro las cartas P1 de la escena.";
            return;
        }
        ocupado = true; cartelOcupado?.SetActive(true);
        try
        {
            textoEstado.text = "Conectando (UGS + login anónimo)...";
            await UgsDefensa.IniciarAsync();
            textoEstado.text = "Publicando defensa pública...";
            var defensa = ArmarDefensa();
            await UgsDefensa.PublicarDefensaAsync(defensa);
            textoEstado.text = "Registrando inventario protegido (servidor)...";
            equipoMio.Clear();
            foreach (var h in defensa.Equipo) equipoMio.Add(h.Id);
            await UgsRival.PublicarInventarioAsync(equipoMio);
            textoEstado.text = $"Online como {defensa.Nombre} (nivel {defensa.NivelCuenta}) — id {UgsDefensa.MiPlayerId}";
            textoResultado.text = "Defensa guardada. Ya puedes ser atacado y puede Buscar rival.";
        }
        catch (System.Exception e)
        {
            textoEstado.text = "Error: " + e.Message;
        }
        finally
        {
            ocupado = false; cartelOcupado?.SetActive(false);
        }
    }

    // ---------- 2. BUSCAR RIVAL ----------
    public async void BuscarRival()
    {
        if (ocupado) return;
        ocupado = true; cartelOcupado?.SetActive(true);
        try
        {
            textoEstado.text = "Consultando el índice nivel_1...";
            rivalElegido = "";
            var res = await UgsRival.BuscarRivalAsync(miNivel, franjaMaxima, suficientes: 2);
            textoEstado.text = $"consultas={res.Consultas} lecturas={res.LecturasConsumidas} (franja ±{res.Franja})";
            if (res.Hubo)
            {
                rivalElegido = res.RivalPlayerId;
                nombreRival = res.Rival?.Nombre ?? "Rival";
                textoResultado.text = $"Rival: {nombreRival} (nivel {res.Rival?.NivelCuenta ?? 0}, {res.Rival?.Equipo?.Count ?? 0} héroes).\nPulsa ATACAR.";
                botonAtacar.image.color = new Color(0.65f, 0.3f, 0.22f, 1f);
            }
            else
            {
                textoResultado.text = res.Mensaje + "\nNo hay rivales: pulsa ATACAR NPC de práctica.";
                botonAtacar.image.color = new Color(0.25f, 0.4f, 0.75f, 1f);
            }
        }
        catch (System.Exception e)
        {
            textoEstado.text = "Error: " + e.Message;
        }
        finally
        {
            ocupado = false; cartelOcupado?.SetActive(false);
        }
    }

    // ---------- 3/4. ATACAR (rival real o NPC) ----------
    public async void Atacar()
    {
        if (ocupado) return;
        if (equipoMio.Count == 0)
        {
            textoEstado.text = "Primero GUARDA defensa (define tu equipo).";
            return;
        }
        bool npc = string.IsNullOrEmpty(rivalElegido);
        ocupado = true; cartelOcupado?.SetActive(true);
        try
        {
            textoEstado.text = npc ? "Atacando NPC de práctica..." : $"Atacando a {nombreRival}...";
            var r = npc
                ? await UgsRival.ResolverCombateAsync("npc_practica_1", equipoMio)
                : await UgsRival.ResolverCombateAsync(rivalElegido, equipoMio, miAlias);
            textoResultado.text = $"{(r.Gano ? "¡VICTORIA!" : "Derrota.")} {r.Mensaje}\n(+{r.Oro} oro, +{r.Trofeos} trofeos)";
            textoEstado.text = "Combate resuelto en la nube (anti-cheat activo).";
        }
        catch (System.Exception e)
        {
            textoEstado.text = "Error: " + e.Message;
        }
        finally
        {
            ocupado = false; cartelOcupado?.SetActive(false);
        }
    }

    // ---------- 5. MI RESUMEN (bolsa + venganzas) ----------
    public async void MiResumen()
    {
        if (ocupado) return;
        ocupado = true; cartelOcupado?.SetActive(true);
        try
        {
            textoEstado.text = "Leyendo tu bolsa y tu registro de ataques...";
            var res = await UgsRival.MiResumenAsync();
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Bolsa: {res.Bolsa.Oro} oro, {res.Bolsa.Trofeos} trofeos.");
            rivalElegido = ""; nombreRival = "";
            foreach (var a in res.Ataques)
            {
                sb.AppendLine($"{a.Cuando} — {a.Quien} {a.Resultado}.");
                if (rivalElegido == "" && a.Resultado == "te venció")
                {
                    rivalElegido = a.QuienId; nombreRival = a.Quien;
                }
            }
            if (rivalElegido != "")
                sb.AppendLine($"PULSA ATACAR para vengarte de {nombreRival}.");
            textoResultado.text = sb.ToString();
            textoEstado.text = " Bolsa y ataques desde la nube.";
        }
        catch (System.Exception e)
        {
            textoEstado.text = "Error: " + e.Message;
        }
        finally
        {
            ocupado = false; cartelOcupado?.SetActive(false);
        }
    }

    // ---------- 6. RANKING por trofeos ----------
    public async void Ranking()
    {
        if (ocupado) return;
        ocupado = true; cartelOcupado?.SetActive(true);
        try
        {
            textoEstado.text = "Consultando el ranking (índice trofeos)...";
            var res = await UgsRival.RankingAsync();
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("TOP 10 POR TROFEOS:");
            int i = 1;
            foreach (var f in res.Filas)
                sb.AppendLine($"{i++}. {f.Nombre} — {f.Trofeos} trofeos (nivel {f.Nivel})");
            if (res.Filas.Count == 0) sb.AppendLine("(nadie con trofeos todavía).");
            sb.AppendLine("Sube trofeos ganando combates pvp.");
            textoResultado.text = sb.ToString();
            textoEstado.text = "Ranking actual.";
        }
        catch (System.Exception e)
        {
            textoEstado.text = "Error: " + e.Message;
        }
        finally
        {
            ocupado = false; cartelOcupado?.SetActive(false);
        }
    }

    // ---------- UI (misma técnica que PanelRegistros) ----------
    void CrearUI()
    {
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject goCanvas = new GameObject("CanvasRival");
            canvas = goCanvas.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            goCanvas.AddComponent<CanvasScaler>();
            goCanvas.AddComponent<GraphicRaycaster>();
        }

        panelUI = new GameObject("PanelRival");
        panelUI.transform.SetParent(canvas.transform, false);
        Image fondo = panelUI.AddComponent<Image>();
        fondo.color = new Color(0.05f, 0.1f, 0.16f, 0.95f);
        RectTransform rt = panelUI.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(430f, 560f);

        Canvas cv = panelUI.AddComponent<Canvas>();
        cv.overrideSorting = true;
        cv.sortingOrder = 260;
        panelUI.AddComponent<GraphicRaycaster>();

        CrearTexto("Titulo", panelUI.transform, "MODO MULTIJUGADOR (UGS)",
            new Vector2(0, 250), new Vector2(380, 30), 20, Color.yellow);
        textoEstado = CrearTexto("Estado", panelUI.transform, "",
            new Vector2(0, 222), new Vector2(380, 20), 13, Color.cyan);

        Button b1 = CrearBoton("BtnGuardar", panelUI.transform, "Guardar defensa",
            new Vector2(-105, 190), new Vector2(190, 32), new Color(0.2f, 0.55f, 0.28f, 1f));
        b1.onClick.AddListener(GuardarDefensa);

        Button b2 = CrearBoton("BtnBuscar", panelUI.transform, "Buscar rival",
            new Vector2(105, 190), new Vector2(190, 32), new Color(0.2f, 0.4f, 0.75f, 1f));
        b2.onClick.AddListener(BuscarRival);

        botonAtacar = CrearBoton("BtnAtacar", panelUI.transform, "ATACAR",
            new Vector2(0, 150), new Vector2(390, 40), new Color(0.45f, 0.45f, 0.45f, 1f));
        botonAtacar.onClick.AddListener(Atacar);

        Button bResumen = CrearBoton("BtnResumen", panelUI.transform, "MI RESUMEN (bolsa + venganzas)",
            new Vector2(-100, 108), new Vector2(190, 30), new Color(0.35f, 0.35f, 0.55f, 1f));
        bResumen.onClick.AddListener(MiResumen);

        Button bRanking = CrearBoton("BtnRanking", panelUI.transform, "RANKING (trofeos)",
            new Vector2(100, 108), new Vector2(190, 30), new Color(0.55f, 0.45f, 0.15f, 1f));
        bRanking.onClick.AddListener(Ranking);

        textoResultado = CrearTexto("Resultado", panelUI.transform,
            "1) GUARDAR DEFENSA  2) BUSCAR RIVAL  3) ATACAR.\n4) NPC de práctica si no hay rivales.",
            new Vector2(0, -210), new Vector2(390, 90), 12, Color.white);
        textoResultado.alignment = TextAlignmentOptions.Top;

        Button cerrar = CrearBoton("BtnCerrar", panelUI.transform, "CERRAR (F8)",
            new Vector2(0, -262), new Vector2(150, 26), new Color(0.4f, 0.4f, 0.4f, 1f));
        cerrar.onClick.AddListener(Toggle);

        panelUI.SetActive(false);
    }

    // Arma DefensaPublica con las 5 cartas P1 de la escena (reutiliza RegistroDefensivo)
    Lab09.DefensaPublica ArmarDefensa()
    {
        var d = new Lab09.DefensaPublica { Nombre = string.IsNullOrEmpty(miAlias) ? "Jugador1" : miAlias, NivelCuenta = miNivel, Trofeos = 0 };
        RegistroDefensivo reg = RegistroDefensivo.DesdeCartas(d.Nombre, gestor.faccionP1, gestor.cartasPlayer1);
        if (reg != null)
            foreach (var p in reg.peleadores)
                d.Equipo.Add(new Lab09.HeroeDefensa { Id = p.nombre.ToLower().Replace(" ", "_"), Nombre = p.nombre, Nivel = 1 });
        return d;
    }

    TextMeshProUGUI CrearTexto(string nombre, Transform padre, string contenido, Vector2 pos, Vector2 size, int fs, Color c)
    {
        GameObject go = new GameObject(nombre);
        go.transform.SetParent(padre, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = contenido;
        tmp.fontSize = fs;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = c;
        if (tmp.font == null)
        {
            tmp.font = TMP_Settings.defaultFontAsset;
            if (tmp.font == null)
            {
                var fuentes = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
                if (fuentes != null && fuentes.Length > 0) tmp.font = fuentes[0];
            }
        }
        return tmp;
    }

    Button CrearBoton(string nombre, Transform padre, string texto, Vector2 pos, Vector2 size, Color c)
    {
        GameObject go = new GameObject(nombre);
        go.transform.SetParent(padre, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        Image img = go.AddComponent<Image>();
        img.color = c;
        Button btn = go.AddComponent<Button>();

        GameObject t = new GameObject("Texto");
        t.transform.SetParent(go.transform, false);
        RectTransform trt = t.AddComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;
        TextMeshProUGUI tmp = t.AddComponent<TextMeshProUGUI>();
        tmp.text = texto;
        tmp.fontSize = 13;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        if (tmp.font == null)
        {
            tmp.font = TMP_Settings.defaultFontAsset;
            if (tmp.font == null)
            {
                var fuentes = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
                if (fuentes != null && fuentes.Length > 0) tmp.font = fuentes[0];
            }
        }
        return btn;
    }
}
#endif
