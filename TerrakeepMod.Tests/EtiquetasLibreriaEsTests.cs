using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Xunit;

namespace TerrakeepMod.Tests.Libreria;

// KeepQA (29-sep-2026): huecos reales de paridad con Terrakeep escritorio (auditoria
// "auditoria-paridad-29sep.md", hueco 1, "Alta, barata"). El JSON real que empaqueta el .tmod
// (Assets/vanilla_library_labels_es.json, leido en runtime por ArbolLibreria.ParsearArchivos via
// LibraryLabelCatalog.LoadFromStream) se habia quedado congelado en la version SIN corregir de
// Terrasavr.es-ES.json real (sin tildes/eñes y con erratas: "Dificil", "Carmesi", "Pinonita",
// "Vortice", "Marmol"...) y con terminologia NO oficial en varias carpetas ("Clorofila" en vez de
// "Clorofita" - un mineral, no el pigmento -, "Pearlwood"/"Bamboo" sin traducir, "Dorado" en vez
// de "Oro", "Lavabos"/"Inodoros" en vez de "Fregaderos"/"Retretes", "Librera"/"Sofa" con erratas,
// "Accessorios" con doble s, "Objectos" con "ct"...) - exactamente el mismo lote de correcciones
// que Terrakeep escritorio ya aplico hoy en su propio Assets/vanilla_library_labels_es.json
// (commits 968d6e67 + los de L-01 de la FASE D, scripts/extraer-etiquetas-libreria-es.js). Las
// 336 claves de los dos catalogos son IDENTICAS (mismo namespace "lib.item" + mismas subcategorias
// por subtipo) - el mod solo necesitaba los VALORES corregidos del escritorio, no una retraduccion
// aparte. Esta prueba es un muestreo real (no las 336 claves) de las correcciones mas visibles,
// para detectar de inmediato si el archivo del mod vuelve a desincronizarse del de escritorio.
public class EtiquetasLibreriaEsTests
{
    private static Dictionary<string, string> CargarEtiquetas()
    {
        string ruta = LocalizarAssetsLibreriaEs();
        using FileStream flujo = File.OpenRead(ruta);
        return JsonSerializer.Deserialize<Dictionary<string, string>>(flujo)
            ?? throw new InvalidDataException($"{ruta} no se pudo deserializar.");
    }

    private static string LocalizarAssetsLibreriaEs()
    {
        DirectoryInfo? dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (int nivel = 0; dir != null && nivel < 10; nivel++, dir = dir.Parent) {
            string candidato = Path.Combine(dir.FullName, "Assets", "vanilla_library_labels_es.json");
            if (File.Exists(candidato)) {
                return candidato;
            }
        }
        throw new DirectoryNotFoundException(
            "No se encontro Assets/vanilla_library_labels_es.json subiendo desde " + AppContext.BaseDirectory);
    }

    [Theory]
    // Tildes/eñes que el es-ES sin corregir de Terrasavr no trae (mismo lote L-01 de escritorio).
    [InlineData("Pre-Hardmode", "Pre-Modo Difícil")]
    [InlineData("Hardmode", "Modo Difícil")]
    [InlineData("Vortex", "Vórtice")]
    [InlineData("Marble", "Mármol")]
    [InlineData("Lesion", "Lesión")]
    [InlineData("Page $1", "Página $1")]
    [InlineData("Items by ID", "Objetos por ID")]
    [InlineData("Categories", "Categorías")]
    // Terminologia OFICIAL de Terraria/Calamity en ES (contrastada con es-ES.Items.json), no la
    // traduccion literal/erronea que trae Terrasavr por defecto.
    [InlineData("Hallowed & Chlorophyte", "Sagrado & Clorofita")]
    [InlineData("Demonite & Crimtane", "Mineral Endemoniado & Mineral Carmesí")]
    [InlineData("Shroomite & Ectoplasm", "Piñonita & Ectoplasma")]
    [InlineData("Pearlwood", "Madera Perlada")]
    [InlineData("Bamboo", "Bambú")]
    [InlineData("Golden", "Oro")]
    [InlineData("Sink", "Fregaderos")]
    [InlineData("Toilet", "Retretes")]
    [InlineData("Bookcase", "Librerías")]
    [InlineData("Sofa", "Sofás")]
    [InlineData("Accessories ($1)", "Accesorios ($1)")]
    public void Etiqueta_CoincideConLaTerminologiaCorregidaDeEscritorio(string clave, string esperado)
    {
        Dictionary<string, string> etiquetas = CargarEtiquetas();
        Assert.True(etiquetas.ContainsKey(clave), $"Falta la clave real \"{clave}\" en el catalogo.");
        Assert.Equal(esperado, etiquetas[clave]);
    }
}
