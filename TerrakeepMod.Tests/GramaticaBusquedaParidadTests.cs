using TerrakeepMod.Common.Libreria;
using Xunit;

namespace TerrakeepMod.Tests.Libreria;

// KeepQA (17-sep-2026): traduccion literal (mismos casos, misma intencion) de
// Terrakeep.App.ViewModels.Tests/LibrarySearchGrammarTests.cs - los tests EXAMPLE-BASED reales
// del original del que GramaticaBusqueda.cs es copia (ver la cabecera de GramaticaBusqueda.cs).
// El objetivo de este archivo NO es duplicar cobertura porque si: es la señal mas directa y
// barata posible de que la copia del mod sigue de acuerdo con el original en los casos concretos
// que ya demostraron importar alli (incluida la tilde de "mascara"/"Máscara" del informe de
// pulido C-09) - si algun dia se corrige un bug real en el original y se le olvida portar aqui
// (como ya paso una vez con best_prefix.json, ver bitacora.md), estos tests son los primeros en
// notarlo.
public class GramaticaBusquedaParidadTests
{
    [Fact]
    public void SinPrefijo_BuscaPorNombreComoSubcadena()
    {
        Assert.True(GramaticaBusqueda.Casa("espada", 1, "espada de hierro", null));
        Assert.False(GramaticaBusqueda.Casa("hacha", 1, "espada de hierro", null));
    }

    [Fact]
    public void Coma_EsOrEntreTerminos()
    {
        Assert.True(GramaticaBusqueda.Casa("hacha, espada", 1, "espada de hierro", null));
        Assert.True(GramaticaBusqueda.Casa("hacha, espada", 2, "hacha de piedra", null));
        Assert.False(GramaticaBusqueda.Casa("hacha, martillo", 1, "espada de hierro", null));
    }

    [Fact]
    public void Espacio_EsAndDentroDeUnTermino()
    {
        Assert.True(GramaticaBusqueda.Casa("espada hierro", 1, "espada de hierro", null));
        Assert.False(GramaticaBusqueda.Casa("espada oro", 1, "espada de hierro", null));
    }

    [Fact]
    public void AlmohadillaId_BuscaPorIdExacto()
    {
        Assert.True(GramaticaBusqueda.Casa("#90", 90, "cualquier nombre", null));
        Assert.False(GramaticaBusqueda.Casa("#90", 91, "cualquier nombre", null));
    }

    [Fact]
    public void AlmohadillaRango_BuscaPorRangoDeIdAmbosExtremosIncluidos()
    {
        Assert.True(GramaticaBusqueda.Casa("#100-200", 100, "x", null));
        Assert.True(GramaticaBusqueda.Casa("#100-200", 200, "x", null));
        Assert.True(GramaticaBusqueda.Casa("#100-200", 150, "x", null));
        Assert.False(GramaticaBusqueda.Casa("#100-200", 99, "x", null));
        Assert.False(GramaticaBusqueda.Casa("#100-200", 201, "x", null));
    }

    [Fact]
    public void PuntoPrefijo_BuscaEnElTooltipEnVezDelNombre()
    {
        Assert.True(GramaticaBusqueda.Casa(".aumenta la defensa", 1, "casco de hierro", "aumenta la defensa en 2"));
        Assert.False(GramaticaBusqueda.Casa(".aumenta la defensa", 1, "casco de aumenta la defensa", null));
    }

    [Fact]
    public void TerminoDeMenosDeDosCaracteres_SeIgnoraPorCompleto()
    {
        Assert.False(GramaticaBusqueda.Casa("5", 5, "objeto con un 5 en el nombre", null));
    }

    // KeepQA (17-sep-2026): mutante superviviente real de Stryker.NET - "termino.Length < 2"
    // mutado a "<= 2" seguia pasando TODA la suite sin este caso concreto, porque ninguna otra
    // prueba obligaba a que un termino de EXACTAMENTE 2 caracteres siguiera contando como
    // busqueda valida (la frontera real, documentada en la cabecera del archivo, es "menos de 2
    // caracteres se ignora" - 2 caracteres exactos ya cuentan).
    [Fact]
    public void TerminoDeExactamenteDosCaracteres_SiCuentaComoBusquedaValida()
    {
        Assert.True(GramaticaBusqueda.Casa("xy", 1, "un xyz cualquiera", null));
    }

    // KeepQA (17-sep-2026): mutante superviviente real de Stryker.NET - el "return \"\";" del
    // guardarrail nulo/vacio de Plegar se podia quitar entero sin que ninguna prueba lo notara
    // (Plegar nunca se llama con null desde el resto del mod, pero el propio metodo documenta
    // ese contrato en su firma publica y lo comprueba explicitamente - merece su propia prueba).
    [Fact]
    public void Plegar_NuloOVacio_DevuelveCadenaVacia()
    {
        Assert.Equal(string.Empty, GramaticaBusqueda.Plegar(null!));
        Assert.Equal(string.Empty, GramaticaBusqueda.Plegar(string.Empty));
    }

    [Fact]
    public void Plegar_QuitaTildesYMinusculizaSinTocarLoQueYaEstaLimpio()
    {
        Assert.Equal("cenit", GramaticaBusqueda.Plegar("Cénit"));
        Assert.Equal("mascara", GramaticaBusqueda.Plegar("MÁSCARA"));
        Assert.Equal("mascara", GramaticaBusqueda.Plegar("mascara"));
        Assert.Equal("nino", GramaticaBusqueda.Plegar("niño"));
    }

    [Fact]
    public void Casa_QuerySinTilde_EncuentraNombrePlegadoDeUnaPalabraConTilde()
    {
        string nombrePlegado = GramaticaBusqueda.Plegar("Cénit corrupto");
        Assert.True(GramaticaBusqueda.Casa("cenit", 4956, nombrePlegado, null));
    }

    [Fact]
    public void Casa_QueryConTilde_TambienEncuentraElMismoNombre()
    {
        string nombrePlegado = GramaticaBusqueda.Plegar("Cénit corrupto");
        Assert.True(GramaticaBusqueda.Casa("cénit", 4956, nombrePlegado, null));
    }

    [Fact]
    public void Casa_BuscarMascaraEncuentraElNombreRealConTilde()
    {
        string nombrePlegado = GramaticaBusqueda.Plegar("Máscara de Cordero");
        Assert.True(GramaticaBusqueda.Casa("mascara", 1, nombrePlegado, null));
    }
}
