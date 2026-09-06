using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.UI;
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
		/// <summary>Alto de una fila.</summary>
		public const float Alto = 42f;

		private readonly BotonTk _boton;

		/// <summary>Tipo de objeto de esta fila.</summary>
		public readonly int Tipo;

		/// <summary>Se dispara al pulsar el boton, con el tipo y si lo que se pide es investigar
		/// (true) o quitar (false).</summary>
		public event Action<int, bool> AlPulsar;

		public FilaObjetoInvestigacion(int tipo)
		{
			Tipo = tipo;
			Width.Set(0f, 1f);
			Height.Set(Alto, 0f);

			SlotMuestraInvestigacion slot = new SlotMuestraInvestigacion(tipo);
			slot.Left.Set(2f, 0f);
			slot.Top.Set(3f, 0f);
			Append(slot);

			_boton = new BotonTk("Investigar", 0.75f);
			_boton.Width.Set(112f, 0f);
			_boton.Height.Set(28f, 0f);
			_boton.HAlign = 1f;
			_boton.Top.Set(6f, 0f);
			_boton.AlPulsar += () => {
				if (AlPulsar != null) {
					AlPulsar(Tipo, !EstadoInvestigacion.Completo(Tipo));
				}
			};
			Append(_boton);
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
			_boton.FijarTexto(completo ? "Quitar" : "Investigar");
			_boton.Ayuda = completo
				? "Deja este objeto sin investigar (Ctrl+Z lo devuelve)"
				: "Marca este objeto como investigado del todo, por la via oficial del juego";
		}

		protected override void DrawSelf(SpriteBatch spriteBatch)
		{
			CalculatedStyle dim = GetDimensions();

			int hechas = EstadoInvestigacion.Hecho(Tipo);
			int necesarias = EstadoInvestigacion.Necesario(Tipo);
			bool completo = necesarias > 0 && hechas >= necesarias;

			float x = dim.X + 46f;
			float anchoTexto = dim.Width - 46f - 120f;
			int caracteres = Math.Max(8, (int)(anchoTexto / 7.6f));

			Utils.DrawBorderString(spriteBatch,
				EstiloInvestigacion.Acortar(EstadoInvestigacion.NombreObjeto(Tipo), caracteres),
				new Vector2(x, dim.Y + 3f), completo ? EstiloInvestigacion.Hecho : Color.White, 0.82f);

			string estado = completo
				? "investigado (" + necesarias + ")"
				: hechas + " de " + necesarias + " sacrificados";
			Utils.DrawBorderString(spriteBatch, estado, new Vector2(x, dim.Y + 22f),
				EstiloInvestigacion.ColorDeEstado(hechas, necesarias), 0.7f);
		}
	}
}
