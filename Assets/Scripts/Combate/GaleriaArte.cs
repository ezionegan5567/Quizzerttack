using UnityEngine;

// Galería: crea un vacío "Galeria" en la escena, ponle este script
// y arrastra los 20 sprites. Vale para carta y campo toda la partida.
//
// "En campo" se separa por jugador/lado:
//   - Player 1 (izquierda): usa los campos "XxxCampo".
//   - Player 2 (derecha):  usa los campos "XxxCampoP2"; si están vacíos
//     se usa el del Player 1 y si tampoco hay, el de la carta.
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

    [Header("En campo Player 1 - izquierda (si vacío usa el de carta)")]
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

    [Header("En campo Player 2 - derecha (si vacío usa el del Player 1)")]
    public Sprite BufeadorCampoP2;
    public Sprite AsesinoCampoP2;
    public Sprite LanzaCampoP2;
    public Sprite EnanoCampoP2;
    public Sprite VampiroCampoP2;
    public Sprite MagoCampoP2;
    public Sprite CazadorCampoP2;
    public Sprite HachaCampoP2;
    public Sprite LanceroCampoP2;
    public Sprite NigromanteCampoP2;
    public Sprite AlquimistaCampoP2;
    public Sprite PistoleroCampoP2;
    public Sprite EscuderoCampoP2;
    public Sprite DoshachasCampoP2;
    public Sprite LoboCampoP2;
    public Sprite CuraCampoP2;
    public Sprite ArqueroCampoP2;
    public Sprite MunecoCampoP2;
    public Sprite EspadachinCampoP2;
    public Sprite DracoCampoP2;

    void Awake()
    {
        Instance = this;
    }

    // jugador: 1 = Player 1 (lado izquierdo), 2 = Player 2 (lado derecho)
    public Sprite Get(string nombre, bool enCampo = false, int jugador = 1)
    {
        string c = DatosFacciones.Clave(nombre);
        if (string.IsNullOrEmpty(c)) return null;
        if (enCampo)
        {
            Sprite s = GetCampo(c, jugador);
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

    Sprite GetCampo(string c, int jugador)
    {
        // Player 2 (derecha): primero su sprite; si está vacío, el del Player 1.
        if (jugador == 2)
        {
            Sprite p2 = GetCampoDe(c, true);
            if (p2 != null) return p2;
        }
        return GetCampoDe(c, false);
    }

    Sprite GetCampoDe(string c, bool player2)
    {
        switch (c)
        {
            case "Bufeador": return player2 ? BufeadorCampoP2 : BufeadorCampo;
            case "Asesino": return player2 ? AsesinoCampoP2 : AsesinoCampo;
            case "Lanza": return player2 ? LanzaCampoP2 : LanzaCampo;
            case "Enano": return player2 ? EnanoCampoP2 : EnanoCampo;
            case "Vampiro": return player2 ? VampiroCampoP2 : VampiroCampo;
            case "Mago": return player2 ? MagoCampoP2 : MagoCampo;
            case "Cazador": return player2 ? CazadorCampoP2 : CazadorCampo;
            case "Hacha": return player2 ? HachaCampoP2 : HachaCampo;
            case "Lancero": return player2 ? LanceroCampoP2 : LanceroCampo;
            case "Nigromante": return player2 ? NigromanteCampoP2 : NigromanteCampo;
            case "Alquimista": return player2 ? AlquimistaCampoP2 : AlquimistaCampo;
            case "Pistolero": return player2 ? PistoleroCampoP2 : PistoleroCampo;
            case "Escudero": return player2 ? EscuderoCampoP2 : EscuderoCampo;
            case "Doshachas": return player2 ? DoshachasCampoP2 : DoshachasCampo;
            case "Lobo": return player2 ? LoboCampoP2 : LoboCampo;
            case "Cura": return player2 ? CuraCampoP2 : CuraCampo;
            case "Arquero": return player2 ? ArqueroCampoP2 : ArqueroCampo;
            case "Muneco": return player2 ? MunecoCampoP2 : MunecoCampo;
            case "Espadachin": return player2 ? EspadachinCampoP2 : EspadachinCampo;
            case "Draco": return player2 ? DracoCampoP2 : DracoCampo;
        }
        return null;
    }

    public static Sprite Buscar(string nombre, bool enCampo = false, int jugador = 1)
    {
        if (Instance != null)
        {
            Sprite s = Instance.Get(nombre, enCampo, jugador);
            if (s != null) return s;
        }
        return DatosFacciones.SpriteDe(nombre);
    }
}
