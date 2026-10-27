// Mi resumen: bolsa privada ("bolsa") + últimos ataques recibidos ("registro_ataques") para venganzas.
const { DataApi } = require("@unity-services/cloud-save-1.4");

module.exports = async ({ params, context, logger }) => {
  const cloudSaveApi = new DataApi(context);
  const pid = context.playerId;

  let bolsa = { oro: 0, trofeos: 0 };
  try {
    const bResp = await cloudSaveApi.getPrivateItems(context.projectId, pid, ["bolsa"]);
    const bItem = (bResp.data.results || []).find((i) => i.key === "bolsa");
    if (bItem && bItem.value) bolsa = bItem.value;
  } catch (e) { /* sin bolsa aún */ }

  let ataques = [];
  try {
    const lResp = await cloudSaveApi.getProtectedItems(context.projectId, pid, ["registro_ataques"]);
    const lItem = (lResp.data.results || []).find((i) => i.key === "registro_ataques");
    if (lItem && lItem.value) ataques = lItem.value;
  } catch (e) { /* sin registro aún */ }

  return {
    Bolsa: { Oro: bolsa.oro || 0, Trofeos: bolsa.trofeos || 0 },
    Ataques: ataques,
  };
};
