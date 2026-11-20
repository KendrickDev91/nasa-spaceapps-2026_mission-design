# AstroByte: Space Mission Simulator — NASA Space Apps Challenge 2026

*Co-op space mission simulator: design a spacecraft and fly to the Moon with real NASA data. English & Español.*

**Developed by** Keni Breython Ramos Ramirez ([KendrickDev91](https://kendrickdev91.itch.io)) — game design and development.  
**Concept and ideas:** team ASTROBYTE (Bolivia) — Alejandro Marco Toranzo Cazorla · Jhamil Obando Requis · Tony Cahuaya — at the NASA Space Apps Challenge 2026.

## 🎮 Play AstroByte · Juega AstroByte

[![Available on itch.io](https://img.shields.io/badge/itch.io-Play%20free%20·%20Juega%20gratis-FA5C5C?logo=itchdotio&logoColor=white)](https://kendrickdev91.itch.io/astrobyte-space-mission-simulator)

**[kendrickdev91.itch.io/astrobyte-space-mission-simulator](https://kendrickdev91.itch.io/astrobyte-space-mission-simulator)** — play in your browser or download for Windows, English & Spanish, 1–4 players. Leave a comment with your score! · Juega en el navegador o descarga gratis para Windows, en inglés y español, de 1 a 4 jugadores. ¡Déjanos un comentario con tu puntuación!

Also on GitHub: [latest release](https://github.com/KendrickDev91/nasa-spaceapps-2026_mission-design/releases/latest).

## 🎬 Demo video (30 s · Spanish narration, English subtitles)

[![AstroByte — 30-second demo (click to watch)](docs/video/thumbnail_en_1280x720.jpg)](https://github.com/KendrickDev91/nasa-spaceapps-2026_mission-design/releases/download/v2026.11.15-ocho-luna-amerizaje/AstroByte_Jury_30s_ES_subs_EN.mp4)

| Video | Language | Length |
|---|---|---|
| [**30-second demo**](https://github.com/KendrickDev91/nasa-spaceapps-2026_mission-design/releases/download/v2026.11.15-ocho-luna-amerizaje/AstroByte_Jury_30s_ES_subs_EN.mp4) | Spanish narration + **English subtitles** | 0:29 |
| [**Full walkthrough**](https://github.com/KendrickDev91/nasa-spaceapps-2026_mission-design/releases/download/v2026.11.15-ocho-luna-amerizaje/AstroByte_Jury_ES_subs_EN.mp4) | Spanish narration + **English subtitles** | 1:13 |
| [30-second demo (English)](https://github.com/KendrickDev91/nasa-spaceapps-2026_mission-design/releases/download/v2026.11.15-ocho-luna-amerizaje/AstroByte_Jury_30s_EN.mp4) | English narration + English captions | 0:29 |

Subtitles (SRT): [`docs/video/`](docs/video/) · **Play the game (browser or Windows):** [itch.io](https://kendrickdev91.itch.io/astrobyte-space-mission-simulator) or the [latest release](https://github.com/KendrickDev91/nasa-spaceapps-2026_mission-design/releases/latest)

## 📊 Presentation (7 slides)

[![AstroByte — 7-slide presentation (click for the PDF)](docs/video/presentation_cover.jpg)](https://github.com/KendrickDev91/nasa-spaceapps-2026_mission-design/releases/download/v2026.11.15-ocho-luna-amerizaje/AstroByte_Presentation_NASA_Space_Apps_2026.pdf)

Objective · How a mission plays · Impact · Solidity · Creativity · Presentation.
[**PDF**](https://github.com/KendrickDev91/nasa-spaceapps-2026_mission-design/releases/download/v2026.11.15-ocho-luna-amerizaje/AstroByte_Presentation_NASA_Space_Apps_2026.pdf) ·
[PowerPoint](https://github.com/KendrickDev91/nasa-spaceapps-2026_mission-design/releases/download/v2026.11.15-ocho-luna-amerizaje/AstroByte_Presentation_NASA_Space_Apps_2026.pptx) (speaker notes in Spanish)

### About (English)

**AstroByte** is a cooperative game for 1-4 players that answers the **Space Mission Design Game** challenge. Four roles
(Mission Director, Systems Engineer, Principal Scientist, Telecom & Launch Engineer) design a mission part by part
from real data: instruments, rockets, antennas and ground stations. **Mass, power, budget, data, communications and time**
pull against each other, and nothing launches without team consensus. Then the mission flies: launch, mission control,
operations, and a post-mission analysis that explains **what went right, what failed and why** (grade A-F).

- **Real physics.** The crewed Artemis II-style free-return "figure eight" is computed with the restricted three-body problem
  and **validated with NASA GMAT R2026a** (a 27 m/s correction reproduces the flyby and the 50 km return perigee; see
  [`docs/validacion/gmat`](docs/validacion/gmat/README.md)).
- **Real data.** The Moon uses NASA LRO color (LROC) and elevation (LOLA) from the CGI Moon Kit (public domain), and every
  part shows its source.
- **Engaging.** Walkable space center, live mission control, onboard cameras that photograph the far side, and a
  splashdown cinematic with parachutes.
- Independent project, **not an official NASA product.**

---

# AstroByte: Space Mission Simulator — proyecto para NASA Space Apps Challenge 2026

> Antes se llamaba **Mission Trade-Off**; el nombre cambió el 2026-11-20.

Juego de diseño de misiones espaciales para 1-4 jugadores (hot-seat) que responde al reto
**Space Mission Design Game**: definir objetivo, diseñar la nave, elegir instrumentos, cohete, órbita y
segmento terreno, gestionar los 6 recursos críticos, negociar en equipo, simular la misión y entender
**por qué** triunfó o falló.

Unity 6000.5.5f1 · URP · TextMeshPro · Input System · data-driven (ScriptableObjects) · arte propio en Blender 5.2.

> Título provisional: se cambia en `Assets/_Project/Scripts/Core/GameInfo.cs`. Proyecto independiente,
> **no es un producto oficial de la NASA**: no usar logos ni nombres de instalaciones reales (p. ej. el "Mission Design Lab" de Goddard).

## Cómo se juega

1. **Menú principal** → **Jugar · diseñar misión**, **Explorar el centro espacial** o **Planos del centro espacial** (F1: tutorial · Esc: pausa).
2. El **Director** elige dificultad (Básico/Avanzado), escenario y órbita; cada rol elige sus piezas (teclas **1-4**, **Tab** pasa el turno).
3. Pasa el ratón por cualquier opción: el tooltip explica la pieza, su fuente y **qué le pasaría a tu misión**.
4. Resuelvan los conflictos del panel inferior, cada jugador **aprueba**, y **LANZAR MISIÓN**.
5. Mira el lanzamiento, la trayectoria y la sala de control; lee el **análisis post-misión** (bien / falló / rango A-F,
   misión real parecida, ranking) y vuelve al diseño para mejorar. **Diseños** guarda alternativas y las compara.

## Centro espacial caminable (escena `SpaceCenter`)

Un personaje en tercera persona recorre el centro en el orden real del trabajo:
**Centro de Control** (vestíbulo, mesa de planos, recepción y sala de control con 3 gradas) → carretera de servicio →
**Edificio de Ensamblaje** (SLS apilado sobre el lanzador móvil, plataformas de trabajo y **sala limpia** con Orion) →
camino de orugas → **plataforma de lanzamiento** (rampa, torre de servicio de 110 m, SLS en el montaje).
El sitio lunar es solo de visualización (no caminable).

| Tecla | Acción |
|---|---|
| WASD / flechas · Shift · Espacio | caminar · correr · saltar (2,75 / 7,5 m/s) |
| Clic derecho + ratón · rueda · Q/Z | girar cámara · zoom · girar con teclado |
| **E** | interactuar (aparece el aviso al acercarse a un rombo azul) |
| **M** | planos 2D (5 láminas) |
| **N** | amplía el mapa geolocalizador (latitud/longitud y rumbo; ubicación ilustrativa en Florida) |
| **Esc** | menú: viaje rápido entre edificios, controles, diseñar la misión, menú principal |

Interacciones: recepción (bienvenida y controles), mesa de planos, consola de diseño (entra a la sala de diseño sin pasar por el menú),
kiosco de la sala limpia (ISO 14644, Orion y su tripulación), apilado del SLS (con la cronología real del ascenso) y panel de la
plataforma (trayectoria de retorno libre y las fotos de Artemis II). Las puertas se abren solas al acercarse.

- **Círculo de entrada al diseño** (anillo con llamas en el puesto de la consola): al pisarlo el personaje se sienta en la silla,
  la pantalla se funde y se abre la sala de diseño.
- **Vestuario según el ambiente**: ropa de ingeniero aeroespacial (Centro de Control), casco de seguridad (nave de ensamblaje),
  mono, capucha y mascarilla (sala limpia) y traje naranja de lanzamiento con casco (plataforma).
- **Personal de la sala de control**: operadores sentados tecleando con auriculares; en la sala de diseño celebran (brazos arriba
  o aplausos, se ponen de pie) cuando el lanzamiento es exitoso y se llevan las manos a la cabeza si falla. Las poses son
  procedurales sobre el esqueleto Humanoid (`CrewPoser`), sin depender de animaciones extra de Mixamo.

## Fotografías de la Luna e informe de exploración

Las **cámaras** son piezas de la nave (rol Científico): *Kit de cámaras de exploración* (cualquier nave), *32 cámaras de Orion*,
*cámaras de la tripulación (Nikon D5/Z9)*, *LROC* y la cámara CubeSat. Si la misión llega a la Luna, durante el sobrevuelo u
operación lunar el juego **renderiza fotografías reales de la escena** (`MoonPhotoStudio`: Luna con cráteres y relieve, puesta de
Tierra tras el limbo, ala solar de Orion, superficie a baja altura…), con destello de cámara y miniaturas en el HUD. Al terminar:

- El análisis post-misión tiene el botón **Informe de la exploración**: bitácora escrita por la tripulación (o el equipo de
  operaciones si es robótica) con despegue y amerizaje geolocalizados, más la galería de fotos con pie de foto (cámara y lente).
- Las fotos y el informe se guardan junto al reporte: `MissionReports/<reporte>_exploracion/foto_NN.png` + `informe_exploracion.txt`.
- Fotos a escala real (p. ej. ~10 000 en el sobrevuelo tripulado): parte por el enlace y el resto en tarjetas SD; si la cápsula no
  vuelve, solo quedan las transmitidas.

## Demostración rápida (ventana «Diseños»)

Tres diseños de demostración vienen con el juego (`StreamingAssets/Demos`, etiqueta **DEMO**). Cada uno guarda una semilla fija,
así el resultado se repite en la presentación. Se generan con el catálogo y el simulador reales (`DemoDesigns`):

| Demo | Resultado | Fotos |
|---|---|---|
| 1 · Artemis II con fotos | Éxito (ciencia 125 %, 98/100) | 8 de 8 |
| 2 · Sobrevuelo económico | Éxito parcial (ciencia 75 %, 83/100) | 8 de 8 |
| 3 · Batería insuficiente | Falla: la cápsula no vuelve (58/100) | 6 de 8, solo las transmitidas |

**Dónde está la cámara:** rol *Científico Principal* (tecla 3) → catálogo de instrumentos → «Kit de cámaras de exploración»
(o las cámaras de Orion / de la tripulación). En órbita terrestre fotografía la Tierra; con destino lunar, la Luna.

## Validación con NASA GMAT

La trayectoria de retorno libre se validó con **NASA GMAT R2026a**, usando efemérides reales DE405. Detalle en
[`docs/validacion/gmat/`](docs/validacion/gmat/README.md). El estado de la TLI del juego, propagado sin corregir, pasa a
11 581 km de la Luna y vuelve demasiado empinado. Con una corrección de rumbo de solo **≈27 m/s**, GMAT reproduce el
sobrevuelo de 7 672 km y el perigeo de 50 km (regreso 8,2 días después de la TLI, ~9,3 días desde el despegue; Artemis II: 9,1).

## Equipo ASTROBYTE

Desarrollado por **Keni Breython Ramos Ramirez** (KendrickDev91): diseño y desarrollo del juego. Concepto e ideas del
equipo **ASTROBYTE** de Bolivia en el NASA Space Apps Challenge 2026: Alejandro Marco Toranzo Cazorla, Jhamil Obando
Requis y Tony Cahuaya. Los nombres viven en `Core/GameInfo.cs` (créditos y menú principal).

## Trayectoria en "8", Luna real y amerizaje (ronda 2026-11-15)

- **Trayectoria como el diagrama de la ESA, con física real.** El solver CR3BP exige la familia de Artemis II: rodea la Luna
  en sentido **horario** (marco rotatorio), llega en ~4 días y la vuelta **cruza** la ida antes de la Tierra. La vista dibuja
  "no a escala" como la ESA (zona terrestre ampliada con `NearEarthWarp`, orientación vista desde el norte), con círculos
  numerados en cada hito y leyenda 1-8 + Ida/Vuelta. Prueba: `FreeReturn_DrawsAFigureEight` (cruce real de los segmentos).
- **Luna real de NASA.** Color LROC 4096×2048 y relieve LOLA del *CGI Moon Kit* (NASA/GSFC SVS, dominio público) en
  `Art/Textures/NASA/`. El mapa de normales se calcula en coma flotante (`ArtSource/NASA/moon_normals.py`). Las fotos del
  sobrevuelo muestran la cuenca Oriental, como las de Artemis II. Sin esos archivos, el juego vuelve a la Luna procedural.
- **Cinemática del amerizaje** (`SplashdownCinematic`, ~17 s, se salta con Espacio/clic): reentrada con plasma → 2 drogues
  (~7 600 m) → 3 principales naranja/blanco que se abren por etapas (~2 900 m) → amerizaje a ~27 km/h con salpicadura →
  bolsas de enderezamiento y buque de recuperación. Se reproduce entre REGRESO y el análisis, solo si la cápsula vuelve
  (interfaz `IMissionCinematic` en la simulación; el botón «Saltar» también la corta). Toda la geometría es procedural.

## Sonido

Todo el sonido es sintetizado por código (`Synth`), sin archivos de terceros:
- **Misión:** cuenta regresiva y rugido del cohete, aplausos de la sala al despegar, campanita en cada hito, obturador por
  cada foto, módem durante la bajada de datos, alarma en eventos críticos, y fanfarria o tono de falla al terminar.
- **Centro espacial:** pasos, puertas, crepitar del círculo de entrada y aviso al cambiar de equipo.

## Simulación más dinámica

Trayectoria con el estilo del diagrama de Artemis II de la ESA (Tierra arriba, Luna abajo, ida a la izquierda y vuelta a la
derecha) y sus **8 hitos**: despegue, órbita alta, separación del lanzador, motor a la Luna (TLI), sobrevuelo, regreso, separación
del módulo de tripulación y amerizaje. Orion recorre el "8" con estela; la cámara se acerca a la cara oculta durante el sobrevuelo;
telemetría en vivo (día, distancias a la Tierra y a la Luna, velocidad inercial) y un cartel por cada hito. Tras el despegue la
cámara corta a la sala de control celebrando. El resultado aparece animado: sello del veredicto, tarjetas que entran, puntuación
que cuenta y confeti en los éxitos.

## Pulido de experiencia (última ronda)

- **Personaje bien apoyado:** las animaciones usan la altura "basada en los pies" y `FootPlanter` ajusta el modelo en cada
  fotograma para que las suelas toquen el suelo en rampas, losas y escalones.
- **Cámara en 1.ª persona (V)** y **ascensor de la torre de servicio**: sube 75 m hasta el brazo de acceso de la tripulación
  (pasarela con barandas), por donde los astronautas entran en Orion.
- **Infraestructura más rica:** transportador de orugas, autos, palmeras, farolas, edificios de apoyo y antena parabólica en el
  Centro de Control. En el edificio de ensamblaje, franjas en las fachadas. En la plataforma, tanques esféricos de LOX y LH2,
  deflector de llamas y cables de pararrayos.
- **Resultados legibles:** letra grande, viñetas cortas (una idea por línea), 5 barras de métricas y animaciones (sello del
  veredicto, viñetas que se escriben, barras que se llenan, puntuación que cuenta y confeti).
- **Informe de exploración:** párrafos cortos con etiquetas de color y la bitácora "escribiéndose".
- **Sala de control:** plano general (sala y pantalla grande) que se desliza hacia el plano lateral de los operadores.
- **Sala de control viva:**
  - Cada monitor de operador tiene un panel animado: telemetría, traza terrestre, estado de subsistemas o trayectoria.
  - Una fila de relojes LED en vivo: UTC, Florida, tiempo de misión (MET), cuenta, distancia a la Luna y la estación DSN activa.
  - Teclados, ratones, tazas, carpetas y teléfonos en los puestos; paneles acústicos y pantallas en las paredes laterales.
- **Movimientos naturales:** IK de dos huesos (manos sobre el teclado con pulsaciones alternas, aplauso frente al pecho, puños
  arriba, manos a la cabeza, señalar), cabeza que mira la pantalla y respiración.
- **Órbita fluida:** la nave avanza con el progreso real de cada fase y las fotos se copian desde la GPU de forma asíncrona,
  sin tirones.

## Planos 2D (`docs/planos/`)

Cinco láminas tipo plano de arquitectura (azul, cajetín, escala gráfica, norte y cotas) renderizadas **desde los mismos modelos
del juego** con `build_plans.py`: plano de conjunto, Centro de Control (corte a 3,5 m), Edificio de Ensamblaje y sala limpia
(corte a 12 m), plataforma de lanzamiento y sitio lunar (relieve). También se consultan dentro del juego (tecla M y menú principal).

## Misión tripulada tipo Artemis II

Escenario **Sobrevuelo lunar tripulado (tipo Artemis II)** con la órbita **Sobrevuelo lunar con retorno libre**:
- `FreeReturnTrajectory` resuelve la trayectoria en el **problema restringido de 3 cuerpos Tierra-Luna** (RK4 adaptativo + disparo):
  órbita de estacionamiento a 185 km → TLI (Δv ≈ 3,18 km/s) → sobrevuelo por la cara oculta a ~7 300 km el día 2,8 → retorno libre
  sin motor → reentrada (perigeo 50 km) a los ~9,3 días.
- La vista de trayectoria dibuja el "8" con flechas de sentido, ida (morado) y vuelta (naranja) y **8 hitos numerados**
  (estacionamiento, TLI, 3 correcciones de rumbo, sobrevuelo, retorno libre, reentrada). Escala no lineal ("no a escala") para que
  el sobrevuelo se vea.
- Piezas nuevas: cápsula Orion con soporte vital (el validador exige soporte vital si la misión es tripulada), módulo de servicio
  europeo, enlace láser O2O + estaciones ópticas (y su regla de compatibilidad), 32 cámaras de Orion, cámaras Nikon D5/Z9 de la
  tripulación, CubeSat ATENEA (CONAE), cronología real del ascenso del SLS.
- Fase **REGRESO**: lo que el enlace no alcanzó a bajar se recupera de las tarjetas SD tras el amerizaje (~10 000 fotos en
  Artemis II); si la cápsula se pierde, esos datos se pierden.

## Funciones para el jurado (enunciado oficial → juego)

Criterios de evaluación: **impacto, creatividad, solidez y presentación**.

| El reto pide / sugiere | Dónde está |
|---|---|
| Objetivo primero, consecuencias en cada decisión | Escenarios; telemetría en vivo; tooltips con vista previa ("Provocaría…") |
| Recursos: masa, energía, presupuesto, datos, comunicaciones, tiempo | `MissionTelemetry` + barras del HUD con explicación al pasar el ratón |
| Comprobar masa/órbita vs lanzador | `OrbitAndLaunchValidator` (Δv, Tsiolkovsky, capacidad por órbita, inserción a bordo) |
| Hasta 4 participantes con roles y negociación | `RoleTurnManager` (hot-seat, consenso) + `CollaborationLog` (puntaje de equipo 0-10) |
| Simulación, eventos aleatorios, análisis post-misión | `MissionSimulator` (semilla), 5 eventos reales, 3 tarjetas + rango A-F |
| Dificultades, escenarios, sandbox | Básico/Avanzado; 5 escenarios (clima LEO, GEO, Luna, CubeSat universitario, Sandbox) |
| Reportes, guardar/comparar diseños, ranking | JSON+TXT por misión; ventana **Diseños**; ranking local por escenario |
| Visualizaciones interactivas | Ensamblador 3D modular, lanzamiento con separación de etapas, trayectoria (Hohmann / translunar / retorno libre en 3 cuerpos), sala de control, centro espacial caminable, planos 2D |
| Valor educativo | Tutorial de 6 páginas, tooltips con fuentes, "Tu misión se parece a…" (8 misiones reales) |

## Estructura

```
Assets/_Project/
├── Scripts/  (MissionDesign.Runtime.asmdef)
│   ├── Core/          enums, GameInfo, MissionStateManager (eventos de fase/diseño/resultado)
│   ├── Data/          ScriptableObjects del catálogo (cohetes, órbitas, piezas, redes terrenas, eventos, escenarios, misiones reales)
│   ├── Simulation/    telemetría, validador, simulador, runner, reportes, diseños guardados, comparación, ranking
│   ├── Multiplayer/   roles hot-seat, consenso, colaboración
│   ├── Exploration/   personaje, cámara en 3.ª persona, puertas, interacciones, zonas, HUD, mapa y círculo de entrada
│   ├── Characters/    poses procedurales (sentado, celebrar, aplaudir, tensión, señalar) y vestuario por ambiente
│   └── UI/            HUD, catálogo, tooltips, post-misión, menús, sonidos; Visual3D/ (ensamblador, lanzamiento, órbita, cámaras)
├── Editor/            generador de la base, vinculador de modelos, texturas procedurales, constructor de escena, build
├── Data/Generated/    catálogo generado (menú NASA SpaceApps)
├── Art/               Models/ (FBX de Blender), Characters/ (personaje + animaciones Mixamo), Plans/ (láminas), Materials/, Textures/
├── Scenes/            MissionDesign_Main.unity y SpaceCenter.unity (generadas)
└── Tests/             EditMode (lógica) y PlayMode (escena + capturas en Logs/)
ArtSource/Blender/     scripts/*.py (modelos y planos reproducibles), blend/*.blend (editables), build_all.ps1
docs/planos/           láminas 2D del centro espacial (PNG)
```

## Pipeline (todo regenerable)

```
powershell -ExecutionPolicy Bypass -File ArtSource/Blender/build_all.ps1          # 1. modelos 3D → Assets/_Project/Art/Models
Unity.exe -batchmode -quit -projectPath . -executeMethod MissionDesign.Editor.MainSceneBuilder.BuildEverything # 2-3. catálogo + escena de diseño + centro espacial
Unity.exe -batchmode -projectPath . -runTests -testPlatform EditMode -testResults Logs/EditMode-results.xml    # 4. pruebas
Unity.exe -batchmode -projectPath . -runTests -testPlatform PlayMode -testResults Logs/PlayMode-results.xml    #    (+ capturas)
Unity.exe -batchmode -quit -projectPath . -executeMethod MissionDesign.Editor.BuildPlayer.BuildWindows         # 5. Builds/Windows/AstroByte.exe
```

Los mismos pasos están en el menú **NASA SpaceApps** del editor. Reconstruir la escena la **sobrescribe**:
tras personalizarla a mano, deja de usar ese menú.

## Arte 3D (Blender)

`ArtSource/Blender/scripts/` genera todo por código (Blender 5.2, modo headless): cohetes a escala real con etapas
separables (Falcon 9, Electron, SLS Block 1, Ariane 64), plataforma con torre de servicio, 25 piezas de nave a escala de
exhibición, sala de control (3 gradas, pantallas `Screen_*`, asientos `Seat_*`) y superficie lunar con módulo de aterrizaje.
`ModelLinker` asigna cada FBX a su pieza por `componentId`. Para usar un modelo hecho a mano, expórtalo con el mismo nombre.
Ejes FBX→Unity: (x, y, z)<sub>Unity</sub> = (−x, z, −y)<sub>Blender</sub>; los grupos de cada modelo se unen en una malla antes de exportar.

**Planos:** `build_plans.py` (Eevee + Freestyle, plano de corte horizontal) escribe `docs/planos/` y `Art/Plans/`.

**Personaje:** `export_character.py -- <PERSONAJE.blend>` exporta el avatar (Avaturn, esqueleto Mixamo de 52 huesos) con sus
texturas a `Art/Characters/`; el constructor lo configura como Humanoid y arma el Animator (Idle/Walk/Run en el sitio + Jump).
`build_all.ps1 -CharacterBlend <ruta>` lo incluye en la regeneración.

**Mixamo:** en `ControlRoom/Operator_<Rol>` asigna Avatar + Animator Controller con triggers `Celebrate`, `Stress`, `Type`, `PointScreen`.

## Simulación

`MissionSimulator.Run()` es puro y reproducible por semilla: lanzamiento (confiabilidad del cohete) → inserción (Δv a bordo)
→ operaciones con 1-2 eventos ponderados por órbita y red terrena → transmisión. La memoria a bordo absorbe cortes de enlace y
la electrónica endurecida ignora los SEU (Avanzado). Puntuación: ciencia 60 + reserva 20 + datos 10 + calendario 10; rango A-F.
Para la demo: fija `Fixed Seed` en `MissionSystems → MissionSimulationRunner`.

Datos del jugador (Windows): `%USERPROFILE%/AppData/LocalLow/<Company>/<Product>/` → `MissionReports/`, `Designs/`, `leaderboard.json`.

## Transparencia de datos

Cada valor lleva `dataConfidence` (**Official** / **Approximate** / **GameEstimate**), `sourceUrl` y notas, visibles en los tooltips.
Validados con fuentes públicas: capacidades de cohetes, ABI (338 kg, 450 W, 66 Mbps; WMO OSCAR), OCO-2 (131 kg; kit de prensa JPL),
GEDI (519 kg, 400 W; NASA NTRS), NISAR (hasta 26 Tb/día; NASA), CubeSat 101 (1U ≈ 1-1.33 kg, lanzamiento CSLI ≤ $300 000).
Siguen siendo estimación de juego: costos de instrumentos, piezas genéricas (buses, paneles, computadoras, ADCS) y redes terrenas.

## Modelo de comunicaciones

`GB/día = tasa(Mbps) · horas de contacto · 3600 / 8 / 1000`, con
`tasa = min(máx. del módem, tasa_ref · (r_ref / r)² · ganancia de la red terrena)` y horas = horas base de la órbita × factor de la red.
