using System.Globalization;
using System.Text;
using Turnero.Api.Contracts;
using Turnero.Api.Data;
using Turnero.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Turnero.Api.Services;

public class ReporteService
{
    private readonly AppDbContext _db;
    private static readonly CultureInfo Es = new("es-CO");

    public ReporteService(AppDbContext db) => _db = db;

    public async Task<ReporteMensualDto> MensualAsync(int anio, int mes, int? empleadoId = null)
    {
        var desde = new DateOnly(anio, mes, 1);
        var hasta = desde.AddMonths(1).AddDays(-1);
        return await PorRangoAsync(desde, hasta, empleadoId, anio, mes);
    }

    public async Task<ReporteMensualDto> PorRangoAsync(
        DateOnly desde, DateOnly hasta, int? empleadoId = null, int? anio = null, int? mes = null)
    {
        var parametros = await _db.Parametros.AsNoTracking().FirstAsync();
        var festivos = await _db.Festivos.AsNoTracking()
            .Where(f => f.Fecha >= desde && f.Fecha <= hasta)
            .Select(f => f.Fecha).ToListAsync();

        var calc = new CalculadoraJornada(parametros, festivos);
        var diasHabiles = calc.DiasHabiles(desde, hasta);

        var empleados = await _db.Empleados.AsNoTracking()
            .Where(e => empleadoId == null || e.Id == empleadoId)
            .Where(e => e.Activo || e.FechaRetiro >= desde)
            .OrderBy(e => e.Apellidos).ThenBy(e => e.Nombres)
            .ToListAsync();

        var ids = empleados.Select(e => e.Id).ToList();

        var marcaciones = await _db.Marcaciones.AsNoTracking()
            .Where(m => ids.Contains(m.EmpleadoId) && m.FechaJornada >= desde && m.FechaJornada <= hasta)
            .ToListAsync();

        var ausencias = await _db.Ausencias.AsNoTracking()
            .Include(a => a.TipoAusencia)
            .Where(a => ids.Contains(a.EmpleadoId) && a.FechaInicio <= hasta && a.FechaFin >= desde)
            .ToListAsync();

        var filas = new List<FilaReporteDto>();
        var alertas = new List<string>();

        foreach (var e in empleados)
        {
            var propias = marcaciones.Where(m => m.EmpleadoId == e.Id).ToList();
            var cerradas = propias.Where(m => m.Salida is not null).ToList();
            var abiertas = propias.Count - cerradas.Count;

            var d = calc.Calcular(cerradas);
            var diasTrabajados = cerradas.Select(m => m.FechaJornada).Distinct().Count();

            // Horas esperadas: jornada semanal repartida en los días hábiles del periodo.
            var horasDia = Math.Round(e.JornadaSemanalHoras / 6m, 2);
            var horasEsperadas = Math.Round(horasDia * diasHabiles, 2);

            var propiasAusencias = ausencias.Where(a => a.EmpleadoId == e.Id).ToList();
            var porTipo = new Dictionary<string, decimal>();
            int diasPerdidos = 0;
            decimal horasAusencia = 0;

            foreach (var a in propiasAusencias)
            {
                var ini = a.FechaInicio > desde ? a.FechaInicio : desde;
                var fin = a.FechaFin < hasta ? a.FechaFin : hasta;

                decimal cantidad;
                if (a.HorasParciales is > 0)
                {
                    cantidad = a.HorasParciales.Value;
                    horasAusencia += cantidad;
                }
                else
                {
                    var habiles = calc.DiasHabiles(ini, fin);
                    cantidad = habiles;
                    horasAusencia += habiles * horasDia;
                    if (a.TipoAusencia?.CuentaAusentismo == true) diasPerdidos += habiles;
                }

                var codigo = a.TipoAusencia?.Codigo ?? "N/D";
                porTipo[codigo] = porTipo.GetValueOrDefault(codigo) + cantidad;
            }

            var indice = diasHabiles > 0
                ? Math.Round(diasPerdidos * 100m / diasHabiles, 2)
                : 0m;

            if (abiertas > 0)
                alertas.Add($"{e.NombreCompleto}: {abiertas} turno(s) sin marcar salida.");

            var extras = d.ExtraDiurnas + d.ExtraNocturnas;
            if (extras > 24)
                alertas.Add($"{e.NombreCompleto}: {extras:0.##} h extra en el periodo. Revisar límite legal.");

            filas.Add(new FilaReporteDto(
                e.Id, e.Documento, e.NombreCompleto, e.Cargo, e.Area,
                diasTrabajados, abiertas,
                horasEsperadas, d.Total, Math.Round(d.Total - horasEsperadas, 2),
                d.Ordinarias, d.ExtraDiurnas, d.ExtraNocturnas,
                d.RecargoNocturno, d.RecargoDominicalFestivo, d.RecargoNocturnoDominicalFestivo,
                diasPerdidos, Math.Round(horasAusencia, 2), indice,
                porTipo));
        }

        var totalPerdidos = filas.Sum(f => f.DiasAusencia);
        var diasProgramados = diasHabiles * Math.Max(filas.Count, 1);

        var resumen = new ResumenReporteDto(
            Empleados: filas.Count,
            HorasTrabajadas: Math.Round(filas.Sum(f => f.HorasTrabajadas), 2),
            HorasExtra: Math.Round(filas.Sum(f => f.ExtraDiurnas + f.ExtraNocturnas), 2),
            HorasEsperadas: Math.Round(filas.Sum(f => f.HorasEsperadas), 2),
            DiasPerdidos: totalPerdidos,
            IndiceAusentismoGeneral: Math.Round(totalPerdidos * 100m / diasProgramados, 2),
            TurnosSinCerrar: filas.Sum(f => f.TurnosAbiertos));

        var periodo = anio is not null && mes is not null
            ? Es.TextInfo.ToTitleCase(new DateTime(anio.Value, mes.Value, 1).ToString("MMMM 'de' yyyy", Es))
            : $"{desde:dd/MM/yyyy} — {hasta:dd/MM/yyyy}";

        return new ReporteMensualDto(
            anio ?? desde.Year, mes ?? desde.Month, periodo,
            desde, hasta, diasHabiles, filas, resumen, alertas);
    }

    /// <summary>Detalle día por día de un empleado. Es la hoja de vida de sus marcaciones.</summary>
    public async Task<List<DetalleDiaDto>> DetalleAsync(int empleadoId, DateOnly desde, DateOnly hasta)
    {
        var parametros = await _db.Parametros.AsNoTracking().FirstAsync();
        var festivos = await _db.Festivos.AsNoTracking()
            .Where(f => f.Fecha >= desde && f.Fecha <= hasta)
            .ToDictionaryAsync(f => f.Fecha, f => f.Nombre);

        var calc = new CalculadoraJornada(parametros, festivos.Keys);

        var marcaciones = await _db.Marcaciones.AsNoTracking()
            .Include(m => m.Empleado)
            .Where(m => m.EmpleadoId == empleadoId && m.FechaJornada >= desde && m.FechaJornada <= hasta)
            .OrderBy(m => m.Entrada)
            .ToListAsync();

        var ausencias = await _db.Ausencias.AsNoTracking()
            .Include(a => a.TipoAusencia)
            .Where(a => a.EmpleadoId == empleadoId && a.FechaInicio <= hasta && a.FechaFin >= desde)
            .ToListAsync();

        var detalle = new List<DetalleDiaDto>();

        for (var f = desde; f <= hasta; f = f.AddDays(1))
        {
            var delDia = marcaciones.Where(m => m.FechaJornada == f).ToList();
            var ausencia = ausencias.FirstOrDefault(a => a.FechaInicio <= f && a.FechaFin >= f);

            detalle.Add(new DetalleDiaDto(
                Fecha: f,
                DiaSemana: Es.TextInfo.ToTitleCase(f.ToString("dddd", Es)),
                EsFestivo: festivos.ContainsKey(f),
                EsDomingo: f.DayOfWeek == DayOfWeek.Sunday,
                Marcaciones: delDia.Select(m => Mapear(m, calc)).ToList(),
                HorasTotales: calc.Calcular(delDia).Total,
                Ausencia: ausencia?.TipoAusencia?.Nombre));
        }

        return detalle;
    }

    public static MarcacionDto Mapear(Marcacion m, CalculadoraJornada calc) => new(
        m.Id, m.EmpleadoId, m.Empleado?.NombreCompleto ?? "",
        m.Entrada, m.Salida, m.FechaJornada,
        calc.Calcular(m).Total, m.Origen.ToString(), m.Observacion,
        m.AjustadaPor, m.EstaAbierta);

    /// <summary>CSV separado por punto y coma: Excel en español lo abre en columnas sin pedir nada.</summary>
    public static string ExportarCsv(ReporteMensualDto r)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Reporte de tiempo laborado;{r.Periodo}");
        sb.AppendLine($"Periodo;{r.Desde:dd/MM/yyyy};{r.Hasta:dd/MM/yyyy};Días hábiles;{r.DiasHabiles}");
        sb.AppendLine();
        sb.AppendLine(string.Join(';',
            "Documento", "Empleado", "Cargo", "Área",
            "Días trabajados", "Horas esperadas", "Horas trabajadas", "Diferencia",
            "Ordinarias", "Extra diurnas", "Extra nocturnas",
            "Rec. nocturno", "Rec. dom/fest", "Rec. noct. dom/fest",
            "Días de ausencia", "Horas de ausencia", "Índice ausentismo %", "Turnos sin cerrar"));

        foreach (var f in r.Filas)
            sb.AppendLine(string.Join(';',
                f.Documento, f.Empleado, f.Cargo ?? "", f.Area ?? "",
                f.DiasTrabajados, N(f.HorasEsperadas), N(f.HorasTrabajadas), N(f.Diferencia),
                N(f.Ordinarias), N(f.ExtraDiurnas), N(f.ExtraNocturnas),
                N(f.RecargoNocturno), N(f.RecargoDominicalFestivo), N(f.RecargoNocturnoDominicalFestivo),
                f.DiasAusencia, N(f.HorasAusencia), N(f.IndiceAusentismo), f.TurnosAbiertos));

        sb.AppendLine();
        sb.AppendLine($"TOTALES;{r.Resumen.Empleados} empleados;;;;{N(r.Resumen.HorasEsperadas)};{N(r.Resumen.HorasTrabajadas)}");
        sb.AppendLine($"Horas extra del periodo;{N(r.Resumen.HorasExtra)}");
        sb.AppendLine($"Días perdidos por ausentismo;{r.Resumen.DiasPerdidos}");
        sb.AppendLine($"Índice de ausentismo general (%);{N(r.Resumen.IndiceAusentismoGeneral)}");

        return sb.ToString();

        static string N(decimal v) => v.ToString("0.00", Es);
    }
}
