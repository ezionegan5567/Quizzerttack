// Ranking por trofeos: consulta el índice público trofeos_1 (desc, creado por CLI).
const { DataApi } = require("@unity-services/cloud-save-1.4");

module.exports = async ({ params, context, logger }) => {
  const cloudSaveApi = new DataApi(context);
  const consulta = {
    fields: [ { key: "trofeos", op: "LT", value: 999999, asc: false } ],
    returnKeys: ["defensa", "trofeos", "nivel"],
    limit: 10,
    offset: 0,
  };
  const r = await cloudSaveApi.queryPublicPlayerData(context.projectId, consulta);

  const filas = [];
  for (const c of r.data.results || []) {
    let nombre = "(sin alias)", trofeos = 0, nivel = 0;
    for (const par of c.data || []) {
      if (par.key === "defensa" && par.value != null) nombre = par.value.Nombre || nombre;
      else if (par.key === "trofeos" && par.value != null) trofeos = Math.round(par.value);
      else if (par.key === "nivel" && par.value != null) nivel = Math.round(par.value);
    }
    if (trofeos > 0)
      filas.push({ Nombre: nombre, Trofeos: trofeos, Nivel: nivel, PlayerId: c.playerId || c.id });
  }
  filas.sort((a, b) => b.Trofeos - a.Trofeos);
  return { Filas: filas.slice(0, 10) };
};
