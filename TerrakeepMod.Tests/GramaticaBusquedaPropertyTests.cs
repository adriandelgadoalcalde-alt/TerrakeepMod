using CsCheck;
using TerrakeepMod.Common.Libreria;
using Xunit;

namespace TerrakeepMod.Tests.Libreria;

// KeepQA (17-sep-2026, paso 2/3 de la integracion de analisis externo aplicada a TerrakeepMod):
// property-based testing real con CsCheck sobre GramaticaBusqueda - la UNICA logica propia del
// mod que resulto ser pura de verdad tras investigar Common/ entero (ver bitacora.md de este
// mismo dia para el detalle de por que el resto no aplica). Las propiedades de abajo se derivan
// de las reglas documentadas en la cabecera del propio GramaticaBusqueda.cs (verificadas contra
// el codigo real, no de memoria), nunca del propio codigo a probar.
public class GramaticaBusquedaPropertyTests
{
    // Alfabeto ASCII imprimible normal, sin coma/almohadilla/punto (los tres caracteres con
    // significado especial en la gramatica) para las propiedades que construyen NOMBRES/TOOLTIPS
    // sinteticos - se quiere texto "de producto" realista, no basura que dispare otra regla sin
    // querer.
    private const string AlfabetoPalabra = "abcdefghijklmnopqrstuvwxyz0123456789";
    private static readonly Gen<string> GenPalabra = Gen.String[Gen.Char[AlfabetoPalabra], 2, 8];

    // Alfabeto mixto: mayusculas/minusculas + las vocales acentuadas y la eñe (español de
    // España), el mismo universo que ya prueba Fold en la app de escritorio (ver
    // LibrarySearchGrammarTests.Fold_QuitaTildesYMinusculizaSinTocarLoQueYaEstaLimpio).
    private const string AlfabetoConTildes = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZáéíóúÁÉÍÓÚñÑüÜ ";
    private static readonly Gen<string> GenTextoConTildes = Gen.String[Gen.Char[AlfabetoConTildes], 0, 40];

    private static readonly Gen<int> GenId = Gen.Int[0, 20_000];

    [Fact]
    public void Plegar_EsIdempotente()
    {
        GenTextoConTildes.Sample(s =>
        {
            string una = GramaticaBusqueda.Plegar(s);
            string dos = GramaticaBusqueda.Plegar(una);
            Assert.Equal(una, dos);
        }, iter: 500, seed: null);
    }

    [Fact]
    public void Plegar_QuitaTodaMarcaDiacritica()
    {
        GenTextoConTildes.Sample(s =>
        {
            string plegado = GramaticaBusqueda.Plegar(s);
            foreach (char c in plegado)
            {
                var categoria = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
                Assert.NotEqual(System.Globalization.UnicodeCategory.NonSpacingMark, categoria);
            }
        }, iter: 500, seed: null);
    }

    [Fact]
    public void Plegar_TextoAsciiSinTildes_CoincideConToLowerInvariant()
    {
        // Sin diacriticos de por medio, plegar es exactamente minusculizar (idem al contrato real
        // que documenta LibrarySearchGrammar.Fold: "solo pliega el lado de la comparacion").
        GenPalabra.Sample(s =>
        {
            Assert.Equal(s.ToLowerInvariant(), GramaticaBusqueda.Plegar(s));
        }, iter: 300, seed: null);
    }

    [Fact]
    public void Casa_ConsultaVaciaONula_SiempreCoincide()
    {
        var gen = Gen.Select(GenId, GenPalabra, GenPalabra,
            (id, nombre, tooltip) => (id, nombre, tooltip));
        gen.Sample(t =>
        {
            Assert.True(GramaticaBusqueda.Casa("", t.id, t.nombre, t.tooltip));
            Assert.True(GramaticaBusqueda.Casa(null!, t.id, t.nombre, t.tooltip));
        }, iter: 200, seed: null);
    }

    [Fact]
    public void Casa_TerminoDeUnSoloCaracter_SeIgnoraSiempre()
    {
        // Quirk real documentado: un termino de menos de 2 caracteres (tras Trim) no cuenta ni
        // como id ni como nombre - "5" suelto nunca encuentra nada, ni siquiera un objeto con "5"
        // en el nombre.
        var genCorto = Gen.Char[AlfabetoPalabra];
        var gen = Gen.Select(genCorto, GenId, GenPalabra, GenPalabra,
            (c, id, nombre, tooltip) => (consulta: c.ToString(), id, nombre, tooltip));
        gen.Sample(t =>
        {
            Assert.False(GramaticaBusqueda.Casa(t.consulta, t.id, t.nombre + t.consulta, t.tooltip));
        }, iter: 200, seed: null);
    }

    [Fact]
    public void Casa_AlmohadillaId_CoincideSoloConEseIdExacto()
    {
        var gen = Gen.Select(GenId, GenId, GenPalabra, GenPalabra,
            (idBuscado, idReal, nombre, tooltip) => (idBuscado, idReal, nombre, tooltip));
        gen.Sample(t =>
        {
            string consulta = "#" + t.idBuscado;
            bool esperado = t.idBuscado == t.idReal;
            Assert.Equal(esperado, GramaticaBusqueda.Casa(consulta, t.idReal, t.nombre, t.tooltip));
        }, iter: 500, seed: null);
    }

    [Fact]
    public void Casa_AlmohadillaRango_CoincideSoloDentroDelRangoAmbosExtremosIncluidos()
    {
        // lo/hi restringidos a no-negativos: con un extremo negativo la cadena "#-5-10" tiene DOS
        // guiones y CasaId (que busca el PRIMER '-') ya no representa el rango que se querria
        // construir - no es un bug (el codigo real de la app tiene el mismo comportamiento con la
        // misma ambiguedad de formato), es una restriccion honesta del dominio de esta propiedad
        // concreta, igual que ya hizo la familia con GenCoordenadaServidor en
        // PlrFilePropertyTests.cs.
        var gen = Gen.Select(Gen.Int[0, 20_000], Gen.Int[0, 20_000], Gen.Int[-1000, 21_000], GenPalabra, GenPalabra,
            (a, b, id, nombre, tooltip) => (lo: System.Math.Min(a, b), hi: System.Math.Max(a, b), id, nombre, tooltip));
        gen.Sample(t =>
        {
            string consulta = "#" + t.lo + "-" + t.hi;
            bool esperado = t.id >= t.lo && t.id <= t.hi;
            Assert.Equal(esperado, GramaticaBusqueda.Casa(consulta, t.id, t.nombre, t.tooltip));
        }, iter: 500, seed: null);
    }

    [Fact]
    public void Casa_ComaEsOrEntreTerminos()
    {
        // Metamorfica: Casa(t1 + "," + t2, ...) == Casa(t1, ...) || Casa(t2, ...). Los dos
        // terminos son palabras "limpias" (sin coma/almohadilla/punto), asi que cada mitad se
        // evalua de forma independiente por diseño.
        var gen = Gen.Select(GenPalabra, GenPalabra, GenId, GenPalabra, GenPalabra,
            (t1, t2, id, nombre, tooltip) => (t1, t2, id, nombre, tooltip));
        gen.Sample(t =>
        {
            bool combinado = GramaticaBusqueda.Casa(t.t1 + "," + t.t2, t.id, t.nombre, t.tooltip);
            bool separado = GramaticaBusqueda.Casa(t.t1, t.id, t.nombre, t.tooltip)
                || GramaticaBusqueda.Casa(t.t2, t.id, t.nombre, t.tooltip);
            Assert.Equal(separado, combinado);
        }, iter: 500, seed: null);
    }

    [Fact]
    public void Casa_EspacioEsAndDentroDeUnTermino_CoincideSoloSiElNombreContieneAmbasPalabras()
    {
        var gen = Gen.Select(GenPalabra, GenPalabra, GenId, GenPalabra,
            (w1, w2, id, relleno) => (w1, w2, id, relleno));
        gen.Sample(t =>
        {
            string nombreCompleto = t.w1 + " " + t.relleno + " " + t.w2;
            Assert.True(GramaticaBusqueda.Casa(t.w1 + " " + t.w2, t.id, nombreCompleto, null));
            // Falta la segunda palabra -> ya no debe coincidir. Casa hace coincidir por
            // SUBCADENA (IndexOf, no por palabra completa), asi que hay que evitar tambien el
            // falso positivo de que w2 aparezca "de casualidad" DENTRO de w1 o del relleno (con
            // un alfabeto de solo 36 caracteres y palabras de 2-8, es un caso real que CsCheck
            // encuentra en pocas iteraciones - no es un bug, es el contrato real y documentado de
            // Casa: coincide por subcadena a proposito).
            if (!t.relleno.Contains(t.w2) && !t.w1.Contains(t.w2))
            {
                Assert.False(GramaticaBusqueda.Casa(t.w1 + " " + t.w2, t.id, t.w1 + " " + t.relleno, null));
            }
        }, iter: 500, seed: null);
    }

    [Fact]
    public void Casa_PuntoPrefijo_BuscaSoloEnElTooltipNuncaEnElNombre()
    {
        var gen = Gen.Select(GenPalabra, GenId, GenPalabra,
            (palabra, id, otroTexto) => (palabra, id, otroTexto));
        gen.Sample(t =>
        {
            // La palabra esta en el tooltip pero NO en el nombre (nombre = otro texto distinto).
            string nombreSinLaPalabra = t.otroTexto;
            string tooltipConLaPalabra = "algo " + t.palabra + " mas";

            Assert.True(GramaticaBusqueda.Casa("." + t.palabra, t.id, nombreSinLaPalabra, tooltipConLaPalabra));
            if (!nombreSinLaPalabra.Contains(t.palabra))
            {
                // Sin el punto, se busca en el NOMBRE - que no la tiene - asi que no coincide
                // aunque el tooltip si la tenga.
                Assert.False(GramaticaBusqueda.Casa(t.palabra, t.id, nombreSinLaPalabra, tooltipConLaPalabra));
            }
        }, iter: 500, seed: null);
    }

    [Fact]
    public void Casa_SinTooltip_PuntoPrefijoNuncaCoincide()
    {
        var gen = Gen.Select(GenPalabra, GenId, GenPalabra,
            (palabra, id, nombre) => (palabra, id, nombre));
        gen.Sample(t =>
        {
            Assert.False(GramaticaBusqueda.Casa("." + t.palabra, t.id, t.nombre + t.palabra, null));
        }, iter: 300, seed: null);
    }
}
