// Paso 6: resolver combate (npc o pvp) + economía + log de ataques.
// Bolsa del atacante: PRIVATE "bolsa" {oro,trofeos} (solo el servidor la toca).
// Trofeos públicos: llave PUBLIC "trofeos" (para el ranking, índice trofeos_1).
// Log del defensor: PROTECTED "registro_ataques" (últimos 10 ataques, para venganzas).
const { DataApi } = require("@unity-services/cloud-save-1.4");

const Tabla = {
  bufeador: [90, 15], asesino: [80, 32], lanza_con_escudo: [150, 16], enano: [125, 24],
  vampiro: [110, 28], mago: [90, 20], cazador: [85, 30], guerrero_del_hacha: [115, 30],
  lancero: [100, 24], nigromante: [90, 22], alquimista: [90, 18], pistolero: [85, 30],
  escudero: [150, 14], guerrero_dos_hachas: [100, 28], hombre_lobo: [110, 28],
  curandero: [95, 16], arquero: [85, 28], muneco_poseido: [100, 20],
  espadachin: [105, 26], draconiano: [120, 30],
};
const ORO_PVP = 100, TROFEOS_PVP = 10;
const Npcs = {
  npc_practica_1: { Nombre: "Rival de práctica", Vida: 80, Ataque: 12, Oro: 50, Trofeos: 2 },
  npc_practica_2: { Nombre: "Sombra entrenada", Vida: 100, Ataque: 16, Oro: 80, Trofeos: 5 },
  npc_jefe_1: { Nombre: "Coloso de arena", Vida: 200, Ataque: 25, Oro: 200, Trofeos: 10 },
};

const poder = (integrantes, ataquePromedio) => integrantes * 100 + ataquePromedio * 10;
const miPoder = (equipo) => {
  if (!equipo || equipo.length === 0) return 0;
  let suma = 0;
  for (const id of equipo) suma += Tabla[id] ? Tabla[id][1] : 20;
  return poder(equipo.length, Math.floor(suma / equipo.length));
};

module.exports = async ({ params, context, logger }) => {
  const cloudSaveApi = new DataApi(context); // serviceToken
  const rival = params.Rival;
  const equipoAtacante = params.Equipo || [];
  const miAlias = params.MiAlias || "Jugador";
  const estaVivo = (h) => h && h.vida > 0;

  const npc = Npcs[rival];
  if (npc) {
    const ganoNpc = miPoder(equipoAtacante) >= npc.Vida + npc.Ataque * 2;
    return { Gano: ganoNpc, RivalNombre: npc.Nombre, Fuente: "npc",
      Oro: ganoNpc ? npc.Oro : 0, Trofeos: ganoNpc ? npc.Trofeos : 0,
      Mensaje: ganoNpc ? "Venciste al npc (pago directo, sin bolsa)." : "El npc te venció." };
  }

  if (rival === context.playerId)
    throw new Error("No puedes pelear contra tu propia defensa.");

  const defResp = await cloudSaveApi.getPublicItems(context.projectId, rival, ["defensa"]);
  const defItem = (defResp.data.results || []).find((i) => i.key === "defensa" && i.value != null);
  const defensa = defItem ? defItem.value : null;
  if (!defensa)
    throw new Error("El rival <" + rival + "> no tiene defensa pública (o la borró). Pídele que pulse GUARDAR DEFENSA.");
  if (!defensa.Equipo || defensa.Equipo.length === 0)
    return { Gano: false, RivalNombre: defensa.Nombre, Fuente: "pvp",
      Oro: 0, Trofeos: 0, Mensaje: "<" + defensa.Nombre + "> tiene una defensa vacía." };

  const invResp = await cloudSaveApi.getProtectedItems(context.projectId, rival, ["inventario"]);
  const inventario = [];
  const invItem = (invResp.data.results || []).find((i) => i.key === "inventario");
  if (invItem && invItem.value && invItem.value.heroes) inventario.push(...invItem.value.heroes);
  if (inventario.length === 0)
    throw new Error("El inventario protegido de " + defensa.Nombre + " está vacío: que pulse GUARDAR DEFENSA (sincroniza defensa e inventario).");

  const declarados = defensa.Equipo.map((h) => h.Id);
  if (declarados.indexOf("heroe_coloso") >= 0)
    throw new Error("La defensa de " + defensa.Nombre + " declara heroe_coloso (coloso): no es peleable.");

  const equipoRival = [];
  let ajeno = "";
  for (const id of declarados) {
    const inv = inventario.find((h) => h.id === id);
    if (!inv) { ajeno = id; break; }
    // casilla ligada: si el héroe murió (vida 0), su slot se libera (equipo más chico)
    equipoRival.push(inv);
  }
  if (ajeno !== "")
    throw new Error("Defensa e inventario de " + defensa.Nombre + " no coinciden: declara '" + ajeno + "' pero no está en su inventario. Que pulse GUARDAR DEFENSA otra vez.");

  const vivos = equipoRival.filter(estaVivo);
  const promAtaque = vivos.reduce((s, h) => s + h.ataque, 0) / Math.max(vivos.length, 1);
  const gano = miPoder(equipoAtacante) >= poder(vivos.length, Math.floor(promAtaque));

  // ---------- Economía: bolsa PRIVATE del atacante + trofeos PUBLIC ----------
  if (gano) {
    let bolsa = { oro: 0, trofeos: 0 };
    try {
      const bResp = await cloudSaveApi.getPrivateItems(context.projectId, context.playerId, ["bolsa"]);
      const bItem = (bResp.data.results || []).find((i) => i.key === "bolsa");
      if (bItem && bItem.value) bolsa = bItem.value;
    } catch (e) { /* primera vez: no hay bolsa */ }
    bolsa.oro = (bolsa.oro || 0) + ORO_PVP;
    bolsa.trofeos = (bolsa.trofeos || 0) + TROFEOS_PVP;
    await cloudSaveApi.setPrivateItem(context.projectId, context.playerId,
      { key: "bolsa", value: { oro: bolsa.oro, trofeos: bolsa.trofeos } });
    try {
      await cloudSaveApi.setPublicItem(context.projectId, context.playerId,
        { key: "trofeos", value: bolsa.trofeos });
    } catch (e) { logger.warn("trofeos públicos no actualizados: " + e.message); }
  }

  // ---------- Log de ataques PROTEGIDO del defensor (para venganzas) ----------
  try {
    const fecha = new Date().toISOString().replace("T", " ").slice(0, 19) + " UTC";
    const logResp = await cloudSaveApi.getProtectedItems(context.projectId, rival, ["registro_ataques"]);
    const logItem = (logResp.data.results || []).find((i) => i.key === "registro_ataques");
    const log = logItem && logItem.value ? logItem.value : [];
    log.unshift({ Quien: miAlias, QuienId: context.playerId, Cuando: fecha,
      Resultado: gano ? "te venció" : "perdió contra ti" });
    await cloudSaveApi.setProtectedItem(context.projectId, rival,
      { key: "registro_ataques", value: log.slice(0, 10) });
  } catch (e) { logger.warn("registro del defensor no escrito: " + e.message); }

  return { Gano: gano, RivalNombre: defensa.Nombre, Fuente: "pvp",
    Oro: gano ? ORO_PVP : 0, Trofeos: gano ? TROFEOS_PVP : 0,
    Mensaje: gano
      ? "Venciste a " + defensa.Nombre + " (+" + ORO_PVP + " oro, +" + TROFEOS_PVP + " trofeos)."
      : defensa.Nombre + " te venció." };
};

module.exports.params = {
  Rival: { type: "String", required: true },
  Equipo: { type: "JSON", required: false },
  MiAlias: { type: "String", required: false },
};
