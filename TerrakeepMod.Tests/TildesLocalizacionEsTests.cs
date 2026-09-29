using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace TerrakeepMod.Tests.Localizacion;

// 29-sep-2026 (v0.6.1): el aviso "Tienes Calamity instalado" y casi todo el texto de los 25 tramos
// de Calamity de la Guia salian SIN tildes en pantalla ("progresion", "arbol", "despues", "Lluvia
// Acida", "Modo Dificil"...): 88 lineas del es-ES_Mods.TerrakeepMod.hjson, escritas a mano fuera
// de scripts/generar-localizacion.py. Esta prueba lee el .hjson REAL que empaqueta el .tmod y falla
// si vuelve a aparecer cualquiera de estas formas sin tilde en un VALOR (nunca en una clave ni en un
// comentario, donde el ASCII es a proposito). Solo palabras sin ambiguedad: "mas", "esta", "si"...
// pueden ir sin tilde correctamente y no se vigilan aqui.
public class TildesLocalizacionEsTests
{
    private static readonly string[] SinTildeProhibidas = {
        "progresion", "guia", "arbol", "ademas", "dificil", "despues", "segun", "corrupcion",
        "carmesi", "nucleo", "ultimo", "acida", "trio", "demoniaco", "linea", "invocacion",
        "oceano", "pocion", "curacion", "rapido", "unica", "unico", "mecanicos", "patron",
        "subterranea", "lagrima", "caustica", "lunatico", "corazon", "usalo", "muchisimo", "aqui",
        "manten", "respiracion", "acuatica", "caparazon", "celula", "cosmicas", "infeccion",
        "laseres", "penultimo", "presion", "practica superada",
        // Nombres OFICIALES es-ES del juego (tModLoader.dll, es_ES.NPCs.json): "Esqueletrón",
        // "Esqueletrón mayor" y "Gólem" - el mod los escribia sin tilde (y "Esqueletron Prime").
        "esqueletron", "esqueletron prime", "golem",
    };

    private static string RutaHjsonEs()
    {
        DirectoryInfo? dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (int nivel = 0; dir != null && nivel < 10; nivel++, dir = dir.Parent) {
            string candidato = Path.Combine(dir.FullName, "Localization", "es-ES_Mods.TerrakeepMod.hjson");
            if (File.Exists(candidato)) {
                return candidato;
            }
        }
        throw new DirectoryNotFoundException("No se encontro Localization/es-ES_Mods.TerrakeepMod.hjson");
    }

    private static IEnumerable<(int Linea, string Valor)> Valores()
    {
        string[] lineas = File.ReadAllLines(RutaHjsonEs());
        for (int i = 0; i < lineas.Length; i++) {
            string l = lineas[i].Trim();
            if (l.Length == 0 || l.StartsWith("//", StringComparison.Ordinal) || l == "{" || l == "}") {
                continue;
            }
            Match m = Regex.Match(l, @"^[\w.]+\s*:\s*(.*)$");
            yield return (i + 1, m.Success ? m.Groups[1].Value : l);
        }
    }

    [Fact]
    public void NingunValorEnEspanolHaPerdidoLaTilde()
    {
        List<string> fallos = new();
        foreach ((int linea, string valor) in Valores()) {
            foreach (string palabra in SinTildeProhibidas) {
                if (Regex.IsMatch(valor, @"(?<![\p{L}])" + Regex.Escape(palabra) + @"(?![\p{L}])",
                        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)) {
                    fallos.Add($"L{linea} \"{palabra}\": {valor[..Math.Min(90, valor.Length)]}");
                }
            }
        }
        Assert.True(fallos.Count == 0, "Textos sin tilde:\n" + string.Join("\n", fallos.Take(40)));
    }

    [Fact]
    public void ElAvisoDeCalamityLlevaSusTildes()
    {
        string aviso = Valores().Select(v => v.Valor)
            .First(v => v.StartsWith("Con Calamity la progres", StringComparison.Ordinal));
        Assert.Contains("progresión", aviso, StringComparison.Ordinal);
        Assert.Contains("guía", aviso, StringComparison.Ordinal);
        Assert.Contains("árbol", aviso, StringComparison.Ordinal);
        Assert.Contains("además", aviso, StringComparison.Ordinal);
    }
}
