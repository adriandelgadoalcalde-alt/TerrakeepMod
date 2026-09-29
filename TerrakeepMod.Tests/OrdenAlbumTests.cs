using System;
using System.Collections.Generic;
using System.Linq;
using CsCheck;
using TerrakeepMod.Common.Hitos;
using Xunit;

namespace TerrakeepMod.Tests.Hitos;

// 29-sep-2026 (v0.6.1): la pestaña "Álbum" enseñaba los hitos mezclados (29/09, 15/09, 20/09,
// 16/09...) porque AlbumHitos.Listar solo invertia el orden de insercion de album.json. OrdenAlbum
// es la pieza pura que ahora ordena de verdad por fecha - ver su cabecera.
public class OrdenAlbumTests
{
    private sealed record Entrada(string Nombre, DateTime Fecha);

    private static DateTime F(string texto)
    {
        Assert.True(OrdenAlbum.IntentarLeerFecha(texto, out DateTime fecha), texto);
        return fecha;
    }

    [Fact]
    public void OrdenDeInsercionMezclado_SaleDeLaMasRecienteALaMasAntigua()
    {
        // Las cuatro primeras filas reales de la captura del bug, en el orden en que salian.
        var entradas = new List<Entrada> {
            new("Plantera", F("2026-09-16 02:21:00")),
            new("El Ejército Goblin", F("2026-09-20 11:20:00")),
            new("The Plaguebringer Goliath", F("2026-09-15 22:59:00")),
            new("Lluvia Ácida", F("2026-09-29 14:44:00")),
        };

        List<Entrada> ordenadas = OrdenAlbum.MasRecientePrimero(entradas, e => e.Fecha);

        Assert.Equal(
            new[] { "Lluvia Ácida", "El Ejército Goblin", "Plantera", "The Plaguebringer Goliath" },
            ordenadas.Select(e => e.Nombre));
    }

    [Fact]
    public void AIgualFecha_VaPrimeroLaAnadidaDespues()
    {
        DateTime misma = F("2026-09-29 10:00:00");
        var entradas = new List<Entrada> { new("primera", misma), new("segunda", misma), new("tercera", misma) };

        Assert.Equal(new[] { "tercera", "segunda", "primera" },
            OrdenAlbum.MasRecientePrimero(entradas, e => e.Fecha).Select(e => e.Nombre));
    }

    [Fact]
    public void ListaVacia_DevuelveListaVacia()
    {
        Assert.Empty(OrdenAlbum.MasRecientePrimero(new List<Entrada>(), e => e.Fecha));
    }

    [Theory]
    [InlineData("2026-09-29 14:44:00", 2026, 9, 29, 14, 44)]
    [InlineData("2026-01-02 03:04:05", 2026, 1, 2, 3, 4)]
    public void FechaDelFormatoDelMod_SeLeeExacta(string texto, int a, int m, int d, int h, int min)
    {
        Assert.True(OrdenAlbum.IntentarLeerFecha(texto, out DateTime fecha));
        Assert.Equal(new DateTime(a, m, d, h, min, fecha.Second), fecha);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("no es una fecha")]
    public void FechaIlegible_DevuelveMinValue_YQuedaAlFinal(string? texto)
    {
        Assert.False(OrdenAlbum.IntentarLeerFecha(texto!, out DateTime fecha));
        Assert.Equal(DateTime.MinValue, fecha);

        var entradas = new List<Entrada> { new("rota", fecha), new("buena", F("2026-09-01 00:00:00")) };
        Assert.Equal("rota", OrdenAlbum.MasRecientePrimero(entradas, e => e.Fecha).Last().Nombre);
    }

    [Fact]
    public void Propiedad_EsUnaPermutacionYNoCreceNuncaLaFecha()
    {
        Gen.Int[0, 2000].Select(n => new DateTime(2026, 1, 1).AddHours(n)).List[0, 60]
            .Sample(fechas => {
                var entradas = fechas.Select((f, i) => new Entrada("e" + i, f)).ToList();
                List<Entrada> ordenadas = OrdenAlbum.MasRecientePrimero(entradas, e => e.Fecha);

                Assert.Equal(entradas.Count, ordenadas.Count);
                Assert.Equal(entradas.Select(e => e.Nombre).OrderBy(n => n),
                    ordenadas.Select(e => e.Nombre).OrderBy(n => n));
                for (int i = 1; i < ordenadas.Count; i++) {
                    Assert.True(ordenadas[i - 1].Fecha >= ordenadas[i].Fecha);
                }
            });
    }
}
