namespace QuizzerMatch;

// Mismos nombres que EfectoH / ObjetivoH de Assets/Scripts/Combate/DatosFacciones.cs.
// El catálogo (generado) conserva los 80 datos; el motor sólo implementa un subconjunto
// (ver Reglas.EfectosSoportados). Lo demás se rechaza con un mensaje claro, no se ignora en silencio.
public enum Efecto
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

public enum Objetivo { Ninguno, Enemigo, Enemigos, Aliado, Aliados, Propio }

public record Habilidad(
    string Nombre, Efecto Efecto, Objetivo Objetivo, int Valor, int Duracion,
    Efecto Efecto2, Objetivo Objetivo2, int Valor2, int Duracion2);

public record HeroeDef(string Id, string Nombre, string Faccion, int Vida, int Ataque, Habilidad[] Habilidades);

public record PreguntaDef(int Id, string Texto, string[] Opciones, int Correcta);

/// <summary>Héroes y facciones. Los datos salen de Catalogo.Generado.cs.</summary>
public static partial class Catalogo
{
    public static readonly string[] Facciones = { "F1", "F2", "F3", "F4" };

    private static readonly Dictionary<string, HeroeDef> s_Heroes = CargarTodos();

    static partial void Cargar(List<HeroeDef> destino);

    private static Dictionary<string, HeroeDef> CargarTodos()
    {
        var lista = new List<HeroeDef>();
        Cargar(lista);
        return lista.ToDictionary(h => h.Id);
    }

    public static IReadOnlyCollection<HeroeDef> Todos => s_Heroes.Values;

    public static HeroeDef? Buscar(string id) => s_Heroes.TryGetValue(id, out var h) ? h : null;

    public static HeroeDef Heroe(string id) =>
        Buscar(id) ?? throw new KeyNotFoundException($"Héroe desconocido: {id}");

    /// <summary>Los 5 héroes de una facción, en el orden de MazoFaccion.</summary>
    public static IReadOnlyList<HeroeDef> DeFaccion(string faccion) =>
        s_Heroes.Values.Where(h => h.Faccion == faccion).ToList();
}

/// <summary>
/// El banco de preguntas vive en el servidor. El cliente sólo recibe el texto y las
/// opciones; el índice correcto nunca sale de aquí antes de que el jugador conteste.
/// </summary>
public static partial class BancoPreguntas
{
    private static readonly List<PreguntaDef> s_Todas = CargarTodas();

    static partial void Cargar(List<PreguntaDef> destino);

    private static List<PreguntaDef> CargarTodas()
    {
        var lista = new List<PreguntaDef>();
        Cargar(lista);
        return lista;
    }

    public static int Cantidad => s_Todas.Count;

    public static PreguntaDef Obtener(int id) => s_Todas[id];
}
