using UnityEngine;

// Galería: crea un vacío "Galeria" en la escena, ponle este script
// y arrastra los 20 sprites. Vale para carta y campo toda la partida.
public class GaleriaArte : MonoBehaviour
{
    public static GaleriaArte Instance;

    [Header("Facción 1")]
    public Sprite Bufeador, Asesino, Lanza, Enano, Vampiro;
    [Header("Facción 2")]
    public Sprite Mago, Cazador, Hacha, Lancero, Nigromante;
    [Header("Facción 3")]
    public Sprite Alquimista, Pistolero, Escudero, Doshachas, Lobo;
    [Header("Facción 4")]
    public Sprite Cura, Arquero, Muneco, Espadachin, Draco;

    [Header("En campo (si vacío usa el de carta)")]
    public Sprite BufeadorCampo;
    public Sprite AsesinoCampo;
    public Sprite LanzaCampo;
    public Sprite EnanoCampo;
    public Sprite VampiroCampo;
    public Sprite MagoCampo;
    public Sprite CazadorCampo;
    public Sprite HachaCampo;
    public Sprite LanceroCampo;
    public Sprite NigromanteCampo;
    public Sprite AlquimistaCampo;
    public Sprite PistoleroCampo;
    public Sprite EscuderoCampo;
    public Sprite DoshachasCampo;
    public Sprite LoboCampo;
    public Sprite CuraCampo;
    public Sprite ArqueroCampo;
    public Sprite MunecoCampo;
    public Sprite EspadachinCampo;
    public Sprite DracoCampo;

    void Awake()
    {
        Instance = this;
    }

    public Sprite Get(string nombre, bool enCampo = false)
    {
        string c = DatosFacciones.Clave(nombre);
        if (string.IsNullOrEmpty(c)) return null;
        if (enCampo)
        {
            Sprite s = GetCampo(c);
            if (s != null) return s;
        }
        switch (c)
        {
            case "Bufeador": return Bufeador;
            case "Asesino": return Asesino;
            case "Lanza": return Lanza;
            case "Enano": return Enano;
            case "Vampiro": return Vampiro;
            case "Mago": return Mago;
            case "Cazador": return Cazador;
            case "Hacha": return Hacha;
            case "Lancero": return Lancero;
            case "Nigromante": return Nigromante;
            case "Alquimista": return Alquimista;
            case "Pistolero": return Pistolero;
            case "Escudero": return Escudero;
            case "Doshachas": return Doshachas;
            case "Lobo": return Lobo;
            case "Cura": return Cura;
            case "Arquero": return Arquero;
            case "Muneco": return Muneco;
            case "Espadachin": return Espadachin;
            case "Draco": return Draco;
        }
        return null;
    }

    Sprite GetCampo(string c)
    {
        switch (c)
        {
            case "Bufeador": return BufeadorCampo;
            case "Asesino": return AsesinoCampo;
            case "Lanza": return LanzaCampo;
            case "Enano": return EnanoCampo;
            case "Vampiro": return VampiroCampo;
            case "Mago": return MagoCampo;
            case "Cazador": return CazadorCampo;
            case "Hacha": return HachaCampo;
            case "Lancero": return LanceroCampo;
            case "Nigromante": return NigromanteCampo;
            case "Alquimista": return AlquimistaCampo;
            case "Pistolero": return PistoleroCampo;
            case "Escudero": return EscuderoCampo;
            case "Doshachas": return DoshachasCampo;
            case "Lobo": return LoboCampo;
            case "Cura": return CuraCampo;
            case "Arquero": return ArqueroCampo;
            case "Muneco": return MunecoCampo;
            case "Espadachin": return EspadachinCampo;
            case "Draco": return DracoCampo;
        }
        return null;
    }

    public static Sprite Buscar(string nombre, bool enCampo = false)
    {
        if (Instance != null)
        {
            Sprite s = Instance.Get(nombre, enCampo);
            if (s != null) return s;
        }
        return DatosFacciones.SpriteDe(nombre);
    }
}
