# Contrato de dominio — Oasis Stay

Lista cerrada de lo acordado en la documentación. **El código usa exactamente estos nombres, firmas,
valores y mensajes.** Cada línea cita su fuente (ver `../fuentes/README.md`). Para añadir, renombrar o
quitar algo de este archivo se necesita una decisión aprobada en `../decisiones.md`.

Abreviaturas de cita: `E1` Entregable 1 · `E2` Entregable 2 · `GoF` plantilla de patrones (pseudocódigo
en `img/gof-image1..5.png`) · `C4-Nn` modelo C4 nivel n · `DE` diagramas de estado · `DS` diagramas de
secuencia · `DC` diagrama de clases · `CMP` diagrama de componentes · `D-nn` decisión.

## 1. Reglas de negocio (valores fijos)

| ID | Regla | Valor en código | Fuente |
|---|---|---|---|
| RN-01 | No asignar una habitación a reservas con fechas solapadas | `ValidadorSolapamiento`, mensaje "Las fechas se solapan con otra reserva (RN-01)" | E1, GoF |
| RN-02 | Cancelación con menos del plazo: sin reembolso y cobro de la primera noche | Plazo **24 h**, configurable | E1, DE, D-01 |
| RN-03 | IVA 19 % sobre subtotal (estadía + consumos + recargos) | `ImpuestoIVA` multiplica por `1.19m`, siempre capa externa | E1, GoF |
| RN-04 | Late check-out: recargo fijo | `CargoLateCheckout` suma `40000m` COP | E1, GoF |
| RN-05 | No exceder la capacidad máxima del tipo de habitación | `ValidadorCapacidad`, mensaje "Excede la capacidad de la habitación (RN-05)" | E1, GoF |
| RN-06 | Métodos autorizados: Efectivo, Tarjeta de crédito, Tarjeta de débito, Transferencia | Enum `MetodoPago { EFECTIVO, TARJETA_CREDITO, TARJETA_DEBITO, TRANSFERENCIA }`; estrategias `Efectivo`, `Tarjeta` (con `TipoTarjeta`), `Transferencia` | E1, DC, GoF |

## 2. Entidades (diagrama de clases)

| Entidad | Atributos acordados | Operaciones | Fuente |
|---|---|---|---|
| `Usuario` (abstracta) | `IdUsuario`, `Nombre`, `Contraseña` | `IniciarSesion()`, `CerrarSesion()` | DC (autenticación vía Identity: D-02) |
| `Administrador` : Usuario | — | `AuditarReservas()`, `GestionarUsuarios()`, `ConsultarHistorial()` | DC |
| `Recepcionista` : Usuario | — | `RegistrarReserva`, `AsignarHabitacion`, `ConsultarDisponibilidad`, `GenerarFactura` | DC |
| `AmaDeLlaves` : Usuario | — | `CambiarEstadoHabitacion` | DC |
| `Cliente` (huésped) | `IdCliente`, `Nombre`, `TipoDocumento` (CC, CE, Pasaporte), `Documento`, `Telefono`, `Correo`, `Edad` (opcional); obligatorios: tipo y número de documento, nombre, teléfono y correo con sintaxis válida; autocompletado por documento | `RegistrarDatos()`, `ActualizarDatos()` | DC, D-08, D-20, JIRA US-03 RN1-RN3 |
| `Reserva` | `IdReserva`, `FechaInicio`, `FechaFin`, `Estado` | `CalcularTotal()`, `CancelarReserva()`, `HacerCheckIn()`, `HacerCheckOut()` | DC, DE#4 |
| `TipoHabitacion` | `IdTipo`, `Nombre`, `CapacidadMax`, `PrecioBase`, `Descripcion` | `CalcularPrecioNoche()` | DC |
| `Habitacion` | `NumeroHabitacion`, `Estado` (texto persistido), `estadoActual: IEstadoHabitacion`, `Piso` (1-3), `MotivoMantenimiento` (D-28) | `Reservar()`, `HacerCheckIn()`, `HacerCheckOut()`, `MarcarDisponible()`, `MarcarMantenimiento(motivo)`, `CambiarEstado(nuevo)`, `PuedeReservarse()` | DC, DE#1, GoF, D-28 |
| `Servicio` (consumo) | `IdServicio`, `Nombre`, `Precio`, `Descripcion` | `AplicarServicio(Reserva)` | DC |
| `Factura` | `IdFactura`, `FechaEmision`, `ImpuestoIVA`, `ValorTotal`; folio único consecutivo | `GenerarFactura()`, `CalcularTotal()` | DC, E1 RF-06 |
| `DetalleFactura` | `IdDetalle`, `Concepto`, `Cantidad`, `ValorUnitario`, `SubTotal` | `CalcularSubtotal()` | DC |
| `Pago` | `IdPago`, `FechaPago`, `Monto`, `MetodoPago`, `TipoTarjeta`, `NumeroAutorizacion`, `ReferenciaBancaria`, `Vuelto`; asociado a `Reserva` y a `Factura` (D-33) | `RegistrarPago()`, `ValidarPago()` | DC, D-33 |
| `HistorialReserva` | `IdHistorial`, `FechaRegistro`, `AccionRealizada`, `IdReserva`, `Usuario`, `ValorAnterior`, `ValorNuevo`; solo inserción (nadie edita ni borra, ni el Administrador) | `RegistrarEvento()`, `ConsultarHistorial()` | DC, E1 RF-07, D-23, JIRA US-07 RN1-RN3 |

## 3. Estados

**Habitación** (5, RF-02): `Disponible`, `Reservada`, `Ocupada`, `EnLimpieza`, `Mantenimiento`. Enum
persistido `DISPONIBLE, OCUPADO, RESERVADA, EN_LIMPIEZA, MANTENIMIENTO` (DC). Transiciones válidas (DE#1):

| Desde | Operación | Guarda | Hacia |
|---|---|---|---|
| Disponible | `Reservar()` | fechas, RN-05, RN-01 | Reservada |
| Disponible | `MarcarMantenimiento(motivo)` | motivo registrado | Mantenimiento |
| Reservada | `HacerCheckIn()` | documento verificado | Ocupada |
| Reservada | `MarcarDisponible()` | reserva cancelada o No-show | Disponible |
| Ocupada | `HacerCheckOut()` | cuenta saldada ($0) | EnLimpieza |
| EnLimpieza | `MarcarDisponible()` | checklist completo (solo Ama de llaves) | Disponible |
| Mantenimiento | `MarcarDisponible()` | reparación terminada | Disponible |
| Reservada (habitación anterior) | reasignación antes del check-in (misma transacción que la nueva) | aviso de "Upgrade" con confirmación si la nueva es de categoría superior | Disponible (y la nueva: Disponible → Reservada) |

La fila de reasignación viene de D-21 (JIRA US-04 Esc3 y RN3). "Fuera de Servicio" (JIRA US-01) es
`Mantenimiento` (D-18). El motivo de `MarcarMantenimiento` tiene al menos 10 caracteres (D-24, JIRA US-02 RN2).
Cualquier otra combinación devuelve `ResultadoOperacion.Rechazada(motivo)` y el estado no cambia.
Mensaje acordado de ejemplo: "La habitación está en limpieza" (GoF).

Check-in (D-31): el recepcionista confirma el documento y debe coincidir con `Cliente.Documento`; se rechaza antes de `FechaInicio` o si la reserva no está `Confirmada`. Disponibilidad (D-35): `BuscarLibres` devuelve solo habitaciones con `Estado = Disponible`, capacidad suficiente y sin reserva solapada. Textos no literales y "categoría superior" (= mayor `PrecioBase`): D-32.

**Reserva** (DE#4): `Confirmada` (inicial), `EnCurso`, `Finalizada`, `Cancelada`, `NoShow`. Los tres
últimos son finales. No existe estado "Pendiente".

## 4. Patrones GoF (nombres y firmas exactos del pseudocódigo)

**State** (F1, RF-02) — `interface IEstadoHabitacion { string Nombre; ResultadoOperacion Reservar(Habitacion h);
HacerCheckIn(h); HacerCheckOut(h); MarcarDisponible(h); }` · clases `EstadoDisponible`, `EstadoReservada`,
`EstadoOcupada`, `EstadoEnLimpieza`, `EstadoMantenimiento` · `ResultadoOperacion.Exitosa()` / `.Rechazada(motivo)`.
El estado se guarda como texto y se reconstruye al cargar (E2).

**Chain of Responsibility** (F2, RF-03/04) — `abstract ValidadorReserva { Siguiente(ValidadorReserva);
virtual ResultadoValidacion Validar(SolicitudReserva) }` · orden fijo: `ValidadorFechas` →
`ValidadorCapacidad` → `ValidadorEstadoHabitacion` → `ValidadorSolapamiento` · `ResultadoValidacion.Ok()` /
`.Error(mensaje)` · `SolicitudReserva { CheckIn, CheckOut, Huespedes, Habitacion, TieneReservaSolapada() }`.
Mensajes: "La fecha de salida debe ser posterior a la fecha de llegada" (D-19, JIRA US-01 Esc3; no se
admiten estancias de 0 noches ni Day Use: D-22), RN-05 y RN-01 de la sección 1,
"La habitación no está disponible para reservar". Dónde se arma la cadena: D-09.

**Decorator** (F3, RF-05) — `interface ICuenta { decimal CalcularCosto(); }` · `EstadiaBase(noches, tarifa)` ·
`abstract CuentaDecorator(ICuenta)` · `CargoMinibar(cuenta, monto)` · `CargoLateCheckout(cuenta)` ·
`ImpuestoIVA(cuenta)`. Orden: base → minibar → late check-out (si aplica) → IVA. La cadena no se persiste:
se reconstruye desde los consumos guardados (E2).

**Strategy** (F6, RF-10) — `interface IEstrategiaPago { string Metodo; ResultadoPago Procesar(decimal total,
DatosPago datos); }` · `PagoEfectivo` ("Efectivo": rechaza "Monto insuficiente", aprueba con vuelto) ·
`PagoTarjeta` ("Tarjeta": exige `TipoTarjeta` y `NumeroAutorizacion`, "Falta tipo de tarjeta o autorización") ·
`PagoTransferencia` ("Transferencia": exige `ReferenciaBancaria`, "Falta la referencia bancaria") ·
`ProcesadorPagos(IEnumerable<IEstrategiaPago>).Cobrar(metodo, total, datos)` ·
`DatosPago { MontoRecibido, TipoTarjeta, NumeroAutorizacion, ReferenciaBancaria }`.

**Facade** (F4, RF-06) — `interface ICheckoutFacade { ResultadoCheckout ProcesarSalida(int idReserva,
string metodoPago, DatosPago datos); }` · `CheckoutFacade(IServicioFacturacion, IServicioHabitacion,
IRepositorioHistorial, IUnidadDeTrabajo)` · dentro de una transacción: `CerrarCuenta` →
`MarcarEnLimpieza(factura.IdHabitacion)` → `Archivar(idReserva, factura)` → `Confirmar()` ·
`ResultadoCheckout.Exitoso(factura)`. Quién la consume: D-10. Pagos (D-33): los abonos parciales se guardan con recibo; `ProcesarSalida` cobra el saldo restante y acepta `metodoPago` vacío si el saldo ya es $0.

**Descartado:** Factory Method (E2). Roles con ASP.NET Core Identity y `[Authorize(Roles = "...")]`.

## 5. Componentes del backend (C4-N3) y repositorios

`GestionReservas` (orquesta, no bloquea) · `MotorDisponibilidadAsignacion` (único dueño del bloqueo
transaccional) · `GestionEstadoHabitacion` · `MotorCuentasFacturacion` (calcula, no genera PDF) ·
`GestionPagos` · `AuditoriaHistorial` (consulta, no replica reglas) · `NotificacionesProceso` (reacciona a
eventos, sin reglas de cálculo) · `FacturaPdfGenerator` (infraestructura, recibe factura cerrada).

Repositorios: `RepositorioHabitaciones { BuscarLibres(fechaInicio, fechaFin, tipo, numHuespedes),
ConsultarTodas(), BuscarPorNumero(numero), Guardar(habitacion) }` · `RepositorioReservas
{ BuscarPorHabitacionYFechas(habitacion, fechaInicio, fechaFin), Guardar(reserva) }` ·
`RepositorioHuespedes` · `RepositorioConsumos` · `FacturaRepository` · `RepositorioPagos` ·
`RepositorioAuditoria` / `IRepositorioHistorial` (DS, C4-N3, C4-N4).

Servicios de aplicación: `GestionReservas.ConsultarDisponibilidad(fechaInicio, fechaFin, tipo, numHuespedes)`,
`GestionReservas.RegistrarReserva(SolicitudReserva)`, `GestionEstadoHabitacion.ObtenerHabitaciones()`,
`GestionEstadoHabitacion.CambiarEstado(numero, nuevoEstado, motivo)` (DS).

Operaciones de los diagramas de secuencia 3-5 (`fuentes/diagramas-secuencia-nuevos.md`, DS3-DS5):
- **Historial y reporte (DS3, RF-07, RF-11):** `AuditoriaHistorial.ObtenerRegistros(filtros)` →
  `RepositorioHistorial.BuscarEventos(filtros)`; `AuditoriaHistorial.GenerarReporte(formato)` crea el archivo y
  llama a `RepositorioHistorial.GuardarAuditoria(reporte)`. Flujo alternativo: formato inválido o error al
  generar → `errorGeneracion(motivo)` mostrado en `Ventanas/Historial`.
- **Facturación y check-out (DS4, RF-05, RF-06, RF-10):** `ServicioFacturacion.CalcularTotales(idReserva)` →
  resumen de cobro; `ICheckoutFacade.ProcesarSalida` → `CerrarCuenta(idReserva, metodoPago, datosPago)` →
  `MarcarEnLimpieza(idHabitacion)` (`Habitacion.HacerCheckOut()` delega en `estadoActual.HacerCheckOut(hab)`) →
  `Archivar`. DS4 escribe `archivarReserva(idReserva)`; se mantiene la firma del GoF `Archivar(idReserva, factura)`
  (fuente #4 manda sobre #7). Flujo alternativo: pago rechazado → `errorPago(motivo)` sin cambiar estados.
- **Mapa interactivo (DS5, RF-09):** `GestionEstadoHabitacion.ObtenerDistribucionHabitaciones()` →
  `RepositorioHabitaciones.ConsultarTodas()`; el filtro por piso o estado se aplica en `Ventanas/PanelControl`
  sin volver al servidor.

## 6. Pantallas del front (existentes, no se crean otras sin decisión)

`Ventanas/PanelControl`, `Ventanas/Habitaciones`, `Ventanas/Reservas`, `Ventanas/Facturacion`,
`Ventanas/Historial`, `Ventanas/Configuracion`, más el Login por rol de Figma (E2 ISP; por crear).
Acceso por rol (E2): Ama de llaves solo `Habitaciones`; Recepcionista operación diaria; Administrador
indicadores, historial y reportes. Matriz (D-34): Administrador accede a todo; Recepcionista: disponibilidad, reservas, check-in, facturación, pagos y cambio de estado salvo EnLimpieza→Disponible; AmaDeLlaves: solo `Habitaciones` (listar y cambiar estado).

Controles añadidos dentro de las pantallas existentes (D-30): menú de estado en la tarjeta de habitación, botón de check-in en `Reservas`, detalle/consumos/pago en `Facturacion`, tipo de documento y aceptación de privacidad en `Reservas`. El alta de habitaciones es el modal de `Habitaciones` (Administrador, Piso 1-3, valor por noche = `PrecioBase`) (D-28).

Datos semilla (D-29): Individual cap. 1 / $120.000; Doble Clásica cap. 2 / $185.000; Suite Ejecutiva cap. 4 / $320.000; habitaciones 101-104, 201-204 y 301-304; un usuario por rol (claves por user-secrets); servicio "Minibar".

## 7. Reglas operativas de Jira (D-24)

| Regla | Valor | Fuente |
|---|---|---|
| Hora oficial de check-in | 15:00 del día de llegada; el plazo de cancelación gratuita (24 h, D-01) se cuenta desde ahí | JIRA US-08 RN1 |
| Hora máxima de check-out | 12:00, configurable | D-12 |
| Exoneración de penalidad | Solo el Administrador, con justificación obligatoria registrada en la bitácora | JIRA US-08 RN3, Esc3 |
| Cálculos monetarios | Redondeo a 2 decimales; sin saldos negativos | JIRA US-05 RN3 |
| Pagos | La suma cubre exactamente lo adeudado (o vuelto si el efectivo es mayor); cada abono genera un recibo de caja digital | JIRA US-10 RN1, RN3; D-13 |
| Reportes | Solo Administrador; rango ≤ 365 días; sin archivos en blanco si no hay movimientos | JIRA US-11 RN1-RN3, Esc3 |
| Mapa de habitaciones | Pisos 1, 2 y 3; Verde Disponible, Rojo Ocupada, Amarillo EnLimpieza, Azul Reservada, Gris Mantenimiento | JIRA US-09 RN1 |

## 8. Casos de prueba obligatorios

CP-H01..CP-H08 y CP-R01..CP-R08 (DE). Cada uno debe existir como prueba con su ID en el nombre
(p. ej. `CP_H06_CheckInEnLimpieza_Rechazado`).

Criterios de aceptación de Jira (`../fuentes/jira/US-xx.md`, 3 escenarios por historia, 33 en total):
cada "Escenario N" de la historia US-xx es una prueba `USxx_ESCn_...` (p. ej.
`US05_ESC2_ConsumosYLateCheckout_Total321300`). Los valores de sus ejemplos (montos, mensajes, estados)
son datos de prueba obligatorios.
