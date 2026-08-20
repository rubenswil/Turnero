using Turnero.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Turnero.Api.Data;

public static class Seed
{
    public static async Task EjecutarAsync(AppDbContext db)
    {
        // Si el proyecto ya tiene migraciones se aplican; si no, se crea el esquema directo.
        if (db.Database.GetMigrations().Any())
            await db.Database.MigrateAsync();
        else
            await db.Database.EnsureCreatedAsync();

        if (!await db.Parametros.AnyAsync())
            db.Parametros.Add(new ParametrosLaborales());

        if (!await db.Usuarios.AnyAsync())
        {
            db.Usuarios.Add(new Usuario
            {
                Email = "admin@turnero.local",
                NombreVisible = "Administrador",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin1234!"),
                Rol = RolUsuario.Admin,
                Activo = true
            });
        }

        if (!await db.TiposAusencia.AnyAsync())
        {
            db.TiposAusencia.AddRange(
                new TipoAusencia { Codigo = "EG",   Nombre = "Incapacidad por enfermedad general", Remunerada = true,  RequiereSoporte = true },
                new TipoAusencia { Codigo = "ATEL", Nombre = "Incapacidad por accidente o enfermedad laboral", Remunerada = true, RequiereSoporte = true },
                new TipoAusencia { Codigo = "MAT",  Nombre = "Licencia de maternidad o paternidad", Remunerada = true, RequiereSoporte = true },
                new TipoAusencia { Codigo = "VAC",  Nombre = "Vacaciones", Remunerada = true, CuentaAusentismo = false },
                new TipoAusencia { Codigo = "COMP", Nombre = "Compensatorio", Remunerada = true, CuentaAusentismo = false },
                new TipoAusencia { Codigo = "LUTO", Nombre = "Licencia por luto", Remunerada = true, RequiereSoporte = true },
                new TipoAusencia { Codigo = "CAL",  Nombre = "Calamidad doméstica", Remunerada = true },
                new TipoAusencia { Codigo = "PERM", Nombre = "Permiso remunerado", Remunerada = true },
                new TipoAusencia { Codigo = "PNR",  Nombre = "Permiso no remunerado", Remunerada = false },
                new TipoAusencia { Codigo = "CITA", Nombre = "Cita médica", Remunerada = true, RequiereSoporte = true },
                new TipoAusencia { Codigo = "INJ",  Nombre = "Ausencia injustificada", Remunerada = false },
                new TipoAusencia { Codigo = "SUSP", Nombre = "Suspensión disciplinaria", Remunerada = false }
            );
        }

        if (!await db.Festivos.AnyAsync())
            db.Festivos.AddRange(FestivosColombia2026());

        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Festivos de Colombia 2026 (Ley 51 de 1983 — traslado al lunes siguiente).
    /// Para años posteriores usar CalcularFestivos(anio).
    /// </summary>
    private static IEnumerable<Festivo> FestivosColombia2026() => new[]
    {
        new Festivo { Fecha = new(2026, 1, 1),   Nombre = "Año Nuevo" },
        new Festivo { Fecha = new(2026, 1, 12),  Nombre = "Reyes Magos" },
        new Festivo { Fecha = new(2026, 3, 23),  Nombre = "San José" },
        new Festivo { Fecha = new(2026, 4, 2),   Nombre = "Jueves Santo" },
        new Festivo { Fecha = new(2026, 4, 3),   Nombre = "Viernes Santo" },
        new Festivo { Fecha = new(2026, 5, 1),   Nombre = "Día del Trabajo" },
        new Festivo { Fecha = new(2026, 5, 18),  Nombre = "Ascensión del Señor" },
        new Festivo { Fecha = new(2026, 6, 8),   Nombre = "Corpus Christi" },
        new Festivo { Fecha = new(2026, 6, 15),  Nombre = "Sagrado Corazón" },
        new Festivo { Fecha = new(2026, 6, 29),  Nombre = "San Pedro y San Pablo" },
        new Festivo { Fecha = new(2026, 7, 20),  Nombre = "Día de la Independencia" },
        new Festivo { Fecha = new(2026, 8, 7),   Nombre = "Batalla de Boyacá" },
        new Festivo { Fecha = new(2026, 8, 17),  Nombre = "Asunción de la Virgen" },
        new Festivo { Fecha = new(2026, 10, 12), Nombre = "Día de la Raza" },
        new Festivo { Fecha = new(2026, 11, 2),  Nombre = "Todos los Santos" },
        new Festivo { Fecha = new(2026, 11, 16), Nombre = "Independencia de Cartagena" },
        new Festivo { Fecha = new(2026, 12, 8),  Nombre = "Inmaculada Concepción" },
        new Festivo { Fecha = new(2026, 12, 25), Nombre = "Navidad" }
    };

    /// <summary>
    /// Genera los 18 festivos de cualquier año aplicando la Ley Emiliani.
    /// Úsalo desde el endpoint POST /api/festivos/generar/{anio}.
    /// </summary>
    public static IEnumerable<Festivo> CalcularFestivos(int anio)
    {
        var pascua = DomingoDePascua(anio);

        // Fijos (no se trasladan)
        yield return new Festivo { Fecha = new(anio, 1, 1),   Nombre = "Año Nuevo" };
        yield return new Festivo { Fecha = new(anio, 5, 1),   Nombre = "Día del Trabajo" };
        yield return new Festivo { Fecha = new(anio, 7, 20),  Nombre = "Día de la Independencia" };
        yield return new Festivo { Fecha = new(anio, 8, 7),   Nombre = "Batalla de Boyacá" };
        yield return new Festivo { Fecha = new(anio, 12, 8),  Nombre = "Inmaculada Concepción" };
        yield return new Festivo { Fecha = new(anio, 12, 25), Nombre = "Navidad" };

        // Semana Santa (no se trasladan)
        yield return new Festivo { Fecha = pascua.AddDays(-3), Nombre = "Jueves Santo" };
        yield return new Festivo { Fecha = pascua.AddDays(-2), Nombre = "Viernes Santo" };

        // Trasladables al lunes siguiente
        yield return new Festivo { Fecha = Lunes(new(anio, 1, 6)),   Nombre = "Reyes Magos" };
        yield return new Festivo { Fecha = Lunes(new(anio, 3, 19)),  Nombre = "San José" };
        yield return new Festivo { Fecha = Lunes(new(anio, 6, 29)),  Nombre = "San Pedro y San Pablo" };
        yield return new Festivo { Fecha = Lunes(new(anio, 8, 15)),  Nombre = "Asunción de la Virgen" };
        yield return new Festivo { Fecha = Lunes(new(anio, 10, 12)), Nombre = "Día de la Raza" };
        yield return new Festivo { Fecha = Lunes(new(anio, 11, 1)),  Nombre = "Todos los Santos" };
        yield return new Festivo { Fecha = Lunes(new(anio, 11, 11)), Nombre = "Independencia de Cartagena" };

        // Móviles trasladados al lunes (siempre caen en lunes por construcción)
        yield return new Festivo { Fecha = pascua.AddDays(43), Nombre = "Ascensión del Señor" };
        yield return new Festivo { Fecha = pascua.AddDays(64), Nombre = "Corpus Christi" };
        yield return new Festivo { Fecha = pascua.AddDays(71), Nombre = "Sagrado Corazón" };
    }

    private static DateOnly Lunes(DateOnly fecha) =>
        fecha.DayOfWeek == DayOfWeek.Monday
            ? fecha
            : fecha.AddDays(((int)DayOfWeek.Monday - (int)fecha.DayOfWeek + 7) % 7);

    /// <summary>Algoritmo de Butcher (calendario gregoriano).</summary>
    private static DateOnly DomingoDePascua(int anio)
    {
        int a = anio % 19;
        int b = anio / 100, c = anio % 100;
        int d = b / 4, e = b % 4;
        int f = (b + 8) / 25;
        int g = (b - f + 1) / 3;
        int h = (19 * a + b - d - g + 15) % 30;
        int i = c / 4, k = c % 4;
        int l = (32 + 2 * e + 2 * i - h - k) % 7;
        int m = (a + 11 * h + 22 * l) / 451;
        int mes = (h + l - 7 * m + 114) / 31;
        int dia = ((h + l - 7 * m + 114) % 31) + 1;
        return new DateOnly(anio, mes, dia);
    }
}
