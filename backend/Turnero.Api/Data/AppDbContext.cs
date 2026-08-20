using Turnero.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Turnero.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Empleado> Empleados => Set<Empleado>();
    public DbSet<Marcacion> Marcaciones => Set<Marcacion>();
    public DbSet<TipoAusencia> TiposAusencia => Set<TipoAusencia>();
    public DbSet<Ausencia> Ausencias => Set<Ausencia>();
    public DbSet<Festivo> Festivos => Set<Festivo>();
    public DbSet<ParametrosLaborales> Parametros => Set<ParametrosLaborales>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Usuario>(e =>
        {
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.Email).HasMaxLength(120).IsRequired();
            e.Property(x => x.NombreVisible).HasMaxLength(120).IsRequired();
            e.Property(x => x.PasswordHash).HasMaxLength(200).IsRequired();
            e.HasOne(x => x.Empleado)
             .WithMany()
             .HasForeignKey(x => x.EmpleadoId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<Empleado>(e =>
        {
            e.HasIndex(x => x.Documento).IsUnique();
            e.Property(x => x.Documento).HasMaxLength(20).IsRequired();
            e.Property(x => x.Nombres).HasMaxLength(80).IsRequired();
            e.Property(x => x.Apellidos).HasMaxLength(80).IsRequired();
            e.Property(x => x.Pin).HasMaxLength(6);
            e.Property(x => x.JornadaSemanalHoras).HasPrecision(5, 2);
            e.Ignore(x => x.NombreCompleto);
        });

        b.Entity<Marcacion>(e =>
        {
            e.HasIndex(x => new { x.EmpleadoId, x.FechaJornada });
            e.HasOne(x => x.Empleado)
             .WithMany(x => x.Marcaciones)
             .HasForeignKey(x => x.EmpleadoId)
             .OnDelete(DeleteBehavior.Cascade);
            e.Ignore(x => x.EstaAbierta);
        });

        b.Entity<TipoAusencia>(e =>
        {
            e.HasIndex(x => x.Codigo).IsUnique();
            e.Property(x => x.Codigo).HasMaxLength(10).IsRequired();
            e.Property(x => x.Nombre).HasMaxLength(120).IsRequired();
        });

        b.Entity<Ausencia>(e =>
        {
            e.HasIndex(x => new { x.EmpleadoId, x.FechaInicio });
            e.Property(x => x.HorasParciales).HasPrecision(5, 2);
            e.HasOne(x => x.Empleado)
             .WithMany(x => x.Ausencias)
             .HasForeignKey(x => x.EmpleadoId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.TipoAusencia)
             .WithMany()
             .HasForeignKey(x => x.TipoAusenciaId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Festivo>(e => e.HasIndex(x => x.Fecha).IsUnique());

        b.Entity<ParametrosLaborales>(e =>
        {
            e.Property(x => x.JornadaSemanalHoras).HasPrecision(5, 2);
            e.Property(x => x.HorasOrdinariasDia).HasPrecision(5, 2);
            e.Property(x => x.RecargoNocturno).HasPrecision(5, 4);
            e.Property(x => x.RecargoDominicalFestivo).HasPrecision(5, 4);
            e.Property(x => x.RecargoExtraDiurna).HasPrecision(5, 4);
            e.Property(x => x.RecargoExtraNocturna).HasPrecision(5, 4);
            e.Property(x => x.DescuentoAlmuerzoHoras).HasPrecision(5, 2);
            e.Property(x => x.TurnoMinimoParaDescontarAlmuerzo).HasPrecision(5, 2);
        });
    }
}
