using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.UI;
using TerrakeepMod.Common.Ajustes;
using TerrakeepMod.Common.Exploracion;
using TerrakeepMod.UI.Personaje.Widgets;

namespace TerrakeepMod.UI.Exploracion
{
	/// <summary>
	/// <b>Idea 10 del catálogo de funciones ("rebobinar el mundo"):</b> marca una foto REAL de los
	/// tiles de un cuadrado alrededor del jugador (terreno, paredes, líquidos, pendientes, pintura,
	/// cables) Y de los cofres reales que caigan dentro, y lo devuelve todo tal cual estaba cuando
	/// se pulsa "Rebobinar ahora" - un deshacer de zona, del mismo tipo que ya existe para objetos
	/// (<see cref="TerrakeepMod.Common.Undo.PilaDeSnapshots"/>) pero aplicado al mundo.
	/// </summary>
	/// <remarks>
	/// <b>Esta clase es solo la VISTA.</b> Los datos reales (la foto, la comparación) viven en
	/// <see cref="EstadoRebobinar"/>, una clase estática ajena al ciclo de vida de este
	/// <c>UIElement</c> - ver su XMLdoc para el bug real que eso arregla (la foto se perdia al
	/// cerrar el panel, porque antes vivia aqui mismo, en campos de instancia de un widget que
	/// <c>PanelTerrakeepSystem</c> destruye en cada cierre). Todos los miembros públicos de esta
	/// clase (<see cref="HayFoto"/>, <see cref="DiferentesAhora"/>, <see cref="Marcar"/>...)
	/// mantienen el MISMO nombre y forma que antes del arreglo - reenvían a
	/// <see cref="EstadoRebobinar"/> tal cual, así que <c>AutopruebaExploracion</c> y el resto del
	/// panel no necesitan tocarse.
	/// </remarks>
	public class PestanaRebobinar : UIElement
	{
		/// <summary>Radio en tiles alrededor del jugador que se fotografía. Ver
		/// <see cref="EstadoRebobinar.RadioTiles"/>.</summary>
		public const int RadioTiles = EstadoRebobinar.RadioTiles;

		private EtiquetaTk _estado;
		private EtiquetaTk _diferencia;
		private EtiquetaTk _avisoMultijugador;
		private BotonTk _botonMarcar;
		private BotonTk _botonRebobinar;
		private string _ultimoResultado = "";

		/// <summary>true si hay una foto guardada ahora mismo (sobrevive a cerrar el panel).
		/// Público para la autoprueba.</summary>
		public bool HayFoto => EstadoRebobinar.HayFoto;

		/// <summary>Cuántos tiles reales han cambiado desde que se marcó la foto, o -1 si todavía no
		/// se ha calculado/no hay foto. Se recalcula solo cada 30 fotogramas
		/// (<see cref="RebobinarSystem"/>), con o sin panel abierto. Público para la autoprueba.</summary>
		public int DiferentesAhora => EstadoRebobinar.DiferentesAhora;

		/// <summary>Esquina superior izquierda real (en tiles) de la última foto marcada. Público
		/// para la autoprueba.</summary>
		public (int x, int y, int ancho, int alto) AreaFoto => EstadoRebobinar.AreaFoto;

		/// <summary>Cuántos cofres reales cayeron dentro de la última foto. Público para la
		/// autoprueba.</summary>
		public int CofresEnFoto => EstadoRebobinar.CofresEnFoto;

		/// <summary>Cuántos de esos cofres tienen contenido distinto AHORA MISMO, o -1 si no se ha
		/// calculado/no hay foto. Público para la autoprueba.</summary>
		public int CofresDistintosAhora => EstadoRebobinar.CofresDistintosAhora;

		/// <summary>Índice real en <c>Main.chest[]</c> del cofre N-ésimo de la última foto. -1 si no
		/// existe.</summary>
		public int IndiceDeCofreEnFoto(int posicion) => EstadoRebobinar.IndiceDeCofreEnFoto(posicion);

		public PestanaRebobinar()
		{
			Width.Set(0f, 1f);
			Height.Set(0f, 1f);

			EtiquetaTk titulo = new EtiquetaTk(() => Idiomas.Texto("Exploracion.Rebobinar.Titulo"), 0.95f, 700f, 26f);
			Append(titulo);

			EtiquetaTk explicacion = new EtiquetaTk(
				() => EtiquetaTk.PartirEnLineas(Idiomas.Texto("Exploracion.Rebobinar.Explicacion", RadioTiles * 2 + 1), 700f, 0.75f),
				0.75f, 700f, 40f);
			explicacion.Top.Set(30f, 0f);
			explicacion.ColorTexto = EstiloTk.TextoSuave;
			Append(explicacion);

			_avisoMultijugador = new EtiquetaTk(
				() => Main.netMode == NetmodeID.SinglePlayer
					? ""
					: EtiquetaTk.PartirEnLineas(Idiomas.Texto("Exploracion.Rebobinar.SoloUnJugador"), 700f, 0.72f),
				0.72f, 700f, 22f);
			_avisoMultijugador.Top.Set(76f, 0f);
			_avisoMultijugador.ColorTexto = EstiloTk.TextoAviso;
			Append(_avisoMultijugador);

			_botonMarcar = new BotonTk(Idiomas.Texto("Exploracion.Rebobinar.Marcar"), 0.85f);
			_botonMarcar.Top.Set(108f, 0f);
			_botonMarcar.Width.Set(220f, 0f);
			_botonMarcar.Height.Set(34f, 0f);
			_botonMarcar.AlPulsar += Marcar;
			Append(_botonMarcar);

			_botonRebobinar = new BotonTk(Idiomas.Texto("Exploracion.Rebobinar.Rebobinar"), 0.85f);
			_botonRebobinar.Top.Set(108f, 0f);
			_botonRebobinar.Left.Set(232f, 0f);
			_botonRebobinar.Width.Set(220f, 0f);
			_botonRebobinar.Height.Set(34f, 0f);
			_botonRebobinar.Habilitado = false;
			_botonRebobinar.AlPulsar += Rebobinar;
			Append(_botonRebobinar);

			_estado = new EtiquetaTk(() => TextoEstado(), 0.78f, 700f, 22f);
			_estado.Top.Set(150f, 0f);
			Append(_estado);

			_diferencia = new EtiquetaTk(() => TextoDiferencia(), 0.78f, 700f, 44f);
			_diferencia.Top.Set(174f, 0f);
			_diferencia.ColorTexto = EstiloTk.TextoSuave;
			Append(_diferencia);

			EtiquetaTk resultado = new EtiquetaTk(() => _ultimoResultado, 0.75f, 700f, 44f);
			resultado.Top.Set(222f, 0f);
			resultado.ColorTexto = EstiloTk.TextoAviso;
			Append(resultado);
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);

			bool unJugador = Main.netMode == NetmodeID.SinglePlayer;
			_botonMarcar.Habilitado = unJugador;
			_botonRebobinar.Habilitado = unJugador && HayFoto;

			// La comparación en si ya NO depende de que esta pestaña este visible - la lleva
			// RebobinarSystem.PostUpdateEverything, siempre. Aqui solo queda el texto/botones.
		}

		private string TextoEstado()
		{
			if (!HayFoto) {
				return Idiomas.Texto("Exploracion.Rebobinar.SinMarcar");
			}
			var area = AreaFoto;
			int segundos = (int)(DateTime.UtcNow - EstadoRebobinar.Momento).TotalSeconds;
			return Idiomas.Texto("Exploracion.Rebobinar.Marcada", area.x + area.ancho / 2, area.y + area.alto / 2, segundos);
		}

		private string TextoDiferencia()
		{
			if (!HayFoto) {
				return "";
			}
			if (DiferentesAhora < 0) {
				return Idiomas.Texto("Exploracion.Rebobinar.Calculando");
			}

			var area = AreaFoto;
			string tiles = DiferentesAhora == 0
				? Idiomas.Texto("Exploracion.Rebobinar.SinCambios")
				: Idiomas.Texto("Exploracion.Rebobinar.Cambiados", DiferentesAhora, area.ancho * area.alto);

			if (CofresEnFoto == 0) {
				return tiles;
			}

			return tiles + "\n" + Idiomas.Texto("Exploracion.Rebobinar.Cofres", CofresEnFoto, CofresDistintosAhora);
		}

		/// <summary>Toma la foto de verdad, centrada en el jugador. Público para la autoprueba.</summary>
		public void Marcar()
		{
			EstadoRebobinar.Marcar();
			var area = AreaFoto;
			_ultimoResultado = Idiomas.Texto("Exploracion.Rebobinar.Marcado", area.ancho, area.alto, area.x, area.y, CofresEnFoto);
		}

		/// <summary>Cuenta cuántos tiles reales del área fotografiada son distintos AHORA MISMO.
		/// Público para la autoprueba.</summary>
		public void RecalcularDiferencia() => EstadoRebobinar.RecalcularDiferencia();

		/// <summary>Devuelve de verdad los tiles del área a como estaban en la foto. Público para la
		/// autoprueba.</summary>
		public void Rebobinar()
		{
			var area = AreaFoto;
			int totalTiles = area.ancho * area.alto;
			int totalCofres = CofresEnFoto;
			(int distintosAntes, int cofresDistintosAntes) = EstadoRebobinar.Rebobinar();
			_ultimoResultado = Idiomas.Texto("Exploracion.Rebobinar.Rebobinado", distintosAntes, totalTiles,
				cofresDistintosAntes, totalCofres);
		}
	}
}
