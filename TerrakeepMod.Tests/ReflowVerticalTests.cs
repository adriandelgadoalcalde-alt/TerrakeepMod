using TerrakeepMod.Common.Panel;
using Xunit;

namespace TerrakeepMod.Tests.Panel;

// KeepQA (29-sep-2026): cierre del arreglo de los 3 defectos reales de la auditoria de UIScale/
// resolucion (bitacora.md, requirement 0446b3c9) acotados a 1366x768@150% - "PestanaInventario"
// (Personaje), "PestanaMapa" (Exploracion) y "ContenidoAjustes" (Ajustes) dejaban de caber en el
// hueco real y se solapaban con el pie del panel o entre si. ReflowVertical.FactorDeCompresion es
// la UNICA pieza de ese arreglo sin dependencia de Terraria/FNA (todo lo demas necesita el motor
// para medir texto real) y por eso la unica que se puede probar aqui - ver su propia cabecera.
public class ReflowVerticalTests
{
    [Fact]
    public void SobraSitio_DevuelveUno()
    {
        Assert.Equal(1f, ReflowVertical.FactorDeCompresion(altoNecesario: 200f, altoDisponible: 300f));
    }

    [Fact]
    public void ElHuecoEsExactoElNecesario_DevuelveUno()
    {
        Assert.Equal(1f, ReflowVertical.FactorDeCompresion(altoNecesario: 250f, altoDisponible: 250f));
    }

    [Fact]
    public void FaltaSitio_ComprimeEnLaProporcionReal()
    {
        // 200 disponibles de 300 necesarios: dos tercios exactos (por encima del suelo duro 0.55).
        Assert.Equal(2f / 3f, ReflowVertical.FactorDeCompresion(altoNecesario: 300f, altoDisponible: 200f), precision: 5);
    }

    [Fact]
    public void FaltaMuchisimoSitio_NuncaBajaDelSueloDuro()
    {
        float factor = ReflowVertical.FactorDeCompresion(altoNecesario: 1000f, altoDisponible: 10f, escalaMinima: 0.55f);
        Assert.Equal(0.55f, factor, precision: 5);
    }

    [Fact]
    public void SueloDuroConfigurable_SeRespeta()
    {
        float factor = ReflowVertical.FactorDeCompresion(altoNecesario: 1000f, altoDisponible: 10f, escalaMinima: 0.3f);
        Assert.Equal(0.3f, factor, precision: 5);
    }

    [Theory]
    [InlineData(0f, 100f)]
    [InlineData(-10f, 100f)]
    [InlineData(100f, 0f)]
    [InlineData(100f, -10f)]
    public void DatosInvalidos_NuncaAgrandaNiRevienta_DevuelveUno(float altoNecesario, float altoDisponible)
    {
        Assert.Equal(1f, ReflowVertical.FactorDeCompresion(altoNecesario, altoDisponible));
    }

    [Fact]
    public void ElFactorAplicadoALaPosicionYAlAltoNuncaSeSale_DelHuecoDisponible()
    {
        // Demuestra la propiedad real que documenta la cabecera de FactorDeCompresion: aplicando el
        // MISMO factor a la posicion de un elemento (respecto al inicio del bloque) y a su propio
        // alto, el borde inferior del ULTIMO elemento del flujo nunca se sale del hueco real -
        // exactamente la garantia que necesitan PestanaMapa/ContenidoAjustes para no solaparse con
        // el pie del panel.
        const float topNatural = 260f;
        const float altoElementoNatural = 22f;
        const float altoNecesario = topNatural + altoElementoNatural;
        const float altoDisponible = 180f; // menos de lo necesario: fuerza compresion real

        float factor = ReflowVertical.FactorDeCompresion(altoNecesario, altoDisponible);

        float topComprimido = topNatural * factor;
        float altoElementoComprimido = altoElementoNatural * factor;
        float bordeInferior = topComprimido + altoElementoComprimido;

        Assert.True(bordeInferior <= altoDisponible + 0.001f,
            $"El borde inferior comprimido ({bordeInferior}) no debe superar el hueco disponible ({altoDisponible}).");
    }
}
