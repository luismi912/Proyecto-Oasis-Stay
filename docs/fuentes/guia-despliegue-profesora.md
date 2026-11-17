# Guía de la profesora — Diagrama de despliegue UML

Transcripción de `Documentacion/Diagrama de despliegue UML.pdf` (23 diapositivas, solo imágenes; Diseño de
Sistemas de Información, ITM). Es lo que la profesora espera ver en el diagrama de despliegue. Fuente #1 de la
jerarquía junto con `guia-entrega-3.md`.

## Conceptos y notación (diapositivas 3-12)

- **Propósito:** mostrar dónde se ejecutan los componentes, qué artefactos se despliegan y la infraestructura que
  los soporta. Complementa el diagrama de componentes (el de componentes responde al *qué*; el de despliegue, al
  *dónde* y *cómo* se ejecuta).
- **Problema de partida:** una aplicación que funciona localmente y falla al desplegarse (tiempos altos, errores de
  conexión, intermitencia). Preguntas: ¿la infraestructura es adecuada?, ¿la comunicación entre componentes está
  bien definida?, ¿se consideraron rendimiento, disponibilidad y seguridad?
- **Nodo «node»:** recurso de ejecución donde se alojan artefactos. **Dispositivo «device»:** nodo físico
  (servidor, computador, IoT). **Recursos computacionales:** CPU, memoria, almacenamiento y sistema operativo;
  determinan la capacidad del sistema. Ejemplos de nodos: PC del usuario, servidor físico, contenedor Docker
  («executionEnvironment»), instancia en la nube («device»).
- **Entorno de ejecución «executionEnvironment»:** sistema operativo, servidor de aplicaciones o contenedor donde
  corren los artefactos; va dentro de un dispositivo.
- **Artefacto «artifact»:** elemento físico desplegable: ejecutable (app.exe), biblioteca (.dll), archivo de
  configuración o datos (schema.sql, application.properties), paquete (.war, .zip, imagen Docker).
- **Relaciones:** «deploy» (artefacto → nodo/entorno donde se ubica) y «manifest» (artefacto → componente que
  implementa). Communication Path entre nodos con **protocolo y puerto** (HTTP/HTTPS 80/443, JDBC/SQL 5432/3306,
  SMTP 25/587, REST, TCP/IP).
- **Nodos jerárquicos:** dispositivo ⊃ contenedor ⊃ servidor de aplicaciones ⊃ artefactos.

## Especificaciones de despliegue (diapositiva 13)

Información adicional (notas, estereotipos o propiedades en el nodo) que define cómo se ejecutan los artefactos.
Ejemplo de la diapositiva: `SO = Linux (Ubuntu 22.04)`, `JVM = OpenJDK 17`, `Tomcat.port = 8080`,
`Memoria = 4 GB`, `CPU = 2 vCPU`, `app.env = producción`, `log.level = INFO`.
Parámetros comunes: **Entorno** (SO, versión, runtime), **Recursos** (CPU, memoria, almacenamiento),
**Puertos** de escucha, **Conexión** (cadenas, usuarios, credenciales) y **Parámetros de la app** (variables de
entorno: recurso, log, modo).

## Nivel de especificación frente a nivel de instancia (diapositiva 14)

| Aspecto | Especificación | Instancia |
|---|---|---|
| Propósito | Definir la arquitectura de despliegue | Mostrar una configuración real |
| Nodos | Tipos de nodo (Servidor de Aplicación) | Instancias con nombre (srv-app-01) |
| Detalles | Sin valores operativos | SO, versiones, recursos, puertos |
| Uso típico | Diseño y planeación | Despliegue, operación y soporte |

Ejemplo de instancia: `srv-app-01` Linux (Ubuntu 22.04), 4 vCPU, 8 GB RAM, Tomcat 10, `app-2.1.0.war`;
`db-prod-01` PostgreSQL 15, 4 vCPU, 16 GB RAM.

## Ejemplos (diapositivas 15-17)

Tres capas (cliente con navegador → servidor de aplicaciones con app + configuración → servidor de BD),
contenedores Docker (API y base de datos en contenedores sobre un servidor Linux, red interna) y microservicios
en la nube (API Gateway, réplicas, bus de mensajes, almacenamiento gestionado).

## Detección de errores (diapositiva 18)

Errores típicos que la profesora señala y cómo corregirlos:
1. **Artefacto de servidor dentro del cliente** (p. ej. app.war en el navegador): el cliente debe contener una
   aplicación de cliente (app.js o una SPA) o ninguna.
2. **Protocolo/puerto inconsistente** para el acceso del cliente (HTTP 8080): usar HTTPS 443 o HTTP 80 según el diseño.
3. **Protocolo de base de datos incorrecto** para el motor (JDBC 3306 es de MySQL): usar el protocolo y puerto del
   motor real.
4. **Falta de «manifest»:** indicar qué artefacto implementa qué componente.

## Decisiones y atributos de calidad (diapositiva 19)

Disponibilidad (balanceador, réplicas, recuperación ante fallos), seguridad (HTTPS, control de acceso, cifrado
de datos), rendimiento (distribución de carga, contenedores optimizados), escalabilidad (nube, instancias dinámicas).

## Relación con C4 (diapositiva 20)

El nivel de despliegue de C4 (nodos y relaciones, notación sencilla, para stakeholders) y el diagrama UML
(nodos, entornos, artefactos y configuración, UML 2.x) son complementarios.

## Auditoría con IA (diapositiva 21)

Proceso: 1) análisis humano (hallazgos e hipótesis), 2) contraste con IA, 3) correcciones al diagrama,
4) justificación documentada. Criterios: coherencia nodos/artefactos/entornos, protocolos y puertos correctos,
configuraciones viables y completas, alineación con atributos de calidad, justificación técnica de cada ajuste.
**La decisión y la justificación siempre son humanas.**

## Referencias (diapositiva 23)

OMG (2017) UML 2.5.1 — https://www.omg.org/spec/UML/2.5.1 · UML Diagrams, deployment diagrams —
https://www.uml-diagrams.org/deployment-diagrams.html · Brown, S., The C4 model — https://c4model.com/diagrams
