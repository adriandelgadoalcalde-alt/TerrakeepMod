using System.Collections.Generic;
using TerrakeepMod.Common.Exploracion;
using TerrakeepMod.Common.Panel;
using TerrakeepMod.Common.Personaje;
using Xunit;

namespace TerrakeepMod.Tests.Paridad;

// Requirement 0446b3c9 (paridad con Terrakeep escritorio 3.3.0, v0.7.0 del mod): las piezas SIN
// Terraria de lo que se porta - los botones rapidos de cantidad (escritorio 09195ce0, mismos tres
// casos de maxStack que StackQuickControlsTests de escritorio: 1, 100 y 9999), el calculo de los
// vecinos que faltan (escritorio ec916e8b, RebuildMissingNpcs) y el suelo de legibilidad de
// "Zoom" (ReflowVertical.FactorConUltimoMinimo). Lo que depende del motor (clic real, NPC.NewNPC,
// NPC.downedGoblins) lo cubren AutopruebaLibreria (paso 27b) y AutopruebaExploracion (36-44).
public class CantidadRapidaTests
{
    [Theory]
    [InlineData(1, 10, 9999, 11)]
    [InlineData(1, 100, 9999, 101)]
    [InlineData(9950, 100, 9999, 9999)]   // nunca pasa del maximo real
    [InlineData(95, 10, 100, 100)]        // moneda de cobre: tope 100
    [InlineData(1, 10, 1, 1)]             // espada: no apilable
    [InlineData(5, -100, 9999, 1)]        // nunca baja de 1
    public void Sumar_AcotaAlMaximoRealYNuncaBajaDeUno(int actual, int delta, int maxStack, int esperado)
    {
        Assert.Equal(esperado, CantidadRapida.Sumar(actual, delta, maxStack));
    }

    [Fact]
    public void Sumar_NoDesbordaConUnMaxStackEnorme()
    {
        Assert.Equal(int.MaxValue, CantidadRapida.Sumar(int.MaxValue - 5, CantidadRapida.PasoLargo, int.MaxValue));
    }

    [Theory]
    [InlineData(9999, 9999)]
    [InlineData(100, 100)]
    [InlineData(1, 1)]
    [InlineData(0, 1)]    // maxStack roto de algun mod: se trata como no apilable
    [InlineData(-3, 1)]
    public void Maximo_EsElMaxStackRealSaneado(int maxStack, int esperado)
    {
        Assert.Equal(esperado, CantidadRapida.Maximo(maxStack));
    }

    [Theory]
    [InlineData(1, 9999, true)]
    [InlineData(9999, 9999, false)]
    [InlineData(1, 1, false)]
    public void PuedeSubir_SoloSiQuedaSitio(int actual, int maxStack, bool esperado)
    {
        Assert.Equal(esperado, CantidadRapida.PuedeSubir(actual, maxStack));
    }
}

public class VecinosQueFaltanTests
{
    [Fact]
    public void DevuelveLosQueNoEstan_EnElOrdenDeLaLista()
    {
        var roster = new[] { 17, 18, 19, 20, 22 };
        var presentes = new[] { 22, 18, 1, 999 };   // 1 y 999 no son de la lista: se ignoran
        Assert.Equal(new List<int> { 17, 19, 20 }, VecinosQueFaltan.Calcular(roster, presentes));
    }

    [Fact]
    public void SinNadiePresente_FaltanTodos_SinRepetidos()
    {
        var roster = new[] { 17, 18, 17 };
        Assert.Equal(new List<int> { 17, 18 }, VecinosQueFaltan.Calcular(roster, new int[0]));
    }

    [Fact]
    public void ConTodosPresentes_NoFaltaNadie()
    {
        var roster = new[] { 17, 18 };
        Assert.Empty(VecinosQueFaltan.Calcular(roster, new[] { 18, 17, 17 }));
    }

    [Fact]
    public void DatosNulos_NoRevientan()
    {
        Assert.Empty(VecinosQueFaltan.Calcular(null, null));
        Assert.Equal(new List<int> { 5 }, VecinosQueFaltan.Calcular(new[] { 5 }, null));
    }
}

public class ReflujoConUltimoMinimoTests
{
    private const float BaseZoom = 0.75f;

    [Fact]
    public void SobraSitio_NoTocaNada()
    {
        float factor = ReflowVertical.FactorConUltimoMinimo(200f, 22f, BaseZoom, ReflowVertical.EscalaMinimaLegible, 300f, out float escala);
        Assert.Equal(1f, factor);
        Assert.Equal(BaseZoom, escala);
    }

    [Fact]
    public void CompresionSuave_ElUltimoSigueElFactorComun()
    {
        // 0.95 * 0.75 = 0.7125 >= 0.68: mismo resultado que FactorDeCompresion.
        float factor = ReflowVertical.FactorConUltimoMinimo(200f, 22f, BaseZoom, ReflowVertical.EscalaMinimaLegible, 190f, out float escala);
        Assert.Equal(ReflowVertical.FactorDeCompresion(200f, 190f), factor, 5);
        Assert.Equal(BaseZoom * factor, escala, 5);
    }

    [Fact]
    public void CompresionFuerte_ElUltimoNuncaBajaDelMinimoLegible()
    {
        // El caso real: 11 px de "Zoom" (factor ~0.6 sobre 0.75 = 0.45 de escala).
        ReflowVertical.FactorConUltimoMinimo(200f, 22f, BaseZoom, ReflowVertical.EscalaMinimaLegible, 120f, out float escala);
        Assert.Equal(ReflowVertical.EscalaMinimaLegible, escala, 5);
    }

    [Theory]
    [InlineData(200f, 22f, 150f)]
    [InlineData(200f, 22f, 130f)]
    [InlineData(260f, 22f, 170f)]
    public void CompresionFuerte_ElTotalSigueCabiendo_SinSolape(float total, float ultimo, float disponible)
    {
        float factor = ReflowVertical.FactorConUltimoMinimo(total, ultimo, BaseZoom, ReflowVertical.EscalaMinimaLegible, disponible, out float escala);
        float ocupado = (total - ultimo) * factor + ultimo * (escala / BaseZoom);
        // Solo se garantiza mientras el resto no toque su propio suelo duro (0.55): en estos casos no lo toca.
        Assert.True(factor > 0.55f, "el caso de prueba no deberia tocar el suelo duro del resto");
        Assert.True(ocupado <= disponible + 0.01f, $"ocupa {ocupado} en {disponible}");
    }

    [Fact]
    public void NiSiquieraCabeElUltimo_ElRestoSeVaASuSueloDuro()
    {
        float factor = ReflowVertical.FactorConUltimoMinimo(200f, 22f, BaseZoom, ReflowVertical.EscalaMinimaLegible, 10f, out float escala);
        Assert.Equal(0.55f, factor, 5);
        Assert.Equal(ReflowVertical.EscalaMinimaLegible, escala, 5);
    }
}
