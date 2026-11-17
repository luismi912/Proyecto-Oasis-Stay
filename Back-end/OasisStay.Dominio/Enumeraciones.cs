namespace OasisStay.Dominio;

/// <summary>Estados persistidos de la habitación [DC][CTR §3].</summary>
public enum EstadoHabitacion { DISPONIBLE, OCUPADO, RESERVADA, EN_LIMPIEZA, MANTENIMIENTO }

/// <summary>Estados de la reserva; Confirmada es el inicial, sin "Pendiente" [DE#4][CTR §3].</summary>
public enum EstadoReserva { Confirmada, EnCurso, Finalizada, Cancelada, NoShow }

/// <summary>Métodos de pago autorizados [E1 RN-06][DC].</summary>
public enum MetodoPago { EFECTIVO, TARJETA_CREDITO, TARJETA_DEBITO, TRANSFERENCIA }

/// <summary>Tipos de documento del huésped [D-20].</summary>
public enum TipoDocumento { CC, CE, Pasaporte }
