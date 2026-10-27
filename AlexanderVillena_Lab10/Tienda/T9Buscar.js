// Pasos 3-5: buscar rival por franja de nivel (índice público nivel_1 en Cloud Save).
const { DataApi } = require("@unity-services/cloud-save-1.4");

module.exports = async ({ params, context, logger }) => {
  const cloudSaveApi = new DataApi(context); // serviceToken: consulta el índice público
  const nivel = params.Nivel;
  const franjaMaxima = params.FranjaMaxima || 10;
  const suficientes = params.CandidatosSuficientes || 2;

  const res = { Hubo: false, RivalPlayerId: "", Rival: null, Franja: 0, Consultas: 0, LecturasConsumidas: 0, Mensaje: "" };

  if (!nivel || nivel <= 0) {
    res.Mensaje = "No has publicado defensa (nivel <= 0): no buscas ni puedes ser atacado.";
    return res;
  }

  const candidatos = [];
  let consultas = 0, lecturas = 0;

  for (let franja = 2; franja <= franjaMaxima; franja += 2) {
    const consulta = {
      fields: [
        { key: "nivel", value: nivel - franja, op: "GE", asc: true },
        { key: "nivel", value: nivel + franja, op: "LE", asc: true },
      ],
      returnKeys: ["defensa", "nivel"],
      limit: 20,
      offset: 0,
    };
    const r = await cloudSaveApi.queryPublicPlayerData(context.projectId, consulta);
    consultas++;
    const crudos = (r.data && r.data.results) || [];
    lecturas += crudos.length;

    for (const c of crudos) {
      let defensa = null, nivelC = 0;
      for (const par of c.data || []) {
        if (par.key === "defensa" && par.value != null) defensa = par.value;
        else if (par.key === "nivel" && par.value != null) nivelC = Math.round(par.value);
      }
      candidatos.push({ Id: c.playerId || c.id, Nivel: nivelC, Defensa: defensa });
    }

    for (let i = candidatos.length - 1; i >= 0; i--)
      if (candidatos[i].Id === context.playerId) candidatos.splice(i, 1);

    res.Franja = franja;
    if (candidatos.length >= suficientes) break;
  }

  res.Consultas = consultas;
  res.LecturasConsumidas = lecturas;

  if (candidatos.length === 0) {
    res.Hubo = false;
    res.Mensaje = "No hay rivales en la franja final ±" + res.Franja + ".";
    return res;
  }

  candidatos.sort((a, b) => Math.abs(a.Nivel - nivel) - Math.abs(b.Nivel - nivel));
  const elegido = candidatos[0];
  res.Hubo = true;
  res.RivalPlayerId = elegido.Id;
  res.Rival = elegido.Defensa;
  return res;
};

module.exports.params = {
  Nivel: { type: "Numeric", required: true },
  FranjaMaxima: { type: "Numeric", required: false },
  CandidatosSuficientes: { type: "Numeric", required: false },
};
