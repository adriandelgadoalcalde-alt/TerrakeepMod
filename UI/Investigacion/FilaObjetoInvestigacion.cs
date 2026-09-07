using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.UI;
using TerrakeepMod.Common.Ajustes;
using TerrakeepMod.Common.Investigacion;
using TerrakeepMod.UI.Personaje.Widgets;

namespace TerrakeepMod.UI.Investigacion
{
	/// <summary>
	/// Una fila de la lista de objetos: el slot con el icono real del juego, el nombre, el "x/N"
	/// que lleva investigado y un boton que hace lo unico que falta hacer con ese objeto
	/// (investigarlo si le falta, quitarle la investigacion si ya esta hecho).
	/// <para />
	/// El boton cambia de texto en vez de haber dos botones siempre: no tiene ningun sentido
	/// ofrecer "Investigar" en algo ya investigado, y con 40 objetos por carpeta la diferencia
	/// entre una columna de botones y dos se nota.
	/// </summary>
	public class FilaObjetoInvestigacion : UIElement
	{
		/// <summary>Alto MINIMO de una fila (nombre en una sola linea). Ver
		/// <see cref="ActualizarLayout"/>: crece de verdad si el nombre no cabe en una linea, nunca
		/// se queda en un numero fijo que recorte texto.</summary>
		public const float AltoMinimo = 42f;

		private const float EscalaNombre = 0.82f;
		private const float EscalaEstado = 0.7f;
		private const float XTexto = 46f;
		private const float AnchoBoton = 112f;
		private const float MargenDerecho = 6f;
		private const float AltoBoton = 28f;

		private readonly BotonTk _boton;

		/// <summary>Tipo de objeto de esta fila.</summary>
		public readonly int Tipo;

		/// <summary>Se dispara al pulsar el boton, con el tipo y si lo que se pide es investigar
		/// (true) o quitar (false).</summary>
		public event Action<int, bool> AlPulsar;

		/// <summary>Nombre YA envuelto (linea 1, mismo patron que <c>PestanaBuffs.CrearFilaBuff</c>:
		/// el nombre nunca se recorta con "..." - antes lo hacia por NUMERO DE CARACTERES, ni
		/// siquiera medido con la fuente real). Ver <see cref="ActualizarLayout"/>.</summary>
		private string _nombrePartido;

		/// <summary>Alto real que ocupa <see cref="_nombrePartido"/> ya envuelto.</summary>
		private float _altoNombre = 16f;

		/// <summary>Y (relativa a esta fila) donde empieza la linea 2 (estado + boton).</summary>
		private float _yLinea2 = 22f;

		public FilaObjetoInvestigacion(int tipo)
		{
			Tipo = tipo;
			_nombrePartido = EstadoInvestigacion.NombreObjeto(tipo);
			Width.Set(0f, 1f);
			Height.Set(AltoMinimo, 0f);

			SlotMuestraInvestigacion slot = new SlotMuestraInvestigacion(tipo);
			slot.Left.Set(2f, 0f);
			slot.Top.Set(3f, 0f);
			Append(slot);

			_boton = new BotonTk(Idiomas.Texto("Investigacion.Investigar"), 0.75f);
			_boton.Width.Set(AnchoBoton, 0f);
			_boton.Height.Set(AltoBoton, 0f);
			_boton.HAlign = 1f;
			_boton.Top.Set(_yLinea2, 0f);
			_boton.AlPulsar += () => {
				if (AlPulsar != null) {
					AlPulsar(Tipo, !EstadoInvestigacion.Completo(Tipo));
				}
			};
			Append(_boton);
		}

		/// <summary>
		/// Mide el nombre ENVUELTO al ancho real de la linea 1 (todo el ancho de la fila, nadie mas
		/// compite por ese espacio) y devuelve el alto que la fila necesita para el nombre YA
		/// envuelto mas la linea 2 (estado + boton) - mismo patron de dos lineas que
		/// <c>PestanaBuffs.CrearFilaBuff</c>/<c>AjustarAltoFilasActivas</c>. Lo llama
		/// <see cref="ContenidoInvestigacion.AjustarAltoFilasObjeto"/> cada fotograma, porque el
		/// ancho disponible cambia con la resolucion de la ventana.
		/// </summary>
		public float ActualizarLayout()
		{
			CalculatedStyle dim = GetDimensions();
			if (dim.Width <= 0f) {
				return Height.Pixels > 0f ? Height.Pixels : AltoMinimo;
			}

			float anchoNombre = dim.Width - XTexto - MargenDerecho;
			if (anchoNombre < 20f) {
				anchoNombre = 20f;
			}

			_nombrePartido = EtiquetaTk.PartirEnLineas(EstadoInvestigacion.NombreObjeto(Tipo), anchoNombre, EscalaNombre);
			_altoNombre = FontAssets.MouseText.Value.MeasureString(_nombrePartido).Y * EscalaNombre;
			if (_altoNombre < 16f) {
				_altoNombre = 16f;
			}

			_yLinea2 = 3f + _altoNombre + 3f;
			_boton.Top.Set(_yLinea2, 0f);

			return _yLinea2 + AltoBoton + 3f;
		}

		/// <summary>
		/// Pulsa de verdad el boton de esta fila, disparando su <c>OnLeftClick</c> con
		/// <c>UIElement.LeftClick</c> - el mismo camino exacto que recorre un clic de raton una vez
		/// resuelto sobre que elemento cae. Lo usa el arnes de pruebas para ejercitar el boton de
		/// punta a punta sin depender de mover el raton (misma tecnica que ya uso WS4 con las
		/// pildoras de clase). Devuelve el texto que tenia el boton al pulsarlo.
		/// </summary>
		public string Pulsar()
		{
			string texto = _boton.Texto;
			_boton.LeftClick(new UIMouseEvent(_boton, _boton.GetDimensions().Center()));
			return texto;
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);

			bool completo = EstadoInvestigacion.Completo(Tipo);
			_boton.FijarTexto(Idiomas.Texto(completo
				? "Investigacion.Quitar"
				: "Investigacion.Investigar"));
			_boton.Ayuda = () => Idiomas.Texto(EstadoInvestigacion.Completo(Tipo)
				? "Investigacion.QuitarAyuda"
				: "Investigacion.InvestigarAyuda");
		}

		protected override void DrawSelf(SpriteBatch spriteBatch)
		{
			CalculatedStyle dim = GetDimensions();

			int hechas = EstadoInvestigacion.Hecho(Tipo);
			int necesarias = EstadoInvestigacion.Necesario(Tipo);
			bool completo = necesarias > 0 && hechas >= necesarias;

			float x = dim.X + XTexto;

			Utils.DrawBorderString(spriteBatch, _nombrePartido,
				new Vector2(x, dim.Y + 3f), completo ? EstiloInvestigacion.Hecho : Color.White, EscalaNombre);

			string estado = completo
				? Idiomas.Texto("Investigacion.EstadoCompleto", necesarias)
				: Idiomas.Texto("Investigacion.EstadoParcial", hechas, necesarias);
			Utils.DrawBorderString(spriteBatch, estado, new Vector2(x, dim.Y + _yLinea2 + 7f),
				EstiloInvestigacion.ColorDeEstado(hechas, necesarias), EscalaEstado);
		}
	}
}
