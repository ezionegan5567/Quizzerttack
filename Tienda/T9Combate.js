// Paso 6: resolver combate (npc o pvp con defensa pública + inventario protegido).
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
  const cloudSaveApi = new DataApi(context); // serviceToken: lee protegidas de cualquier jugador
  const rival = params.Rival;
  const equipoAtacante = params.Equipo || [];

  const npc = Npcs[rival];
  if (npc) {
    const ganoNpc = miPoder(equipoAtacante) >= npc.Vida + npc.Ataque * 2;
    return { Gano: ganoNpc, RivalNombre: npc.Nombre, Fuente: "npc",
      Oro: ganoNpc ? npc.Oro : 0, Trofeos: ganoNpc ? npc.Trofeos : 0,
      Mensaje: ganoNpc ? "Venciste al npc." : "El npc te venció." };
  }

  if (rival === context.playerId)
    throw new Error("No puedes pelear contra tu propia defensa.");

  const defResp = await cloudSaveApi.getPublicItems(context.projectId, rival, ["defensa"]);
  const defensa = (defResp.data.results || []).find((i) => i.key === "defensa").value;
  if (!defensa) throw new Error("No hay ningún rival <" + rival + ">.");
  if (!defensa.Equipo || defensa.Equipo.length === 0)
    return { Gano: false, RivalNombre: defensa.Nombre, Fuente: "pvp",
      Oro: 0, Trofeos: 0, Mensaje: "<" + defensa.Nombre + "> tiene una defensa vacía." };

  const invResp = await cloudSaveApi.getProtectedItems(context.projectId, rival, ["inventario"]);
  const inventario = [];
  const invItem = (invResp.data.results || []).find((i) => i.key === "inventario");
  if (invItem && invItem.value && invItem.value.heroes) inventario.push(...invItem.value.heroes);

  const declarados = defensa.Equipo.map((h) => h.Id);
  if (declarados.indexOf("heroe_coloso") >= 0)
    throw new Error("La defensa de " + defensa.Nombre + " declara heroe_coloso (coloso): no es peleable.");

  const equipoRival = [];
  let ajeno = "";
  for (const id of declarados) {
    const inv = inventario.find((h) => h.id === id);
    if (!inv) { ajeno = id; break; }
    equipoRival.push(inv);
  }
  if (ajeno !== "")
    throw new Error("La defensa de " + defensa.Nombre + " declara " + ajeno + ", que no está en su inventario.");

  const promAtaque = equipoRival.reduce((s, h) => s + h.ataque, 0) / equipoRival.length;
  const gano = miPoder(equipoAtacante) >= poder(equipoRival.length, Math.floor(promAtaque));
  return { Gano: gano, RivalNombre: defensa.Nombre, Fuente: "pvp",
    Oro: gano ? ORO_PVP : 0, Trofeos: gano ? TROFEOS_PVP : 0,
    Mensaje: gano
      ? "Venciste a " + defensa.Nombre + " (+" + ORO_PVP + " oro, +" + TROFEOS_PVP + " trofeos)."
      : defensa.Nombre + " te venció." };
};

module.exports.params = {
  Rival: { type: "String", required: true },
  Equipo: { type: "JSON", required: false },
};
