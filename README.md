# Oasis Stay

Sistema de gestión hotelera del ITM (asignatura Diseño de Sistemas de Información, Entrega 3). Cubre el ciclo
completo de operación de un hotel: consulta de disponibilidad, reservas y huéspedes, asignación y reasignación de
habitaciones, check-in, cotización, pagos, check-out con factura, cancelaciones con penalidad, mapa de habitaciones,
historial de auditoría y reportes financieros exportables.

El repositorio contiene el **MVP** de la Entrega 3: backend ASP.NET Core Web API con los flujos alternativos y
validaciones implementados, integrado con el front existente en ASP.NET Core Razor Pages.

## Índice

1. [Integrantes](#1-integrantes)
2. [Tecnologías](#2-tecnologías)
3. [Arquitectura general](#3-arquitectura-general)
4. [Funcionalidades implementadas (US-01 a US-11)](#4-funcionalidades-implementadas-us-01-a-us-11)
5. [Requisitos de instalación](#5-requisitos-de-instalación)
6. [Configuración del entorno y dependencias](#6-configuración-del-entorno-y-dependencias)
7. [Cómo ejecutar el backend y el frontend](#7-cómo-ejecutar-el-backend-y-el-frontend)
8. [Pruebas](#8-pruebas)
9. [Semilla demo (datos de cada historia de usuario)](#9-semilla-demo-datos-de-cada-historia-de-usuario)
10. [Validaciones, excepciones y flujos alternativos](#10-validaciones-excepciones-y-flujos-alternativos)
11. [Patrones GoF y principios SOLID](#11-patrones-gof-y-principios-solid)
12. [Coherencia código, UML y C4](#12-coherencia-código-uml-y-c4)
13. [Despliegue](#13-despliegue)
14. [Respaldos de la base de datos](#14-respaldos-de-la-base-de-datos)
15. [Documentación técnica y diagramas](#15-documentación-técnica-y-diagramas)
16. [Evidencias del MVP](#16-evidencias-del-mvp)
17. [Historial de commits y participación](#17-historial-de-commits-y-participación)
18. [Decisiones y pendientes](#18-decisiones-y-pendientes)

---

## 1. Integrantes

| Integrante | Participación en la documentación y el proyecto |
|---|---|
| Simon Herrera Acosta | Backend, integración con el front, pruebas, semilla demo y documentación base |
| Luis Miguel Arango | Requisitos del Entregable 1 e historias de usuario de Jira |
| Yeison Cardozo | Modelo C4, diagrama de componentes y despliegue |
| Thomas Muñoz | Interfaz del front según Figma, Entregable 2 y patrones GoF |
| Juan David Jiménez | Diagramas UML de estado, secuencia, actividad y clases |

## 2. Tecnologías

| Capa | Tecnología | Versión / nota |
|---|---|---|
| Lenguaje y runtime | C# sobre .NET | `net10.0` en todos los proyectos (`*.csproj`) |
| Frontend | ASP.NET Core Razor Pages | `Front-end/` (proyecto `OasisStay.UI`, D-26) |
| API | ASP.NET Core Web API | `Back-end/OasisStay.Api` |
| Persistencia | Entity Framework Core + SQL Server | `Microsoft.EntityFrameworkCore.SqlServer` 10.0.15 (D-06) |
| Autenticación | ASP.NET Core Identity + JWT Bearer | `Microsoft.AspNetCore.Identity.EntityFrameworkCore` y `JwtBearer` 10.0.15 (D-02) |
| Cifrado de datos personales | AES-256 | `CifradorAes256` (D-03) |
| Reportes `.xlsx` | ClosedXML | 0.105.1 (D-46) |
| Reportes y factura PDF | QuestPDF | 2026.9.2 (D-04, D-46) |
| Pruebas | xUnit, `Microsoft.AspNetCore.Mvc.Testing`, EF Core InMemory | xUnit 2.9.3; `Microsoft.NET.Test.Sdk` 17.14.1 |
| Cobertura | coverlet | `coverlet.collector` 6.0.4 |

Las dependencias exactas están en `Back-end/OasisStay.Infraestructura/OasisStay.Infraestructura.csproj`,
`Back-end/OasisStay.Api/OasisStay.Api.csproj`, `Back-end/OasisStay.Pruebas/OasisStay.Pruebas.csproj` y `Front-end/Front-end.csproj`.

## 3. Arquitectura general

La solución (`Oasis_Stay.slnx`) separa el backend en capas y añade el front Razor, que consume la API por HTTP.

```
Front-end/ (Razor Pages, OasisStay.UI)
    │  HTTP + JWT (cliente tipado; Api:BaseUrl)
    ▼
Back-end/OasisStay.Api            Controladores, filtros de errores, autenticación JWT, NoShowServicio
    │
Back-end/OasisStay.Aplicacion     Casos de uso (GestionReservas, GestionEstadoHabitacion, ServicioFacturacion,
    │                             CheckoutFacade, AuditoriaHistorial, ServicioReportes) y contratos (interfaces)
    │
Back-end/OasisStay.Dominio        Entidades, reglas de negocio, State, Chain of Responsibility, Decorator, Strategy
    ▲
    │
Back-end/OasisStay.Infraestructura  EF Core (OasisStayDbContext, migraciones), repositorios, Identity, AES-256,
                                    generadores de PDF/xlsx, semillas demo, inyección de dependencias (ExtensionesDi.cs)

Back-end/OasisStay.Pruebas        Pruebas xUnit de dominio, aplicación, API (en memoria y SQL Server) y pantallas Razor
```

- Dependencias: Api → Aplicacion → Dominio; Infraestructura → Aplicacion (implementa sus contratos). El dominio no
  depende de ninguna otra capa.
- Modelo completo: [C4 (niveles 1 a 4)](docs/fuentes/c4-modelo.md) y [diagrama de componentes](docs/fuentes/diagrama-componentes.md).
  Los diagramas fuente están en [docs/fuentes/diagramas/](docs/fuentes/diagramas/) (`c4-nivel1..4`, `componentes`, `clases-e1`, `actividad-1..5`, `secuencia-*`).

## 4. Funcionalidades implementadas (US-01 a US-11)

Las 11 historias de usuario de Jira (proyecto SCRUM) están implementadas, cada una con sus tres escenarios
(33 escenarios). Fuente: [historias de usuario](docs/fuentes/jira/README.md) (`docs/fuentes/jira/US-01.md` a `US-11.md`).
Alcance: [D-41](docs/decisiones.md) (las 11 historias con sus 3 escenarios).

| Funcionalidad | Historia (RF) | Fase | Resumen | Decisiones |
|---|---|---|---|---|
| Base común | — | F0 | Dominio, EF Core, migración inicial, Identity con roles, JWT, AES-256, login por rol | D-02, D-03, D-06, D-11, D-34 |
| Consulta de disponibilidad | US-01 (RF-01) | F1 / F2b | Búsqueda por fechas, tipo y capacidad; alerta con alternativas | D-19, D-35, D-42 |
| Cambiar estado de habitación | US-02 (RF-02) | F1 | Ciclo de 5 estados con State; motivo de mantenimiento; restricción por ocupación | D-07, D-16, D-18, D-24, D-28 |
| Registro de huésped y reserva | US-03 (RF-03) | F2 / F2b | Alta o autocompletado por documento, validación de campos, cadena de validación | D-08, D-09, D-20, D-40 |
| Asignación y reasignación | US-04 (RF-04) | F2 / F2b | Asignación, conflicto por concurrencia, capacidad, reasignación con aviso de Upgrade, check-in | D-21, D-31 |
| Cotización | US-05 (RF-05) | F3 | Decorator: estadía, minibar, late check-out e IVA | D-12, D-22, D-43 |
| Factura y check-out | US-06 (RF-06) | F3 | Facade `ProcesarSalida`; factura única con folio y PDF; bloqueo por saldo | D-10, D-43 |
| Historial de reservas | US-07 (RF-07) | F4a | Bitácora de solo inserción, línea de tiempo, filtros por usuario y fecha | D-23, D-36, D-37, D-38, D-47 |
| Penalización y No-Show | US-08 (RF-08) | F4 | Penalidad por plazo de 24 h, exoneración solo del Administrador, No-Show | D-01, D-44 |
| Mapa de habitaciones | US-09 (RF-09) | F5 | Mapa por pisos con colores de estado y filtros; panel con datos reales | D-24, D-28, D-45 |
| Pagos múltiples | US-10 (RF-10) | F3 | Strategy: efectivo con vuelto, tarjeta, transferencia; pagos mixtos | D-13, D-27, D-33 |
| Reportes financieros | US-11 (RF-11) | F6 | Exportación `.xlsx` y `.pdf`, KPIs y auditoría de exportaciones | D-46 |
| Indicadores de facturación | Panel de Facturación e Historial | F7 | Cifras reales en Facturación e Historial | D-48 |

Hitos de la semilla por escenario: `--semilla-demo --escenario USnn` (sección 9).

**Exclusiones documentadas:** la tarifa Day Use (estancias de 0 noches, US-05 RN1) queda fuera de alcance por
[D-22](docs/decisiones.md). La épica de US-02 en Jira ([D-25](docs/decisiones.md)) está pendiente de corrección en Jira.

## 5. Requisitos de instalación

- Windows con PowerShell o Git Bash.
- **.NET SDK 10** (proyectos `net10.0`).
- **SQL Server 2025 Developer** local, instancia predeterminada `MSSQLSERVER` (`localhost`), autenticación de Windows ([D-06](docs/decisiones.md)).
- Git.
- Un navegador (Chrome o Edge) para el front. Los certificados HTTPS de desarrollo deben estar confiados
  (`dotnet dev-certs https --trust`); si no lo están, usar HTTP (ver sección 7).
- Opcional: `dotnet-ef` para crear migraciones (`dotnet-tools.json` en la raíz).

## 6. Configuración del entorno y dependencias

**Restaurar y compilar:**

```
dotnet restore Oasis_Stay.slnx
dotnet build Oasis_Stay.slnx
```

**Cadena de conexión** (`Back-end/OasisStay.Api/appsettings.json`, sección `ConnectionStrings:OasisStay`): apunta a
`localhost` con `Trusted_Connection=True`, base `OasisStay` (D-06). Se crea o migra automáticamente al arrancar la API.

**Secretos (no versionados).** La API usa *user secrets* (`UserSecretsId` en `OasisStay.Api.csproj`). Los valores reales
no están en el repositorio; se configuran así (sustituya los valores por los suyos):

```
dotnet user-secrets set "Jwt:Clave" "<clave-jwt-larga>" --project Back-end/OasisStay.Api
dotnet user-secrets set "Seguridad:ClaveAes" "<clave-aes-256-en-base64>" --project Back-end/OasisStay.Api
dotnet user-secrets set "Semilla:Usuarios:Administrador:Contrasena" "<clave>" --project Back-end/OasisStay.Api
dotnet user-secrets set "Semilla:Usuarios:Recepcionista:Contrasena" "<clave>" --project Back-end/OasisStay.Api
dotnet user-secrets set "Semilla:Usuarios:AmaDeLlaves:Contrasena" "<clave>" --project Back-end/OasisStay.Api
```

- `Jwt:Clave`: obligatoria; si falta, la API no arranca (`Back-end/OasisStay.Api/Program.cs`). Emisor `OasisStay.Api`,
  audiencia `OasisStay.UI`, expiración 60 minutos (`Jwt` en `appsettings.json`) [D-02].
- `Seguridad:ClaveAes`: clave AES-256 en base64, obligatoria; si falta, la infraestructura no arranca
  (`Back-end/OasisStay.Infraestructura/ExtensionesDi.cs`) [D-03]. Se puede definir también como variable de entorno.
- `Semilla:Usuarios:<Rol>:Contrasena`: contraseñas de las cuentas semilla de cada rol (Administrador, Recepcionista,
  AmaDeLlaves). Los correos son `admin@`, `recepcion@` y `amadellaves@oasisstay.co` (`appsettings.json`).

**Otros parámetros** (`appsettings.json`): sección `Politicas` (`PlazoCancelacionHoras` 24, `HoraMaximaCheckout` 12:00,
`HoraCheckIn` 15:00; D-01, D-12), `Cors:Origenes` (por defecto `https://localhost:5001`), `Api:BaseUrl` del front
(por defecto `https://localhost:7001/`). El job de No-Show se puede desactivar con `NoShow:AutoActivo` (D-44).

**Puertos** ([D-11](docs/decisiones.md)): front `https://localhost:5001`, API `https://localhost:7001`
(`Properties/launchSettings.json` de cada proyecto). Chrome bloquea los puertos 5060 y 5061; no los use.

## 7. Cómo ejecutar el backend y el frontend

Ejecute la API primero (el front la consume).

**Backend (API):**

```
dotnet run --project Back-end/OasisStay.Api
```

Al arrancar aplica las migraciones y crea las cuentas semilla (`Back-end/OasisStay.Api/Program.cs`). Los endpoints
se prueban con el front o con `Back-end/OasisStay.Pruebas`.

**Frontend (Razor Pages):**

```
dotnet run --project Front-end --launch-profile https
```

Abra `https://localhost:5001`. La pantalla de inicio es el login; cada rol entra a su pantalla
(`Front-end/Program.cs`: el Ama de llaves va a `Habitaciones`, los demás a `PanelControl`).

**Si el certificado HTTPS no es de confianza:** ejecute la API y el front con HTTP (`--urls http://localhost:<puerto>`),
ajuste `Api:BaseUrl` del front (variable `Api__BaseUrl`) al puerto HTTP de la API y añada el origen del front a
`Cors:Origenes` (variable `Cors__Origenes__0`), porque la política CORS por defecto solo admite `https://localhost:5001`.

**Demo de inicio a fin:** consulte la sección 9 para cargar los datos de una historia.

## 8. Pruebas

```
dotnet test Oasis_Stay.slnx --nologo -v q
```

Resultado de la última ejecución (2026-11-18, sobre `main` en 7f51925): **244 pruebas superadas, 0 fallidas**.
Para cobertura: `dotnet test Oasis_Stay.slnx --collect:"XPlat Code Coverage"`.

Las pruebas están en `Back-end/OasisStay.Pruebas/` por fase (`F0` a `F7`, `F2b` dentro de `F2`, `F4a`). Cada escenario
de Jira es una prueba `USnn_ESCn_...` (33 en total); los casos de prueba del contrato (`CP_H01..08`, `CP_R01..08`) tienen
su ID en el nombre. La trazabilidad completa de estas pruebas está en la [matriz de trazabilidad](docs/sustentacion/matriz-trazabilidad.md).

## 9. Semilla demo (datos de cada historia de usuario)

Solo en el entorno `Development`, idempotente ([D-39](docs/decisiones.md), [D-41](docs/decisiones.md)). Carga los datos
exactos de los escenarios de Jira. Con `--escenario` reinicia solo los datos de esa historia.

```
dotnet run --project Back-end/OasisStay.Api -- --semilla-demo
dotnet run --project Back-end/OasisStay.Api -- --semilla-demo --escenario US03
```

| Escenario | Fase que la carga | Datos |
|---|---|---|
| `US01`, `US02` | F1 | Habitaciones y tipos (D-29) |
| `US03`, `US04` | F2 | Reservas, huéspedes y reasignación (habitación 304, 105 a 107) |
| `US05`, `US06`, `US10` | F3 | Cuentas, consumos, pagos y facturas (`RS` 1025, habitación 208) |
| `US07` | F4a | Historial de la reserva #RS-4011 |
| `US08` | F4 | Reservas para cancelación gratuita, tardía y exoneración |
| `US09` | F5 | Inventario de 30 habitaciones por pisos |
| `US11` | F6 | Movimientos del 01 al 15 de octubre de 2026 y rango sin movimientos |

Como el reloj del sistema es el de Colombia (`IReloj`, UTC-5), las reservas de la semilla usan fechas relativas; la demo
depende de la fecha en que se ejecuta.

## 10. Validaciones, excepciones y flujos alternativos

### 10.1 Validaciones y excepciones (contrato)

Las excepciones de dominio se traducen a ProblemDetails sin trazas en `Back-end/OasisStay.Api/Filtros/`:
`ValidacionDominioException` → 400, `NoEncontradoException` → 404, `ConflictoDominioException` → 409
(`ErroresDominioReservasFiltro.cs`); `AccesoProhibidoException` → 403 (`ErroresReportesFiltro.cs`). Sin token → 401; rol
no permitido → 403 (`[Authorize(Roles)]`).

| Entrada inválida | HTTP | Mensaje | Clase que lo valida |
|---|---|---|---|
| Fecha de salida igual o anterior a la de llegada | 400 | "La fecha de salida debe ser posterior a la fecha de llegada" | `ValidadorFechas` (`Dominio/Reservas.cs`) |
| Huéspedes por encima de la capacidad (RN-05) | 400 | "Excede la capacidad de la habitación (RN-05)" | `ValidadorCapacidad` (`Dominio/Reservas.cs`) |
| Habitación no disponible para reservar | 400 | "La habitación no está disponible para reservar" | `ValidadorEstadoHabitacion` (`Dominio/Reservas.cs`) |
| Fechas solapadas con otra reserva (RN-01) | 409 | "Las fechas se solapan con otra reserva (RN-01)" | `ValidadorSolapamiento` (`Dominio/Reservas.cs`); `GestionReservas.Registro.cs` |
| Documento de huésped vacío | 400 | `detail`: "Hay campos con formato inválido."; `errors.documento`: "El número de documento es obligatorio" | `Cliente` (`Dominio/Entidades.cs`) |
| Tipo de documento fuera de CC, CE o Pasaporte | 400 | `detail`: "Hay campos con formato inválido."; `errors.tipoDocumento`: "El tipo de documento es obligatorio (CC, CE o Pasaporte)" | `Cliente` (`Dominio/Entidades.cs`) |
| Nombre vacío | 400 | `detail`: "Hay campos con formato inválido."; `errors.nombre`: "Los nombres completos son obligatorios" | `Cliente` (`Dominio/Entidades.cs`) |
| Teléfono vacío o con caracteres no permitidos | 400 | `detail`: "Hay campos con formato inválido."; `errors.telefono`: "El teléfono es obligatorio" / "El teléfono solo admite dígitos, espacios, + y -" | `Cliente` y `ReglasHuesped` (`Dominio/Entidades.cs`, `Dominio/Reservas.cs`) |
| Correo sin formato válido (sin `@`) | 400 | `detail`: "Hay campos con formato inválido."; `errors.correo`: "El correo electrónico no tiene un formato válido" | `Cliente` y `ReglasHuesped` (`Dominio/Entidades.cs`, `Dominio/Reservas.cs`) |
| Motivo de mantenimiento con menos de 10 caracteres | 400 | "Debe ingresar el motivo del mantenimiento (mínimo 10 caracteres)" | `GestionEstadoHabitacion` (`Aplicacion/GestionEstadoHabitacion.cs`) → `DesenlaceCambioEstado.DatosInvalidos` → `HabitacionesController.CambiarEstado`; prueba `US02_ESC2_MantenimientoSinMotivo_Bloquea` |
| Habitación ocupada que se fuerza a disponible | 409 | "La habitación está ocupada: requiere check-out previo" | `EstadoOcupada.MarcarDisponible` (`Dominio/EstadosHabitacion.cs`) → `DesenlaceCambioEstado.Rechazado` → `HabitacionesController.CambiarEstado` (rama por defecto); prueba `US02_ESC3_Hab204Ocupada_ForzarDisponible_ImpideYAvisaCheckOut` |
| Cambiar de En Limpieza a Disponible sin ser ama de llaves | 403 | "Solo el ama de llaves puede marcar la habitación como disponible tras la limpieza" | `GestionEstadoHabitacion` (`Aplicacion/GestionEstadoHabitacion.cs`) → `DesenlaceCambioEstado.SinPermiso` → `HabitacionesController.CambiarEstado`; prueba `US02_Recepcionista_NoPuedeCompletarLimpieza` (verifica el 403, no el texto) |
| Check-in sin verificar el documento | 409 | "Debe verificar el documento del huésped" | `Reserva.HacerCheckIn` (`Dominio/Entidades.cs`) → `GestionReservas.HacerCheckIn` lanza `ConflictoDominioException` (`Aplicacion/GestionReservas.Registro.cs`); prueba `Api_CheckIn_SinDocumento_409_ConDocumento_200` |
| Check-in antes de la fecha de llegada | 409 | "El check-in no puede hacerse antes de la fecha de llegada" | `Reserva.HacerCheckIn` (`Dominio/Entidades.cs`) → `GestionReservas.HacerCheckIn` lanza `ConflictoDominioException`; **sin prueba HTTP** (solo prueba unitaria `RegistroReservasPruebas`, que verifica el mensaje) |
| Reasignación con Upgrade sin confirmar | 409 | "La habitación nueva es de categoría superior (Upgrade). Confirme para continuar." | `GestionReservas.Reasignar` (`Aplicacion/GestionReservas.Registro.cs`) |
| Cancelar una reserva en curso | 409 | "Una reserva en curso no puede cancelarse" | `Reserva.CancelarReserva` (`Dominio/Entidades.cs`) |
| Cambiar una reserva ya cerrada | 409 | "La reserva ya está cerrada y no puede cambiar de estado" | `Reserva.CancelarReserva` (`Dominio/Entidades.cs`) → `ConflictoDominioException`; **sin prueba HTTP** (solo prueba unitaria `CancelacionPruebas`, que verifica el mensaje) |
| Exonerar penalidad sin justificación | 400 | "Debe ingresar la justificación de la exoneración" | `GestionReservas.Cancelacion.cs` → `ValidacionDominioException`; prueba `US08_Exonerar_SinMotivo_400` |
| Exonerar penalidad sin ser Administrador | 403 | "Solo el Administrador puede exonerar la penalidad" | `ReservasController.Cancelar` (`Api/Controllers/ReservasController.cs`); prueba `US08_Exonerar_Recepcionista_403` |
| Monto de pago menor o igual a cero | 400 | "El monto debe ser mayor que cero" | `ServicioFacturacion` / `GestionPagos` (`Aplicacion/ServiciosFacturacion.cs`) |
| Monto de pago mayor al saldo | 400 | "El monto excede el saldo pendiente de {saldo}" | `GestionPagos` (`Aplicacion/ServiciosFacturacion.cs`) |
| Método de pago no autorizado (RN-06) | 400 | "El método de pago no es válido" | `GestionPagos` (`Aplicacion/ServiciosFacturacion.cs`) |
| Efectivo insuficiente | 400 | "Monto insuficiente" | `PagoEfectivo` (`Dominio/Pagos.cs`) |
| Tarjeta sin tipo o autorización | 400 | "Falta tipo de tarjeta o autorización" | `PagoTarjeta` (`Dominio/Pagos.cs`) |
| Transferencia sin referencia bancaria | 400 | "Falta la referencia bancaria" | `PagoTransferencia` (`Dominio/Pagos.cs`) |
| Check-out con saldo pendiente | 409 | "La reserva tiene saldo pendiente de {monto}: registre el pago antes del check-out" | `ServicioFacturacion` (`Aplicacion/ServiciosFacturacion.cs`) |
| Check-out de una reserva que no está en curso | 409 | "La reserva no está en curso" | `ServicioFacturacion` (`Aplicacion/ServiciosFacturacion.cs`, línea 227, `CerrarCuenta`) → `ConflictoDominioException`; verificada por código; prueba unitaria `US06_CerrarCuenta_ReservaNoEnCurso_Conflicto409` (`Pruebas/F3/FacturacionPruebas.cs`; el mismo mensaje en consumos tiene prueba HTTP en `Api_FlujoCompleto_Consumo_Pago_Totales_Checkout_Pdf`, que verifica el 409, no el texto) |
| Segundo check-out de la misma reserva | 409 | "La reserva ya tiene una factura emitida" | `ServicioFacturacion` (`Aplicacion/ServiciosFacturacion.cs`); prueba `Api_FlujoCompleto_Consumo_Pago_Totales_Checkout_Pdf` (verifica el 409, no el texto) |
| Consumo sin servicio, cantidad o monto | 400 | "Indique el servicio, la cantidad y el monto del consumo" | `ServicioFacturacion` (`Aplicacion/ServiciosFacturacion.cs`) → `ValidacionDominioException`; **sin prueba HTTP localizada**; prueba unitaria `Consumos_SoloEnReservaEnCurso_YValidados` (`FacturacionPruebas`, cantidad 0; verifica el 400, no el texto) |
| Salida tardía registrada dos veces | 409 | "La salida tardía ya está registrada" | `ServicioFacturacion` (`Aplicacion/ServiciosFacturacion.cs`) → `ConflictoDominioException`; **sin prueba HTTP localizada**; prueba unitaria `Consumos_SoloEnReservaEnCurso_YValidados` (`FacturacionPruebas`, segunda salida tardía; verifica el 409, no el texto) |
| Reporte por un usuario que no es Administrador | 403 | "Solo el Administrador puede generar y descargar reportes" | `ServicioReportes` (`Aplicacion/Reportes.cs`) |
| Reporte con rango mayor a 365 días | 400 | "El rango máximo por reporte es de 365 días" | `ServicioReportes` (`Aplicacion/Reportes.cs`); prueba `US11_ESC3_Api_Rango400Dias_400` |
| Reporte con fecha inicial posterior a la final | 400 | Reportes: "La fecha inicial no puede ser posterior a la fecha final" (sin punto); historial: "La fecha inicial no puede ser posterior a la fecha final." (con punto) | `ServicioReportes` (`Aplicacion/Reportes.cs`) y `AuditoriaHistorial` (`Aplicacion/AuditoriaHistorial.cs`); el 400 del historial lo prueba `HistorialPruebas` (línea 170); el 400 de reportes lo prueba la unitaria `US11_ESC3_DesdeMayorQueHasta_Rechaza` (`ReportesPruebas`, verifica el texto sin punto) |
| Formato distinto de xlsx o pdf | 400 | "El formato del reporte no es válido: use xlsx o pdf" | `AuditoriaHistorial` / `ServicioReportes` |
| Reporte sin movimientos en el periodo | 409 | "No hay movimientos en el periodo seleccionado: no se genera un archivo en blanco" | `ServicioReportes` (`Aplicacion/Reportes.cs`) |
| Habitación o reserva inexistente | 404 | "La habitación no existe." / "La reserva no existe." | `NoEncontradoException` (traducida por `ErroresDominioReservasFiltro`); para cambio de estado, `DesenlaceCambioEstado.NoExiste` en `HabitacionesController`; pruebas `US02_TransicionNoPermitida_409_YNoExiste_404`, `Api_Cancelar_Inexistente_404_YSinToken_401_YAma_403` |
| Número de habitación repetido | 409 | "Ya existe una habitación con ese número." | `GestionEstadoHabitacion` (`Aplicacion/GestionEstadoHabitacion.cs`) → `DesenlaceAlta.NumeroRepetido` → `HabitacionesController.Crear`; prueba `D16_CrearHabitacion_Valida_Guarda` (verifica el 409 al repetir) |

Las filas sin prueba HTTP se respaldan con el código fuente y con pruebas unitarias; solo se cita como prueba HTTP la que existe (p. ej. `Api_FlujoCompleto_Consumo_Pago_Totales_Checkout_Pdf` para el 400 de la estrategia).

Los textos exactos de cada regla están en `Dominio/Reservas.cs`, `Dominio/Facturacion.cs`, `Dominio/EstadosHabitacion.cs`
y `Aplicacion/Reportes.cs`, y su origen en el [contrato](docs/contrato/CONTRATO.md).

### 10.2 Flujos alternativos demostrables por historia

Cada escenario tiene su prueba xUnit (`Back-end/OasisStay.Pruebas`) y, cuando existe, su captura en
`docs/evidencias/`. Los nombres de prueba son los de la [matriz de trazabilidad](docs/sustentacion/matriz-trazabilidad.md).

| Historia | Escenario 1 (flujo principal) | Escenario 2 (alternativo) | Escenario 3 (alternativo o restricción) | Capturas |
|---|---|---|---|---|
| US-01 Disponibilidad | `US01_ESC1_ConsultaSuite_10al12Nov2026...` | `US01_ESC2_EstandarAgotada_AlertaYSugerencias` | `US01_ESC3_FechasInvalidas_15y14Nov2026_Bloquea` | `docs/evidencias/F2b/US01-ESC1-suites-libres.png`, `US01-ESC2-alerta-con-alternativas.png`, `US01-ESC3-fechas-invalidas.png` |
| US-02 Estado de habitación | `US02_ESC1_Hab301EnLimpiezaCompleta_PasaADisponibleVerde` | `US02_ESC2_MantenimientoSinMotivo_Bloquea` | `US02_ESC3_Hab204Ocupada_ForzarDisponible_ImpideYAvisaCheckOut` | Pendiente de capturas (ver sección 16) |
| US-03 Huésped | `US03_ESC1_HuespedNuevo_Guarda` | `US03_ESC2_Autocompletado_CC1020304050` | `US03_ESC3_CorreoSinArrobaOTelefono310ABC_Invalido` | `docs/evidencias/F2b/US03-ESC1-US04-ESC1-reserva-creada-304.png`, `US03-ESC2-autocompletado.png`, `US03-ESC3-campos-invalidos.png` |
| US-04 Asignación | `US04_ESC1_Hab304Suite_Asigna_PasaAReservada` | `US04_ESC2_DosAsignacionesSimultaneas_SoloUnaGana` y `US04_ESC2_5HuespedesEnEstandarCapMax2_RechazaPorCapacidad` | `US04_ESC3_Reasigna105a107_105Disponible_107Reservada` y `US04_ESC3_Reasignar_Pantalla_Upgrade_PideConfirmacionConTextoExacto` | `docs/evidencias/F2b/US04-ESC2-capacidad-excedida.png`, `US04-ESC2-conflicto-toast.png`, `US04-ESC3-1..4` |
| US-05 Cotización | `US05_ESC1_3Noches150000_Subtotal450000_Iva85500_Total535500` | `US05_ESC2_ConsumosYLateCheckout_Total321300` | `US05_ESC3_Ampl` (recálculo al ampliar fechas) | `docs/evidencias/F3/06-US05-ESC1-3noches-535500.png`, `07-US05-ESC2-consumos-late-321300.png`, `11-US05-ESC3-modal-3a5-noches.png` |
| US-06 Check-out y factura | `US06_ESC1_Rs1025_AnaGomez_Hab208_Saldo0_FacturaFolioPdfYEnLimpieza` | `US06_ESC2_Rs1026_Saldo80000_RechazaYRedirigePagos` | `US06_ESC3_Hab208_AmaDeLlavesLaVeEnLimpiezaDeInmediato` | `docs/evidencias/F3/05-US06-ESC1-factura-generada.png`, `02-US06-ESC2-saldo-pendiente.png`, `12-US06-ESC3-ama-ve-208-en-limpieza.png` |
| US-07 Historial | `US07_ESC1_HistorialReserva_RS4011_LineaDeTiempo` | `US07_ESC2_FiltroUsuarioYFecha_jrodriguez_20Oct2026` | `US07_ESC3_SinEditarNiBorrar_Administrador` | `docs/evidencias/F4a/esc1-linea-tiempo-rs4011.png`, `esc2-filtro-jrodriguez-20oct.png`, `alt-sin-resultados.png`, `alt-recepcionista-denegado.png` |
| US-08 Penalidad | `US08_ESC1_Api_CancelacionGratuita_PenalidadCero` | `US08_ESC2_Api_CancelacionTardia_Penalidad180000_HabitacionDisponible` | `US08_ESC3_Api_Exoneracion_Administrador_CobroCeroYMotivoEnHistorial` | `docs/evidencias/F4/esc1-cancelada-gratis.png`, `esc2-cancelada-180000.png`, `esc3-exonerada.png`, `no-show-manual.png`, `rechazos-api.txt` |
| US-09 Mapa | `US09_ESC1_Visualizacion_AgrupadaPorPisos_ConColoresDeEstado` | `US09_ESC2_Hab302Ocupada_DevuelveHuespedYSalidaPrevista` | `US09_ESC3_FiltroPiso2Disponible_SoloLibresDelPiso2` | `docs/evidencias/F5/esc1-mapa-por-pisos.png`, `esc2-panel-302.png`, `esc3-filtro-piso2-disponible.png`, `alt-cambiar-estado-error.png`, `alt-amadellaves-sin-acceso.png` |
| US-10 Pagos | `US10_ESC1_Deuda250000_TarjetaCredito_SaldoCero_ConComprobante` | `US10_ESC2_Mixto120000_50000TC_Aprob4892_Mas70000Transf_TRF9921_Cubre100` | `US10_ESC3_Cobro150000_Solo100000_OSinReferencia_NoCierra` | `docs/evidencias/F3/09-US10-ESC1-tarjeta-250000-saldo-cero.png`, `04-US10-ESC2-pago-mixto-saldo-cero.png`, `03-US10-ESC3-monto-insuficiente.png`, `13-US10-efectivo-120000-vuelto-30000.png` |
| US-11 Reportes | `US11_ESC1_Api_ExportaExcel_01al15Oct2026` | `US11_ESC2_Api_ExportaPdf` | `US11_ESC3_Api_SinMovimientos_Ano2027_ResumenEnCeroYExportar409` y `US11_ESC3_Api_Rango400Dias_400` | `docs/evidencias/F6/ui-esc1-modal-kpis.png`, `ui-esc1-esc2-exito-descarga.png`, `esc2-pdf-abierto.png`, `ui-esc3-2027-sin-movimientos.png`, `ui-esc3-rango-400-dias.png` |

Flujos alternativos de roles (demostrables): el Ama de llaves no accede a reservas ni a facturación
(`US01_Disponibilidad_AmaDeLlaves_403_YSinToken_401`, `US09_Distribucion_Recepcionista_Ok_AmaDeLlaves_403`); el
Recepcionista no accede al historial (`docs/evidencias/F4a/alt-recepcionista-denegado.png`) ni a los reportes
(`docs/evidencias/F6/ui-recepcionista-facturacion-sin-boton.png`).

## 11. Patrones GoF y principios SOLID

### 11.1 Patrones GoF

Las clases están verificadas con búsqueda en el código. El pseudocódigo de cada patrón es el de la
[plantilla GoF](docs/fuentes/plantilla-patrones-gof.md) (`docs/fuentes/img/gof-image1..5.png`).

| Patrón GoF | Archivo : clase | Problema que resuelve | Fuente |
|---|---|---|---|
| State | `Back-end/OasisStay.Dominio/Estados.cs` : `IEstadoHabitacion`, `ResultadoOperacion`; `Back-end/OasisStay.Dominio/EstadosHabitacion.cs` : `EstadoDisponible`, `EstadoReservada`, `EstadoOcupada`, `EstadoEnLimpieza`, `EstadoMantenimiento` | Transiciones válidas de la habitación sin `switch` repetido; una transición inválida devuelve `Rechazada(motivo)` | [C4-N4 §5](docs/fuentes/c4-modelo.md), [CONTRATO §4](docs/contrato/CONTRATO.md), `gof-image5.png`, RF-02, US-02 |
| Chain of Responsibility | `Back-end/OasisStay.Dominio/Reservas.cs` : `ValidadorReserva` (abstracta), `ValidadorFechas`, `ValidadorCapacidad`, `ValidadorEstadoHabitacion`, `ValidadorSolapamiento`; armado en `Back-end/OasisStay.Aplicacion/GestionReservas.Registro.cs` (líneas 37-40) | Reglas de reserva (RN-01, RN-05) en orden y cortando en el primer error | `gof-image3.png`, [D-09](docs/decisiones.md), RF-03/RF-04 |
| Decorator | `Back-end/OasisStay.Dominio/Cuenta.cs` : `ICuenta`, `EstadiaBase`, `CuentaDecorator` (abstracta), `CargoMinibar`, `CargoLateCheckout`, `ImpuestoIVA`; `ArmadorCuenta` | Cálculo de la cuenta en capas (base, minibar, late check-out, IVA como capa externa) | `gof-image4.png`, RN-03, RN-04, US-05 |
| Strategy | `Back-end/OasisStay.Dominio/Pagos.cs` : `IEstrategiaPago`, `PagoEfectivo`, `PagoTarjeta`, `PagoTransferencia`, `ProcesadorPagos`; registro en `Back-end/OasisStay.Infraestructura/ExtensionesDi.cs` (líneas 59-62) | Un algoritmo de cobro por método (RN-06) sin condicionales por método | `gof-image2.png`, [D-13](docs/decisiones.md), [D-33](docs/decisiones.md), US-10 |
| Facade | `Back-end/OasisStay.Aplicacion/Facturacion.cs` : `ICheckoutFacade`; `Back-end/OasisStay.Aplicacion/ServiciosFacturacion.cs` : `CheckoutFacade`; uso en `Back-end/OasisStay.Api/Controllers/FacturacionController.cs` (`api/reservas/{id}/checkout`) | Check-out en una transacción: cerrar cuenta, habitación a En Limpieza, archivar historial | `gof-image1.png`, [D-10](docs/decisiones.md), US-06 |

### 11.2 Principios SOLID

La plantilla institucional de SOLID de Entregable 2 está **pendiente** ([D-17](docs/decisiones.md)): el enlace de SharePoint no
tiene copia local. La sección 6 de [`docs/fuentes/entregable-2.md`](docs/fuentes/entregable-2.md) es el análisis de SOLID de Entregable 2 y
describe el estado previo a la implementación, así que sus niveles de cumplimiento ya no reflejan el código. La tabla
siguiente cita solo lo que se ve hoy en el código.

| Principio | Archivo : clase | Evidencia en el código |
|---|---|---|
| S (responsabilidad única) | `Back-end/OasisStay.Dominio/Reservas.cs` (cada `ValidadorReserva` aplica una sola regla); `GestionReservas` orquesta | Cada validador cambia solo si cambia su regla |
| O (abierto/cerrado) | `Back-end/OasisStay.Dominio/Pagos.cs` : `ProcesadorPagos(IEnumerable<IEstrategiaPago>)` | Un método de pago nuevo es una clase más registrada en `ExtensionesDi.cs`, sin tocar `ProcesadorPagos` |
| L (sustitución de Liskov) | `Back-end/OasisStay.Dominio/EstadosHabitacion.cs` : `EstadoDisponible`... `EstadoMantenimiento` | Los cinco estados cumplen el contrato `IEstadoHabitacion` y devuelven `ResultadoOperacion` |
| I (segregación de interfaces) | `Back-end/OasisStay.Aplicacion/Facturacion.cs` : `ICheckoutFacade` (una operación); `Back-end/OasisStay.Dominio/Pagos.cs` : `IEstrategiaPago` (una operación) | Interfaces con una operación |
| D (inversión de dependencias) | `Back-end/OasisStay.Api/Controllers/FacturacionController.cs` (recibe `ICheckoutFacade`); `Back-end/OasisStay.Infraestructura/ExtensionesDi.cs` | El controlador depende de la abstracción; la infraestructura registra las implementaciones |

## 12. Coherencia código, UML y C4

- **C4 nivel 4 y tabla de elementos**: [`docs/fuentes/c4-modelo.md`](docs/fuentes/c4-modelo.md) §5 relaciona cada patrón con su clase.
- **Diagramas de clases y de componentes**: [`docs/fuentes/diagramas/clases-e1.md`](docs/fuentes/diagramas/clases-e1.md),
  [`docs/fuentes/diagramas/componentes.md`](docs/fuentes/diagramas/componentes.md) y `docs/fuentes/img/diagrama-clases.svg`.
- **Diferencias de nombre conocidas**: el C4 usa `ValidadorSolap` y `ValidadorEstadoHab`; en el código son
  `ValidadorSolapamiento` y `ValidadorEstadoHabitacion`, nombres del contrato. El C4 también nombra `Facturacion.cshtml.cs`
  como consumidor de `ICheckoutFacade`, y la decisión [D-10](docs/decisiones.md) lo cambia por el controlador de la API.
- **Coherencia completa por clase** (cada clase del C4-N4 con su archivo real y la decisión `D-nn` de cada diferencia):
  **pendiente**; no existe todavía `docs/entrega3/coherencia.md`.

## 13. Despliegue

- **Nivel de especificación (nube)**: diagrama de producción con balanceador, PaaS y puerto 443, en
  [`docs/fuentes/despliegue.md`](docs/fuentes/despliegue.md) y la guía de la profesora en
  [`docs/fuentes/guia-despliegue-profesora.md`](docs/fuentes/guia-despliegue-profesora.md).
- **Nivel de instancia local**: el entorno real del MVP (SQL Server local, puertos 5001 y 7001), descrito en las secciones 5 a 7 y
  en [`docs/entrega3/despliegue-complemento.md`](docs/entrega3/despliegue-complemento.md).
- **Estado**: **PENDIENTE de despliegue en la nube** ([D-14](docs/decisiones.md)). El proyecto no se ha desplegado a la nube;
  el MVP se evalúa en la instancia local.

## 14. Respaldos de la base de datos

Las copias de seguridad automáticas y la retención de 5 años de la bitácora ([D-38](docs/decisiones.md), JIRA US-07) **no son
código de la aplicación**. Son una tarea de administración de SQL Server que debe configurarse en el equipo (por ejemplo, un
plan de mantenimiento con `BACKUP DATABASE OasisStay`). La aplicación no borra ni purga registros de la bitácora
(`HistorialReserva` es de solo inserción). La configuración del plan está **pendiente** de ejecutarse en el equipo.

## 15. Documentación técnica y diagramas

| Documento | Contenido |
|---|---|
| [`docs/fuentes/README.md`](docs/fuentes/README.md) | Jerarquía de las fuentes |
| [`docs/fuentes/guia-entrega-3.md`](docs/fuentes/guia-entrega-3.md) | Guía de la Entrega 3 |
| [`docs/fuentes/entregable-1-requisitos.md`](docs/fuentes/entregable-1-requisitos.md) | Requisitos (RU, RF, RNF, RN) |
| [`docs/fuentes/entregable-2.md`](docs/fuentes/entregable-2.md) | Entregable 2: alcance, patrones, prototipo, SOLID |
| [`docs/fuentes/jira/`](docs/fuentes/jira/README.md) | Historias de usuario US-01 a US-11 con criterios Gherkin |
| [`docs/contrato/CONTRATO.md`](docs/contrato/CONTRATO.md) | Lista cerrada de reglas, estados, nombres, firmas y mensajes |
| [`docs/decisiones.md`](docs/decisiones.md) | Registro de decisiones D-01 a D-48 |
| [`docs/enlaces.md`](docs/enlaces.md) | Inventario de enlaces de diagramas |
| [`docs/fuentes/c4-modelo.md`](docs/fuentes/c4-modelo.md) | Modelo C4 niveles 1 a 4 |
| [`docs/fuentes/diagrama-componentes.md`](docs/fuentes/diagrama-componentes.md) | Diagrama de componentes |
| [`docs/fuentes/diagramas/`](docs/fuentes/diagramas/) | Diagramas fuente: `c4-nivel1..4`, `componentes`, `clases-e1`, `actividad-1..5` (`.drawio` y `.md`) |
| [`docs/fuentes/diagramas-estado.md`](docs/fuentes/diagramas-estado.md) | Diagramas de estado |
| [`docs/fuentes/diagramas-secuencia.md`](docs/fuentes/diagramas-secuencia.md), [`diagramas-secuencia-nuevos.md`](docs/fuentes/diagramas-secuencia-nuevos.md) | Diagramas de secuencia |
| [`docs/fuentes/plantilla-patrones-gof.md`](docs/fuentes/plantilla-patrones-gof.md) | Plantilla de patrones GoF |
| [`docs/fuentes/despliegue.md`](docs/fuentes/despliegue.md) | Diagrama de despliegue |
| [`docs/sustentacion/matriz-trazabilidad.md`](docs/sustentacion/matriz-trazabilidad.md) | Matriz de trazabilidad por historia (RU, RF, US, escenarios, pruebas, commits) |
| [`specs/`](specs/) | Fichas de cada funcionalidad (F0 a F7) |

## 16. Evidencias del MVP

Capturas tomadas sobre la aplicación en marcha y salidas de consola, en `docs/evidencias/Fn/`:

| Fase | Carpeta | Contenido representativo |
|---|---|---|
| F0 | [`docs/evidencias/F0/`](docs/evidencias/F0/) | Acceso denegado por rol (`alt3-acceso-denegado.png`) |
| F2 | [`docs/evidencias/F2/`](docs/evidencias/F2/) | Disponibilidad, fechas inválidas, check-in con y sin documento, reasignación por API (`08-reasignacion-api.txt`) |
| F2b | [`docs/evidencias/F2b/`](docs/evidencias/F2b/) | US-01, US-03 y US-04 en pantalla (alternativas, autocompletado, reasignación con Upgrade) |
| F3 | [`docs/evidencias/F3/`](docs/evidencias/F3/) | Cotización, pagos (tarjeta, mixto, efectivo con vuelto), factura y saldo pendiente |
| F4 | [`docs/evidencias/F4/`](docs/evidencias/F4/) | Cancelación gratuita, con penalidad y exonerada; No-Show; rechazos por API (`rechazos-api.txt`) |
| F4a | [`docs/evidencias/F4a/`](docs/evidencias/F4a/) | Línea de tiempo RS-4011, filtro por usuario y fecha, sin resultados, acceso denegado |
| F5 | [`docs/evidencias/F5/`](docs/evidencias/F5/) | Mapa por pisos, panel 302, filtro piso 2, alternativas de estado y acceso |
| F6 | [`docs/evidencias/F6/`](docs/evidencias/F6/) | Modal de KPIs, exportación `.xlsx` y `.pdf`, rango de 400 días, sin movimientos (`api-escenarios.txt`) |
| F7 | [`docs/evidencias/F7/`](docs/evidencias/F7/) | Indicadores de Facturación antes y después de emitir una factura (`verificacion.txt`) |

**Pendiente:** no hay carpeta `docs/evidencias/F1/`; la pantalla de US-02 (cambio de estado) no tiene captura. Sus tres
escenarios están cubiertos por pruebas xUnit.

## 17. Historial de commits y participación

Historial de la rama `main` (`git shortlog -sne HEAD`, 2026-11-18). Historial general de commits:
[github.com/luismi912/Proyecto-Oasis-Stay/commits/main](https://github.com/luismi912/Proyecto-Oasis-Stay/commits/main).

| Integrante | N.º de commits | Principales contribuciones | Enlace al historial |
|---|---|---|---|
| Simon Herrera Acosta | 69 | Backend F0 a F7, migraciones EF Core, Identity y JWT, cadena de validación, Decorator, Strategy, Facade, reportes, semillas demo, pruebas xUnit y evidencias | [Historial general](https://github.com/luismi912/Proyecto-Oasis-Stay/commits/main) |
| Thomas Muñoz | 2 | Front-end según Figma y `.gitignore` | [Historial general](https://github.com/luismi912/Proyecto-Oasis-Stay/commits/main) |
| Luis Miguel Arango | 1 | Primer commit del repositorio | [Historial general](https://github.com/luismi912/Proyecto-Oasis-Stay/commits/main) |
| Yeison Cardozo | 0 | Modelo C4, diagrama de componentes y despliegue | [Historial general](https://github.com/luismi912/Proyecto-Oasis-Stay/commits/main) |
| Juan David Jiménez | 0 | Diagramas UML de estado, secuencia, actividad y clases | [Historial general](https://github.com/luismi912/Proyecto-Oasis-Stay/commits/main) |

## 18. Decisiones y pendientes

Las decisiones se resuelven en [`docs/decisiones.md`](docs/decisiones.md). Solo las filas **Aprobada** se implementan.

| Decisión | Tema | Estado |
|---|---|---|
| D-17 | Plantilla SOLID del Entregable 2 (SharePoint sin copia local) | **Pendiente** |
| D-25 | Épica de US-02 en Jira (SCRUM-6 cuelga de SCRUM-19; E1 la asigna a SCRUM-16) | **Pendiente** |
| D-14 | Despliegue en la nube | **Pendiente** (solo especificación; instancia local en uso) |
| D-22 | Tarifa Day Use (estancias de 0 noches) | Fuera de alcance |

Otros pendientes de documentación: la verificación de coherencia por clase (sección 12) y la trazabilidad
`docs/trazabilidad.md`, y la captura de US-02 (sección 16).
