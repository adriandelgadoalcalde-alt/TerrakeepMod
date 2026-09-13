using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Terraria.GameContent.UI.Elements;
using Terraria.UI;
using TerrakeepMod.Common.Ajustes;
using TerrakeepMod.Common.Hitos;
using TerrakeepMod.UI.Personaje.Widgets;

namespace TerrakeepMod.UI.Hitos
{
	/// <summary>
	/// Pestaña "Álbum": la lista de capturas automáticas de hito, más reciente primero.
	/// </summary>
	/// <remarks>
	/// <b>Lista con fecha, no miniaturas.</b> El encargo admitía las dos ("miniaturas o lista con
	/// fecha"); se elige la lista a propósito: cargar cada <c>.png</c> como <c>Texture2D</c> a la
	/// resolución real del back buffer solo para pintar un cuadradito de 40x40 es memoria de vídeo
	/// que crece sin límite según se acumulan hitos en una partida larga (un álbum de 50 capturas a
	/// 1920x1080 son ~400 MB de textura viva solo para el panel), por una miniatura que además
	/// saldría borrosa al reescalarla tanto. Pulsar una fila abre el archivo real con el visor de
	/// imágenes del sistema (<c>Process.Start</c>, igual que "Abrir carpeta"): se ve la captura
	/// entera, a su resolución real, sin gastar nada mientras el panel está abierto.
	/// </remarks>
	public class ContenidoAlbum : UIElement
	{
		private UIList _lista;
		private EtiquetaTk _resumen;
		private List<EntradaAlbum> _entradas = new List<EntradaAlbum>();

		/// <summary>Las entradas mostradas ahora mismo. Pública para que la autoprueba pueda
		/// comprobar con datos reales que un hito recién disparado aparece tras "Actualizar".</summary>
		public IReadOnlyList<EntradaAlbum> Entradas => _entradas;

		public ContenidoAlbum()
		{
			Width.Set(0f, 1f);
			Height.Set(0f, 1f);
			Construir();
			Recargar();
		}

		private void Construir()
		{
			_resumen = new EtiquetaTk(TextoResumen, 0.85f, 700f, 24f);
			_resumen.Width.Set(-336f, 1f);
			_resumen.Top.Set(4f, 0f);
			Append(_resumen);

			BotonTk actualizar = new BotonTk(Idiomas.Texto("Hitos.Actualizar"), EstiloTk.EscalaBoton);
			actualizar.Ayuda = () => Idiomas.Texto("Hitos.ActualizarAyuda");
			actualizar.Width.Set(150f, 0f);
			actualizar.Height.Set(28f, 0f);
			actualizar.Left.Set(-316f, 1f);
			actualizar.Top.Set(0f, 0f);
			actualizar.AlPulsar += Recargar;
			Append(actualizar);

			BotonTk abrirCarpeta = new BotonTk(Idiomas.Texto("Hitos.AbrirCarpeta"), EstiloTk.EscalaBoton);
			abrirCarpeta.Ayuda = () => Idiomas.Texto("Hitos.AbrirCarpetaAyuda");
			abrirCarpeta.Width.Set(150f, 0f);
			abrirCarpeta.Height.Set(28f, 0f);
			abrirCarpeta.HAlign = 1f;
			abrirCarpeta.Top.Set(0f, 0f);
			abrirCarpeta.AlPulsar += AbrirCarpeta;
			Append(abrirCarpeta);

			UIPanel caja = new UIPanel();
			caja.Width.Set(0f, 1f);
			caja.Top.Set(36f, 0f);
			caja.Height.Set(-36f, 1f);
			caja.BackgroundColor = EstiloTk.FondoCaja;
			caja.BorderColor = new Color(0, 0, 0, 0);
			caja.SetPadding(6f);
			Append(caja);

			_lista = new UIList();
			_lista.Width.Set(-24f, 1f);
			_lista.Height.Set(0f, 1f);
			_lista.ListPadding = 4f;
			caja.Append(_lista);

			UIScrollbar barra = new UIScrollbar();
			barra.Width.Set(20f, 0f);
			barra.Height.Set(0f, 1f);
			barra.HAlign = 1f;
			barra.SetView(100f, 1000f);
			caja.Append(barra);
			_lista.SetScrollbar(barra);
		}

		/// <summary>
		/// Vuelve a leer el álbum del disco y repinta la lista entera. Pública: la usa el botón
		/// "Actualizar" y la autoprueba, que necesita ver un hito recién disparado sin cerrar y
		/// volver a abrir el panel.
		/// </summary>
		public void Recargar()
		{
			_entradas = AlbumHitos.Listar();
			_lista.Clear();

			if (_entradas.Count == 0) {
				EtiquetaTk vacio = new EtiquetaTk(() => Idiomas.Texto("Hitos.Vacio"), 0.8f, 700f, 60f);
				vacio.ColorTexto = EstiloTk.TextoSuave;
				vacio.Width.Set(0f, 1f);
				_lista.Add(vacio);
				return;
			}

			foreach (EntradaAlbum entrada in _entradas) {
				EntradaAlbum actual = entrada;
				string texto = Idiomas.Texto("Hitos.Fila",
					actual.Fecha.ToString("dd/MM/yyyy HH:mm"), actual.Nombre);

				BotonTk fila = new BotonTk(texto, 0.75f);
				fila.Width.Set(0f, 1f);
				fila.Height.Set(28f, 0f);
				fila.Ayuda = () => Idiomas.Texto("Hitos.FilaAyuda", actual.Personaje, actual.Mundo);
				fila.AlPulsar += () => AbrirCaptura(actual);
				_lista.Add(fila);
			}
		}

		private string TextoResumen()
		{
			return _entradas.Count == 0
				? Idiomas.Texto("Hitos.ResumenVacio")
				: Idiomas.Texto("Hitos.Resumen", _entradas.Count);
		}

		private static void AbrirCarpeta()
		{
			try {
				System.IO.Directory.CreateDirectory(AlbumHitos.CarpetaAbsoluta);
				Process.Start(new ProcessStartInfo {
					FileName = AlbumHitos.CarpetaAbsoluta,
					UseShellExecute = true
				});
			}
			catch (Exception e) {
				RegistroHitos.Aviso(Terrakeep.LogTag + " Álbum: no se pudo abrir la carpeta: " +
					e.GetType().Name + ": " + e.Message);
			}
		}

		private static void AbrirCaptura(EntradaAlbum entrada)
		{
			try {
				string ruta = System.IO.Path.Combine(AlbumHitos.CarpetaAbsoluta, entrada.Archivo);
				Process.Start(new ProcessStartInfo {
					FileName = ruta,
					UseShellExecute = true
				});
			}
			catch (Exception e) {
				RegistroHitos.Aviso(Terrakeep.LogTag + " Álbum: no se pudo abrir la captura \"" +
					entrada.Archivo + "\": " + e.GetType().Name + ": " + e.Message);
			}
		}
	}
}
