# MISSION TRADE-OFF — proyecto para NASA Space Apps Challenge 2026

Unity 6000.5.5f1 · URP · TextMeshPro · Input System · Arquitectura data-driven (ScriptableObjects).

> Título provisional: se cambia en un solo lugar, `Assets/_Project/Scripts/Core/GameInfo.cs`.
> No usar nombres de instalaciones reales de la NASA (p. ej. "Mission Design Lab" de Goddard) ni sus logos.

## Funciones clave para el jurado

- **Dos dificultades.** Básico: piezas esenciales, 1 evento y el cohete no falla. Avanzado: añade computadora de a bordo
  (memoria que absorbe cortes de enlace y electrónica endurecida contra radiación) y control de orientación (el apuntamiento limita la ciencia).
- **Segmento terreno.** El rol Telecom elige la red de estaciones (Near Space Network, red comercial, Deep Space Network): ganancia, horas de contacto, costo y fiabilidad.
- **Tooltips educativos con vista previa.** Cada opción del catálogo muestra qué es, sus números con la fuente y su nivel de confianza, y
  qué le pasaría a tu misión si la eliges ("Provocaría: Masa excede la capacidad del lanzador…"). Las barras de recursos explican cada recurso.
- **Post-misión en 3 tarjetas.** "Lo que salió bien", "Lo que falló" y la puntuación con rango A-F.
- **"Tu misión se parece a…":** la misión real de la NASA más parecida a tu diseño (Terra, OCO-2, NISAR, GEDI, GOES-16, LRO, CAPSTONE).

## Estructura

```
Assets/_Project/
├── Scripts/                 (MissionDesign.Runtime.asmdef)
│   ├── Core/                enums, MissionStateManager (pendiente)
│   ├── Data/                ScriptableObjects: cohetes, órbitas, bus, paneles, baterías, antenas, instrumentos, eventos, escenarios
│   ├── Simulation/          MissionTelemetry, OrbitAndLaunchValidator, simulador (paso 4)
│   ├── UI/                  (tras revisar la escena principal)
│   └── Multiplayer/         roles hot-seat y negociación de trade-offs
├── Editor/                  (MissionDesign.Editor.asmdef) NasaDatabaseGenerator
├── Data/Generated/          assets creados por el menú "NASA SpaceApps"
├── Prefabs/ Art/ Scenes/ Settings/ Tests/
```

## Generar la base de datos

Menú **NASA SpaceApps → Generate Initial NASA Database**. Es idempotente: si ya existen,
actualiza los valores sin perder GUIDs ni los prefabs 3D que hayas asignado.

En batch:

```
Unity.exe -batchmode -quit -projectPath . -executeMethod MissionDesign.Editor.NasaDatabaseGenerator.Generate
```

## Motor matemático (Simulation/)

- `SpacecraftDesign`: estado del diseño; eventos `Changed`, `ComponentAdded`, `ComponentRemoved`.
- `MissionTelemetry`: recalcula los 6 recursos (masa, energía, presupuesto, datos, comunicaciones, tiempo)
  + valor científico en cada cambio. `Calculate()` es puro y se exporta como JSON (`TelemetryReport`).
- `OrbitAndLaunchValidator`: Δv, ecuación de Tsiolkovsky, capacidad del lanzador por órbita, límites del bus,
  presupuesto, energía, enlace y DSN. Cada problema indica severidad y rol responsable.

Demo en consola: **NASA SpaceApps → Run Telemetry Demo (Console)**.
Pruebas: Window → General → Test Runner → EditMode, o en batch:

```
Unity.exe -batchmode -projectPath . -runTests -testPlatform EditMode -testResults Logs/editmode-results.xml
```

## Escena principal

**NASA SpaceApps → Build Main Scene** genera `Assets/_Project/Scenes/MissionDesign_Main.unity`
(y el asset URP en `Settings/`). Contenido:

| Objeto | Qué hace |
|---|---|
| `MissionSystems` | `MissionStateManager` (base de datos, diseño, eventos) + `RoleTurnManager` (hot-seat 1-4 jugadores, consenso) |
| `Spacecraft` | `ModularSpacecraftAssembler` con sockets `CoreBus`, `TopPayloadSocket`, `SolarWingLeft/Right`, `AntennaSocket`, `ThrusterSocket`, `InternalBay` |
| `ControlRoom` (x=40, fuera de cámara) | `MissionControlAnimatorBridge` + 4 `Operator_<Rol>` con `Animator` para los personajes de Mixamo |
| `HUD_Canvas` | Barra de roles, catálogo por rol, telemetría de 6 recursos, panel de validación/consenso/lanzamiento |

Arte 3D: asigna tu prefab de Blender en el campo `prefab` de cada ScriptableObject; mientras esté vacío se dibuja un placeholder.
Mixamo: en cada `Operator_<Rol>` asigna Avatar + Animator Controller con triggers `Celebrate`, `Stress`, `Type`, `PointScreen`.

Volver a ejecutar el menú **sobrescribe** la escena: tras personalizarla a mano, deja de usarlo.

Prueba de humo PlayMode (carga la escena, juega el flujo y guarda capturas en `Logs/screenshot_*.png`):

```
Unity.exe -batchmode -projectPath . -runTests -testPlatform PlayMode -testResults Logs/PlayMode-results.xml
```

## Simulación y análisis post-misión

Al pulsar **LANZAR MISIÓN** (con consenso del equipo y sin bloqueos):

1. `MissionSimulator.Run()` (función pura, reproducible por semilla) calcula toda la misión:
   lanzamiento (falla según la confiabilidad del cohete) → inserción orbital (Δv del motor del bus, Tsiolkovsky) →
   operaciones científicas con 1-2 eventos aleatorios ponderados por órbita → transmisión de datos.
   Eventos con modo seguro exigen que la batería sobreviva; los impactos pueden inutilizar un instrumento.
2. `MissionSimulationRunner` lo reproduce en pantalla (3 s por fase, botón "Saltar") y exporta
   el reporte a `%USERPROFILE%/AppData/LocalLow/DefaultCompany/nasa-spaceapps-2026_mission-design/MissionReports/` (`.json` + `.txt`).
3. La pantalla **Análisis post-misión** muestra veredicto (Éxito / Éxito parcial / Fallo), puntuación 0-100
   (ciencia 60 + reserva de presupuesto 20 + retorno de datos 10 + calendario 10), métricas y el **¿Por qué?** con los números exactos.

Para la demo ante el jurado: fija `Fixed Seed` en `MissionSystems → MissionSimulationRunner` para repetir la misma misión.

## Transparencia de datos

Cada asset lleva `dataConfidence`: **Official** (publicado por NASA/operador),
**Approximate** (redondeado de fuentes públicas) o **GameEstimate** (diseño de juego, validar antes de la final),
más `sourceUrl` y `sourceNotes`.

## Modelo de comunicaciones

`GB/día = tasa(Mbps) · horas de contacto · 3600 / 8 / 1000`, con
`tasa = min(máx. del módem, tasa_ref · (r_ref / r)² · ganancia_estación)`.
