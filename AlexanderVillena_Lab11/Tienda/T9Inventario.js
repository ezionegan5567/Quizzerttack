// S07: el servidor escribe el inventario PROTEGIDO ("inventario") del jugador.
const { DataApi } = require("@unity-services/cloud-save-1.4");

const Tabla = {
  bufeador: [90, 15], asesino: [80, 32], lanza_con_escudo: [150, 16], enano: [125, 24],
  vampiro: [110, 28], mago: [90, 20], cazador: [85, 30], guerrero_del_hacha: [115, 30],
  lancero: [100, 24], nigromante: [90, 22], alquimista: [90, 18], pistolero: [85, 30],
  escudero: [150, 14], guerrero_dos_hachas: [100, 28], hombre_lobo: [110, 28],
  curandero: [95, 16], arquero: [85, 28], muneco_poseido: [100, 20],
  espadachin: [105, 26], draconiano: [120, 30],
};

module.exports = async ({ params, context, logger }) => {
  const cloudSaveApi = new DataApi(context); // serviceToken: solo el servidor escribe protegidas
  const equipo = params.Equipo || [];
  const heroes = equipo.map((id) => {
    const s = Tabla[id];
    return s ? { id: id, vida: s[0], ataque: s[1] } : { id: id, vida: 100, ataque: 20 };
  });
  await cloudSaveApi.setProtectedItem(context.projectId, context.playerId,
    { key: "inventario", value: { heroes: heroes } });
  return "inventario listo: " + heroes.length + " héroes";
};

module.exports.params = { Equipo: { type: "JSON", required: false } };
