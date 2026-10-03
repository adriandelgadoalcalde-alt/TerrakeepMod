using System.Collections.Generic;
using System.Linq;
using Terrakeep.Core.Guia.V2;
using TerrakeepMod.Common.GuiaV2;
using TerrakeepMod.Common.Libreria;

namespace TerrakeepMod.Tests.GuiaV2;

// Guia v2 (F3, 02-oct-2026): contrato del mod contra el contenido REAL incrustado en
// lib\Terrakeep.Core.dll (el mismo que viaja dentro del .tmod). Lo que depende del motor
// (marca del mapa, clics, Libreria) lo cubre AutopruebaGuiaV2 en el juego.
public class GuiaV2ContratoTests
{
    private static IEnumerable<Condicion> Hojas(Condicion c)
    {
        if (c == null) yield break;
        if (c.Condiciones != null)
            foreach (Condicion h in c.Condiciones)
                foreach (Condicion x in Hojas(h)) yield return x;
        yield return c;
    }

    public static IEnumerable<object[]> Guias() =>
        GuiaV2Cargador.GuiasDisponibles().Select(g => new object[] { g });

    [Fact]
    public void HayAlMenosLaGuiaDeCalamityIncrustada()
    {
        Assert.Contains("calamity", GuiaV2Cargador.GuiasDisponibles());
    }

    [Theory]
    [MemberData(nameof(Guias))]
    public void TodaBanderaQueUsaLaGuiaLaSabeLeerElMod(string guia)
    {
        GuiaV2Doc doc = GuiaV2Cargador.CargarGuiaIncrustada(guia);
        var conocidas = new HashSet<string>(ContratoGuiaV2.BanderasVanilla.Concat(guia == "calamity" ? ContratoGuiaV2.BanderasCalamity : new string[0]));
        var usadas = new HashSet<string>();
        foreach (Parada p in doc.Paradas)
        {
            foreach (Condicion h in Hojas(p.CompletadaCuando)) if (h.Tipo == "bandera") usadas.Add(h.Bandera);
            foreach (Tarea t in p.Tareas)
                foreach (Condicion h in Hojas(t.Condicion)) if (h.Tipo == "bandera") usadas.Add(h.Bandera);
        }
        var faltan = usadas.Where(b => !conocidas.Contains(b)).OrderBy(b => b).ToList();
        Assert.True(faltan.Count == 0, "Banderas de la guia " + guia + " que el mod no lee: " + string.Join(", ", faltan));
    }

    [Theory]
    [InlineData("downedLeviathan")]
    [InlineData("downedEoCAcidRain")]
    [InlineData("downedAquaticScourgeAcidRain")]
    [InlineData("downedCLAMHardMode")]
    [InlineData("downedNuclearTerror")]
    [InlineData("downedBossRush")]
    public void LasSeisBanderasNuevasDeLaGuiaV2EstanEnElMod(string bandera)
    {
        Assert.Contains(bandera, ContratoGuiaV2.BanderasCalamity);
    }

    [Fact]
    public void LasListasDeBanderasNoTienenRepetidos()
    {
        Assert.Equal(ContratoGuiaV2.BanderasVanilla.Length, ContratoGuiaV2.BanderasVanilla.Distinct().Count());
        Assert.Equal(ContratoGuiaV2.BanderasCalamity.Length, ContratoGuiaV2.BanderasCalamity.Distinct().Count());
    }

    [Theory]
    [InlineData("Medallón del Desierto", 5000, "Medallón del Desierto")]
    [InlineData("Ark of the Cosmos", 5001, "Ark of the Cosmos")]
    [InlineData("Lingote de oro, viejo", 19, "#19")]   // la coma es "O" en la gramatica de la Libreria
    [InlineData("X", 7, "#7")]                          // menos de 2 letras se ignora
    [InlineData("", 9, "#9")]
    [InlineData(".tooltip", 11, "#11")]                 // "." buscaria en el tooltip
    public void ConsultaDeLaLibreria(string nombre, int tipo, string esperada)
    {
        Assert.Equal(esperada, ContratoGuiaV2.ConsultaLibreria(nombre, tipo));
    }

    [Fact]
    public void LaConsultaPorNombreEncuentraElObjetoConLaGramaticaRealDeLaLibreria()
    {
        // Todos los nombres oficiales de la tabla de referencias: la consulta que escribe "Coger en
        // la Libreria" tiene que casar con su propio objeto (por nombre o por #id).
        GuiaV2Referencias refs = GuiaV2Cargador.CargarReferenciasIncrustadas();
        int id = 1;
        foreach (RefObjeto o in refs.Objetos.Values)
        {
            foreach (string nombre in new[] { o.Es, o.En })
            {
                string q = ContratoGuiaV2.ConsultaLibreria(nombre, id);
                Assert.True(GramaticaBusqueda.Casa(q, id, GramaticaBusqueda.Plegar(nombre), null),
                    "La consulta \"" + q + "\" no encuentra \"" + nombre + "\"");
            }
            id++;
        }
    }

    // Parche 0.8.1: las condiciones del codigo se traducen (Terrakeep.Core.Guia.V2.GuiaV2Condiciones,
    // compartida con Terrakeep escritorio) y, si no hay forma legible, se omiten: nunca se enseña codigo.
    [Theory]
    [InlineData("CalamityConditions.DownedOldDuke", "tras derrotar al Viejo Duque")]
    [InlineData("Condition.Hardmode", "en Modo Difícil")]
    [InlineData("solo modo normal (DefineNormalOnlyDropSet)", "solo en modo normal")]
    [InlineData("DropHelper.GFB | por jugador (PerPlayer)", "")]
    [InlineData("", "")]
    public void CondicionLegible(string entrada, string esperada)
    {
        Assert.Equal(esperada, GuiaV2Condiciones.Legible(entrada, true));
    }

    [Fact]
    public void NingunaCondicionDeLaTablaSeEnseñaComoCodigo()
    {
        GuiaV2Referencias refs = GuiaV2Cargador.CargarReferenciasIncrustadas();
        int vistas = 0;
        foreach (RefObjeto o in refs.Objetos.Values)
        {
            foreach (Obtencion ob in o.Obtencion)
            {
                foreach (string c in ob.Condiciones.Concat(new[] { ob.Condicion }))
                {
                    foreach (bool es in new[] { true, false })
                    {
                        string l = GuiaV2Condiciones.Legible(c, es);
                        string resto = ContratoGuiaV2.RestoTecnico(l);
                        Assert.True(resto == null, "«" + c + "» → «" + l + "»: " + resto);
                    }
                    vistas++;
                }
            }
        }
        Assert.True(vistas > 1000, "condiciones revisadas: " + vistas);
    }

    [Theory]
    [InlineData(5098, "ID 5098")]
    [InlineData(7428, "ID 7428")]
    [InlineData(0, "")]
    [InlineData(-1, "")]
    public void BajoElSpriteSoloVaElIdONada(int tipo, string esperado)
    {
        Assert.Equal(esperado, ContratoGuiaV2.IdVisible(tipo));
    }

    [Theory]
    [InlineData("CalamityMod/BurntSienna")]
    [InlineData("CalamityMod/WulfrumController")]
    [InlineData("dato del código del juego: CalamityMod.Items.SummonItems.DesertMedallion.cs:56")]
    [InlineData("40 × Cualquiera Bloque de arena")]
    [InlineData("{z:sunken_sea}")]
    [InlineData("Guia.Ficha.Receta")]
    [InlineData("DropHelper.PostDoG()")]
    public void ElCanarioDeRestosTecnicosCazaLosDefectosDeLaRelease080(string texto)
    {
        Assert.NotNull(ContratoGuiaV2.RestoTecnico(texto));
    }

    [Theory]
    [InlineData("Medallón del desierto")]
    [InlineData("Receta: 40 × Cualquier bloque de arena (tienes 0) + 4 × Mandíbula de hormiga león")]
    [InlineData("ID 7428")]
    [InlineData("Calamity · ID 7428")]
    [InlineData("Estación: Altar demoníaco / Altar carmesí")]
    public void ElCanarioDeRestosTecnicosDejaPasarElTextoLimpio(string texto)
    {
        Assert.Null(ContratoGuiaV2.RestoTecnico(texto));
    }

    [Fact]
    public void LosGruposDeRecetaSonFrases()
    {
        GuiaV2Referencias refs = GuiaV2Cargador.CargarReferenciasIncrustadas();
        Assert.Equal("Cualquier bloque de arena", refs.Grupos["Sand"].Es);
        Assert.Equal("Cualquier madera", refs.Grupos["Wood"].Es);
        Assert.Equal("Cualquier lingote de hierro", refs.Grupos["IronBar"].Es);
        foreach (var kv in refs.Grupos)
        {
            Assert.Null(ContratoGuiaV2.RestoTecnico(kv.Value.Es));
        }
    }
}
