// ============ LAB14 · FASE 1: PRUEBAS DEL CATÁLOGO DE LA TIENDA ============
using Tienda;
using Xunit;

namespace Tienda.Pruebas
{
    public class CatalogoPruebas
    {
        [Fact]
        public void ElMagoCuestaMilDoscientos()
        {
            var mago = Catalogo.Buscar("mago");

            Assert.NotNull(mago);
            Assert.Equal(1200, mago.Precio);
        }

        [Fact]
        public void ElGuerreroDelHachaEsElMasCaroDelBloqueBase()
        {
            var hacha = Catalogo.Buscar("guerrero_del_hacha");

            Assert.NotNull(hacha);
            Assert.Equal(1500, hacha.Precio);
        }

        [Theory]
        [InlineData("mago")]
        [InlineData("vampiro")]
        [InlineData("arquero")]
        [InlineData("escudero")]
        [InlineData("guerrero_del_hacha")]
        [InlineData("draconiano")]
        public void TodoHeroeVendibleTienePrecioPositivo(string id)
        {
            var item = Catalogo.Buscar(id);

            Assert.NotNull(item);
            Assert.True(item.Precio > 0);
        }

        [Fact]
        public void UnHeroeInexistenteDevuelveNulo()
        {
            Assert.Null(Catalogo.Buscar("no_existe"));
            Assert.Null(Catalogo.Buscar(null));
            Assert.Null(Catalogo.Buscar(""));
        }
    }
}
