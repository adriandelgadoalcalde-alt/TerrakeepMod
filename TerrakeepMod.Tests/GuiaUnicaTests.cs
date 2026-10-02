using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Terrakeep.Core.Guia.V2;
using TerrakeepMod.Common.GuiaV2;

namespace TerrakeepMod.Tests.GuiaV2;

// F2b (02-oct-2026, "una sola guia en toda la app"):
//   - el mapa del juego solo enseña la brujula v1 cuando la Guia v2 NO marca su siguiente parada;
//   - toda bandera que usan las guias v2 incrustadas tiene su texto Guia.Bandera.<nombre> en los DOS
//     .hjson (ES y EN) que empaqueta el .tmod (las seis nuevas de F1 incluidas), y la sub-pestaña
//     Brujula tiene los textos nuevos de "el mapa marca la Guia".
public class GuiaUnicaTests
{
    [Theory]
    [InlineData(true, true, false)]   // Guia v2 cargada y marca visible: solo la marca v2
    [InlineData(true, false, true)]   // marca v2 apagada en Ajustes: vuelve la brujula v1
    [InlineData(false, true, true)]   // sin Guia v2 (contenido no disponible): brujula v1
    [InlineData(false, false, true)]
    public void BrujulaV1SoloSinMarcaV2(bool hayGuiaV2, bool marcaVisible, bool esperada)
    {
        Assert.Equal(esperada, ContratoGuiaV2.BrujulaV1EnElMapa(hayGuiaV2, marcaVisible));
    }

    public static IEnumerable<object[]> Idiomas() => new[] { new object[] { "es-ES" }, new object[] { "en-US" } };

    [Theory]
    [MemberData(nameof(Idiomas))]
    public void CadaBanderaDeLasGuiasTieneSuTextoEnElHjson(string idioma)
    {
        var bloque = Bloque(LeerHjson(idioma), "Guia", "Bandera");
        var usadas = new SortedSet<string>(StringComparer.Ordinal);
        foreach (string guia in GuiaV2Cargador.GuiasDisponibles())
        {
            GuiaV2Doc doc = GuiaV2Cargador.CargarGuiaIncrustada(guia);
            foreach (Parada p in doc.Paradas)
            {
                foreach (Condicion h in Hojas(p.CompletadaCuando)) if (h.Tipo == "bandera") usadas.Add(h.Bandera);
                foreach (Tarea t in p.Tareas)
                    foreach (Condicion h in Hojas(t.Condicion)) if (h.Tipo == "bandera") usadas.Add(h.Bandera);
            }
        }
        Assert.True(usadas.Count > 30, "muy pocas banderas en las guias incrustadas: " + usadas.Count);
        foreach (string nueva in new[] { "downedDD2InvasionT1", "downedDD2InvasionT2", "downedDD2InvasionT3",
                     "combatBookWasUsed", "combatBookVolumeTwoWasUsed", "peddlersSatchelWasUsed" })
            Assert.Contains(nueva, usadas);
        var faltan = usadas.Where(b => !bloque.TryGetValue(b, out string? v) || string.IsNullOrWhiteSpace(v)).ToList();
        Assert.True(faltan.Count == 0, $"Guia.Bandera.* sin texto en {idioma}: {string.Join(", ", faltan)}");
    }

    [Theory]
    [MemberData(nameof(Idiomas))]
    public void LaBrujulaTieneLosTextosDeLaMarcaV2(string idioma)
    {
        var bloque = Bloque(LeerHjson(idioma), "Guia", "Brujula");
        Assert.Contains("{0}", bloque["MapaV2"]);
        Assert.False(string.IsNullOrWhiteSpace(bloque["MapaV2SinParada"]));
    }

    private static IEnumerable<Condicion> Hojas(Condicion? c)
    {
        if (c == null) yield break;
        if (c.Condiciones != null)
            foreach (Condicion h in c.Condiciones)
                foreach (Condicion x in Hojas(h)) yield return x;
        yield return c;
    }

    private static string[] LeerHjson(string idioma)
    {
        DirectoryInfo? dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (int nivel = 0; dir != null && nivel < 10; nivel++, dir = dir.Parent)
        {
            string candidato = Path.Combine(dir.FullName, "Localization", idioma + "_Mods.TerrakeepMod.hjson");
            if (File.Exists(candidato)) return File.ReadAllLines(candidato);
        }
        throw new DirectoryNotFoundException("No se encontro Localization/" + idioma + "_Mods.TerrakeepMod.hjson");
    }

    /// <summary>Claves de primer nivel de "Seccion: { Sub: { clave: valor } }" (indentacion con tabs).</summary>
    private static Dictionary<string, string> Bloque(string[] lineas, string seccion, string sub)
    {
        int i = Array.FindIndex(lineas, l => l == seccion + ": {");
        Assert.True(i >= 0, "no hay seccion " + seccion);
        int j = Array.FindIndex(lineas, i, l => l == "\t" + sub + ": {");
        Assert.True(j >= 0, "no hay bloque " + seccion + "." + sub);
        var r = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int k = j + 1; k < lineas.Length && lineas[k] != "\t}"; k++)
        {
            Match m = Regex.Match(lineas[k], @"^\t\t([A-Za-z0-9_]+):\s*(.*)$");
            if (m.Success) r[m.Groups[1].Value] = m.Groups[2].Value;
        }
        return r;
    }
}
