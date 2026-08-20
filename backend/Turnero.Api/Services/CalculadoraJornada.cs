using Turnero.Api.Domain;

namespace Turnero.Api.Services;

/// <summary>Desglose de un turno o de un conjunto de turnos, en horas.</summary>
public record DesgloseHoras(
    decimal Total,
    decimal Ordinarias,
    decimal ExtraDiurnas,
    decimal ExtraNocturnas,
    decimal RecargoNocturno,
    decimal RecargoDominicalFestivo,
    decimal RecargoNocturnoDominicalFestivo)
{
    public static readonly DesgloseHoras Cero = new(0, 0, 0, 0, 0, 0, 0);

    public static DesgloseHoras operator +(DesgloseHoras a, DesgloseHoras b) => new(
        a.Total + b.Total,
        a.Ordinarias + b.Ordinarias,
        a.ExtraDiurnas + b.ExtraDiurnas,
        a.ExtraNocturnas + b.ExtraNocturnas,
        a.RecargoNocturno + b.RecargoNocturno,
        a.RecargoDominicalFestivo + b.RecargoDominicalFestivo,
        a.RecargoNocturnoDominicalFestivo + b.RecargoNocturnoDominicalFestivo);
}

/// <summary>
/// Reparte el tiempo trabajado minuto a minuto entre las franjas que la nómina necesita.
///
/// Criterios aplicados (todos configurables en ParametrosLaborales):
///  - Franja nocturna: entre InicioNocturno y FinNocturno del día siguiente.
///  - Dominical/festivo: domingos y las fechas cargadas en la tabla Festivos.
///  - Extras: minutos que exceden HorasOrdinariasDia dentro del mismo turno.
///  - Almuerzo: se descuenta del bloque diurno central si el turno supera el mínimo.
///
/// Los porcentajes de recargo NO se aplican aquí: este servicio entrega horas por franja
/// y la nómina las multiplica. Así el reporte sirve igual aunque cambien los porcentajes.
/// </summary>
public class CalculadoraJornada
{
    private readonly ParametrosLaborales _p;
    private readonly HashSet<DateOnly> _festivos;

    public CalculadoraJornada(ParametrosLaborales parametros, IEnumerable<DateOnly> festivos)
    {
        _p = parametros;
        _festivos = festivos.ToHashSet();
    }

    public bool EsDiaRecargado(DateOnly fecha) =>
        fecha.DayOfWeek == DayOfWeek.Sunday || _festivos.Contains(fecha);

    public bool EsNocturno(DateTime momento)
    {
        var hora = TimeOnly.FromDateTime(momento);
        // La franja cruza medianoche: es nocturno si va después del inicio o antes del fin.
        return _p.InicioNocturno > _p.FinNocturno
            ? hora >= _p.InicioNocturno || hora < _p.FinNocturno
            : hora >= _p.InicioNocturno && hora < _p.FinNocturno;
    }

    public DesgloseHoras Calcular(Marcacion m)
    {
        if (m.Salida is null || m.Salida <= m.Entrada)
            return DesgloseHoras.Cero;

        var totalMinutos = (int)Math.Round((m.Salida.Value - m.Entrada).TotalMinutes);

        // Minutos de almuerzo a descontar, tomados del centro del turno.
        var minutosAlmuerzo = totalMinutos >= _p.TurnoMinimoParaDescontarAlmuerzo * 60
            ? (int)(_p.DescuentoAlmuerzoHoras * 60)
            : 0;
        var inicioAlmuerzo = minutosAlmuerzo > 0 ? (totalMinutos - minutosAlmuerzo) / 2 : -1;
        var finAlmuerzo = inicioAlmuerzo + minutosAlmuerzo;

        var limiteOrdinarias = (int)(_p.HorasOrdinariasDia * 60);

        decimal ord = 0, extraD = 0, extraN = 0, recN = 0, recDF = 0, recNDF = 0;
        int trabajados = 0;

        for (int i = 0; i < totalMinutos; i++)
        {
            if (minutosAlmuerzo > 0 && i >= inicioAlmuerzo && i < finAlmuerzo)
                continue;

            var momento = m.Entrada.AddMinutes(i);
            var nocturno = EsNocturno(momento);
            var recargado = EsDiaRecargado(DateOnly.FromDateTime(momento));

            trabajados++;
            var esExtra = trabajados > limiteOrdinarias;

            if (esExtra)
            {
                if (nocturno) extraN++; else extraD++;
            }
            else
            {
                ord++;
            }

            // Los recargos conviven con la clasificación anterior: una hora extra
            // nocturna en domingo genera extra nocturna + recargo dominical.
            if (nocturno && recargado) recNDF++;
            else if (nocturno) recN++;
            else if (recargado) recDF++;
        }

        return new DesgloseHoras(
            Total: EnHoras(ord + extraD + extraN),
            Ordinarias: EnHoras(ord),
            ExtraDiurnas: EnHoras(extraD),
            ExtraNocturnas: EnHoras(extraN),
            RecargoNocturno: EnHoras(recN),
            RecargoDominicalFestivo: EnHoras(recDF),
            RecargoNocturnoDominicalFestivo: EnHoras(recNDF));
    }

    public DesgloseHoras Calcular(IEnumerable<Marcacion> marcaciones) =>
        marcaciones.Aggregate(DesgloseHoras.Cero, (acc, m) => acc + Calcular(m));

    /// <summary>Días hábiles (lunes a sábado, sin festivos) dentro de un rango.</summary>
    public int DiasHabiles(DateOnly desde, DateOnly hasta)
    {
        int dias = 0;
        for (var d = desde; d <= hasta; d = d.AddDays(1))
            if (!EsDiaRecargado(d) && d.DayOfWeek != DayOfWeek.Saturday)
                dias++;
        return dias;
    }

    private static decimal EnHoras(decimal minutos) => Math.Round(minutos / 60m, 2);
}
