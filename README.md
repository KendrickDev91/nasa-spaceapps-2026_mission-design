# MISSION TRADE-OFF — proyecto para NASA Space Apps Challenge 2026

Juego de diseño de misiones espaciales para 1-4 jugadores (hot-seat) que responde al reto
**Space Mission Design Game**: definir objetivo, diseñar la nave, elegir instrumentos, cohete, órbita y
segmento terreno, gestionar los 6 recursos críticos, negociar en equipo, simular la misión y entender
**por qué** triunfó o falló.

Unity 6000.5.5f1 · URP · TextMeshPro · Input System · data-driven (ScriptableObjects) · arte propio en Blender 5.2.

> Título provisional: se cambia en `Assets/_Project/Scripts/Core/GameInfo.cs`. Proyecto independiente,
> **no es un producto oficial de la NASA**: no usar logos ni nombres de instalaciones reales (p. ej. el "Mission Design Lab" de Goddard).

## Cómo se juega

1. **Menú principal** → Jugar (F1: tutorial · Esc: pausa).
2. El **Director** elige dificultad (Básico/Avanzado), escenario y órbita; cada rol elige sus piezas (teclas **1-4**, **Tab** pasa el turno).
3. Pasa el ratón por cualquier opción: el tooltip explica la pieza, su fuente y **qué le pasaría a tu misión**.
4. Resuelvan los conflictos del panel inferior, cada jugador **aprueba**, y **LANZAR MISIÓN**.
5. Mira el lanzamiento, la trayectoria y la sala de control; lee el **análisis post-misión** (bien / falló / rango A-F,
   misión real parecida, ranking) y vuelve al diseño para mejorar. **Diseños** guarda alternativas y las compara.

## Funciones para el jurado (enunciado oficial → juego)

| El reto pide / sugiere | Dónde está |
|---|---|
| Objetivo primero, consecuencias en cada decisión | Escenarios; telemetría en vivo; tooltips con vista previa ("Provocaría…") |
| Recursos: masa, energía, presupuesto, datos, comunicaciones, tiempo | `MissionTelemetry` + barras del HUD con explicación al pasar el ratón |
| Comprobar masa/órbita vs lanzador | `OrbitAndLaunchValidator` (Δv, Tsiolkovsky, capacidad por órbita, inserción a bordo) |
| Hasta 4 participantes con roles y negociación | `RoleTurnManager` (hot-seat, consenso) + `CollaborationLog` (puntaje de equipo 0-10) |
| Simulación, eventos aleatorios, análisis post-misión | `MissionSimulator` (semilla), 5 eventos reales, 3 tarjetas + rango A-F |
| Dificultades, escenarios, sandbox | Básico/Avanzado; 5 escenarios (clima LEO, GEO, Luna, CubeSat universitario, Sandbox) |
| Reportes, guardar/comparar diseños, ranking | JSON+TXT por misión; ventana **Diseños**; ranking local por escenario |
| Visualizaciones interactivas | Ensamblador 3D modular, lanzamiento con separación de etapas, trayectoria (Hohmann / translunar), sala de control |
| Valor educativo | Tutorial de 6 páginas, tooltips con fuentes, "Tu misión se parece a…" (8 misiones reales) |

## Estructura

```
Assets/_Project/
├── Scripts/  (MissionDesign.Runtime.asmdef)
│   ├── Core/          enums, GameInfo, MissionStateManager (eventos de fase/diseño/resultado)
│   ├── Data/          ScriptableObjects del catálogo (cohetes, órbitas, piezas, redes terrenas, eventos, escenarios, misiones reales)
│   ├── Simulation/    telemetría, validador, simulador, runner, reportes, diseños guardados, comparación, ranking
│   ├── Multiplayer/   roles hot-seat, consenso, colaboración
│   └── UI/            HUD, catálogo, tooltips, post-misión, menús, sonidos; Visual3D/ (ensamblador, lanzamiento, órbita, cámaras)
├── Editor/            generador de la base, vinculador de modelos, texturas procedurales, constructor de escena, build
├── Data/Generated/    catálogo generado (menú NASA SpaceApps)
├── Art/               Models/ (FBX de Blender), Materials/, Textures/ (procedurales)
├── Scenes/            MissionDesign_Main.unity (generada)
└── Tests/             EditMode (lógica) y PlayMode (escena + capturas en Logs/)
ArtSource/Blender/     scripts/*.py (modelos reproducibles), blend/*.blend (editables), build_all.ps1
```

## Pipeline (todo regenerable)

```
powershell -ExecutionPolicy Bypass -File ArtSource/Blender/build_all.ps1          # 1. modelos 3D → Assets/_Project/Art/Models
Unity.exe -batchmode -quit -projectPath . -executeMethod MissionDesign.Editor.NasaDatabaseGenerator.Generate   # 2. catálogo + vincula modelos
Unity.exe -batchmode -quit -projectPath . -executeMethod MissionDesign.Editor.MainSceneBuilder.Build           # 3. escena completa
Unity.exe -batchmode -projectPath . -runTests -testPlatform EditMode -testResults Logs/EditMode-results.xml    # 4. pruebas
Unity.exe -batchmode -projectPath . -runTests -testPlatform PlayMode -testResults Logs/PlayMode-results.xml    #    (+ capturas)
Unity.exe -batchmode -quit -projectPath . -executeMethod MissionDesign.Editor.BuildPlayer.BuildWindows         # 5. Builds/Windows/MissionTradeOff.exe
```

Los mismos pasos están en el menú **NASA SpaceApps** del editor. Reconstruir la escena la **sobrescribe**:
tras personalizarla a mano, deja de usar ese menú.

## Arte 3D (Blender)

`ArtSource/Blender/scripts/` genera todo por código (Blender 5.2, modo headless): cohetes a escala real con etapas
separables (Falcon 9, Electron, SLS Block 1, Ariane 64), plataforma con torre de servicio, 25 piezas de nave a escala de
exhibición, sala de control (3 gradas, pantallas `Screen_*`, asientos `Seat_*`) y superficie lunar con módulo de aterrizaje.
`ModelLinker` asigna cada FBX a su pieza por `componentId`. Para usar un modelo hecho a mano, expórtalo con el mismo nombre.
Ejes FBX→Unity: (x, y, z)<sub>Unity</sub> = (−x, z, −y)<sub>Blender</sub>; los grupos de cada modelo se unen en una malla antes de exportar.

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
