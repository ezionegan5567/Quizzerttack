module.exports = async ({ params, context, logger }) => {
  return JSON.stringify({
    nombre: params.nombre,
    playerId: context.playerId,
  });
};

module.exports.params = { nombre: "String" };
