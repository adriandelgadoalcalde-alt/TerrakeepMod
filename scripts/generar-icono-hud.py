#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""
Genera Assets/IconoTerrakeep.png: el icono de Terrakeep del HUD del juego.

Es una hoja de DOS fotogramas de 30x30 puestos uno al lado del otro (60x30 en total),
exactamente la misma convencion que usan los botones del inventario de Terraria: el
codigo real de Main.DrawBestiaryIcon hace

    Rectangle val3 = value.Frame(2, 1, flag ? 1 : 0);   // 0 = normal, 1 = raton encima
    val3.Width -= 2; val3.Height -= 2;

o sea, dos columnas y una fila, y recorta 2 px por si acaso. Por eso el dibujo util de
cada fotograma se queda dentro de 28x28 y los 2 px del borde derecho/inferior van vacios.

Medidas y colores:
  - 30x30 por fotograma, como el boton del bestiario y el de emotes (rectangulo real
    (498,278,30,30) y (534,278,30,30) en el codigo del juego).
  - La paleta es la de EstiloTk (los azules del inventario de Terraria), no colores
    inventados: fondo (63,82,151) en reposo y (88,112,194) con el raton encima.
  - El dibujo es un cofre, que es lo que da nombre al mod (Terra-KEEP) y se lee de un
    vistazo a 30 px.

Uso:  python scripts/generar-icono-hud.py
"""

import os
from PIL import Image

LADO = 30
UTIL = 28          # lo que de verdad se dibuja (Frame() recorta 2 px)

NEGRO = (0, 0, 0, 255)
TRANSPARENTE = (0, 0, 0, 0)

# Paleta del panel, la misma de UI/Personaje/Widgets/EstiloTk.cs
FONDO_REPOSO = (63, 82, 151, 255)
FONDO_SOBRE = (88, 112, 194, 255)
BRILLO_REPOSO = (96, 120, 200, 255)
BRILLO_SOBRE = (140, 170, 240, 255)
BORDE_SOBRE = (200, 220, 255, 255)

# Cofre
MADERA = (146, 96, 48, 255)
MADERA_OSCURA = (92, 58, 28, 255)
MADERA_CLARA = (186, 132, 72, 255)
ORO = (255, 208, 90, 255)
ORO_OSCURO = (176, 132, 40, 255)


def marco(px, dx, fondo, brillo, borde_externo):
	"""Rectangulo redondeado con bisel, al estilo de los botones cuadrados del juego."""
	for y in range(UTIL):
		for x in range(UTIL):
			# Esquinas recortadas: 2 px en diagonal, que es como se ven los botones de
			# Terraria a este tamaño (no hay antialias en el pixel art del juego).
			if (x + y < 2) or (x + (UTIL - 1 - y) < 2) or \
			   ((UTIL - 1 - x) + y < 2) or ((UTIL - 1 - x) + (UTIL - 1 - y) < 2):
				px[dx + x, y] = TRANSPARENTE
				continue

			borde = x == 0 or y == 0 or x == UTIL - 1 or y == UTIL - 1 or \
				(x + y < 4) or (x + (UTIL - 1 - y) < 4) or \
				((UTIL - 1 - x) + y < 4) or ((UTIL - 1 - x) + (UTIL - 1 - y) < 4)
			if borde:
				px[dx + x, y] = borde_externo
			elif x == 1 or y == 1:
				px[dx + x, y] = brillo          # bisel claro arriba y a la izquierda
			elif x == UTIL - 2 or y == UTIL - 2:
				px[dx + x, y] = MADERA_OSCURA[:3] + (0,)  # se rellena abajo con el fondo
				px[dx + x, y] = tuple(int(c * 0.7) for c in fondo[:3]) + (255,)
			else:
				px[dx + x, y] = fondo


def rect(px, dx, x0, y0, x1, y1, color):
	for y in range(y0, y1 + 1):
		for x in range(x0, x1 + 1):
			px[dx + x, y] = color


def cofre(px, dx):
	"""Un cofre de 16x14, centrado y con aire suficiente alrededor."""
	x0, x1 = 6, 21
	tapa_y0, tapa_y1 = 9, 13     # tapa curvada
	base_y0, base_y1 = 14, 21

	# Contorno negro alrededor de todo el cofre, para que despegue del fondo azul.
	rect(px, dx, x0 - 1, tapa_y0 - 1, x1 + 1, base_y1 + 1, NEGRO)

	# Tapa: la fila de arriba va un pixel mas estrecha por cada lado, que es como Terraria
	# insinua la curva de la tapa de un cofre a esta escala.
	rect(px, dx, x0 + 1, tapa_y0, x1 - 1, tapa_y0, MADERA_CLARA)
	rect(px, dx, x0, tapa_y0 + 1, x1, tapa_y1, MADERA)
	rect(px, dx, x0, tapa_y0 + 1, x1, tapa_y0 + 1, MADERA_CLARA)

	# Linea de union tapa/base: negra y de lado a lado, es lo que hace que se lea "cofre".
	rect(px, dx, x0, tapa_y1 + 1, x1, tapa_y1 + 1, NEGRO)

	# Base
	rect(px, dx, x0, base_y0 + 1, x1, base_y1, MADERA)
	rect(px, dx, x0, base_y1, x1, base_y1, MADERA_OSCURA)
	rect(px, dx, x0, base_y1 - 3, x1, base_y1 - 3, MADERA_OSCURA)

	# Herrajes de oro: dos bandas verticales, con su sombra a la derecha.
	for bx in (x0 + 2, x1 - 3):
		rect(px, dx, bx, tapa_y0 + 1, bx + 1, base_y1 - 1, ORO_OSCURO)
		rect(px, dx, bx, tapa_y0 + 1, bx, base_y1 - 1, ORO)

	# Cierre central: placa de oro a caballo entre la tapa y la base, con bocallave.
	cx = (x0 + x1) // 2
	rect(px, dx, cx - 1, tapa_y1 - 1, cx + 2, base_y0 + 2, ORO)
	rect(px, dx, cx + 2, tapa_y1 - 1, cx + 2, base_y0 + 2, ORO_OSCURO)
	rect(px, dx, cx, base_y0, cx + 1, base_y0 + 1, MADERA_OSCURA)


def fotograma(px, dx, hover):
	marco(px, dx,
		FONDO_SOBRE if hover else FONDO_REPOSO,
		BRILLO_SOBRE if hover else BRILLO_REPOSO,
		BORDE_SOBRE if hover else NEGRO)
	cofre(px, dx)


def main():
	raiz = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
	destino = os.path.join(raiz, "Assets", "IconoTerrakeep.png")

	imagen = Image.new("RGBA", (LADO * 2, LADO), TRANSPARENTE)
	px = imagen.load()

	fotograma(px, 0, False)
	fotograma(px, LADO, True)

	imagen.save(destino)
	print("Escrito", destino, imagen.size)


if __name__ == "__main__":
	main()
