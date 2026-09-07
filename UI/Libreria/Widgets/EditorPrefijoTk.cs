using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.UI;
using TerrakeepMod.Common.Ajustes;
using TerrakeepMod.Common.Prefijos;
using TerrakeepMod.Common.Undo;
using TerrakeepMod.UI.Personaje.Widgets;
using Terrakeep.Core.Data;

namespace TerrakeepMod.UI.Libreria.Widgets
{
	/// <summary>
	/// Cambia el PREFIJO (<c>Item.prefix</c>) del objeto seleccionado en <see cref="SlotSeleccionTk"/>.
	/// Un boton pequeño ("Prefijo: X") que al pulsarlo despliega una lista de los prefijos
	/// LEGALES para ESE objeto en concreto, agrupados igual que en la app de escritorio hermana
	/// (<see cref="CatalogoPrefijosLegales"/>, que reutiliza <c>Terrakeep.Core</c> tal cual
	/// - ver su cabecera para de donde sale la legalidad real).
	/// </summary>
	/// <remarks>
	/// <b>Como se cambia el prefijo SIN acumular multiplicadores.</b> <c>Item.Prefix(int)</c>
	/// multiplica las estadisticas ACTUALES del objeto (daño, tiempo de uso...) por las del
	/// prefijo pedido - llamarlo dos veces seguidas sobre el mismo objeto compondria los bonos
	/// (visto en el codigo real, <c>Terraria\Item.cs:1334</c>). La forma real y correcta de vanilla
	/// de CAMBIAR un prefijo ya puesto es la misma que usa el propio Puesto de Reforma
	/// (<c>Main.cs</c>, boton de reforjar: <c>reforgeItem.ResetPrefix(); reforgeItem.Prefix(-2);</c>):
	/// <see cref="Item.ResetPrefix"/> primero (vuelve las estadisticas a su base real, preservando
	/// tipo/pila/favorito) y solo entonces <c>Item.Prefix(idExacto)</c> con el prefijo elegido.
	/// </remarks>
	public class EditorPrefijoTk : UIElement
	{
		private const float AnchoPopup = 240f;
		private const float AltoPopup = 220f;

		private readonly Func<Item> _proveedor;
		private readonly BotonTk _botonToggle;
		private readonly float _ancho;
		private UIPanel _popup;
		private UIList _lista;
		private UIScrollbar _scroll;
		private bool _abierto;
		private Item _itemDelPopup;

		public EditorPrefijoTk(Func<Item> proveedor, float ancho = 150f)
		{
			_proveedor = proveedor;
			_ancho = ancho;

			Width.Set(ancho, 0f);
			Height.Set(26f, 0f);

			_botonToggle = new BotonTk("", 0.68f);
			_botonToggle.Width.Set(ancho, 0f);
			_botonToggle.Height.Set(26f, 0f);
			_botonToggle.Ayuda = () => Idiomas.Texto("Libreria.EditorPrefijo.Ayuda");
			_botonToggle.AlPulsar += Alternar;
			Append(_botonToggle);
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);

			Item objeto = _proveedor != null ? _proveedor() : null;
			bool hay = objeto != null && !objeto.IsAir && CatalogoPrefijosLegales.Listo;

			_botonToggle.Habilitado = hay;
			_botonToggle.FijarTexto(TextoBoton(objeto));

			// Si el objeto seleccionado cambia (se arrastra otro al recuadro, o se vacia) mientras
			// el popup esta abierto, se cierra: la lista que tendria delante dejaria de valer para
			// el objeto nuevo.
			if (_abierto && !ReferenceEquals(objeto, _itemDelPopup)) {
				Cerrar();
			}
			if (!hay && _abierto) {
				Cerrar();
			}
		}

		private string TextoBoton(Item objeto)
		{
			if (objeto == null || objeto.IsAir) {
				return Idiomas.Texto("Libreria.EditorPrefijo.BotonSinObjetivo");
			}
			string nombrePrefijo = objeto.prefix > 0
				? CatalogoPrefijosLegales.NombrePrefijo(objeto.prefix)
				: Idiomas.Texto("Libreria.Prefijo.Ninguno");
			return Idiomas.Texto("Libreria.EditorPrefijo.Boton", nombrePrefijo);
		}

		/// <summary>Abre el popup por su ruta real, para que la autoprueba pueda listar sus
		/// botones. No hace nada si ya estaba abierto o no hay objeto seleccionado.</summary>
		public void AbrirParaAutoprueba()
		{
			if (!_abierto) {
				Abrir();
			}
		}

		/// <summary>Los botones de prefijo del popup ABIERTO ahora mismo (el primero, "Ninguno",
		/// incluido), o una lista vacia si el popup no esta abierto. Los devuelve en el mismo orden
		/// en que estan en la lista, para que la autoprueba pueda pulsar uno por su ruta REAL
		/// (<c>BotonTk.LeftClick</c>).</summary>
		public List<BotonTk> BotonesPopupParaAutoprueba()
		{
			List<BotonTk> botones = new List<BotonTk>();
			if (_popup == null) {
				return botones;
			}
			_popup.ExecuteRecursively(elemento => {
				BotonTk boton = elemento as BotonTk;
				if (boton != null) {
					botones.Add(boton);
				}
			});
			return botones;
		}

		/// <summary>true si el popup esta abierto ahora mismo. Lo lee la autoprueba.</summary>
		public bool PopupAbierto => _abierto;

		/// <summary>SOLO DIAGNOSTICO: geometria real YA CALCULADA del popup y su lista, para el log
		/// de la autoprueba - demuestra si el popup ocupa el sitio real que deberia o si algo se ha
		/// quedado con tamaño 0 sin que ningun recuento de botones lo delate.</summary>
		public string DiagnosticoGeometria()
		{
			if (_popup == null) {
				return "(popup null)";
			}
			CalculatedStyle p = _popup.GetDimensions();
			CalculatedStyle l = _lista != null ? _lista.GetDimensions() : default;
			CalculatedStyle b = _botonToggle.GetDimensions();
			return "boton x=" + (int)b.X + " y=" + (int)b.Y + " " + (int)b.Width + "x" + (int)b.Height
				+ "; popup x=" + (int)p.X + " y=" + (int)p.Y + " " + (int)p.Width + "x" + (int)p.Height
				+ "; lista x=" + (int)l.X + " y=" + (int)l.Y + " " + (int)l.Width + "x" + (int)l.Height
				+ "; filas internas=" + (_lista != null ? _lista.Count : 0)
				+ "; alturaInternaTotal=" + (_lista != null ? _lista.GetTotalHeight() : 0f)
				+ "; Main.screenWidth=" + Main.screenWidth + " Main.screenHeight=" + Main.screenHeight;
		}

		private void Alternar()
		{
			if (_abierto) {
				Cerrar();
			}
			else {
				Abrir();
			}
		}

		private void Abrir()
		{
			Item objeto = _proveedor != null ? _proveedor() : null;
			if (objeto == null || objeto.IsAir || !CatalogoPrefijosLegales.Listo) {
				return;
			}

			_itemDelPopup = objeto;
			ConstruirPopup(objeto);
			_abierto = true;
		}

		private void Cerrar()
		{
			if (_popup != null) {
				RemoveChild(_popup);
			}
			_popup = null;
			_lista = null;
			_scroll = null;
			_itemDelPopup = null;
			_abierto = false;
		}

		/// <summary>Reconstruye el popup entero para <paramref name="objeto"/>: los grupos legales
		/// dependen del objeto concreto, asi que no tiene sentido guardar un popup entre aperturas.</summary>
		private void ConstruirPopup(Item objeto)
		{
			// Se abre hacia donde de verdad quede sitio en la VENTANA REAL, midiendo con
			// Main.screenWidth/Height en vez de suponer un lado fijo - mismo criterio que ya usa
			// este mod para no fiarse de min()/max() en CSS o de un ancho de fuente supuesto:
			// medir lo real del motor, no adivinar. Encargo explicito del usuario tras probarlo
			// ("el panel de prefijos se abre arriba, mejor seria a la derecha, que todavia hay
			// espacio"): por defecto se abre a la DERECHA del boton, con el borde superior
			// alineado - solo cae hacia la izquierda o se desplaza verticalmente si de verdad no
			// cabe en la ventana actual (ventanas pequeñas, o la Libreria pegada al borde).
			_popup = new UIPanel();
			_popup.Width.Set(AnchoPopup, 0f);
			_popup.Height.Set(AltoPopup, 0f);
			// EL HALLAZGO REAL de este bug (el "no muestra ninguna opcion de nada" que reporto el
			// usuario): UIElement.MaxWidth/MaxHeight valen StyleDimension.Fill POR DEFECTO (100% del
			// padre) - y el padre de este popup es el propio boton "Prefijo: X" (176x26 px). Sin
			// fijarlos aqui, el motor RECORTA el popup a como mucho 176x26 pese a pedirle
			// Width/Height=240x220 explicitamente: el popup SI se abria y SI tenia filas reales
			// dentro (confirmado con un diagnostico real: 66 filas internas, 1602px de alto interno
			// acumulado), pero solo se veian ~14px de la primera ("Ninguno"), el resto quedaba
			// recortado sin ningun aviso. Visto con un diagnostico de geometria real en el juego, no
			// adivinado - el mismo motivo por el que este mod no se fia de min()/max() en CSS ni de
			// un ancho de fuente supuesto: medir SIEMPRE lo real del motor.
			_popup.MaxWidth.Set(AnchoPopup, 0f);
			_popup.MaxHeight.Set(AltoPopup, 0f);
			_popup.BackgroundColor = EstiloTk.FondoCaja;
			_popup.BorderColor = new Color(0, 0, 0, 0);
			_popup.SetPadding(6f);

			CalculatedStyle boton = _botonToggle.GetDimensions();

			float left = boton.Width + 4f;
			if (boton.X + boton.Width + 4f + AnchoPopup > Main.screenWidth) {
				left = -(AnchoPopup + 4f);
				// Tampoco cabe a la izquierda (ventana muy estrecha): se deja pegado al borde
				// izquierdo de la ventana en vez de salirse por fuera.
				if (boton.X + left < 0f) {
					left = -boton.X;
				}
			}

			// Margen de sobra por abajo: el pie del marco (boton "Cerrar", AltoPie en
			// PanelTerrakeepState) vive en la franja final de la ventana, y pegar el popup al borde
			// EXACTO de Main.screenHeight se lo comia por encima en una captura real. No hace falta
			// conocer el marco desde aqui (este widget no sabe nada de PanelTerrakeepState a
			// proposito): un margen fijo de sobra alcanza para dejarlo siempre claramente encima.
			const float MargenInferior = 66f;

			float top = 0f;
			if (boton.Y + AltoPopup > Main.screenHeight - MargenInferior) {
				top = Main.screenHeight - MargenInferior - AltoPopup - boton.Y;
			}
			if (boton.Y + top < 0f) {
				top = -boton.Y;
			}

			_popup.Left.Set(left, 0f);
			_popup.Top.Set(top, 0f);
			Append(_popup);

			_lista = new UIList();
			_lista.Width.Set(-20f, 1f);
			_lista.Height.Set(0f, 1f);
			_lista.ListPadding = 3f;
			_popup.Append(_lista);

			_scroll = new UIScrollbar();
			_scroll.Width.Set(16f, 0f);
			_scroll.Height.Set(0f, 1f);
			_scroll.HAlign = 1f;
			_scroll.SetView(100f, 1000f);
			_popup.Append(_scroll);
			_lista.SetScrollbar(_scroll);

			// "Ninguno" (quitar el prefijo) siempre arriba del todo, fuera de cualquier grupo.
			_lista.Add(CrearFilaPrefijo(0, objeto, Idiomas.Texto("Libreria.Prefijo.Ninguno"), ""));

			string metaAnterior = null;
			foreach (var entrada in CatalogoPrefijosLegales.GruposLegales(objeto)) {
				string nombreMeta = Idiomas.EnEspanol ? entrada.Meta.NameEs : entrada.Meta.NameEn;
				if (nombreMeta != metaAnterior) {
					metaAnterior = nombreMeta;
					_lista.Add(CrearCabecera(nombreMeta));
				}

				string nombreGrupo = Idiomas.EnEspanol ? entrada.Grupo.NameEs : entrada.Grupo.NameEn;
				_lista.Add(CrearCabecera("  " + nombreGrupo, EstiloTk.TextoSuave));

				foreach (int id in entrada.Ids) {
					string nombre = CatalogoPrefijosLegales.NombrePrefijo(id);
					string efecto = CatalogoPrefijosLegales.Efecto(id);
					_lista.Add(CrearFilaPrefijo(id, objeto, nombre, efecto));
				}
			}

			_lista.Recalculate();
		}

		private EtiquetaTk CrearCabecera(string texto, Color? color = null)
		{
			string capturado = texto;
			EtiquetaTk etiqueta = new EtiquetaTk(() => capturado, 0.72f, AnchoPopup - 24f, 18f);
			etiqueta.ColorTexto = color ?? EstiloTk.TextoAviso;
			return etiqueta;
		}

		private BotonTk CrearFilaPrefijo(int prefixId, Item objeto, string nombre, string efecto)
		{
			BotonTk fila = new BotonTk(nombre, 0.7f);
			fila.Width.Set(0f, 1f);
			fila.Height.Set(22f, 0f);
			fila.Activo = objeto.prefix == prefixId;
			fila.Ayuda = () => efecto;
			fila.AlPulsar += () => AplicarPrefijo(objeto, prefixId);
			return fila;
		}

		/// <summary>
		/// Aplica el prefijo elegido de verdad, sin acumular multiplicadores (ver la nota de
		/// cabecera: <c>ResetPrefix</c> primero, <c>Prefix(id)</c> despues) y deja el cambio
		/// deshacible con Ctrl+Z, igual que el resto del panel.
		/// </summary>
		private void AplicarPrefijo(Item objeto, int prefixId)
		{
			if (objeto == null || objeto.IsAir) {
				return;
			}

			int antes = objeto.prefix;
			if (antes == prefixId) {
				Cerrar();
				return;
			}

			AplicarPrefijoReal(objeto, prefixId);
			int aplicadoDeVerdad = objeto.prefix;

			Historial.CambiarValor(
				Idiomas.Texto("Libreria.EditorPrefijo.Historial", objeto.Name,
					CatalogoPrefijosLegales.NombrePrefijo(antes), CatalogoPrefijosLegales.NombrePrefijo(aplicadoDeVerdad)),
				antes, aplicadoDeVerdad, (int valor) => AplicarPrefijoReal(objeto, valor));

			Terrakeep.Instance.Logger.Info(Terrakeep.LogTag +
				" Libreria: prefijo cambiado en vivo sobre \"" + objeto.Name + "\": " + antes +
				" -> " + objeto.prefix + " (pedido " + prefixId + ").");

			Cerrar();
		}

		/// <summary>El unico sitio que toca <c>Item.prefix</c> de verdad: reset + aplicar, para que
		/// tanto el clic real como Deshacer/Rehacer pasen por el MISMO camino y nunca compongan
		/// multiplicadores sobre un prefijo anterior.</summary>
		private static void AplicarPrefijoReal(Item objeto, int prefixId)
		{
			objeto.ResetPrefix();
			if (prefixId > 0) {
				objeto.Prefix(prefixId);
			}
		}
	}
}
