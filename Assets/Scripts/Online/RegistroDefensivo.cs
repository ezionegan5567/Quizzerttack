using System;
using System.Collections.Generic;
using UnityEngine;

// ============ ONLINE: DATOS DEL ESCUADRON DEFENSIVO ============
// Registro que se guarda/lee de la nube: nombre de jugador + faccion
// + sus 5 peleadores con stats. No depende de MonoBehaviour.

[Serializable]
public class PeleadorRegistro
{
    public string nombre = "";
    public int vida = 0;
    public int ataque = 0;
}

[Serializable]
public class RegistroDefensivo
{
    public string id = "";
    public string jugador = "Anonimo";
    public string faccion = "F1";
    public long fecha = 0;
    public List<PeleadorRegistro> peleadores = new List<PeleadorRegistro>();

    public string ToJson()
    {
        return JsonUtility.ToJson(this);
    }

    [Serializable]
    class ListaJson { public List<RegistroDefensivo> l = new List<RegistroDefensivo>(); }

    // Firebase RTDB devuelve MAPA {id:{...}, id2:{...}}: se corta a objetos
    // por balance de llaves y se parsea cada uno con JsonUtility.
    public static List<RegistroDefensivo> ListaDesdeJson(string json)
    {
        var lista = new List<RegistroDefensivo>();
        if (string.IsNullOrEmpty(json) || json == "null") return lista;
        json = json.Trim();
        if (!json.StartsWith("{"))
        {
            try
            {
                ListaJson l = JsonUtility.FromJson<ListaJson>(json);
                if (l != null && l.l != null) lista.AddRange(l.l);
            }
            catch { }
            return lista;
        }
        foreach (string trozo in CortarObjetosTop(json))
        {
            RegistroDefensivo r = null;
            try { r = JsonUtility.FromJson<RegistroDefensivo>(trozo); } catch { }
            if (r != null && !string.IsNullOrEmpty(r.jugador)) lista.Add(r);
        }
        return lista;
    }

    static List<string> CortarObjetosTop(string json)
    {
        var res = new List<string>();
        int depth = 0;
        bool enCadena = false;
        bool escape = false;
        int inicio = -1;
        bool esperandoValor = false;
        for (int i = 0; i < json.Length; i++)
        {
            char c = json[i];
            if (enCadena)
            {
                if (escape) escape = false;
                else if (c == '\\') escape = true;
                else if (c == '"') enCadena = false;
                continue;
            }
            if (c == '"') { enCadena = true; continue; }
            if (c == '{')
            {
                if (depth == 1 && esperandoValor) { inicio = i; depth = 2; esperandoValor = false; }
                else depth++;
                continue;
            }
            if (c == '}')
            {
                if (depth == 2 && inicio >= 0)
                {
                    res.Add(json.Substring(inicio, i - inicio + 1));
                    inicio = -1; depth--;
                }
                else if (depth > 0) depth--;
                continue;
            }
            if (depth == 1 && c == ':') esperandoValor = true;
        }
        return res;
    }

    // Construye el registro desde las 5 cartas P1 de tu escena actual.
    public static RegistroDefensivo DesdeCartas(string jugador, string faccion, GameObject[] cartas)
    {
        string n = Sanear(jugador);
        var r = new RegistroDefensivo
        {
            id = n.ToLower(),
            jugador = n,
            faccion = string.IsNullOrEmpty(faccion) ? "F1" : faccion.Trim().ToUpper(),
            fecha = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };
        if (cartas != null)
            foreach (GameObject go in cartas)
            {
                if (go == null) continue;
                CartaSeleccion c = go.GetComponent<CartaSeleccion>();
                if (c == null) continue;
                r.peleadores.Add(new PeleadorRegistro
                {
                    nombre = string.IsNullOrEmpty(c.nombreHeroe) ? "Misterio" : c.nombreHeroe,
                    vida = c.vida,
                    ataque = c.ataque
                });
            }
        return r;
    }

    public static string Sanear(string s)
    {
        if (string.IsNullOrEmpty(s)) return "Anonimo";
        string r = s.Replace("\"", "'").Replace("\\", "")
            .Replace("{", "").Replace("}", "").Trim();
        if (r.Length == 0) return "Anonimo";
        return r.Length > 24 ? r.Substring(0, 24) : r;
    }
}
