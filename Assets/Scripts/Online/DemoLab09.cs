// ============ LAB09: DEMO DE VERIFICACIÓN (requiere UGS_ON) ============
// Corre TODO el flujo de la guía en una pantalla para tus capturas:
//  PASO 1-2: publico mi defensa → cambio de perfil → leo la ajena → 403 verificado
//  PASO 3-5: BuscarRival con franja ±2→paso 2→tope 10 + tabla de consultas/lecturas
//  PASO 6:   ResolverCombate contra el rival encontrado
// Montaje: GameObject "Registros" → Add Component → DemoLab09. Corre con F10.

#if UGS_ON
using System.Collections.Generic;
using Lab09;
using UnityEngine;

public class DemoLab09 : MonoBehaviour
{
    public string miAlias = "seijer";
    public int miNivel = 20;           // nivel 20 = el ejemplo de la guía
    public string aliasOtro = "pepe";  // la "segunda computadora" emulada
    public int nivelOtro = 19;         // franja inicial ±2 lo incluye
    public int franjaMaxima = 10;
    public int candidatosSuficientes = 2;

    bool corriendo;

    void Start()
    {
        // Auto-montaje del modo online real (F8) si no existe todavía
        if (FindFirstObjectByType<PanelRival>() == null)
            gameObject.AddComponent<PanelRival>();
    }

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
    void Update()
    {
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null && kb.f10Key.wasPressedThisFrame) Correr();
    }
#else
    void Update() { if (Input.GetKeyDown(KeyCode.F10)) Correr(); }
#endif

    [ContextMenu("Correr demo")]
    public async void Correr()
    {
        if (corriendo)
        {
            Debug.Log("[LAB09] La demo YA está corriendo (ignorada esta pulsación).");
            return;
        }
        corriendo = true;
        string paso = "inicio";
        try
        {
            // PASO 1: mi cuenta + mi defensa pública + mi inventario PROTEGIDO
            paso = "1 IniciarAsync";
            await UgsDefensa.IniciarAsync("principal");
            paso = "1 SondaAsync";
            await UgsRival.SondaAsync();
            var gestor = Object.FindFirstObjectByType<GestorTurnos>();
            var defensa = CrearDefensaDesdeEscena(gestor, miAlias, miNivel);
            paso = "1 PublicarDefensaAsync";
            await UgsDefensa.PublicarDefensaAsync(defensa);
            var equipo = new List<string>();
            foreach (var h in defensa.Equipo) equipo.Add(h.Id);
            paso = "1 PublicarInventarioAsync (propio)";
            await UgsRival.PublicarInventarioAsync(equipo);
            string miId = UgsDefensa.MiPlayerId;

            // PASO 1 (segunda computadora emulada): la cuenta rival publica SU defensa e inventario
            paso = "1 CambiarPerfilAsync('otro')";
            await UgsDefensa.CambiarPerfilAsync("otro");
            var defensaRival = CrearDefensaDesdeEscena(gestor, aliasOtro, nivelOtro);
            paso = "1 PublicarDefensaAsync (rival)";
            await UgsDefensa.PublicarDefensaAsync(defensaRival);
            paso = "1 PublicarInventarioAsync (rival)";
            await UgsRival.PublicarInventarioAsync(equipo);

            // PASO 2: la cuenta rival lee MI defensa (segunda computadora sin dos PC)
            paso = "2 LeerDefensaDeAsync";
            var mia = await UgsDefensa.LeerDefensaDeAsync(miId);
            Debug.Log(mia != null
                ? $"[PASO 2 OK] El perfil 'otro' leyó mi defensa: {mia.Nombre}, {mia.Equipo.Count} héroes."
                : "[PASO 2] Mi defensa no apareció (¿se publicó?)");

            // PASO 2 (verificación): intentar escribir la defensa ajena → 403 code 53
            paso = "2 VerificarEscrituraAjenaAsync";
            await UgsDefensa.VerificarEscrituraAjenaAsync(miId);

            // volver a mi perfil para jugar
            paso = "2 CambiarPerfilAsync('principal')";
            await UgsDefensa.CambiarPerfilAsync("principal");

            // PASO 3-5: BuscarRival (franja que se amplía, cuenta consultas/lecturas)
            paso = "3-5 BuscarRivalAsync";
            var res = await UgsRival.BuscarRivalAsync(miNivel, franjaMaxima, candidatosSuficientes);
            if (!res.Hubo)
            {
                Debug.Log("[PASO 3-5] Sin rivales: " + res.Mensaje + " (el cliente ofrecería práctica S08/S09).");
                return;
            }

            // PASO 6: resolver el combate con mi equipo real declarado
            paso = "6 ResolverCombateAsync (T9Combate)";
            await UgsRival.ResolverCombateAsync(res.RivalPlayerId, equipo);

            Debug.Log("[LAB09] Demo completo. Captura la consola para el documento.");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[LAB09] Falló en el PASO '{paso}':\n{DetalleExcepcion(e)}");
        }
        finally
        {
            corriendo = false;
        }
    }

    // Devuelve el detalle real de una excepción de Cloud Code (el servidor lanzó un error 422)
    static string DetalleExcepcion(System.Exception e)
    {
        var sb = new System.Text.StringBuilder();
        var cc = e as Unity.Services.CloudCode.CloudCodeException;
        if (cc != null)
        {
            sb.AppendLine($"Reason={cc.Reason} ErrorCode={cc.ErrorCode}");
            sb.AppendLine(cc.Message); // == ToString(): trae el mensaje que lanzó el script
        }
        else
        {
            sb.AppendLine($"[{e.GetType().Name}] {e.Message}");
        }
        for (var ex = e.InnerException; ex != null; ex = ex.InnerException)
            sb.AppendLine($"  <-- [{ex.GetType().Name}] {ex.Message}");
        return sb.ToString();
    }

    // Arma DefensaPublica desde las 5 cartas P1 de tu escena (reutiliza RegistroDefensivo)
    DefensaPublica CrearDefensaDesdeEscena(GestorTurnos gestor, string alias, int nivel)
    {
        var d = new DefensaPublica { Nombre = alias, NivelCuenta = nivel, Trofeos = 0 };
        RegistroDefensivo reg = null;
        if (gestor != null && gestor.cartasPlayer1 != null)
            reg = RegistroDefensivo.DesdeCartas(alias, gestor.faccionP1, gestor.cartasPlayer1);
        if (reg != null)
            foreach (var p in reg.peleadores)
                d.Equipo.Add(new HeroeDefensa { Id = p.nombre.ToLower().Replace(" ", "_"), Nombre = p.nombre, Nivel = 1 });
        return d;
    }
}
#endif
