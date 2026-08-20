using Turnero.Api.Domain;

namespace Turnero.Api.Contracts;

// ---------- Auth ----------

public record LoginDto(string Email, string Password);

public record TokenDto(string Token, string Rol, string NombreVisible, int? EmpleadoId);

public record CrearUsuarioDto(
    string Email, string NombreVisible, string Password,
    string Rol, int? EmpleadoId);

public record UsuarioDto(
    int Id, string Email, string NombreVisible, string Rol, bool Activo, int? EmpleadoId);

public record CambiarPasswordDto(string PasswordActual, string PasswordNuevo);

// ---------- Empleados ----------

public record EmpleadoDto(
    int Id, string Documento, string Nombres, string Apellidos, string NombreCompleto,
    string? Cargo, string? Area, string? Email,
    DateOnly FechaIngreso, DateOnly? FechaRetiro,
    decimal JornadaSemanalHoras, bool Activo);

public record GuardarEmpleadoDto(
    string Documento, string Nombres, string Apellidos,
    string? Cargo, string? Area, string? Email,
    DateOnly FechaIngreso, DateOnly? FechaRetiro,
    decimal JornadaSemanalHoras, string? Pin, bool Activo);

// ---------- Marcaciones ----------

public record MarcacionDto(
    int Id, int EmpleadoId, string Empleado,
    DateTime Entrada, DateTime? Salida, DateOnly FechaJornada,
    decimal HorasTotales, string Origen, string? Observacion,
    string? AjustadaPor, bool EstaAbierta);

public record MarcarDto(int EmpleadoId, string? Pin, string? Observacion);

public record RegistroManualDto(
    int EmpleadoId, DateTime Entrada, DateTime? Salida, string? Observacion, string? Usuario);

public record EstadoRelojDto(
    int EmpleadoId, string Empleado, bool TurnoAbierto,
    DateTime? DesdeHora, decimal HorasHoy, decimal HorasSemana, string? Mensaje);

// ---------- Ausencias ----------

public record TipoAusenciaDto(
    int Id, string Codigo, string Nombre, bool Remunerada,
    bool CuentaAusentismo, bool RequiereSoporte, bool Activo);

public record AusenciaDto(
    int Id, int EmpleadoId, string Empleado,
    int TipoAusenciaId, string TipoCodigo, string TipoNombre,
    DateOnly FechaInicio, DateOnly FechaFin, int DiasCalendario,
    decimal? HorasParciales, bool Justificada,
    string? NumeroSoporte, string? Observacion);

public record GuardarAusenciaDto(
    int EmpleadoId, int TipoAusenciaId,
    DateOnly FechaInicio, DateOnly FechaFin,
    decimal? HorasParciales, bool Justificada,
    string? NumeroSoporte, string? RutaSoporte, string? Observacion, string? Usuario);

// ---------- Reportes ----------

public record FilaReporteDto(
    int EmpleadoId, string Documento, string Empleado, string? Cargo, string? Area,
    int DiasTrabajados, int TurnosAbiertos,
    decimal HorasEsperadas, decimal HorasTrabajadas, decimal Diferencia,
    decimal Ordinarias, decimal ExtraDiurnas, decimal ExtraNocturnas,
    decimal RecargoNocturno, decimal RecargoDominicalFestivo, decimal RecargoNocturnoDominicalFestivo,
    int DiasAusencia, decimal HorasAusencia, decimal IndiceAusentismo,
    Dictionary<string, decimal> AusenciasPorTipo);

public record ReporteMensualDto(
    int Anio, int Mes, string Periodo,
    DateOnly Desde, DateOnly Hasta, int DiasHabiles,
    List<FilaReporteDto> Filas,
    ResumenReporteDto Resumen,
    List<string> Alertas);

public record ResumenReporteDto(
    int Empleados,
    decimal HorasTrabajadas, decimal HorasExtra, decimal HorasEsperadas,
    int DiasPerdidos, decimal IndiceAusentismoGeneral,
    int TurnosSinCerrar);

public record DetalleDiaDto(
    DateOnly Fecha, string DiaSemana, bool EsFestivo, bool EsDomingo,
    List<MarcacionDto> Marcaciones, decimal HorasTotales,
    string? Ausencia);
