# Matriz de trazabilidad — Oasis Stay

Generada el 2026-11-19 a partir del código, las pruebas y el historial de Git. Cadena: Problema (E1) → RU → RF → US/Jira → escenarios → RN/RNF → decisión → diagrama → patrón → clase → endpoint → pantalla → prueba → commit.

## US-01 (SCRUM-5) — Consultar habitaciones disponibles

- Épica en Jira: SCRUM-16 Gestión de Habitaciones y Disponibilidad
- Cadena: **US-01**: problema E1 (consulta manual) → RU-01 → RF-01 → SCRUM-5 ESC1-3 → RN-01, RN-05 → D-19, D-35 → DS1 `consultarDisponibilidad` → `GestionReservas.ConsultarDisponibilidad`/`BuscarLibres` → `GET /api/habitaciones/disponibilidad` → paso 3 de `Reservas` (lo conecta F2) → `US01_ESC1..3` → commit.
- Pruebas de escenarios encontradas: 3/3
  - ESC1: prueba sí (2) · pantalla docs/evidencias/F2b/US01-ESC1-suites-libres.png
  - ESC2: prueba sí (2) · pantalla docs/evidencias/F2b/US01-ESC2-alerta-con-alternativas.png
  - ESC3: prueba sí (1) · pantalla docs/evidencias/F2b/US01-ESC3-fechas-invalidas.png
  - US01_ESC1_ConsultaSuite_10al12Nov2026_ListaConNumeroCapacidadYPrecio (Back-end/OasisStay.Pruebas/F1/HabitacionesApiPruebas.cs:84)
  - US01_ESC1_CapacidadInsuficiente_ExcluyeHabitaciones (Back-end/OasisStay.Pruebas/F1/HabitacionesApiPruebas.cs:114)
  - US01_ESC2_EstandarAgotada_AlertaYSugerencias (Back-end/OasisStay.Pruebas/F1/HabitacionesApiPruebas.cs:122)
  - US01_ESC3_FechasInvalidas_15y14Nov2026_Bloquea (Back-end/OasisStay.Pruebas/F1/HabitacionesApiPruebas.cs:145)
  - US01_ESC2_Semilla_EstandarAgotada_AlertaTraeAlternativas (Back-end/OasisStay.Pruebas/F2/ReasignarYSemillaPruebas.cs:159)
- Commits (citan US-01 o pertenecen a la rama F1): 7
  - 009d134 docs: evidencias de pantalla de US-01, US-03 y US-04 (F2b)
  - dec647a test: pruebas de F1 (estados, disponibilidad, alta y semilla demo)
  - 2602c48 feat: pantalla Habitaciones conectada a la API y semilla demo de F1
  - 83decbc feat: endpoints de habitaciones y tipos con control por rol (F1)
  - 47719a2 feat: consulta de disponibilidad y gestión de estado de habitaciones (F1)
  - 545ad92 feat: estados de habitación con patrón State y reconstrucción desde texto (F1)
  - cffbcd7 Merge de la rama feat/F1-habitaciones: habitaciones, estados con State y alta

## US-02 (SCRUM-6) — Cambiar estado de habitación

- Épica en Jira: SCRUM-19 Auditoría y Reportería Financiera
- Cadena: **US-02**: problema E1 (estados en Excel) → RU-02 → RF-02 → SCRUM-6 ESC1-3 → RN-01 → D-07, D-18, D-28 → DE#1 + DS2 + actividad-2 → State (`IEstadoHabitacion`, 5 estados) → `PUT /api/habitaciones/{n}/estado` → `Habitaciones` → `CP_H01/03/05/06/07/08`, `US02_ESC1..3` → commit.
- Pruebas de escenarios encontradas: 3/3
  - ESC1: prueba sí (1) · pantalla sin captura propia (cubierto por prueba)
  - ESC2: prueba sí (1) · pantalla sin captura propia (cubierto por prueba)
  - ESC3: prueba sí (1) · pantalla sin captura propia (cubierto por prueba)
  - US02_ESC1_Hab301EnLimpiezaCompleta_PasaADisponibleVerde (Back-end/OasisStay.Pruebas/F1/HabitacionesApiPruebas.cs:164)
  - US02_ESC2_MantenimientoSinMotivo_Bloquea (Back-end/OasisStay.Pruebas/F1/HabitacionesApiPruebas.cs:181)
  - US02_ESC3_Hab204Ocupada_ForzarDisponible_ImpideYAvisaCheckOut (Back-end/OasisStay.Pruebas/F1/HabitacionesApiPruebas.cs:205)
- Commits (citan US-02 o pertenecen a la rama F1): 6
  - dec647a test: pruebas de F1 (estados, disponibilidad, alta y semilla demo)
  - 2602c48 feat: pantalla Habitaciones conectada a la API y semilla demo de F1
  - 83decbc feat: endpoints de habitaciones y tipos con control por rol (F1)
  - 47719a2 feat: consulta de disponibilidad y gestión de estado de habitaciones (F1)
  - 545ad92 feat: estados de habitación con patrón State y reconstrucción desde texto (F1)
  - cffbcd7 Merge de la rama feat/F1-habitaciones: habitaciones, estados con State y alta

## US-03 (SCRUM-7) — Registrar datos del huésped y fechas

- Épica en Jira: SCRUM-17 Gestión de Reservas y Huéspedes
- Cadena: **US-03**: problema E1 (datos a mano) → RU-03 → RF-03 → SCRUM-7 ESC1-3 → RN-05, RNF-08 → D-03, D-20 → DS1 + actividad-5 → `Cliente`, `RepositorioHuespedes` → `GET /api/huespedes/buscar`, `POST /api/huespedes` → `Reservas` → `US03_ESC1..3` → commit.
- Pruebas de escenarios encontradas: 3/3
  - ESC1: prueba sí (2) · pantalla docs/evidencias/F2/01-disponibilidad-ESC1.png, docs/evidencias/F2/04-US03-ESC1-reserva-creada.png, docs/evidencias/F2b/US03-ESC1-US04-ESC1-reserva-creada-304.png
  - ESC2: prueba sí (1) · pantalla docs/evidencias/F2b/US03-ESC2-autocompletado.png
  - ESC3: prueba sí (1) · pantalla docs/evidencias/F2/02-disponibilidad-ESC3-fechas-invalidas.png, docs/evidencias/F2/03-US03-ESC3-campos-invalidos.png, docs/evidencias/F2b/US03-ESC3-campos-invalidos.png
  - US03_ESC1_HuespedNuevo_Guarda (Back-end/OasisStay.Pruebas/F2/RegistroReservasPruebas.cs:98)
  - US03_ESC1_DocumentoExistente_ActualizaDatosSinDuplicar (Back-end/OasisStay.Pruebas/F2/RegistroReservasPruebas.cs:112)
  - US03_ESC2_Autocompletado_CC1020304050 (Back-end/OasisStay.Pruebas/F2/RegistroReservasPruebas.cs:124)
  - US03_ESC3_CorreoSinArrobaOTelefono310ABC_Invalido (Back-end/OasisStay.Pruebas/F2/RegistroReservasPruebas.cs:136)
- Commits (citan US-03 o pertenecen a la rama F2): 7
  - 009d134 docs: evidencias de pantalla de US-01, US-03 y US-04 (F2b)
  - d4832d3 test: pruebas de F2 (registro, atomicidad, integración con estados reales) y evidencias
  - e322424 feat: pantalla Reservas conectada a la API, disponibilidad real y semilla demo de F2
  - 37f855d feat: endpoints de huéspedes y reservas con control por rol (F2)
  - aea522c feat: registro, reasignación y check-in de reservas, y migración F2_HuespedesEnReserva (D-40)
  - 4f06cea feat: cadena de validación y dominio de reservas con huésped (F2)
  - c005d28 Merge de la rama feat/F2-reservas: reservas, cadena de validación, reasignación y check-in

## US-04 (SCRUM-8) — Asignar habitación libre

- Épica en Jira: SCRUM-17 Gestión de Reservas y Huéspedes
- Cadena: **US-04**: problema E1 (dobles reservas) → RU-04 → RF-04 → SCRUM-8 ESC1-3 → RN-01, RN-05 → D-09, D-21, D-31 → DE#1 + DE#4 + DS1 → Chain of Responsibility (4 validadores) → `POST /api/reservas`, `PUT /api/reservas/{id}/habitacion`, check-in → `Reservas` → `CP_R01/02/06`, `CP_H02`, `US04_ESC1..3` → commit.
- Pruebas de escenarios encontradas: 3/3
  - ESC1: prueba sí (1) · pantalla docs/evidencias/F2/01-disponibilidad-ESC1.png, docs/evidencias/F2b/US03-ESC1-US04-ESC1-reserva-creada-304.png
  - ESC2: prueba sí (3) · pantalla docs/evidencias/F2b/US04-ESC2-capacidad-excedida.png, docs/evidencias/F2b/US04-ESC2-conflicto-toast.png
  - ESC3: prueba sí (4) · pantalla docs/evidencias/F2/02-disponibilidad-ESC3-fechas-invalidas.png, docs/evidencias/F2b/US04-ESC3-1-modal-reasignar.png, docs/evidencias/F2b/US04-ESC3-2-aviso-upgrade.png, docs/evidencias/F2b/US04-ESC3-3-reasignada-105-a-107.png, docs/evidencias/F2b/US04-ESC3-4-habitaciones-105-disponible-107-reservada.png
  - US04_ESC3_Real_Reasigna105a107 (Back-end/OasisStay.Pruebas/F2/IntegracionEstadosRealesPruebas.cs:59)
  - US04_ESC3_Reasignar_Pantalla_105a107_ListaModalYEstados (Back-end/OasisStay.Pruebas/F2/ReasignarYSemillaPruebas.cs:50)
  - US04_ESC3_Reasignar_Pantalla_Upgrade_PideConfirmacionConTextoExacto (Back-end/OasisStay.Pruebas/F2/ReasignarYSemillaPruebas.cs:73)
  - US04_ESC2_Reasignar_Pantalla_HabitacionYaTomada_RechazaConMotivo (Back-end/OasisStay.Pruebas/F2/ReasignarYSemillaPruebas.cs:92)
  - US04_ESC1_Hab304Suite_Asigna_PasaAReservada (Back-end/OasisStay.Pruebas/F2/RegistroReservasPruebas.cs:161)
  - US04_ESC2_5HuespedesEnEstandarCapMax2_RechazaPorCapacidad (Back-end/OasisStay.Pruebas/F2/RegistroReservasPruebas.cs:173)
  - US04_ESC2_DosAsignacionesSimultaneas_SoloUnaGana (Back-end/OasisStay.Pruebas/F2/RegistroReservasPruebas.cs:181)
  - US04_ESC3_Reasigna105a107_105Disponible_107Reservada (Back-end/OasisStay.Pruebas/F2/RegistroReservasPruebas.cs:202)
- Commits (citan US-04 o pertenecen a la rama F2): 7
  - 009d134 docs: evidencias de pantalla de US-01, US-03 y US-04 (F2b)
  - d4832d3 test: pruebas de F2 (registro, atomicidad, integración con estados reales) y evidencias
  - e322424 feat: pantalla Reservas conectada a la API, disponibilidad real y semilla demo de F2
  - 37f855d feat: endpoints de huéspedes y reservas con control por rol (F2)
  - aea522c feat: registro, reasignación y check-in de reservas, y migración F2_HuespedesEnReserva (D-40)
  - 4f06cea feat: cadena de validación y dominio de reservas con huésped (F2)
  - c005d28 Merge de la rama feat/F2-reservas: reservas, cadena de validación, reasignación y check-in

## US-05 (SCRUM-9) — Cálculo automático de cotización

- Épica en Jira: SCRUM-18 Facturación, Caja y Check-out
- Cadena: **US-05**: problema E1 (cálculo manual) → RU-05 → RF-05 → SCRUM-9 ESC1-3 → RN-03, RN-04 → D-12, D-22 → DS4 + actividad-4 → Decorator (`EstadiaBase`…`ImpuestoIVA`) → `POST /api/cotizaciones`, `GET .../totales` → resumen de `Reservas` y `Facturacion` → `US05_ESC1..3` → commit.
- Pruebas de escenarios encontradas: 3/3
  - ESC1: prueba sí (1) · pantalla docs/evidencias/F3/06-US05-ESC1-3noches-535500.png
  - ESC2: prueba sí (1) · pantalla docs/evidencias/F3/07-US05-ESC2-consumos-late-321300.png
  - ESC3: prueba sí (1) · pantalla docs/evidencias/F3/11-US05-ESC3-modal-3a5-noches.png
  - US05_ESC1_3Noches150000_Subtotal450000_Iva85500_Total535500 (Back-end/OasisStay.Pruebas/F3/FacturacionPruebas.cs:20)
  - US05_ESC2_ConsumosYLateCheckout_Total321300 (Back-end/OasisStay.Pruebas/F3/FacturacionPruebas.cs:31)
  - US05_ESC3_AmplíaDe3a5Noches_Recalcula (Back-end/OasisStay.Pruebas/F3/FacturacionPruebas.cs:52)
- Commits (citan US-05 o pertenecen a la rama F3): 7
  - 863fb2e test: pruebas de F3 (cuenta, pagos, check-out y semillas) y evidencias de pantalla
  - bfab5e5 feat: pantalla Facturación, resumen de Reservas con cotización y semilla demo por escenario de F3
  - 7bdb4f9 feat: endpoints de facturación, pagos y check-out con control por rol (F3)
  - 1b1dfdb feat: migración de consumos y factura única por reserva (D-43)
  - 2fae7ac feat: servicios de facturación y pagos, Facade de check-out y PDF de factura con QuestPDF (F3)
  - 83d5c79 feat: Decorator de cuenta, estrategias de pago y modelo de facturación (F3)
  - d034b03 Merge de la rama feat/F3-facturacion: cotización, pagos, check-out y facturación

## US-06 (SCRUM-10) — Generar factura y Check-out

- Épica en Jira: SCRUM-18 Facturación, Caja y Check-out
- Cadena: **US-06**: RU-06 → RF-06 → SCRUM-10 ESC1-3 → RN-04, RN-06 → D-10, D-04 → DE#1 + DE#4 + DS4 → Facade (`CheckoutFacade`) + State → `POST /api/reservas/{id}/checkout` → `Facturacion` → `CP_H04`, `CP_R07`, `US06_ESC1..3` → commit.
- Pruebas de escenarios encontradas: 3/3
  - ESC1: prueba sí (1) · pantalla docs/evidencias/F3/05-US06-ESC1-factura-generada.png, docs/evidencias/F3/08-US06-ESC1-Rs1025-folio2.png
  - ESC2: prueba sí (1) · pantalla docs/evidencias/F3/02-US06-ESC2-saldo-pendiente.png
  - ESC3: prueba sí (1) · pantalla docs/evidencias/F3/12-US06-ESC3-ama-ve-208-en-limpieza.png
  - US06_ESC1_Rs1025_AnaGomez_Hab208_Saldo0_FacturaFolioPdfYEnLimpieza (Back-end/OasisStay.Pruebas/F3/FacturacionPruebas.cs:196)
  - US06_ESC2_Rs1026_Saldo80000_RechazaYRedirigePagos (Back-end/OasisStay.Pruebas/F3/FacturacionPruebas.cs:228)
  - US06_ESC3_Hab208_AmaDeLlavesLaVeEnLimpiezaDeInmediato (Back-end/OasisStay.Pruebas/F3/FacturacionPruebas.cs:414)
- Commits (citan US-06 o pertenecen a la rama F3): 7
  - 863fb2e test: pruebas de F3 (cuenta, pagos, check-out y semillas) y evidencias de pantalla
  - bfab5e5 feat: pantalla Facturación, resumen de Reservas con cotización y semilla demo por escenario de F3
  - 7bdb4f9 feat: endpoints de facturación, pagos y check-out con control por rol (F3)
  - 1b1dfdb feat: migración de consumos y factura única por reserva (D-43)
  - 2fae7ac feat: servicios de facturación y pagos, Facade de check-out y PDF de factura con QuestPDF (F3)
  - 83d5c79 feat: Decorator de cuenta, estrategias de pago y modelo de facturación (F3)
  - d034b03 Merge de la rama feat/F3-facturacion: cotización, pagos, check-out y facturación

## US-07 (SCRUM-11) — Historial de reservas respaldado

- Épica en Jira: SCRUM-19 Auditoría y Reportería Financiera
- Cadena: **US-07**: problema E1 (sin trazabilidad de cambios) → RU-07 → RF-07 → SCRUM-11 ESC1-3 → RNF-01/04/06/08 → D-23, D-36, D-37, D-38 → DS3 + actividad-3 → `HistorialReserva` (F0, solo inserción) + `AuditoriaHistorial` → `GET /api/historial` → `Historial` → `US07_ESC1..3` → commit. Eventos escritos por F2 (crear, reasignar, check-in) y F3 (check-out).
- Pruebas de escenarios encontradas: 3/3
  - ESC1: prueba sí (1) · pantalla docs/evidencias/F4a/esc1-linea-tiempo-rs4011.png
  - ESC2: prueba sí (1) · pantalla docs/evidencias/F4a/esc2-filtro-jrodriguez-20oct.png
  - ESC3: prueba sí (1) · pantalla sin captura propia (cubierto por prueba)
  - US07_ESC1_HistorialReserva_RS4011_LineaDeTiempo (Back-end/OasisStay.Pruebas/F4a/HistorialPruebas.cs:56)
  - US07_ESC2_FiltroUsuarioYFecha_jrodriguez_20Oct2026 (Back-end/OasisStay.Pruebas/F4a/HistorialPruebas.cs:73)
  - US07_ESC3_SinEditarNiBorrar_Administrador (Back-end/OasisStay.Pruebas/F4a/HistorialPruebas.cs:96)
- Commits (citan US-07 o pertenecen a la rama F4a): 5
  - a3d6a3b feat: consulta de bitácora con AuditoriaHistorial y filtros por reserva, usuario y fecha (US-07)
  - 558786c test: pruebas de F4a, semilla US07 y evidencias de pantalla
  - 45a9e00 feat: pantalla Historial con línea de tiempo, filtros por usuario y fecha, y sin resultados (F4a)
  - 5451503 feat: endpoint GET /api/historial solo Administrador e índice por fecha de registro
  - 9b32a62 Merge de la rama feat/F4a-historial: historial y bitácora de auditoría (consulta)

## US-08 (SCRUM-12) — Penalización por cancelación tardía

- Épica en Jira: SCRUM-17 Gestión de Reservas y Huéspedes
- Cadena: **US-08**: problema E1 (pérdidas por cancelaciones tardías) → RU-08 → RF-08 → SCRUM-12 ESC1-3 → RN-02 → D-01, D-23, D-24, D-44 → DE#4 → `Reserva.CancelarReserva`, `GestionReservas.Cancelacion.cs` → `POST /api/reservas/{id}/cancelar`, `/no-show/ejecutar` → `Reservas` → `CP_R03/R04/R05/R08`, `US08_ESC1..3` → commit.
- Pruebas de escenarios encontradas: 3/3
  - ESC1: prueba sí (2) · pantalla docs/evidencias/F4/esc1-cancelada-gratis.png, docs/evidencias/F4/esc1-modal-gratis.png
  - ESC2: prueba sí (2) · pantalla docs/evidencias/F4/esc2-cancelada-180000.png, docs/evidencias/F4/esc2-modal-penalidad-roja.png
  - ESC3: prueba sí (2) · pantalla docs/evidencias/F4/esc3-exonerada.png, docs/evidencias/F4/esc3-historial-bd.txt, docs/evidencias/F4/esc3-modal-exonerar-con-motivo.png
  - US08_ESC2_Api_CancelacionTardia_Penalidad180000_HabitacionDisponible (Back-end/OasisStay.Pruebas/F4/ApiCancelacionPruebas.cs:52)
  - US08_ESC1_Api_CancelacionGratuita_PenalidadCero (Back-end/OasisStay.Pruebas/F4/ApiCancelacionPruebas.cs:72)
  - US08_ESC3_Api_Exoneracion_Administrador_CobroCeroYMotivoEnHistorial (Back-end/OasisStay.Pruebas/F4/ApiCancelacionPruebas.cs:84)
  - US08_ESC1_CancelacionGratuita_48h_PenalidadCero_HabitacionDisponible (Back-end/OasisStay.Pruebas/F4/CancelacionPruebas.cs:104)
  - US08_ESC2_CancelacionTardia_12h_Penalidad180000 (Back-end/OasisStay.Pruebas/F4/CancelacionPruebas.cs:115)
  - US08_ESC3_Exoneracion_Administrador_CobroCeroYJustificacionEnLog (Back-end/OasisStay.Pruebas/F4/CancelacionPruebas.cs:125)
- Commits (citan US-08 o pertenecen a la rama F4): 5
  - 3dab3dd test: pruebas de F4, semilla US08 y evidencias de pantalla
  - 3077e09 feat: botón Cancelar con modal de penalidad y exoneración en Reservas (F4)
  - ee4ea60 feat: servicio de cancelación y No-Show con endpoints y control por rol (F4)
  - 0c1cfe1 feat: migración F4 y dominio de cancelación con penalidad (D-44)
  - 83bdb4b Merge de la rama feat/F4-cancelacion: cancelación, No-Show y penalidad

## US-09 (SCRUM-13) — Mapa gráfico interactivo de habitaciones

- Épica en Jira: SCRUM-16 Gestión de Habitaciones y Disponibilidad
- Cadena: **US-09**: problema E1 (ocupación consultada a mano) → RU-09 → RF-09 → SCRUM-13 ESC1-3 → RN-01; RNF-01/03 → D-24, D-28, D-45 → DS5 → `GestionEstadoHabitacion.ObtenerDistribucionHabitaciones` → `GET /api/habitaciones/distribucion` → `PanelControl` → `US09_ESC1..3` → commit.
- Pruebas de escenarios encontradas: 3/3
  - ESC1: prueba sí (1) · pantalla docs/evidencias/F5/esc1-mapa-por-pisos.png
  - ESC2: prueba sí (1) · pantalla docs/evidencias/F5/esc2-panel-302.png
  - ESC3: prueba sí (1) · pantalla docs/evidencias/F5/esc3-filtro-piso2-disponible.png
  - US09_ESC1_Visualizacion_AgrupadaPorPisos_ConColoresDeEstado (Back-end/OasisStay.Pruebas/F5/MapaPruebas.cs:164)
  - US09_ESC2_Hab302Ocupada_DevuelveHuespedYSalidaPrevista (Back-end/OasisStay.Pruebas/F5/MapaPruebas.cs:179)
  - US09_ESC3_FiltroPiso2Disponible_SoloLibresDelPiso2 (Back-end/OasisStay.Pruebas/F5/MapaPruebas.cs:199)
- Commits (citan US-09 o pertenecen a la rama F5): 6
  - 0c807a6 docs: evidencias de pantalla de US-09 (F5)
  - c1ba825 feat: distribución de habitaciones por piso con la reserva activa (US-09)
  - 8d6eb57 test: pruebas del mapa y semilla por escenario US09 con inventario de 30 habitaciones
  - 4f690f5 feat: PanelControl con mapa por pisos, filtros, panel lateral e indicadores reales (F5)
  - 3900e4a feat: endpoint de distribución de habitaciones con control por rol (F5)
  - 5c25cc3 Merge de la rama feat/F5-mapa: mapa interactivo de habitaciones por pisos

## US-10 (SCRUM-14) — Múltiples métodos de pago

- Épica en Jira: SCRUM-18 Facturación, Caja y Check-out
- Cadena: **US-10**: RU-10 → RF-10 → SCRUM-14 ESC1-3 → RN-06 → D-13, D-27, D-33 → DS4 → Strategy (`IEstrategiaPago`) → `POST /api/reservas/{id}/pagos` → `Facturacion` → `US10_ESC1..3` → commit.
- Pruebas de escenarios encontradas: 3/3
  - ESC1: prueba sí (1) · pantalla docs/evidencias/F3/09-US10-ESC1-tarjeta-250000-saldo-cero.png
  - ESC2: prueba sí (1) · pantalla docs/evidencias/F3/04-US10-ESC2-pago-mixto-saldo-cero.png
  - ESC3: prueba sí (1) · pantalla docs/evidencias/F3/03-US10-ESC3-monto-insuficiente.png
  - US10_ESC1_Deuda250000_TarjetaCredito_SaldoCero_ConComprobante (Back-end/OasisStay.Pruebas/F3/FacturacionPruebas.cs:85)
  - US10_ESC2_Mixto120000_50000TC_Aprob4892_Mas70000Transf_TRF9921_Cubre100 (Back-end/OasisStay.Pruebas/F3/FacturacionPruebas.cs:106)
  - US10_ESC3_Cobro150000_Solo100000_OSinReferencia_NoCierra (Back-end/OasisStay.Pruebas/F3/FacturacionPruebas.cs:127)
- Commits (citan US-10 o pertenecen a la rama F3): 7
  - 863fb2e test: pruebas de F3 (cuenta, pagos, check-out y semillas) y evidencias de pantalla
  - bfab5e5 feat: pantalla Facturación, resumen de Reservas con cotización y semilla demo por escenario de F3
  - 7bdb4f9 feat: endpoints de facturación, pagos y check-out con control por rol (F3)
  - 1b1dfdb feat: migración de consumos y factura única por reserva (D-43)
  - 2fae7ac feat: servicios de facturación y pagos, Facade de check-out y PDF de factura con QuestPDF (F3)
  - 83d5c79 feat: Decorator de cuenta, estrategias de pago y modelo de facturación (F3)
  - d034b03 Merge de la rama feat/F3-facturacion: cotización, pagos, check-out y facturación

## US-11 (SCRUM-15) — Reportes financieros exportables

- Épica en Jira: SCRUM-19 Auditoría y Reportería Financiera
- Cadena: **US-11**: problema E1 (consolidación manual en hojas de cálculo) → RU-11 → RF-11 → SCRUM-15 ESC1-3 → RNF-02/06 → D-04, D-24, D-34, D-46 → DS3 (exportar) + actividad-3 → `AuditoriaHistorial.GenerarReporte`, `ServicioReportes` → `GET /api/reportes/exportar` y `/resumen` → `Historial` (modal) → `US11_ESC1..3` → commit.
- Pruebas de escenarios encontradas: 3/3
  - ESC1: prueba sí (2) · pantalla docs/evidencias/F6/ui-esc1-esc2-exito-descarga.png, docs/evidencias/F6/ui-esc1-modal-kpis.png
  - ESC2: prueba sí (2) · pantalla docs/evidencias/F6/esc2-pdf-abierto.png, docs/evidencias/F6/ui-esc1-esc2-exito-descarga.png
  - ESC3: prueba sí (5) · pantalla docs/evidencias/F6/ui-esc3-2027-sin-movimientos.png, docs/evidencias/F6/ui-esc3-rango-400-dias.png
  - US11_ESC1_Api_ExportaExcel_01al15Oct2026 (Back-end/OasisStay.Pruebas/F6/ApiReportesPruebas.cs:50)
  - US11_ESC2_Api_ExportaPdf (Back-end/OasisStay.Pruebas/F6/ApiReportesPruebas.cs:63)
  - US11_ESC3_Api_SinMovimientos_Ano2027_ResumenEnCeroYExportar409 (Back-end/OasisStay.Pruebas/F6/ApiReportesPruebas.cs:91)
  - US11_ESC3_Api_Rango400Dias_400 (Back-end/OasisStay.Pruebas/F6/ApiReportesPruebas.cs:104)
  - US11_ESC1_ExportaExcel_01al15Oct2026_ColumnasFechaFolioConceptoImpuestosMetodoTotal (Back-end/OasisStay.Pruebas/F6/ReportesPruebas.cs:81)
  - US11_ESC2_ExportaPdf_ConGraficosYTotalesConsolidados (Back-end/OasisStay.Pruebas/F6/ReportesPruebas.cs:126)
  - US11_ESC3_SinMovimientos_Ano2027_NoGeneraArchivo (Back-end/OasisStay.Pruebas/F6/ReportesPruebas.cs:144)
  - US11_ESC3_Rango400Dias_Rechaza (Back-end/OasisStay.Pruebas/F6/ReportesPruebas.cs:159)
  - US11_ESC3_DesdeMayorQueHasta_Rechaza (Back-end/OasisStay.Pruebas/F6/ReportesPruebas.cs:173)
- Commits (citan US-11 o pertenecen a la rama F6): 6
  - 5fb518b feat: ServicioReportes y generación de reportes en AuditoriaHistorial con tabla de auditoría (US-11)
  - c7bdd79 test: pruebas de F6, semilla US11 y evidencias de reportes
  - eac6998 feat: modal de exportación de reportes en Historial y botón de Facturación solo para Administrador
  - e1d4456 feat: servicio de usuario autenticado para la bitácora (D-47)
  - d95003b feat: generadores .xlsx (ClosedXML) y PDF (QuestPDF), endpoints /api/reportes y usuario en el evento de check-out (D-47)
  - a0c2407 Merge de la rama feat/F6-reportes: reportes financieros exportables y usuario real en el check-out

## Casos de prueba CP encontrados en el código

CP_H01, CP_H02, CP_H03, CP_H04, CP_H05, CP_H06, CP_H07, CP_H08, CP_R01, CP_R02, CP_R03, CP_R04, CP_R05, CP_R06, CP_R07, CP_R08
