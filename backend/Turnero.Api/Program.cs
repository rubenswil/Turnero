using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using Turnero.Api.Contracts;
using Turnero.Api.Data;
using Turnero.Api.Domain;
using Turnero.Api.Services;

var builder = WebApplication.CreateBuilder(args);

var motor = builder.Configuration["Motor"] ?? "Sqlite";
var esSqlServer = motor.Equals("SqlServer", StringComparison.OrdinalIgnoreCase);
var cadena = esSqlServer
    ? builder.Configuration.GetConnectionString("Default")!
    : builder.Configuration.GetConnectionString("Sqlite") ?? "Data Source=turnero.db";

builder.Services.AddDbContext<AppDbContext>(o =>
{
    if (esSqlServer)
        o.UseSqlServer(cadena);
    else
        o.UseSqlite(cadena);
});

builder.Services.AddScoped<ReporteService>();
builder.Services.AddScoped<TokenService>();
builder.Services.AddOpenApi();

// JWT
var jwtClave = builder.Configuration["Jwt:Clave"]!;
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.MapInboundClaims = false;
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Emisor"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audiencia"],
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtClave)),
            RoleClaimType = "role",
            NameClaimType = "sub"
        };
    });

builder.Services.AddAuthorization(o =>
{
    o.AddPolicy("SoloAdmin",        p => p.RequireClaim("role", "Admin"));
    o.AddPolicy("AdminOSupervisor", p => p.RequireClaim("role", "Admin", "Supervisor"));
    o.AddPolicy("Autenticado",      p => p.RequireAuthenticatedUser());
});

builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins(builder.Configuration["FrontendUrl"] ?? "http://localhost:5173")
    .AllowAnyHeader()
    .AllowAnyMethod()));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
    await Seed.EjecutarAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>());

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(o => o.WithTitle("Turnero — API"));
}

app.UsePathBase("/turnero/backend");
app.UseRouting();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

// =====================================================================
//  Auth
// =====================================================================

var auth = app.MapGroup("/api/auth").WithTags("Auth");

auth.MapPost("/login", async (AppDbContext db, TokenService tokens, LoginDto dto) =>
{
    var usuario = await db.Usuarios.FirstOrDefaultAsync(u => u.Email == dto.Email && u.Activo);
    if (usuario is null || !BCrypt.Net.BCrypt.Verify(dto.Password, usuario.PasswordHash))
        return Results.Unauthorized();

    var token = tokens.Generar(usuario);
    return Results.Ok(new TokenDto(token, usuario.Rol.ToString(), usuario.NombreVisible, usuario.EmpleadoId));
});

auth.MapGet("/yo", async (AppDbContext db, ClaimsPrincipal user) =>
{
    var id = int.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
    var u = await db.Usuarios.FindAsync(id);
    return u is null ? Results.NotFound() : Results.Ok(MapUsuario(u));
}).RequireAuthorization("Autenticado");

auth.MapPost("/cambiar-password", async (AppDbContext db, ClaimsPrincipal user, CambiarPasswordDto dto) =>
{
    var id = int.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
    var u = await db.Usuarios.FindAsync(id);
    if (u is null) return Results.NotFound();
    if (!BCrypt.Net.BCrypt.Verify(dto.PasswordActual, u.PasswordHash))
        return Results.BadRequest(new { mensaje = "La contraseña actual no es correcta." });

    u.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.PasswordNuevo);
    await db.SaveChangesAsync();
    return Results.Ok(new { mensaje = "Contraseña actualizada." });
}).RequireAuthorization("Autenticado");

// =====================================================================
//  Gestión de usuarios (solo Admin)
// =====================================================================

var usuarios = app.MapGroup("/api/usuarios").WithTags("Usuarios").RequireAuthorization("SoloAdmin");

usuarios.MapGet("/", async (AppDbContext db) =>
    (await db.Usuarios.AsNoTracking().ToListAsync()).Select(MapUsuario));

usuarios.MapPost("/", async (AppDbContext db, CrearUsuarioDto dto) =>
{
    if (!Enum.TryParse<RolUsuario>(dto.Rol, ignoreCase: true, out var rol))
        return Results.BadRequest(new { mensaje = "Rol inválido. Use: Admin, Supervisor o Empleado." });

    if (await db.Usuarios.AnyAsync(u => u.Email == dto.Email))
        return Results.Conflict(new { mensaje = "Ya existe un usuario con ese email." });

    var u = new Usuario
    {
        Email = dto.Email.Trim().ToLower(),
        NombreVisible = dto.NombreVisible.Trim(),
        PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
        Rol = rol,
        EmpleadoId = dto.EmpleadoId,
        Activo = true
    };

    db.Usuarios.Add(u);
    await db.SaveChangesAsync();
    return Results.Created($"/api/usuarios/{u.Id}", MapUsuario(u));
});

usuarios.MapPut("/{id:int}", async (AppDbContext db, int id, CrearUsuarioDto dto) =>
{
    var u = await db.Usuarios.FindAsync(id);
    if (u is null) return Results.NotFound();

    if (!Enum.TryParse<RolUsuario>(dto.Rol, ignoreCase: true, out var rol))
        return Results.BadRequest(new { mensaje = "Rol inválido. Use: Admin, Supervisor o Empleado." });

    u.Email = dto.Email.Trim().ToLower();
    u.NombreVisible = dto.NombreVisible.Trim();
    u.Rol = rol;
    u.EmpleadoId = dto.EmpleadoId;
    if (!string.IsNullOrWhiteSpace(dto.Password))
        u.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);

    await db.SaveChangesAsync();
    return Results.Ok(MapUsuario(u));
});

usuarios.MapDelete("/{id:int}", async (AppDbContext db, int id) =>
{
    var u = await db.Usuarios.FindAsync(id);
    if (u is null) return Results.NotFound();
    u.Activo = false;
    await db.SaveChangesAsync();
    return Results.Ok(new { mensaje = "Usuario desactivado." });
});

// =====================================================================
//  Empleados  (Admin: todo | Supervisor: solo lectura)
// =====================================================================

var empleados = app.MapGroup("/api/empleados").WithTags("Empleados").RequireAuthorization("Autenticado");

empleados.MapGet("/", async (AppDbContext db, bool? incluirInactivos) =>
{
    var q = db.Empleados.AsNoTracking();
    if (incluirInactivos != true) q = q.Where(e => e.Activo);
    var lista = await q.OrderBy(e => e.Apellidos).ThenBy(e => e.Nombres).ToListAsync();
    return Results.Ok(lista.Select(MapEmpleado));
});

empleados.MapGet("/{id:int}", async (AppDbContext db, int id) =>
    await db.Empleados.FindAsync(id) is { } e
        ? Results.Ok(MapEmpleado(e))
        : Results.NotFound(new { mensaje = "No existe un empleado con ese id." }));

empleados.MapPost("/", async (AppDbContext db, GuardarEmpleadoDto dto) =>
{
    if (await db.Empleados.AnyAsync(e => e.Documento == dto.Documento))
        return Results.Conflict(new { mensaje = $"Ya hay un empleado con el documento {dto.Documento}." });

    var e = new Empleado
    {
        Documento = dto.Documento.Trim(),
        Nombres = dto.Nombres.Trim(),
        Apellidos = dto.Apellidos.Trim(),
        Cargo = dto.Cargo,
        Area = dto.Area,
        Email = dto.Email,
        FechaIngreso = dto.FechaIngreso,
        FechaRetiro = dto.FechaRetiro,
        JornadaSemanalHoras = dto.JornadaSemanalHoras,
        Pin = string.IsNullOrWhiteSpace(dto.Pin)
            ? dto.Documento[Math.Max(0, dto.Documento.Length - 4)..]
            : dto.Pin,
        Activo = dto.Activo
    };

    db.Empleados.Add(e);
    await db.SaveChangesAsync();
    return Results.Created($"/api/empleados/{e.Id}", MapEmpleado(e));
}).RequireAuthorization("SoloAdmin");

empleados.MapPut("/{id:int}", async (AppDbContext db, int id, GuardarEmpleadoDto dto) =>
{
    var e = await db.Empleados.FindAsync(id);
    if (e is null) return Results.NotFound();

    e.Documento = dto.Documento.Trim();
    e.Nombres = dto.Nombres.Trim();
    e.Apellidos = dto.Apellidos.Trim();
    e.Cargo = dto.Cargo;
    e.Area = dto.Area;
    e.Email = dto.Email;
    e.FechaIngreso = dto.FechaIngreso;
    e.FechaRetiro = dto.FechaRetiro;
    e.JornadaSemanalHoras = dto.JornadaSemanalHoras;
    e.Activo = dto.Activo;
    if (!string.IsNullOrWhiteSpace(dto.Pin)) e.Pin = dto.Pin;

    await db.SaveChangesAsync();
    return Results.Ok(MapEmpleado(e));
}).RequireAuthorization("SoloAdmin");

empleados.MapDelete("/{id:int}", async (AppDbContext db, int id) =>
{
    var e = await db.Empleados.FindAsync(id);
    if (e is null) return Results.NotFound();

    e.Activo = false;
    e.FechaRetiro ??= DateOnly.FromDateTime(DateTime.Today);
    await db.SaveChangesAsync();
    return Results.Ok(new { mensaje = $"{e.NombreCompleto} quedó inactivo. Su historial se conserva." });
}).RequireAuthorization("SoloAdmin");

// =====================================================================
//  Reloj / marcaciones
// =====================================================================

var marcaciones = app.MapGroup("/api/marcaciones").WithTags("Marcaciones").RequireAuthorization("Autenticado");

marcaciones.MapGet("/estado/{empleadoId:int}", async (AppDbContext db, int empleadoId, ClaimsPrincipal user) =>
{
    // Un empleado solo puede ver su propio estado
    if (user.FindFirstValue("role") == "Empleado")
    {
        var miEmpleadoId = user.FindFirstValue("empleadoId");
        if (miEmpleadoId != empleadoId.ToString())
            return Results.Forbid();
    }

    var e = await db.Empleados.FindAsync(empleadoId);
    if (e is null) return Results.NotFound();

    var p = await db.Parametros.AsNoTracking().FirstAsync();
    var calc = await CalculadoraAsync(db, p);
    var ahora = Ahora(p);
    var hoy = DateOnly.FromDateTime(ahora);
    var inicioSemana = hoy.AddDays(-(((int)hoy.DayOfWeek + 6) % 7));

    var abierta = await db.Marcaciones
        .Where(m => m.EmpleadoId == empleadoId && m.Salida == null)
        .OrderByDescending(m => m.Entrada)
        .FirstOrDefaultAsync();

    var deHoy = await db.Marcaciones.AsNoTracking()
        .Where(m => m.EmpleadoId == empleadoId && m.FechaJornada == hoy && m.Salida != null)
        .ToListAsync();

    var deSemana = await db.Marcaciones.AsNoTracking()
        .Where(m => m.EmpleadoId == empleadoId && m.FechaJornada >= inicioSemana && m.Salida != null)
        .ToListAsync();

    return Results.Ok(new EstadoRelojDto(
        e.Id, e.NombreCompleto,
        abierta is not null, abierta?.Entrada,
        calc.Calcular(deHoy).Total, calc.Calcular(deSemana).Total,
        abierta is not null ? "Turno en curso" : "Sin turno abierto"));
});

marcaciones.MapPost("/entrada", async (AppDbContext db, MarcarDto dto) =>
{
    var e = await db.Empleados.FindAsync(dto.EmpleadoId);
    if (e is null || !e.Activo)
        return Results.BadRequest(new { mensaje = "El empleado no existe o está inactivo." });

    if (!string.IsNullOrEmpty(e.Pin) && dto.Pin is not null && dto.Pin != e.Pin)
        return Results.BadRequest(new { mensaje = "El PIN no coincide." });

    if (await db.Marcaciones.AnyAsync(m => m.EmpleadoId == e.Id && m.Salida == null))
        return Results.Conflict(new { mensaje = "Ya hay un turno abierto. Marca la salida antes de una nueva entrada." });

    var p = await db.Parametros.AsNoTracking().FirstAsync();
    var ahora = Ahora(p);

    var m = new Marcacion
    {
        EmpleadoId = e.Id,
        Entrada = ahora,
        FechaJornada = DateOnly.FromDateTime(ahora),
        Origen = OrigenMarcacion.Reloj,
        Observacion = dto.Observacion
    };

    db.Marcaciones.Add(m);
    await db.SaveChangesAsync();
    return Results.Ok(new { mensaje = $"Entrada registrada a las {ahora:HH:mm}.", marcacionId = m.Id, hora = ahora });
});

marcaciones.MapPost("/salida", async (AppDbContext db, MarcarDto dto) =>
{
    var e = await db.Empleados.FindAsync(dto.EmpleadoId);
    if (e is null) return Results.NotFound();

    if (!string.IsNullOrEmpty(e.Pin) && dto.Pin is not null && dto.Pin != e.Pin)
        return Results.BadRequest(new { mensaje = "El PIN no coincide." });

    var abierta = await db.Marcaciones
        .Where(m => m.EmpleadoId == e.Id && m.Salida == null)
        .OrderByDescending(m => m.Entrada)
        .FirstOrDefaultAsync();

    if (abierta is null)
        return Results.Conflict(new { mensaje = "No hay un turno abierto para cerrar." });

    var p = await db.Parametros.AsNoTracking().FirstAsync();
    var ahora = Ahora(p);

    abierta.Salida = ahora;
    if (dto.Observacion is not null) abierta.Observacion = dto.Observacion;
    await db.SaveChangesAsync();

    var calc = await CalculadoraAsync(db, p);
    return Results.Ok(new
    {
        mensaje = $"Salida registrada a las {ahora:HH:mm}.",
        horas = calc.Calcular(abierta).Total,
        hora = ahora
    });
});

marcaciones.MapGet("/", async (AppDbContext db, int? empleadoId, DateOnly? desde, DateOnly? hasta) =>
{
    var p = await db.Parametros.AsNoTracking().FirstAsync();
    var calc = await CalculadoraAsync(db, p);

    var q = db.Marcaciones.AsNoTracking().Include(m => m.Empleado).AsQueryable();
    if (empleadoId is not null) q = q.Where(m => m.EmpleadoId == empleadoId);
    if (desde is not null) q = q.Where(m => m.FechaJornada >= desde);
    if (hasta is not null) q = q.Where(m => m.FechaJornada <= hasta);

    var lista = await q.OrderByDescending(m => m.Entrada).Take(500).ToListAsync();
    return Results.Ok(lista.Select(m => ReporteService.Mapear(m, calc)));
}).RequireAuthorization("AdminOSupervisor");

marcaciones.MapPost("/", async (AppDbContext db, RegistroManualDto dto) =>
{
    if (dto.Salida is not null && dto.Salida <= dto.Entrada)
        return Results.BadRequest(new { mensaje = "La salida debe ser posterior a la entrada." });

    var m = new Marcacion
    {
        EmpleadoId = dto.EmpleadoId,
        Entrada = dto.Entrada,
        Salida = dto.Salida,
        FechaJornada = DateOnly.FromDateTime(dto.Entrada),
        Origen = OrigenMarcacion.RegistroManual,
        Observacion = dto.Observacion,
        AjustadaPor = dto.Usuario ?? "admin",
        AjustadaEn = DateTime.UtcNow
    };

    db.Marcaciones.Add(m);
    await db.SaveChangesAsync();
    return Results.Created($"/api/marcaciones/{m.Id}", new { m.Id });
}).RequireAuthorization("AdminOSupervisor");

marcaciones.MapPut("/{id:int}", async (AppDbContext db, int id, RegistroManualDto dto) =>
{
    var m = await db.Marcaciones.FindAsync(id);
    if (m is null) return Results.NotFound();

    if (dto.Salida is not null && dto.Salida <= dto.Entrada)
        return Results.BadRequest(new { mensaje = "La salida debe ser posterior a la entrada." });

    m.Entrada = dto.Entrada;
    m.Salida = dto.Salida;
    m.FechaJornada = DateOnly.FromDateTime(dto.Entrada);
    m.Observacion = dto.Observacion;
    m.AjustadaPor = dto.Usuario ?? "admin";
    m.AjustadaEn = DateTime.UtcNow;

    await db.SaveChangesAsync();
    return Results.Ok(new { mensaje = "Marcación corregida." });
}).RequireAuthorization("AdminOSupervisor");

marcaciones.MapDelete("/{id:int}", async (AppDbContext db, int id) =>
{
    var m = await db.Marcaciones.FindAsync(id);
    if (m is null) return Results.NotFound();
    db.Marcaciones.Remove(m);
    await db.SaveChangesAsync();
    return Results.Ok(new { mensaje = "Marcación eliminada." });
}).RequireAuthorization("SoloAdmin");

// =====================================================================
//  Ausencias
// =====================================================================

var ausencias = app.MapGroup("/api/ausencias").WithTags("Ausencias").RequireAuthorization("AdminOSupervisor");

app.MapGet("/api/tipos-ausencia", async (AppDbContext db) =>
    (await db.TiposAusencia.AsNoTracking().Where(t => t.Activo).OrderBy(t => t.Nombre).ToListAsync())
        .Select(t => new TipoAusenciaDto(t.Id, t.Codigo, t.Nombre, t.Remunerada, t.CuentaAusentismo, t.RequiereSoporte, t.Activo)))
    .WithTags("Ausencias")
    .RequireAuthorization("Autenticado");

ausencias.MapGet("/", async (AppDbContext db, int? empleadoId, DateOnly? desde, DateOnly? hasta) =>
{
    var q = db.Ausencias.AsNoTracking().Include(a => a.Empleado).Include(a => a.TipoAusencia).AsQueryable();
    if (empleadoId is not null) q = q.Where(a => a.EmpleadoId == empleadoId);
    if (desde is not null) q = q.Where(a => a.FechaFin >= desde);
    if (hasta is not null) q = q.Where(a => a.FechaInicio <= hasta);

    var lista = await q.OrderByDescending(a => a.FechaInicio).ToListAsync();
    return Results.Ok(lista.Select(MapAusencia));
});

ausencias.MapPost("/", async (AppDbContext db, GuardarAusenciaDto dto) =>
{
    if (dto.FechaFin < dto.FechaInicio)
        return Results.BadRequest(new { mensaje = "La fecha final no puede ser anterior a la inicial." });

    var solapada = await db.Ausencias.AnyAsync(a =>
        a.EmpleadoId == dto.EmpleadoId &&
        a.FechaInicio <= dto.FechaFin &&
        a.FechaFin >= dto.FechaInicio);

    if (solapada)
        return Results.Conflict(new { mensaje = "Ya hay una ausencia registrada que se cruza con esas fechas." });

    var a = new Ausencia
    {
        EmpleadoId = dto.EmpleadoId,
        TipoAusenciaId = dto.TipoAusenciaId,
        FechaInicio = dto.FechaInicio,
        FechaFin = dto.FechaFin,
        HorasParciales = dto.HorasParciales,
        Justificada = dto.Justificada,
        NumeroSoporte = dto.NumeroSoporte,
        RutaSoporte = dto.RutaSoporte,
        Observacion = dto.Observacion,
        RegistradaPor = dto.Usuario ?? "admin"
    };

    db.Ausencias.Add(a);
    await db.SaveChangesAsync();
    return Results.Created($"/api/ausencias/{a.Id}", new { a.Id });
});

ausencias.MapPut("/{id:int}", async (AppDbContext db, int id, GuardarAusenciaDto dto) =>
{
    var a = await db.Ausencias.FindAsync(id);
    if (a is null) return Results.NotFound();

    a.TipoAusenciaId = dto.TipoAusenciaId;
    a.FechaInicio = dto.FechaInicio;
    a.FechaFin = dto.FechaFin;
    a.HorasParciales = dto.HorasParciales;
    a.Justificada = dto.Justificada;
    a.NumeroSoporte = dto.NumeroSoporte;
    a.RutaSoporte = dto.RutaSoporte;
    a.Observacion = dto.Observacion;

    await db.SaveChangesAsync();
    return Results.Ok(new { mensaje = "Ausencia actualizada." });
});

ausencias.MapDelete("/{id:int}", async (AppDbContext db, int id) =>
{
    var a = await db.Ausencias.FindAsync(id);
    if (a is null) return Results.NotFound();
    db.Ausencias.Remove(a);
    await db.SaveChangesAsync();
    return Results.Ok(new { mensaje = "Ausencia eliminada." });
}).RequireAuthorization("SoloAdmin");

// =====================================================================
//  Reportes  (Admin y Supervisor)
// =====================================================================

var reportes = app.MapGroup("/api/reportes").WithTags("Reportes").RequireAuthorization("AdminOSupervisor");

reportes.MapGet("/mensual", async (ReporteService svc, int anio, int mes, int? empleadoId) =>
    Results.Ok(await svc.MensualAsync(anio, mes, empleadoId)));

reportes.MapGet("/rango", async (ReporteService svc, DateOnly desde, DateOnly hasta, int? empleadoId) =>
    Results.Ok(await svc.PorRangoAsync(desde, hasta, empleadoId)));

reportes.MapGet("/detalle", async (ReporteService svc, int empleadoId, DateOnly desde, DateOnly hasta) =>
    Results.Ok(await svc.DetalleAsync(empleadoId, desde, hasta)));

reportes.MapGet("/mensual.csv", async (ReporteService svc, int anio, int mes, int? empleadoId) =>
{
    var r = await svc.MensualAsync(anio, mes, empleadoId);
    var csv = ReporteService.ExportarCsv(r);
    var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv)).ToArray();
    return Results.File(bytes, "text/csv", $"tiempo-laborado-{anio}-{mes:00}.csv");
});

// =====================================================================
//  Parámetros y festivos  (solo Admin)
// =====================================================================

app.MapGet("/api/parametros", async (AppDbContext db) =>
    await db.Parametros.AsNoTracking().FirstAsync())
    .WithTags("Configuración")
    .RequireAuthorization("AdminOSupervisor");

app.MapPut("/api/parametros", async (AppDbContext db, ParametrosLaborales dto) =>
{
    var p = await db.Parametros.FirstAsync();
    p.JornadaSemanalHoras = dto.JornadaSemanalHoras;
    p.HorasOrdinariasDia = dto.HorasOrdinariasDia;
    p.InicioNocturno = dto.InicioNocturno;
    p.FinNocturno = dto.FinNocturno;
    p.RecargoNocturno = dto.RecargoNocturno;
    p.RecargoDominicalFestivo = dto.RecargoDominicalFestivo;
    p.RecargoExtraDiurna = dto.RecargoExtraDiurna;
    p.RecargoExtraNocturna = dto.RecargoExtraNocturna;
    p.ToleranciaIngresoMinutos = dto.ToleranciaIngresoMinutos;
    p.DescuentoAlmuerzoHoras = dto.DescuentoAlmuerzoHoras;
    p.TurnoMinimoParaDescontarAlmuerzo = dto.TurnoMinimoParaDescontarAlmuerzo;
    p.MaximoHorasTurnoAbierto = dto.MaximoHorasTurnoAbierto;
    p.ZonaHoraria = dto.ZonaHoraria;
    await db.SaveChangesAsync();
    return Results.Ok(p);
}).WithTags("Configuración").RequireAuthorization("SoloAdmin");

app.MapGet("/api/festivos", async (AppDbContext db, int? anio) =>
{
    var q = db.Festivos.AsNoTracking().AsQueryable();
    if (anio is not null) q = q.Where(f => f.Fecha.Year == anio);
    return await q.OrderBy(f => f.Fecha).ToListAsync();
}).WithTags("Configuración").RequireAuthorization("Autenticado");

app.MapPost("/api/festivos/generar/{anio:int}", async (AppDbContext db, int anio) =>
{
    var existentes = await db.Festivos.Where(f => f.Fecha.Year == anio).Select(f => f.Fecha).ToListAsync();
    var nuevos = Seed.CalcularFestivos(anio).Where(f => !existentes.Contains(f.Fecha)).ToList();
    db.Festivos.AddRange(nuevos);
    await db.SaveChangesAsync();
    return Results.Ok(new { mensaje = $"Se agregaron {nuevos.Count} festivos para {anio}.", festivos = nuevos });
}).WithTags("Configuración").RequireAuthorization("SoloAdmin");

app.MapGet("/api/salud", () => Results.Ok(new { estado = "ok", hora = DateTime.UtcNow }));

app.Run();

// ---------- Helpers ----------

DateTime Ahora(ParametrosLaborales p)
{
    try
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById(p.ZonaHoraria);
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
    }
    catch (TimeZoneNotFoundException)
    {
        return DateTime.UtcNow.AddHours(-5);
    }
}

async Task<CalculadoraJornada> CalculadoraAsync(AppDbContext db, ParametrosLaborales p)
{
    var festivos = await db.Festivos.AsNoTracking().Select(f => f.Fecha).ToListAsync();
    return new CalculadoraJornada(p, festivos);
}

// ---------- Mapeos ----------

static EmpleadoDto MapEmpleado(Empleado e) => new(
    e.Id, e.Documento, e.Nombres, e.Apellidos, e.NombreCompleto,
    e.Cargo, e.Area, e.Email, e.FechaIngreso, e.FechaRetiro,
    e.JornadaSemanalHoras, e.Activo);

static AusenciaDto MapAusencia(Ausencia a) => new(
    a.Id, a.EmpleadoId, a.Empleado?.NombreCompleto ?? "",
    a.TipoAusenciaId, a.TipoAusencia?.Codigo ?? "", a.TipoAusencia?.Nombre ?? "",
    a.FechaInicio, a.FechaFin, a.FechaFin.DayNumber - a.FechaInicio.DayNumber + 1,
    a.HorasParciales, a.Justificada, a.NumeroSoporte, a.Observacion);

static UsuarioDto MapUsuario(Usuario u) => new(
    u.Id, u.Email, u.NombreVisible, u.Rol.ToString(), u.Activo, u.EmpleadoId);
