# Fuentes de verdad — Oasis Stay

Esta carpeta es una **copia de solo lectura** de la documentación aprobada del proyecto. Los originales
están en `../../../Documentacion/` (fuera del repositorio). Los `.docx` se convirtieron a Markdown y sus
imágenes (pseudocódigo de los patrones, diagramas) se extrajeron a `img/`.

**Nadie edita estos archivos** (lo bloquea un hook). Si una fuente está mal, se registra en
`../decisiones.md` y se pide decisión; no se "corrige" la fuente para que encaje con el código.

## Jerarquía (si dos fuentes se contradicen, manda la de número menor, salvo decisión registrada)

| # | Fuente | Archivo | Para qué se usa |
|---|---|---|---|
| 0 | Decisiones aprobadas por el equipo | `../decisiones.md` (solo filas con Estado = Aprobada) | Resuelve conflictos entre fuentes. Prevalece sobre todo lo demás. |
| 1 | Guías de la profesora | `guia-entrega-3.md`, `guia-despliegue-profesora.md` | Qué se evalúa: flujos alternativos, validaciones, errores, GoF/SOLID, README, commits; qué debe tener el diagrama de despliegue. |
| 2 | Historias de usuario en Jira (proyecto SCRUM) | `jira/` (instantánea fechada) | Criterios de aceptación Gherkin de US-01..US-11. Base funcional. |
| 3 | Entregable 1 — requisitos | `entregable-1-requisitos.md` | RU-01..11, RF-01..11, RNF-01..08, RN-01..06, matriz de trazabilidad. |
| 4 | Entregable 2 (versión 2026-11-17, incluye SOLID) y plantilla GoF | `entregable-2.md`, `plantilla-patrones-gof.md`, `img/gof-image1..5.png` | Alcance MVP (F1..F6), los 5 patrones y su pseudocódigo (nombres y firmas exactos). |
| 5 | Modelo C4 (niveles 1-4) | `c4-modelo.md` | Contenedores, componentes del backend y clases por patrón. |
| 6 | Diagramas de estado | `diagramas-estado.md` | Transiciones de Habitación y Reserva; casos CP-H01..08 y CP-R01..08. |
| 7 | Diagramas de secuencia y de clases | `diagramas-secuencia.md` (1-2), `diagramas-secuencia-nuevos.md` (3-5), `img/diagrama-clases.svg`, `img/secuencia-*` | Operaciones, participantes y atributos de las entidades. |
| 8 | Diagrama de componentes y de despliegue | `diagrama-componentes.md`, `despliegue.md` | Interfaces entre componentes, nodos, artefactos, protocolos y puertos. |
| 9 | Front-end existente (Figma) | `../../Front-end/` | Pantallas ya construidas. Se conectan, no se rediseñan. |

## Pendientes sin fuente

- **Diagrama de despliegue:** recibido el 2026-11-17 (`despliegue.md`). Sus diferencias con el entorno local del MVP se resuelven en D-14/D-26/D-27.
- **Hora máxima de check-out (RN-04):** ninguna fuente la define (resuelto en D-12).

Todo elemento del código debe poder citar una fila de esta tabla. Lo que no tenga cita es un invento
y se rechaza en la verificación.
