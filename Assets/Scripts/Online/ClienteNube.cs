using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

// ============ ONLINE: CLIENTE DE LA BASE DE DATOS COMPARTIDA ============
// Habla con Firebase Realtime Database por REST (UnityWebRequest, sin SDK).
// URL se pega en el Inspector (PASO 4 del plan). P2 slots:
//   PUT  {url}/registros/{id}.json   (publicar mi escuadrón)
//   GET  {url}/registros.json        (lista de registros públicos)

public class ClienteNube : MonoBehaviour
{
    static ClienteNube inst;
    public static ClienteNube Instance { get { return inst; } }

    [Header("PASO 4: pega aquí la URL de tu Firebase RTDB (sin / final)")]
    [TextArea(2, 3)] public string urlBase = "https://TU-PROYECTO-default-rtdb.firebaseio.com";

    [Header("Todos pueden leer/definir registro (según reglas)")]
    public int timeoutSegundos = 10;

    void Awake()
    {
        inst = this;
    }

    bool UrlValida()
    {
        if (string.IsNullOrEmpty(urlBase)) return false;
        if (!urlBase.StartsWith("http")) return false;
        if (urlBase.Contains("TU-PROYECTO")) return false;
        return true;
    }

    public void SubirRegistro(RegistroDefensivo reg, Action<bool> termino = null)
    {
        StartCoroutine(RutinaSubir(reg, termino));
    }

    IEnumerator RutinaSubir(RegistroDefensivo reg, Action<bool> termino)
    {
        if (reg == null || !UrlValida())
        {
            Debug.LogWarning("[Nube] Subida saltada: URL no configurada aún (PASO 4).");
            if (termino != null) termino(false);
            yield break;
        }
        string url = urlBase.TrimEnd('/') + "/registros/" + reg.id + ".json";
        UnityWebRequest req = new UnityWebRequest(url, "PUT");
        req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(reg.ToJson()));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json; charset=UTF-8");
        req.timeout = timeoutSegundos;
        yield return req.SendWebRequest();
        bool ok = req.result == UnityWebRequest.Result.Success;
        if (ok) Debug.Log("[Nube] Registro publicado: " + reg.jugador + " (" + reg.id + ")");
        else Debug.LogWarning("[Nube] Error subiendo: " + req.error + " " + (req.downloadHandler != null ? req.downloadHandler.text : ""));
        if (termino != null) termino(ok);
        req.Dispose();
    }

    public void DescargarRegistros(Action<List<RegistroDefensivo>> termino = null)
    {
        StartCoroutine(RutinaDescargar(termino));
    }

    IEnumerator RutinaDescargar(Action<List<RegistroDefensivo>> termino)
    {
        var vacia = new List<RegistroDefensivo>();
        if (!UrlValida())
        {
            Debug.LogWarning("[Nube] Descarga saltada: URL no configurada aún (PASO 4).");
            if (termino != null) termino(vacia);
            yield break;
        }
        string url = urlBase.TrimEnd('/') + "/registros.json";
        using (UnityWebRequest req = UnityWebRequest.Get(url))
        {
            req.timeout = timeoutSegundos;
            yield return req.SendWebRequest();
            List<RegistroDefensivo> lista = vacia;
            if (req.result == UnityWebRequest.Result.Success)
            {
                lista = RegistroDefensivo.ListaDesdeJson(req.downloadHandler.text);
                Debug.Log("[Nube] Registros descargados: " + lista.Count);
            }
            else Debug.LogWarning("[Nube] Error descargando: " + req.error);
            if (termino != null) termino(lista);
        }
    }
}
