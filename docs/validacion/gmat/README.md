# Validación de la trayectoria de retorno libre con NASA GMAT

**Herramienta:** NASA GMAT R2026a (General Mission Analysis Tool), en modo consola. Es uno de los recursos oficiales del reto.
**Script:** [`free_return_artemis2.script`](free_return_artemis2.script). Lo genera Unity a partir del modelo del juego
(menú *NASA SpaceApps → Export GMAT Validation Script*, o dentro de *Build Everything*).
**Resultados:** [`resultados_gmat.txt`](resultados_gmat.txt).

## Qué se hizo

1. El juego calcula la trayectoria tipo Artemis II con el **problema restringido de 3 cuerpos (CR3BP)**: la Tierra y la Luna
   en órbitas circulares, integrado con RK4 adaptativo.
2. El estado justo después de la TLI (185 km, 10,95 km/s) se exporta a GMAT en un sistema rotatorio Tierra-Luna (*ObjectReferenced*).
3. GMAT propaga ese estado con **efemérides reales DE405**: campo gravitatorio de la Tierra 4×4, Sol y Luna como masas
   puntuales, e integrador Runge-Kutta 8(9) con precisión 1e-11. La época es ilustrativa: 2 de abril de 2026.
4. Un **corrector diferencial** de GMAT busca la corrección de rumbo (TCM, en ejes VNB) que logra el mismo sobrevuelo y el
   perigeo de 50 km.

## Resultados (trayectoria en "8", 2026-11-15)

El solver exige la misma familia que Artemis II: rodea la Luna en sentido horario (marco rotatorio), tarda ~4 días en
llegar y la vuelta cruza la ida antes de volver a la Tierra (el cruce del "8").

| | Juego (CR3BP) | GMAT: misma TLI sin corregir | GMAT con corrección de rumbo |
|---|---|---|---|
| Sobrevuelo lunar | 7 671 km · día 3,72 | 11 581 km · día 4,05 | **7 672 km · día 4,11** |
| Perigeo de regreso | 50 km · día 7,67 | −4 132 km (entra demasiado empinada) | **49,8 km · día 8,19** |
| Corrección necesaria | — | — | **≈ 26,6 m/s** justo después de la TLI |

Días contados desde la TLI. Sumando ~1,1 días previos (órbita de estacionamiento y órbita alta), GMAT da un viaje de
**~9,3 días** desde el despegue; Artemis II duró ~9,1 días (1 → 10 de abril de 2026).

Versión anterior del solver (antes de exigir el "8"): la corrección necesaria era de 142 m/s y el regreso, el día 10,9.

## Lectura

- La geometría del modelo del juego (el "8", el sobrevuelo por la cara oculta y la duración del viaje) se confirma con
  una herramienta profesional de la NASA.
- Con el sistema real (la órbita lunar es excéntrica e inclinada y hay perturbación solar) hace falta una **corrección de
  rumbo**. Por eso las misiones reales planifican correcciones (TCM). En Artemis II la TLI se calculó directamente con
  efemérides, así que sus correcciones fueron de pocos m/s; aquí son ~27 m/s porque se parte del modelo simplificado
  (con la trayectoria anterior, que no era la familia del "8", hacían falta 142 m/s).
- El juego lo cuenta en el hito «Corrección de rumbo» y en el panel de la plataforma del centro espacial.

## Reproducir

```
GmatConsole.exe docs/validacion/gmat/free_return_artemis2.script
```
