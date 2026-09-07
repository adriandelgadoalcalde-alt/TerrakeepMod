using System;
using System.Globalization;
using System.Text;

namespace TerrakeepMod.Common.Libreria
{
	/// <summary>
	/// Gramatica de busqueda REAL de la Libreria de Terrasavr, la misma que usa la app de
	/// escritorio. Reglas (verificadas en el codigo real, no de memoria):
	/// <list type="bullet">
	/// <item>terminos separados por COMA = OR entre ellos;</item>
	/// <item>dentro de un termino, palabras separadas por ESPACIO = AND (todas tienen que
	/// aparecer);</item>
	/// <item>un termino de menos de 2 caracteres se ignora por completo (quirk real de
	/// Terrasavr: un "5" suelto no cuenta ni como id ni como nombre);</item>
	/// <item><c>#123</c> = por id exacto, <c>#100-200</c> = por rango de id (extremos
	/// incluidos);</item>
	/// <item><c>.texto</c> busca en el TOOLTIP en vez de en el nombre;</item>
	/// <item>los acentos se pliegan en los dos lados de la comparacion, asi que "mascara"
	/// encuentra "Máscara".</item>
	/// </list>
	/// </summary>
	/// <remarks>
	/// <b>Por que esta copiado aqui y no se reutiliza desde <c>Terrakeep.Core</c>.</b> La
	/// logica SI es pura (solo <c>System.Globalization</c>/<c>System.Text</c>, ni una linea de
	/// WPF), pero el archivo original vive en <c>Terrakeep.App/ViewModels/
	/// LibrarySearchGrammar.cs</c>, o sea <b>dentro del ensamblado WPF</b> de la app de
	/// escritorio, que solo compila para <c>net10.0-windows</c>. El mod solo referencia
	/// <c>Terrakeep.Core</c> (build net8.0), asi que desde aqui ese tipo es inalcanzable.
	/// <para />
	/// Las dos salidas eran mover el archivo a Core o copiarlo. Se ha copiado, a proposito:
	/// moverlo obliga a tocar el repo hermano, a recompilar y re-empaquetar
	/// <c>lib\Terrakeep.Core.dll</c> (archivo compartido por todos los workstreams) y a
	/// arreglar los <c>using</c> de la app y de sus pruebas, todo ello mientras hay otros
	/// agentes trabajando en paralelo sobre los dos repositorios. Son 40 lineas de logica
	/// cerrada, con pruebas propias en el repo hermano
	/// (<c>LibrarySearchGrammarTests</c>) y sin ninguna razon previsible para cambiar. Si algun
	/// dia se toca, se toca en los dos sitios; queda anotado aqui para que se sepa.
	/// </remarks>
	public static class GramaticaBusqueda
	{
		/// <summary>
		/// Pliega acentos y pasa a minusculas. <c>FormD</c> descompone cada caracter acentuado en
		/// base + marca (a + ´), se tira la marca (<c>NonSpacingMark</c>) y se recompone en
		/// <c>FormC</c>. Solo se pliega el lado de la COMPARACION: lo que se ve en pantalla nunca
		/// pasa por aqui.
		/// </summary>
		public static string Plegar(string s)
		{
			if (string.IsNullOrEmpty(s)) {
				return "";
			}

			string d = s.Normalize(NormalizationForm.FormD);
			StringBuilder sb = new StringBuilder(d.Length);
			for (int i = 0; i < d.Length; i++) {
				if (CharUnicodeInfo.GetUnicodeCategory(d[i]) != UnicodeCategory.NonSpacingMark) {
					sb.Append(d[i]);
				}
			}
			return sb.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
		}

		/// <param name="consulta">Lo que ha escrito el usuario, tal cual.</param>
		/// <param name="id">Id real del objeto (<c>Item.type</c>).</param>
		/// <param name="nombrePlegado">Nombre ya plegado con <see cref="Plegar"/>.</param>
		/// <param name="tooltipPlegado">Tooltip ya plegado, o null si no tiene.</param>
		public static bool Casa(string consulta, int id, string nombrePlegado, string tooltipPlegado)
		{
			if (string.IsNullOrEmpty(consulta)) {
				return true;
			}

			string[] terminos = consulta.Split(',');
			for (int t = 0; t < terminos.Length; t++) {
				string termino = Plegar(terminos[t].Trim());
				if (termino.Length < 2) {
					continue;
				}

				if (termino[0] == '#') {
					if (CasaId(termino.Substring(1), id)) {
						return true;
					}
					continue;
				}

				bool enTooltip = termino[0] == '.';
				string objetivo = enTooltip ? (tooltipPlegado ?? "") : (nombrePlegado ?? "");
				string cuerpo = enTooltip ? termino.Substring(1) : termino;
				string[] palabras = cuerpo.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

				if (palabras.Length == 0) {
					continue;
				}

				bool todas = true;
				for (int p = 0; p < palabras.Length; p++) {
					if (objetivo.IndexOf(palabras[p], StringComparison.Ordinal) < 0) {
						todas = false;
						break;
					}
				}
				if (todas) {
					return true;
				}
			}

			return false;
		}

		private static bool CasaId(string parte, int id)
		{
			int guion = parte.IndexOf('-');
			if (guion < 0) {
				int exacto;
				return int.TryParse(parte, out exacto) && id == exacto;
			}

			int desde;
			int hasta;
			return int.TryParse(parte.Substring(0, guion), out desde)
				&& int.TryParse(parte.Substring(guion + 1), out hasta)
				&& id >= desde && id <= hasta;
		}
	}
}
