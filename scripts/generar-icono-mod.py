"""Genera icon.png (80x80) e icon_small.png (30x30), los dos iconos del MOD.

Son los que se ven en la lista de Mods del menu de tModLoader y en el Taller, no el del HUD
(ese lo genera generar-icono-hud.py y es otra cosa).

Por que no vale el logo a secas. La primera version era el logo-256.png del repo hermano
reescalado: el hexagono naranja de Terrakeep sobre fondo TRANSPARENTE. En la lista de Mods eso
se ve como una figura flotando en el hueco, al lado de mods como Calamity que llenan sus 80x80
enteros con un icono a sangre y con marco. Se vio en una captura real del menu.

Lo que se hace ahora es rellenar el cuadro con la paleta del propio mod y dejar el logo encima:
    - fondo: EstiloTk.FondoPanel, el mismo azul del panel de Terrakeep;
    - un borde de dos tonos con las esquinas recortadas, al estilo del marco de Terraria;
    - el hexagono del logo centrado, sin deformarlo.
No se inventa arte nuevo: la marca sigue siendo la misma que la de la app de escritorio.

Uso:  python scripts/generar-icono-mod.py
Necesita Pillow y el logo del repo hermano (Terrasavr-Native/.../branding/logo-256.png).
"""
import os
from PIL import Image, ImageDraw

AQUI = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(AQUI)
LOGO = os.path.join(
    os.path.expanduser("~"),
    "Downloads", "Terrasavr-Win", "Terrasavr-Native",
    "TerrasavrNative.App", "Assets", "branding", "logo-256.png")

# Los mismos colores que EstiloTk (UI/Personaje/Widgets/EstiloTk.cs), ya multiplicados por su
# alfa como hace XNA: new Color(33, 43, 79) * 0.94f, etc.
FONDO = (31, 40, 74)
BORDE_EXTERIOR = (18, 23, 44)
BORDE_INTERIOR = (88, 112, 194)


def generar(lado, margen_logo, grosor_borde, radio):
    imagen = Image.new("RGBA", (lado, lado), (0, 0, 0, 0))
    dibujo = ImageDraw.Draw(imagen)

    # Fondo con las esquinas redondeadas: un cuadrado a sangre queda duro al lado de los demas
    # iconos de la lista, que casi todos llevan marco.
    dibujo.rounded_rectangle([0, 0, lado - 1, lado - 1], radius=radio, fill=FONDO,
                             outline=BORDE_EXTERIOR, width=grosor_borde)
    dibujo.rounded_rectangle([grosor_borde, grosor_borde,
                              lado - 1 - grosor_borde, lado - 1 - grosor_borde],
                             radius=max(1, radio - grosor_borde),
                             outline=BORDE_INTERIOR, width=1)

    logo = Image.open(LOGO).convert("RGBA")
    lado_logo = lado - 2 * margen_logo
    logo = logo.resize((lado_logo, lado_logo), Image.LANCZOS)
    imagen.alpha_composite(logo, (margen_logo, margen_logo))
    return imagen


def main():
    if not os.path.exists(LOGO):
        raise SystemExit("No se encuentra el logo de Terrakeep en " + LOGO)

    # 80x80 y 30x30 son las medidas REALES, sacadas de extraer icon.png/icon_small.png de un mod
    # ya instalado (CalamityMod.tmod), no supuestas.
    for nombre, lado, margen, grosor, radio in [
            ("icon.png", 80, 6, 2, 10),
            ("icon_small.png", 30, 2, 1, 4)]:
        ruta = os.path.join(REPO, nombre)
        generar(lado, margen, grosor, radio).save(ruta, "PNG")
        print("escrito", ruta, lado, "x", lado)


if __name__ == "__main__":
    main()
