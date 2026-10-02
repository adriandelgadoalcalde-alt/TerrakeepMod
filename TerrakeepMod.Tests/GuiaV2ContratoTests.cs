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

    [Theory]
    [InlineData("CalamityConditions.DownedOldDuke", "Downed Old Duke")]
    [InlineData("Condition.Hardmode", "Hardmode")]
    [InlineData("solo modo normal (DefineNormalOnlyDropSet)", "solo modo normal (Define Normal Only Drop Set)")]
    [InlineData("", "")]
    public void CondicionLegible(string entrada, string esperada)
    {
        Assert.Equal(esperada, ContratoGuiaV2.Legible(entrada));
    }
}
