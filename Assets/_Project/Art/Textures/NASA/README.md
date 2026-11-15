# Texturas de la Luna: NASA CGI Moon Kit

Origen: **NASA's Scientific Visualization Studio, "CGI Moon Kit"** (Ernie Wright, NASA/GSFC, 2019),
https://svs.gsfc.nasa.gov/4720. Material de NASA, de dominio público. Se usa con atribución.

| Archivo del juego | Original | Datos |
|---|---|---|
| `Moon_LRO_Albedo_4k.jpg` | `lroc_color_poles_4k.tif` | Color real del Lunar Reconnaissance Orbiter (cámara LROC WAC), 4096×2048, equirectangular, longitud 0 en el centro |
| `Moon_LRO_Normal_2k.png` | `ldem_4.tif` | Mapa de normales (espacio tangente) calculado del relieve del altímetro láser LOLA (LRO), 4 píxeles/grado (−8,78 a +10,46 km), exageración ×5, 2048×1024 |

Conversión (scripts en `ArtSource/NASA/`): `convert_moon.py` pasa el color a JPG calidad 92 y `moon_normals.py` calcula las
normales en coma flotante (pendiente este/norte según la latitud), así no aparecen escalones de 8 bits.

Si faltan estos archivos, `TextureFactory` vuelve a la Luna procedural y el proyecto compila igual.
