namespace Turnero.Api.Domain;

public enum RolUsuario { Admin = 0, Supervisor = 1, Empleado = 2 }

public class Usuario
{
    public int Id { get; set; }
    public string Email { get; set; } = "";
    public string NombreVisible { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public RolUsuario Rol { get; set; } = RolUsuario.Empleado;
    public bool Activo { get; set; } = true;

    /// <summary>Vincula al usuario con su ficha de empleado (opcional para Admin/Supervisor).</summary>
    public int? EmpleadoId { get; set; }
    public Empleado? Empleado { get; set; }
}

public class Empleado
{
    public int Id { get; set; }
    public string Documento { get; set; } = "";
    public string Nombres { get; set; } = "";
    public string Apellidos { get; set; } = "";
    public string? Cargo { get; set; }
    public string? Area { get; set; }
    public string? Email { get; set; }
    public DateOnly FechaIngreso { get; set; }
    public DateOnly? FechaRetiro { get; set; }

    /// <summary>Horas máximas semanales pactadas. Ver ParametrosLaborales.JornadaSemanalHoras.</summary>
    public decimal JornadaSemanalHoras { get; set; } = 42m;

    /// <summary>PIN de 4-6 dígitos para marcar desde el reloj compartido.</summary>
    public string Pin { get; set; } = "";

    public bool Activo { get; set; } = true;

    public string NombreCompleto => $"{Nombres} {Apellidos}".Trim();

    public List<Marcacion> Marcaciones { get; set; } = new();
    public List<Ausencia> Ausencias { get; set; } = new();
}

/// <summary>Un par entrada/salida. Salida null = turno abierto.</summary>
public class Marcacion
{
    public int Id { get; set; }
    public int EmpleadoId { get; set; }
    public Empleado? Empleado { get; set; }

    public DateTime Entrada { get; set; }
    public DateTime? Salida { get; set; }

    /// <summary>Fecha contable del turno (la del inicio). Facilita agrupar turnos nocturnos.</summary>
    public DateOnly FechaJornada { get; set; }

    public OrigenMarcacion Origen { get; set; } = OrigenMarcacion.Reloj;
    public string? Observacion { get; set; }

    /// <summary>Trazabilidad de correcciones manuales del administrador.</summary>
    public string? AjustadaPor { get; set; }
    public DateTime? AjustadaEn { get; set; }

    public bool EstaAbierta => Salida is null;
}

public enum OrigenMarcacion
{
    Reloj = 0,
    RegistroManual = 1,
    Importacion = 2
}

public class TipoAusencia
{
    public int Id { get; set; }
    public string Codigo { get; set; } = "";
    public string Nombre { get; set; } = "";

    /// <summary>Si el tiempo se paga al trabajador.</summary>
    public bool Remunerada { get; set; }

    /// <summary>Si cuenta como día perdido para el indicador de ausentismo (SST).</summary>
    public bool CuentaAusentismo { get; set; } = true;

    /// <summary>Si exige adjuntar soporte (incapacidad, certificado, etc.).</summary>
    public bool RequiereSoporte { get; set; }

    public bool Activo { get; set; } = true;
}

public class Ausencia
{
    public int Id { get; set; }
    public int EmpleadoId { get; set; }
    public Empleado? Empleado { get; set; }

    public int TipoAusenciaId { get; set; }
    public TipoAusencia? TipoAusencia { get; set; }

    public DateOnly FechaInicio { get; set; }
    public DateOnly FechaFin { get; set; }

    /// <summary>Para ausencias de horas sueltas (permisos). Si es null se cuentan días completos.</summary>
    public decimal? HorasParciales { get; set; }

    public bool Justificada { get; set; } = true;
    public string? NumeroSoporte { get; set; }
    public string? RutaSoporte { get; set; }
    public string? Observacion { get; set; }

    public string? RegistradaPor { get; set; }
    public DateTime RegistradaEn { get; set; } = DateTime.UtcNow;
}

public class Festivo
{
    public int Id { get; set; }
    public DateOnly Fecha { get; set; }
    public string Nombre { get; set; } = "";
}

/// <summary>
/// Parámetros de cálculo. Se editan desde la app: cuando cambia la ley,
/// no hay que tocar código ni recompilar.
/// </summary>
public class ParametrosLaborales
{
    public int Id { get; set; }

    public decimal JornadaSemanalHoras { get; set; } = 42m;
    public decimal HorasOrdinariasDia { get; set; } = 8m;

    /// <summary>Inicio de la franja nocturna.</summary>
    public TimeOnly InicioNocturno { get; set; } = new(19, 0);
    /// <summary>Fin de la franja nocturna.</summary>
    public TimeOnly FinNocturno { get; set; } = new(6, 0);

    public decimal RecargoNocturno { get; set; } = 0.35m;
    public decimal RecargoDominicalFestivo { get; set; } = 0.90m;
    public decimal RecargoExtraDiurna { get; set; } = 0.25m;
    public decimal RecargoExtraNocturna { get; set; } = 0.75m;

    /// <summary>Minutos de tolerancia antes de marcar un retardo.</summary>
    public int ToleranciaIngresoMinutos { get; set; } = 5;

    /// <summary>Horas de almuerzo descontadas automáticamente si el turno las supera.</summary>
    public decimal DescuentoAlmuerzoHoras { get; set; } = 1m;
    public decimal TurnoMinimoParaDescontarAlmuerzo { get; set; } = 6m;

    /// <summary>Cierra automáticamente turnos que superen este número de horas.</summary>
    public int MaximoHorasTurnoAbierto { get; set; } = 16;

    public string ZonaHoraria { get; set; } = "America/Bogota";
}
