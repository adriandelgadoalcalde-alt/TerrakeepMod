using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace TerrakeepMod.Tests.Localizacion;

// 02-oct-2026 (F4 de la Guía v2): "Distancia: {0} tiles away" en en-US_Mods.TerrakeepMod.hjson. En Hjson
// un valor SIN comillas que empieza por '{' se lee como un objeto, así que tModLoader no pudo cargar
// el mod ("Found '}' where a key name was expected ... line 2476") y el cliente se quedó en la
// pantalla de error. dotnet test seguía en verde porque nada leía el .hjson como lo lee el juego.
// Esta prueba recorre los dos .hjson reales y falla si un valor de una sola línea, fuera de un bloque
// ''' ... ''', empieza por '{' o '[' y sigue con más texto en la misma línea (eso nunca es abrir un
// objeto o una lista: tiene que ir entre comillas).
public class HjsonValoresTests
{
    private static readonly Regex ValorPeligroso = new(@"^\s*[^\s:/#'""]+\s*:\s*[\{\[]\s*\S", RegexOptions.Compiled);

    private static string RutaLocalizacion()
    {
        DirectoryInfo? dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (int nivel = 0; dir != null && nivel < 10; nivel++, dir = dir.Parent) {
            string candidato = Path.Combine(dir.FullName, "Localization");
            if (File.Exists(Path.Combine(candidato, "es-ES_Mods.TerrakeepMod.hjson"))) {
                return candidato;
            }
        }
        throw new DirectoryNotFoundException("No se encontro la carpeta Localization del mod");
    }

    [Theory]
    [InlineData("es-ES_Mods.TerrakeepMod.hjson")]
    [InlineData("en-US_Mods.TerrakeepMod.hjson")]
    public void NingunValorSinComillasEmpiezaPorLlaveOCorchete(string archivo)
    {
        string[] lineas = File.ReadAllLines(Path.Combine(RutaLocalizacion(), archivo));
        List<string> malas = new();
        bool enBloque = false;
        for (int i = 0; i < lineas.Length; i++) {
            string l = lineas[i];
            int comillas = CuentaTriples(l);
            if (enBloque) {
                if (comillas % 2 == 1) enBloque = false;
                continue;
            }
            if (comillas % 2 == 1) {
                enBloque = true;
                continue;
            }
            if (ValorPeligroso.IsMatch(l)) {
                malas.Add((i + 1) + ": " + l.Trim());
            }
        }
        Assert.True(malas.Count == 0, archivo + ": valores que Hjson leería como objeto o lista (ponlos entre comillas):\n" + string.Join("\n", malas));
    }

    [Fact]
    public void LaPruebaDetectaElCasoReal()
    {
        Assert.Matches(ValorPeligroso, "\t\tDistancia: {0} tiles away");
        Assert.DoesNotMatch(ValorPeligroso, "\t\tDistancia: \"{0} tiles away\"");
        Assert.DoesNotMatch(ValorPeligroso, "\tGuiaV2: {");
        Assert.DoesNotMatch(ValorPeligroso, "\t\tDistancia: a {0} casillas");
    }

    private static int CuentaTriples(string l)
    {
        int n = 0, i = 0;
        while ((i = l.IndexOf("'''", i, StringComparison.Ordinal)) >= 0) {
            n++;
            i += 3;
        }
        return n;
    }
}
