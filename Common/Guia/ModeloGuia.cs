// Consolidacion T1 (I+D-PROXIMOS-PASOS-FAMILIA-KEEP.md, 16-sep-2026): "un solo cerebro de Guia en
// Terraria", el mismo principio que ya aplicaba StarvekeepMod/Starvekeep con el Lua real del mod -
// aqui no hay nada que interpretar, asi que la forma real es UN SOLO MODELO DE DATOS compartido en
// vez de dos definiciones identicas (esta y Terrakeep.Core/Guia/GuideModel.cs) sincronizadas solo
// de memoria.
//
// Antes de esta ronda, este archivo DEFINIA su propio TipoRequisito/RequisitoGuia/PasoGuia/
// TramoGuia/ResultadoRequisito/AmbitoGuia/CapaMundo - identicos campo a campo (mismos nombres,
// mismo orden de enum) a los que ya vivian en Terrakeep.Core.Guia.GuideModel.cs para la app de
// escritorio. Ahora esos tipos viven UNA sola vez alli (Terrakeep.Core.dll, referenciada por este
// proyecto via lib\Terrakeep.Core.dll - ver TerrakeepMod.csproj), y este archivo solo los trae al
// alcance del mod bajo el MISMO nombre corto que ya usaba todo el codigo existente, via alias
// GLOBALES de tipo (C# 10+, soportado: LangVersion=12.0 en TerrakeepMod.csproj) - asi ningun
// archivo del mod (CatalogoGuia.cs, EstadoGuia.cs, AutopruebaGuia.cs, la UI...) ha tenido que
// cambiar ni un "using" ni el nombre de un tipo, solo los sitios puntuales que usaban las
// propiedades de texto YA RESUELTO (Titulo/Porque/Como/ZonaLegible/Nombre/Resumen), que
// Terrakeep.Core NO puede tener (no conoce ningun idioma - ver la cabecera real de GuideModel.cs)
// y que ahora son metodos de extension en TextosGuiaMod.cs.
//
// TipoRequisitoGuia (Core) vale por TipoRequisito (mod): mismos 12 valores, mismo orden
// (Desconocido, CristalesVida, VidaMaxima, Defensa, NpcsPueblo, Npc, NpcActivo, Objeto,
// ObjetoCualquiera, DanoArma, Gancho, Bandera) - confirmado leyendo los dos archivos lado a lado
// antes de unificar. AmbitoGuia y CapaMundoGuia/CapaMundo, igual.
global using AmbitoGuia = Terrakeep.Core.Guia.AmbitoGuia;
global using TipoRequisito = Terrakeep.Core.Guia.TipoRequisitoGuia;
global using RequisitoGuia = Terrakeep.Core.Guia.RequisitoGuia;
global using PasoGuia = Terrakeep.Core.Guia.PasoGuia;
global using TramoGuia = Terrakeep.Core.Guia.TramoGuia;
global using CapaMundo = Terrakeep.Core.Guia.CapaMundoGuia;
global using ResultadoRequisito = Terrakeep.Core.Guia.ResultadoRequisitoGuia;
