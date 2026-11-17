# Plan de commits — Entrega 3

Regla acordada con la profesora el 2026-11-17 (D-15): el proyecto se reparte en **mínimo 5 commits**. Cada
compañero hace **mínimo 1**, y Simon, que lleva el desarrollo, hace el resto. La guía (`guia-entrega-3.md`)
exige que los commits sean reales, con mensaje descriptivo, hechos desde la cuenta Git de cada integrante y sin
cambios artificiales. Por eso cada compañero sube un bloque completo y distinto de la documentación técnica.
Los archivos no se repiten entre bloques, así que ningún commit queda vacío ni choca con otro.

## Reparto

| # | Integrante | Bloque | Archivos | Mensaje del commit |
|---|---|---|---|---|
| 1 | Simon Herrera | Limpieza del repositorio | Deja de versionar `Front-end/bin/` y `Front-end/obj/` (ya están en `.gitignore`, pero se subieron antes) | `chore: dejar de versionar bin/ y obj/ del front-end` |
| 2 | Simon Herrera | Base documental | `docs/fuentes/README.md`, `guia-entrega-3.md`, `guia-despliegue-profesora.md`, `docs/contrato/`, `docs/decisiones.md`, `docs/enlaces.md`, `docs/entrega3/plan-commits.md` | `docs: agregar jerarquía de fuentes, contrato y registro de decisiones` |
| 3 | Luis Miguel Arango | Requisitos | `docs/fuentes/entregable-1-requisitos.md`, `docs/fuentes/jira/`, `docs/fuentes/img/e1-*` | `docs: agregar requisitos del Entregable 1 e historias de usuario de Jira (US-01 a US-11)` |
| 4 | Yeison Cardozo | Arquitectura y despliegue | `docs/fuentes/c4-modelo.md`, `diagrama-componentes.md`, `despliegue.md`, `diagramas/c4-nivel*`, `diagramas/componentes.*`, `img/despliegue-*`, `docs/entrega3/despliegue-complemento.md` | `docs: agregar modelo C4, diagrama de componentes y diagrama de despliegue` |
| 5 | Thomas Muñoz | Entregable 2 y patrones | `docs/fuentes/entregable-2.md`, `plantilla-patrones-gof.md`, `img/e2-*`, `img/gof-*` | `docs: agregar Entregable 2 con alcance del MVP, patrones GoF y análisis SOLID` |
| 6 | Juan David Jiménez | Diagramas UML de comportamiento | `docs/fuentes/diagramas-estado.md`, `diagramas-secuencia.md`, `diagramas-secuencia-nuevos.md`, `diagramas/actividad-*`, `diagramas/clases-e1.*`, `diagramas/secuencia-*`, `img/secuencia-*`, `img/diagrama-clases.svg` | `docs: agregar diagramas de estado, secuencia, actividad y clases` |
| 7+ | Simon Herrera | Desarrollo | Un commit por paso real de cada funcionalidad F0..F6 (dominio, pruebas, API, integración con el front), más README y evidencias | Según cada funcionalidad |

**Estado al 2026-11-17 (`git shortlog -sne origin/main`):** Thomas Muñoz ya tiene 2 commits (`gitignore`,
`FRONT-END Segun Figma`) y Luis Miguel Arango 1 (`Primer commiy`). Con ellos el mínimo por compañero ya está
cubierto, así que:

- **Obligatorios:** Yeison (bloque 4) y Juan David (bloque 6), que aún no tienen commits.
- **Recomendados:** Luis Miguel (bloque 3) y Thomas (bloque 5). Sus commits actuales son de octubre (Entregable 2),
  y la guía habla de commits "correspondientes al trabajo desarrollado para el Entregable No. 3". Un commit
  nuevo los deja cubiertos si la profesora revisa fechas. Si no lo hacen, Simon sube esos bloques al final.

Cuenta mínima: Luis 1 + Thomas 2 + Juan David 1 + Yeison 1 + Simon 2 = 7 commits antes de escribir código.
`docs/fuentes/fuentes comprimidas.zip` no se sube (es una copia redundante).

**Orden:** Simon sube primero los commits 1 y 2. Los compañeros pueden subir los suyos en cualquier orden,
porque no comparten archivos. **Simon no debe hacer commit de los archivos de los bloques 3 a 6**: si lo hace,
el commit del compañero queda vacío.

**Opcional, para mejorar la nota de participación:** cada compañero puede hacer un segundo commit cuando su
bloque cambie, por ejemplo Yeison al pasar al Word las correcciones de `despliegue-complemento.md`, o
cualquiera al agregar capturas de evidencia en `docs/evidencias/`.

## Instrucciones para cada compañero

Cada uno recibe un `.zip` con sus archivos (las rutas ya vienen dentro de `docs/...`) y un `LEEME.txt`.

1. Pedirle a Luis Miguel acceso de colaborador al repositorio `luismi912/Proyecto-Oasis-Stay` (si no lo tiene).
2. Configurar Git con **su** cuenta, la misma de GitHub (si no, el commit no cuenta como suyo):
   ```
   git config --global user.name "Nombre Apellido"
   git config --global user.email "correo-de-su-cuenta-github"
   ```
3. Clonar o actualizar: `git clone https://github.com/luismi912/Proyecto-Oasis-Stay.git` o `git pull`.
4. Descomprimir el `.zip` **en la raíz del repositorio** (donde está `Oasis_Stay.slnx`) para que se cree `docs/...`.
5. Agregar solo sus rutas (nunca `git add .`), por ejemplo: `git add docs/fuentes/jira docs/fuentes/entregable-1-requisitos.md`.
6. `git commit -m "<mensaje de la tabla>"`
7. `git pull --rebase` y luego `git push`.
8. Mandarle a Simon el enlace del commit para la tabla de seguimiento.

## Tabla de seguimiento (la pide la guía; se completa al final con `git shortlog -sne`)

| Integrante | N.º de commits | Principales contribuciones | Historial |
|---|---|---|---|
| Luis Miguel Arango | | Requisitos E1 e historias de Jira | https://github.com/luismi912/Proyecto-Oasis-Stay/commits?author=USUARIO |
| Juan David Jiménez | | Diagramas UML | ídem |
| Thomas Muñoz | | Front-end (Figma), Entregable 2 y GoF | ídem |
| Yeison Cardozo | | C4, componentes y despliegue | ídem |
| Simon Herrera | | Backend, integración, pruebas y documentación base | ídem |
