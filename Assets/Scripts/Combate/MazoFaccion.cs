using UnityEngine;
using System.Collections.Generic;

// Cargador de mazos: pones F1-F4 (o Random) y llena las 5 cartas
// con los 5 peleadores de esa facción (nombre, stats, facción).
public class MazoFaccion : MonoBehaviour
{
    [Header("Elige: F1, F2, F3, F4 o Random")]
    public string faccionP1 = "F1";
    public string faccionP2 = "F2";

    [Header("Grupos de cartas")]
    public GameObject[] cartasP1;
    public GameObject[] cartasP2;

    static readonly Dictionary<string, string[]> roster = new Dictionary<string, string[]>()
    {
        { "F1", new string[]{ "Bufeador", "Asesino", "Lanza con escudo", "Enano", "Vampiro" } },
        { "F2", new string[]{ "Mago", "Cazador", "Guerrero del hacha", "Lancero", "Nigromante" } },
        { "F3", new string[]{ "Alquimista", "Pistolero", "Escudero", "Guerrero dos hachas", "Hombre lobo" } },
        { "F4", new string[]{ "Curandero", "Arquero", "Muneco poseido", "Espadachin", "Draconiano" } },
    };

    public static string[] FaccionesDisponibles() { return new string[]{ "F1", "F2", "F3", "F4", "Random" }; }

    void Awake()
    {
        CargarMazo(cartasP1, Resolver(faccionP1), 1);
        CargarMazo(cartasP2, Resolver(faccionP2), 2);
    }

    string Resolver(string f)
    {
        if (string.IsNullOrEmpty(f)) return "F1";
        f = f.Trim();
        if (f.Equals("Random", System.StringComparison.OrdinalIgnoreCase))
        {
            string[] keys = new string[] { "F1", "F2", "F3", "F4" };
            return keys[Random.Range(0, keys.Length)];
        }
        f = f.ToUpper();
        if (!roster.ContainsKey(f)) return "F1";
        return f;
    }

    void CargarMazo(GameObject[] cartas, string faccion, int dueno)
    {
        if (cartas == null || !roster.ContainsKey(faccion)) return;
        string[] nombres = roster[faccion];
        for (int i = 0; i < cartas.Length && i < nombres.Length; i++)
        {
            if (cartas[i] == null) continue;
            CartaSeleccion c = cartas[i].GetComponent<CartaSeleccion>();
            if (c == null) continue;
            c.ReiniciarCarta();
            c.nombreHeroe = nombres[i];
            int[] st = DatosFacciones.GetStats(nombres[i]);
            if (st != null) { c.vida = st[0]; c.ataque = st[1]; }
            c.jugadorDueno = dueno;
            c.faccion = faccion;
            Debug.Log($"Carta {cartas[i].name} = {nombres[i]} ({faccion} P{dueno})");
        }
    }
}
