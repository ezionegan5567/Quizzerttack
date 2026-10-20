using UnityEngine;

public enum ObjetivoH { Ninguno, Enemigo, Enemigos, Aliado, Aliados, Propio }

public enum EfectoH
{
    Ninguno,
    Dano, DanoTodos, DanoDos,
    Veneno, Sangrado, Quemadura, Maldicion,
    Curar, CurarZona, HotZona,
    BuffAtk, BuffAtkZona, BuffDef, BuffDefZona,
    Velocidad, VelocidadZona, Punteria, Lentitud, Intimidar,
    Ceguera, Paralisis, Incapacitar, Invisible, Provocar,
    Inmune, InmuneZona, Impedir, Proteger, Purificar,
    MarcaPolvora, Reflejo, Confusion,
    ContadorDevolver, ContadorSangre
}

[System.Serializable]
public struct InfoHabilidad
{
    public string nombre;
    public EfectoH efecto;
    public ObjetivoH objetivo;
    public int valor;
    public int duracion;
    public EfectoH efecto2;
    public ObjetivoH objetivo2;
    public int valor2;
    public int duracion2;
    public float robo;
    public bool fuego;

    public InfoHabilidad(string n, EfectoH e, ObjetivoH o, int v = 0, int d = 0,
        EfectoH e2 = EfectoH.Ninguno, ObjetivoH o2 = ObjetivoH.Ninguno, int v2 = 0, int d2 = 0,
        float r = 0f, bool f = false)
    {
        nombre = n; efecto = e; objetivo = o; valor = v; duracion = d;
        efecto2 = e2; objetivo2 = o2; valor2 = v2; duracion2 = d2;
        robo = r; fuego = f;
    }
}

public class DatosPeleador
{
    public string display;
    public InfoHabilidad[] habs;
    public EfectoH pasiva;
    public DatosPeleador(string d, InfoHabilidad[] h, EfectoH p = EfectoH.Ninguno)
    { display = d; habs = h; pasiva = p; }
}

public static class DatosFacciones
{
    static InfoHabilidad H(string n, EfectoH e, ObjetivoH o, int v = 0, int d = 0,
        EfectoH e2 = EfectoH.Ninguno, ObjetivoH o2 = ObjetivoH.Ninguno, int v2 = 0, int d2 = 0,
        float r = 0f, bool f = false)
    { return new InfoHabilidad(n, e, o, v, d, e2, o2, v2, d2, r, f); }

    static readonly System.Collections.Generic.Dictionary<string, DatosPeleador> datos =
        new System.Collections.Generic.Dictionary<string, DatosPeleador>()
    {
        // FACCION 1
        { "bufeador", new DatosPeleador("Bufeador", new InfoHabilidad[]{
            H("Velocidad aumentada", EfectoH.VelocidadZona, ObjetivoH.Aliados, 10, 2),
            H("Campo curativo", EfectoH.HotZona, ObjetivoH.Aliados, 12, 2),
            H("Aumento de ataque", EfectoH.BuffAtkZona, ObjetivoH.Aliados, 25, 2),
            H("Aumento de defensa", EfectoH.BuffDefZona, ObjetivoH.Aliados, 30, 2),
        })},
        { "asesino", new DatosPeleador("Asesino", new InfoHabilidad[]{
            H("Lluvia de dagas", EfectoH.DanoTodos, ObjetivoH.Enemigos, 18),
            H("Ceguera", EfectoH.Ceguera, ObjetivoH.Enemigo, 50, 1),
            H("Cuchillo letal", EfectoH.Dano, ObjetivoH.Enemigo, 12, 0, EfectoH.Sangrado, ObjetivoH.Enemigo, 8, 2),
            H("Cuchillo veneno", EfectoH.Dano, ObjetivoH.Enemigo, 10, 0, EfectoH.Veneno, ObjetivoH.Enemigo, 6, 4),
        })},
        { "lanza", new DatosPeleador("Lanza con escudo", new InfoHabilidad[]{
            H("Provocar", EfectoH.Provocar, ObjetivoH.Propio, 0, 2),
            H("Endurecimiento", EfectoH.BuffDef, ObjetivoH.Propio, 50, 1),
            H("Perforacion y devolucion", EfectoH.Dano, ObjetivoH.Enemigo, 20),
            H("Escudo para todos", EfectoH.BuffDefZona, ObjetivoH.Aliados, 25, 2),
        }, EfectoH.ContadorDevolver)},
        { "enano", new DatosPeleador("Enano", new InfoHabilidad[]{
            H("Golpe martillo", EfectoH.Dano, ObjetivoH.Enemigo, 26),
            H("Mejora de armas", EfectoH.BuffAtkZona, ObjetivoH.Aliados, 20, 3),
            H("Insulto", EfectoH.BuffAtk, ObjetivoH.Propio, 20, 2, EfectoH.Ceguera, ObjetivoH.Enemigos, 25, 2),
            H("Cerveza para todos", EfectoH.CurarZona, ObjetivoH.Aliados, 15, 0, EfectoH.VelocidadZona, ObjetivoH.Aliados, 10, 2),
        })},
        { "vampiro", new DatosPeleador("Vampiro", new InfoHabilidad[]{
            H("Mordida", EfectoH.Dano, ObjetivoH.Enemigo, 24, 0, EfectoH.Ninguno, ObjetivoH.Ninguno, 0, 0, 0.25f),
            H("Ataque garra", EfectoH.Dano, ObjetivoH.Enemigo, 26),
            H("Intimidar", EfectoH.Intimidar, ObjetivoH.Enemigo, 25, 2),
            H("Ataque sangre", EfectoH.Dano, ObjetivoH.Enemigo, 20),
        }, EfectoH.ContadorSangre)},
        // FACCION 2
        { "mago", new DatosPeleador("Mago", new InfoHabilidad[]{
            H("Curacion en area", EfectoH.CurarZona, ObjetivoH.Aliados, 25),
            H("Campo electrico", EfectoH.DanoTodos, ObjetivoH.Enemigos, 14),
            H("Bola de fuego", EfectoH.Dano, ObjetivoH.Enemigo, 30, 0, EfectoH.Ninguno, ObjetivoH.Ninguno, 0, 0, 0f, true),
            H("Paralisis", EfectoH.Paralisis, ObjetivoH.Enemigo, 0, 2),
        })},
        { "cazador", new DatosPeleador("Cazador", new InfoHabilidad[]{
            H("Disparo", EfectoH.Dano, ObjetivoH.Enemigo, 28),
            H("Ojo de halcon", EfectoH.BuffAtk, ObjetivoH.Propio, 30, 2, EfectoH.Punteria, ObjetivoH.Propio, 0, 2),
            H("Disparos multiples", EfectoH.DanoTodos, ObjetivoH.Enemigos, 16),
            H("Bano de polvora", EfectoH.Dano, ObjetivoH.Enemigo, 5, 0, EfectoH.MarcaPolvora, ObjetivoH.Enemigo, 0, 3),
        })},
        { "hacha", new DatosPeleador("Guerrero hacha", new InfoHabilidad[]{
            H("Ataque", EfectoH.Dano, ObjetivoH.Enemigo, 30),
            H("Grito de guerra", EfectoH.BuffAtkZona, ObjetivoH.Aliados, 20, 2),
            H("Aguante", EfectoH.Curar, ObjetivoH.Propio, 25),
            H("Lanza hacha", EfectoH.Dano, ObjetivoH.Enemigo, 24),
        })},
        { "lancero", new DatosPeleador("Lancero", new InfoHabilidad[]{
            H("Ataque lanza", EfectoH.Dano, ObjetivoH.Enemigo, 24),
            H("Impedir", EfectoH.Impedir, ObjetivoH.Aliado, 0, 2),
            H("Agilidad", EfectoH.Velocidad, ObjetivoH.Propio, 10, 2, EfectoH.Punteria, ObjetivoH.Propio, 0, 2),
            H("Perforacion", EfectoH.Dano, ObjetivoH.Enemigo, 16, 0, EfectoH.Sangrado, ObjetivoH.Enemigo, 8, 2),
        })},
        { "nigromante", new DatosPeleador("Nigromante", new InfoHabilidad[]{
            H("Magia negra", EfectoH.Dano, ObjetivoH.Enemigo, 26),
            H("Maldicion", EfectoH.Maldicion, ObjetivoH.Enemigo, 8, 3),
            H("Invisible", EfectoH.Invisible, ObjetivoH.Propio, 0, 3),
            H("Magia negra II", EfectoH.Dano, ObjetivoH.Enemigo, 22),
        })},
        // FACCION 3
        { "alquimista", new DatosPeleador("Alquimista", new InfoHabilidad[]{
            H("Recuperacion", EfectoH.Curar, ObjetivoH.Aliado, 30),
            H("Veneno", EfectoH.Dano, ObjetivoH.Enemigo, 8, 0, EfectoH.Veneno, ObjetivoH.Enemigo, 7, 2),
            H("Invisible", EfectoH.Invisible, ObjetivoH.Propio, 0, 2),
            H("Viscosidad", EfectoH.Lentitud, ObjetivoH.Enemigo, 15, 3),
        })},
        { "pistolero", new DatosPeleador("Pistolero", new InfoHabilidad[]{
            H("Disparo", EfectoH.Dano, ObjetivoH.Enemigo, 28),
            H("Ojo de aguila", EfectoH.BuffAtk, ObjetivoH.Propio, 20, 2, EfectoH.Punteria, ObjetivoH.Propio, 0, 2),
            H("Disparo multiple", EfectoH.DanoTodos, ObjetivoH.Enemigos, 16),
            H("Bala incineradora", EfectoH.Dano, ObjetivoH.Enemigo, 18, 0, EfectoH.Quemadura, ObjetivoH.Enemigo, 7, 2, 0f, true),
        })},
        { "escudero", new DatosPeleador("Escudero", new InfoHabilidad[]{
            H("Provocar", EfectoH.Provocar, ObjetivoH.Propio, 0, 2),
            H("Resistencia", EfectoH.BuffDef, ObjetivoH.Propio, 40, 2),
            H("Escudo para todos", EfectoH.BuffDefZona, ObjetivoH.Aliados, 25, 2),
            H("Resistir ante todo", EfectoH.Inmune, ObjetivoH.Propio, 0, 1),
        })},
        { "doshachas", new DatosPeleador("Guerrero dos hachas", new InfoHabilidad[]{
            H("Ataque hacha", EfectoH.Dano, ObjetivoH.Enemigo, 26),
            H("Agilidad", EfectoH.Velocidad, ObjetivoH.Propio, 10, 2, EfectoH.Punteria, ObjetivoH.Propio, 0, 2),
            H("Lanzar hachas", EfectoH.DanoDos, ObjetivoH.Enemigo, 16),
            H("Motivar", EfectoH.CurarZona, ObjetivoH.Aliados, 12),
        })},
        { "lobo", new DatosPeleador("Hombre lobo", new InfoHabilidad[]{
            H("Mordisco", EfectoH.Dano, ObjetivoH.Enemigo, 24),
            H("Garras", EfectoH.Dano, ObjetivoH.Enemigo, 16, 0, EfectoH.Sangrado, ObjetivoH.Enemigo, 8, 3),
            H("Aullido", EfectoH.BuffAtk, ObjetivoH.Propio, 100, 2),
            H("Proteger", EfectoH.Proteger, ObjetivoH.Aliado, 0, 99),
        })},
        // FACCION 4
        { "cura", new DatosPeleador("Curandero", new InfoHabilidad[]{
            H("Curacion en area", EfectoH.CurarZona, ObjetivoH.Aliados, 25),
            H("Purificar", EfectoH.Purificar, ObjetivoH.Aliados, 0, 0),
            H("Campo de muro", EfectoH.InmuneZona, ObjetivoH.Aliados, 0, 1),
            H("Incapacitar", EfectoH.Incapacitar, ObjetivoH.Enemigo, 0, 2),
        })},
        { "arquero", new DatosPeleador("Arquero", new InfoHabilidad[]{
            H("Flechas venenosas", EfectoH.Dano, ObjetivoH.Enemigo, 14, 0, EfectoH.Veneno, ObjetivoH.Enemigo, 6, 3),
            H("Lluvia de flechas", EfectoH.DanoTodos, ObjetivoH.Enemigos, 16),
            H("Flechas", EfectoH.Dano, ObjetivoH.Enemigo, 28),
            H("Respiro", EfectoH.Curar, ObjetivoH.Propio, 15, 0, EfectoH.BuffAtk, ObjetivoH.Propio, 15, 2),
        })},
        { "muneco", new DatosPeleador("Muneco poseido", new InfoHabilidad[]{
            H("Intimidar", EfectoH.Intimidar, ObjetivoH.Enemigo, 25, 2),
            H("Maldicion", EfectoH.Maldicion, ObjetivoH.Enemigo, 8, 3),
            H("Senuelo", EfectoH.Reflejo, ObjetivoH.Propio, 50, 3),
            H("Confusion", EfectoH.Confusion, ObjetivoH.Enemigo, 50, 2),
        })},
        { "espadachin", new DatosPeleador("Espadachin", new InfoHabilidad[]{
            H("Ataque", EfectoH.Dano, ObjetivoH.Enemigo, 26),
            H("Lluvia de espadas", EfectoH.DanoTodos, ObjetivoH.Enemigos, 15),
            H("Resistencia", EfectoH.BuffDef, ObjetivoH.Propio, 30, 2),
            H("Provocar", EfectoH.Provocar, ObjetivoH.Propio, 0, 2),
        })},
        { "draco", new DatosPeleador("Draconiano", new InfoHabilidad[]{
            H("Llamarada", EfectoH.DanoTodos, ObjetivoH.Enemigos, 16, 0, EfectoH.Quemadura, ObjetivoH.Enemigos, 6, 2, 0f, true),
            H("Garras", EfectoH.Dano, ObjetivoH.Enemigo, 18, 0, EfectoH.Sangrado, ObjetivoH.Enemigo, 8, 2),
            H("Endurecimiento", EfectoH.BuffDef, ObjetivoH.Propio, 50, 1),
            H("Doble golpe", EfectoH.Dano, ObjetivoH.Enemigo, 20, 0, EfectoH.Dano, ObjetivoH.Enemigo, 20, 0),
        })},
    };

    static readonly System.Collections.Generic.Dictionary<string, int[]> stats =
        new System.Collections.Generic.Dictionary<string, int[]>()
    {
        { "bufeador", new int[]{ 90, 15 } }, { "asesino", new int[]{ 80, 32 } },
        { "lanza", new int[]{ 150, 16 } }, { "enano", new int[]{ 125, 24 } },
        { "vampiro", new int[]{ 110, 28 } }, { "mago", new int[]{ 90, 20 } },
        { "cazador", new int[]{ 85, 30 } }, { "hacha", new int[]{ 115, 30 } },
        { "lancero", new int[]{ 100, 24 } }, { "nigromante", new int[]{ 90, 22 } },
        { "alquimista", new int[]{ 90, 18 } }, { "pistolero", new int[]{ 85, 30 } },
        { "escudero", new int[]{ 150, 14 } }, { "doshachas", new int[]{ 100, 28 } },
        { "lobo", new int[]{ 110, 28 } }, { "cura", new int[]{ 95, 16 } },
        { "arquero", new int[]{ 85, 28 } }, { "muneco", new int[]{ 100, 20 } },
        { "espadachin", new int[]{ 105, 26 } }, { "draco", new int[]{ 120, 30 } },
    };

    public static int[] GetStats(string nombre)
    {
        if (string.IsNullOrEmpty(nombre)) return null;
        string n = nombre.ToLower();
        if (n.Contains("lancer")) return stats["lancero"];
        if (n.Contains("dos") && n.Contains("hacha")) return stats["doshachas"];
        if (n.Contains("muñec") || n.Contains("munec") || n.Contains("poseido")) return stats["muneco"];
        if (n.Contains("escudero")) return stats["escudero"];
        foreach (var kv in stats)
            if (n.Contains(kv.Key)) return kv.Value;
        if ((n.Contains("lanza") && n.Contains("escudo"))) return stats["lanza"];
        return null;
    }

    public static string Clave(string nombre)
    {
        if (string.IsNullOrEmpty(nombre)) return null;
        string n = nombre.ToLower();
        if (n.Contains("bufe")) return "Bufeador";
        if (n.Contains("asesino")) return "Asesino";
        if ((n.Contains("lanza") && n.Contains("escudo")) || n.Contains("lanzaesc")) return "Lanza";
        if (n.Contains("lancer")) return "Lancero";
        if (n.Contains("enano")) return "Enano";
        if (n.Contains("vampir")) return "Vampiro";
        if (n.Contains("mago")) return "Mago";
        if (n.Contains("cazador") || n.Contains("rifle")) return "Cazador";
        if (n.Contains("dos") && n.Contains("hacha")) return "Doshachas";
        if (n.Contains("hacha")) return "Hacha";
        if (n.Contains("nigrom")) return "Nigromante";
        if (n.Contains("alquim")) return "Alquimista";
        if (n.Contains("pistol")) return "Pistolero";
        if (n.Contains("escudero")) return "Escudero";
        if (n.Contains("lobo")) return "Lobo";
        if (n.Contains("cur")) return "Cura";
        if (n.Contains("arquer")) return "Arquero";
        if (n.Contains("muñec") || n.Contains("munec") || n.Contains("poseido")) return "Muneco";
        if (n.Contains("espadach")) return "Espadachin";
        if (n.Contains("drac")) return "Draco";
        return null;
    }

    public static Sprite SpriteDe(string nombre)
    {
        string c = Clave(nombre);
        if (string.IsNullOrEmpty(c)) return null;
        return Resources.Load<Sprite>("Arte/" + c);
    }

    public static DatosPeleador Get(string nombre)
    {
        if (string.IsNullOrEmpty(nombre)) return null;
        string n = nombre.ToLower();
        if (n.Contains("bufe")) return datos["bufeador"];
        if (n.Contains("asesino")) return datos["asesino"];
        if ((n.Contains("lanza") && n.Contains("escudo")) || n.Contains("lanzaesc")) return datos["lanza"];
        if (n.Contains("enano")) return datos["enano"];
        if (n.Contains("vampir")) return datos["vampiro"];
        if (n.Contains("mago")) return datos["mago"];
        if (n.Contains("cazador") || n.Contains("rifle")) return datos["cazador"];
        if (n.Contains("dos") && n.Contains("hacha")) return datos["doshachas"];
        if (n.Contains("hacha")) return datos["hacha"];
        if (n.Contains("lancer")) return datos["lancero"];
        if (n.Contains("nigrom")) return datos["nigromante"];
        if (n.Contains("alquim")) return datos["alquimista"];
        if (n.Contains("pistol")) return datos["pistolero"];
        if (n.Contains("escudero")) return datos["escudero"];
        if (n.Contains("escudero")) return datos["escudero"];
        if (n.Contains("lobo")) return datos["lobo"];
        if (n.Contains("cur")) return datos["cura"];
        if (n.Contains("arquer")) return datos["arquero"];
        if (n.Contains("muñec") || n.Contains("munec") || n.Contains("poseido")) return datos["muneco"];
        if (n.Contains("espadach")) return datos["espadachin"];
        if (n.Contains("drac")) return datos["draco"];
        return null;
    }
}
