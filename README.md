# Turnero

Registro de entrada y salida, ausencias y reportes mensuales para equipos pequeños.
Backend en .NET 9 (Minimal API + EF Core 9), frontend en React + Vite.

---

## Arrancar en local

### 1. Backend

```bash
cd backend/Turnero.Api
dotnet restore
dotnet run
```

Queda en `http://localhost:5080`. La documentación interactiva (Scalar) está en
`http://localhost:5080/scalar/v1` y el documento OpenAPI 3.1 en `http://localhost:5080/openapi/v1.json`.

En el primer arranque se crea `turnero.db` (SQLite) y se siembran automáticamente:
- Parámetros de cálculo por defecto
- 12 tipos de ausencia
- 18 festivos de Colombia 2026
- Usuario administrador inicial (`admin@turnero.local` / `Admin1234!`)

> **Cambia la contraseña del admin y la clave JWT antes de pasar a producción.**

### 2. Frontend

```bash
cd frontend
npm install
npm run dev
```

Queda en `http://localhost:5173`. La URL de la API se configura en `frontend/.env`.

### Pasar a SQL Server

En `appsettings.json`:

```json
"Motor": "SqlServer",
"ConnectionStrings": { "Default": "Server=localhost;Database=Turnero;Trusted_Connection=True;TrustServerCertificate=True" }
```

Y genera la migración inicial:

```bash
dotnet tool install --global dotnet-ef
dotnet ef migrations add Inicial
dotnet ef database update
```

Mientras no existan migraciones, el arranque usa `EnsureCreated`. Apenas agregues la
primera migración, el proyecto pasa a aplicarlas automáticamente.

---

## Configuración JWT

En `appsettings.json`:

```json
"Jwt": {
  "Clave": "TU_CLAVE_SECRETA_MIN_32_CARACTERES!!",
  "Emisor": "turnero-api",
  "Audiencia": "turnero-frontend"
}
```

Usa una cadena aleatoria de al menos 32 caracteres para `Clave` en producción.

---

## Autenticación y roles

Todos los endpoints (excepto `/api/salud`) requieren un token JWT:

```
Authorization: Bearer <token>
```

### Login

```http
POST /api/auth/login
Content-Type: application/json

{ "email": "admin@turnero.local", "password": "Admin1234!" }
```

Respuesta:
```json
{ "token": "eyJ...", "rol": "Admin", "nombreVisible": "Administrador", "empleadoId": null }
```

### Roles

| Rol | Descripción |
|---|---|
| `Admin` | Acceso total |
| `Supervisor` | Consultas, reportes y corrección de marcaciones. No puede eliminar ni cambiar configuración |
| `Empleado` | Solo puede marcar entrada/salida y ver su propio estado |

---

## Endpoints

### Auth

| Método | Ruta | Acceso | Descripción |
|---|---|---|---|
| POST | `/api/auth/login` | Público | Obtiene token JWT |
| GET | `/api/auth/yo` | Autenticado | Perfil del usuario actual |
| POST | `/api/auth/cambiar-password` | Autenticado | Cambia la propia contraseña |

### Usuarios

| Método | Ruta | Acceso | Descripción |
|---|---|---|---|
| GET | `/api/usuarios` | Admin | Lista usuarios |
| POST | `/api/usuarios` | Admin | Crea usuario |
| PUT | `/api/usuarios/{id}` | Admin | Actualiza usuario |
| DELETE | `/api/usuarios/{id}` | Admin | Desactiva usuario |

### Empleados

| Método | Ruta | Acceso | Descripción |
|---|---|---|---|
| GET | `/api/empleados` | Autenticado | Lista empleados activos |
| GET | `/api/empleados/{id}` | Autenticado | Detalle de un empleado |
| POST | `/api/empleados` | Admin | Crea empleado |
| PUT | `/api/empleados/{id}` | Admin | Actualiza empleado |
| DELETE | `/api/empleados/{id}` | Admin | Retiro lógico (conserva historial) |

### Marcaciones

| Método | Ruta | Acceso | Descripción |
|---|---|---|---|
| GET | `/api/marcaciones/estado/{id}` | Autenticado* | Estado del turno actual |
| POST | `/api/marcaciones/entrada` | Autenticado | Registra entrada |
| POST | `/api/marcaciones/salida` | Autenticado | Registra salida |
| GET | `/api/marcaciones` | Admin / Supervisor | Historial con filtros |
| POST | `/api/marcaciones` | Admin / Supervisor | Registro manual |
| PUT | `/api/marcaciones/{id}` | Admin / Supervisor | Corrige una marcación |
| DELETE | `/api/marcaciones/{id}` | Admin | Elimina una marcación |

*Un usuario con rol `Empleado` solo puede consultar su propio estado.

### Ausencias

| Método | Ruta | Acceso | Descripción |
|---|---|---|---|
| GET | `/api/tipos-ausencia` | Autenticado | Tipos de ausencia activos |
| GET | `/api/ausencias` | Admin / Supervisor | Lista ausencias con filtros |
| POST | `/api/ausencias` | Admin / Supervisor | Registra ausencia |
| PUT | `/api/ausencias/{id}` | Admin / Supervisor | Actualiza ausencia |
| DELETE | `/api/ausencias/{id}` | Admin | Elimina ausencia |

### Reportes

| Método | Ruta | Acceso | Descripción |
|---|---|---|---|
| GET | `/api/reportes/mensual` | Admin / Supervisor | Reporte mensual |
| GET | `/api/reportes/mensual.csv` | Admin / Supervisor | Exporta CSV (compatible Excel en español) |
| GET | `/api/reportes/rango` | Admin / Supervisor | Reporte por rango de fechas |
| GET | `/api/reportes/detalle` | Admin / Supervisor | Detalle diario de un empleado |

### Configuración

| Método | Ruta | Acceso | Descripción |
|---|---|---|---|
| GET | `/api/parametros` | Admin / Supervisor | Parámetros laborales |
| PUT | `/api/parametros` | Admin | Actualiza parámetros |
| GET | `/api/festivos` | Autenticado | Lista festivos |
| POST | `/api/festivos/generar/{anio}` | Admin | Genera los 18 festivos de un año |
| GET | `/api/salud` | Público | Health check |

---

## Cómo funciona

### Marcación

Un turno es un par entrada/salida. Solo puede haber un turno abierto por persona. Cada
turno guarda su **fecha de jornada** (la del inicio), así un turno que cruza medianoche
sigue contando para el día en que empezó.

Cuando alguien olvida marcar, el administrador corrige desde **Registros**. Toda
corrección queda firmada (`AjustadaPor`, `AjustadaEn`).

### Cálculo de horas

`CalculadoraJornada` reparte el tiempo entre:

| Franja | Criterio |
|---|---|
| Ordinarias | Hasta el tope diario configurado |
| Extra diurnas / nocturnas | Lo que excede ese tope |
| Recargo nocturno | Dentro de la franja nocturna configurable (19:00–06:00) |
| Recargo dominical y festivo | Domingos y fechas de la tabla `Festivos` |
| Recargo nocturno dominical | Cuando coinciden las dos condiciones |

El almuerzo se descuenta del centro del turno cuando este supera el mínimo configurado.

**El reporte entrega horas, no dinero.** Los porcentajes de recargo se guardan en
`ParametrosLaborales` como referencia; nómina liquida con los valores vigentes.

### Recargos por defecto (Colombia)

| Concepto | Porcentaje |
|---|---|
| Hora nocturna (19:00–06:00) | 35% |
| Dominical / festivo | 90% |
| Hora extra diurna | 25% |
| Hora extra nocturna | 75% |

Todos configurables desde `PUT /api/parametros`.

### Ausencias

Doce tipos precargados. Cada tipo define si es remunerado, si exige soporte y si cuenta
para el indicador de ausentismo (vacaciones y compensatorios no cuentan). El sistema
bloquea rangos que se crucen con una ausencia ya registrada del mismo empleado.

### Reportes

`GET /api/reportes/mensual?anio=2026&mes=8` devuelve por empleado: días trabajados,
horas esperadas vs. trabajadas, desglose por franja, días perdidos e índice de ausentismo.
Incluye alertas automáticas: turnos sin cerrar y personas con exceso de horas extra.

---

## Pendiente para producción

1. **Cierre automático de turnos.** `MaximoHorasTurnoAbierto` está en los parámetros pero
   falta un `BackgroundService` que lo aplique periódicamente.
2. **Archivos de soporte.** `RutaSoporte` está en el modelo; falta el endpoint de carga
   para adjuntar incapacidades en PDF.
3. **Exportar a Excel real.** El CSV cubre el 90% de los casos; ClosedXML sobre el mismo
   `ReporteMensualDto` si se necesita formato.
4. **Bitácora de auditoría.** Hoy se firma quién ajustó cada marcación pero no se guarda
   el valor anterior. Una tabla `AuditoriaMarcacion` cerraría ese hueco.
5. **Marcación desde el celular.** El siguiente paso sería geocerca o QR por sede.

---

## Estructura

```
backend/Turnero.Api/
  Domain/Entities.cs                Empleado, Marcacion, Ausencia, TipoAusencia,
                                    Festivo, ParametrosLaborales, Usuario
  Data/AppDbContext.cs              Mapeo EF Core
  Data/Seed.cs                      Datos iniciales + generador de festivos (Ley Emiliani)
  Services/CalculadoraJornada.cs    Reparto de horas por franja
  Services/ReporteService.cs        Reportes y exportación CSV
  Services/TokenService.cs          Generación de tokens JWT
  Contracts/Dtos.cs                 Contratos de la API
  Program.cs                        Endpoints y configuración

frontend/src/
  api.js                Cliente y utilidades de formato
  App.jsx               Navegación
  pages/Reloj.jsx       Marcación con cronómetro y cinta de jornada
  pages/Registros.jsx   Histórico y correcciones
  pages/Ausencias.jsx   Ausencias
  pages/Reportes.jsx    Reporte mensual y descarga
  pages/Equipo.jsx      Empleados
  pages/Ajustes.jsx     Parámetros de cálculo y festivos
```
