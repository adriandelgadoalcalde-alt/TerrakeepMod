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
using TerrasavrNative.Core.Data;

namespace TerrakeepMod.UI.Libreria.Widgets
{
	/// <summary>
	/// Cambia el PREFIJO (<c>Item.prefix</c>) del objeto seleccionado en <see cref="SlotSeleccionTk"/>.
	/// Un boton pequeño ("Prefijo: X") que al pulsarlo despliega una lista de los prefijos
	/// LEGALES para ESE objeto en concreto, agrupados igual que en la app de escritorio hermana
	/// (<see cref="CatalogoPrefijosLegales"/>, que reutiliza <c>TerrasavrNative.Core</c> tal cual
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
			// Se abre HACIA ARRIBA y con el borde derecho alineado con el del boton: este control
			// vive pegado al borde inferior derecho de la Libreria (hueco de herramientas junto al
			// inventario), asi que abrir hacia abajo o hacia la izquierda se saldria de la ventana.
			_popup = new UIPanel();
			_popup.Width.Set(AnchoPopup, 0f);
			_popup.Height.Set(AltoPopup, 0f);
			_popup.Top.Set(-(AltoPopup + 4f), 0f);
			_popup.Left.Set(_ancho - AnchoPopup, 0f);
			_popup.BackgroundColor = EstiloTk.FondoCaja;
			_popup.BorderColor = new Color(0, 0, 0, 0);
			_popup.SetPadding(6f);
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
