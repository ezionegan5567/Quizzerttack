using Unity.Services.CloudCode.Core;

namespace TestA
{
    public static class Prueba
    {
        [CloudCodeFunction("Hola")]
        public static string Hola(string nombre)
        {
            return "Hola " + nombre;
        }
    }
}
