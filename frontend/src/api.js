import { tokenActual, cerrarSesion } from './auth'

const BASE = import.meta.env.VITE_API_URL || 'http://localhost:5080/api'

function cabeceras() {
  const t = tokenActual()
  return {
    'Content-Type': 'application/json',
    ...(t ? { Authorization: `Bearer ${t}` } : {}),
  }
}

async function pedir(ruta, opciones = {}) {
  const res = await fetch(`${BASE}${ruta}`, {
    headers: cabeceras(),
    ...opciones,
  })

  if (res.status === 401) {
    cerrarSesion()
    window.location.reload()
    return
  }

  if (!res.ok) {
    let mensaje = `La solicitud falló (${res.status}).`
    try {
      const cuerpo = await res.json()
      if (cuerpo?.mensaje) mensaje = cuerpo.mensaje
    } catch {
      /* la respuesta no traía JSON */
    }
    throw new Error(mensaje)
  }

  return res.status === 204 ? null : res.json()
}

const qs = (params) => {
  const limpio = Object.entries(params).filter(([, v]) => v !== undefined && v !== null && v !== '')
  return limpio.length ? `?${new URLSearchParams(limpio)}` : ''
}

export const api = {
  auth: {
    login: (email, password) =>
      pedir('/auth/login', { method: 'POST', body: JSON.stringify({ email, password }) }),
    yo: () => pedir('/auth/yo'),
    cambiarPassword: (passwordActual, passwordNuevo) =>
      pedir('/auth/cambiar-password', { method: 'POST', body: JSON.stringify({ passwordActual, passwordNuevo }) }),
  },

  usuarios: {
    listar: () => pedir('/usuarios'),
    crear: (dto) => pedir('/usuarios', { method: 'POST', body: JSON.stringify(dto) }),
    actualizar: (id, dto) => pedir(`/usuarios/${id}`, { method: 'PUT', body: JSON.stringify(dto) }),
    desactivar: (id) => pedir(`/usuarios/${id}`, { method: 'DELETE' }),
  },

  empleados: {
    listar: (incluirInactivos = false) => pedir(`/empleados${qs({ incluirInactivos })}`),
    crear: (dto) => pedir('/empleados', { method: 'POST', body: JSON.stringify(dto) }),
    actualizar: (id, dto) => pedir(`/empleados/${id}`, { method: 'PUT', body: JSON.stringify(dto) }),
    desactivar: (id) => pedir(`/empleados/${id}`, { method: 'DELETE' }),
  },

  reloj: {
    estado: (empleadoId) => pedir(`/marcaciones/estado/${empleadoId}`),
    entrada: (empleadoId, pin) =>
      pedir('/marcaciones/entrada', { method: 'POST', body: JSON.stringify({ empleadoId, pin }) }),
    salida: (empleadoId, pin) =>
      pedir('/marcaciones/salida', { method: 'POST', body: JSON.stringify({ empleadoId, pin }) }),
  },

  marcaciones: {
    listar: (filtros) => pedir(`/marcaciones${qs(filtros)}`),
    crear: (dto) => pedir('/marcaciones', { method: 'POST', body: JSON.stringify(dto) }),
    actualizar: (id, dto) => pedir(`/marcaciones/${id}`, { method: 'PUT', body: JSON.stringify(dto) }),
    eliminar: (id) => pedir(`/marcaciones/${id}`, { method: 'DELETE' }),
  },

  ausencias: {
    tipos: () => pedir('/tipos-ausencia'),
    listar: (filtros) => pedir(`/ausencias${qs(filtros)}`),
    crear: (dto) => pedir('/ausencias', { method: 'POST', body: JSON.stringify(dto) }),
    actualizar: (id, dto) => pedir(`/ausencias/${id}`, { method: 'PUT', body: JSON.stringify(dto) }),
    eliminar: (id) => pedir(`/ausencias/${id}`, { method: 'DELETE' }),
  },

  reportes: {
    mensual: (anio, mes, empleadoId) => pedir(`/reportes/mensual${qs({ anio, mes, empleadoId })}`),
    rango: (desde, hasta, empleadoId) => pedir(`/reportes/rango${qs({ desde, hasta, empleadoId })}`),
    detalle: (empleadoId, desde, hasta) => pedir(`/reportes/detalle${qs({ empleadoId, desde, hasta })}`),
    urlCsv: (anio, mes, empleadoId) => `${BASE}/reportes/mensual.csv${qs({ anio, mes, empleadoId })}`,
  },

  config: {
    parametros: () => pedir('/parametros'),
    guardarParametros: (dto) => pedir('/parametros', { method: 'PUT', body: JSON.stringify(dto) }),
    festivos: (anio) => pedir(`/festivos${qs({ anio })}`),
    generarFestivos: (anio) => pedir(`/festivos/generar/${anio}`, { method: 'POST' }),
  },
}

// ---------- Utilidades de formato ----------

export const horas = (n) => `${Number(n ?? 0).toFixed(2).replace('.', ',')} h`

export const hora = (iso) =>
  iso ? new Date(iso).toLocaleTimeString('es-CO', { hour: '2-digit', minute: '2-digit', hour12: false }) : '—'

export const fecha = (iso) =>
  iso ? new Date(`${iso}T00:00:00`).toLocaleDateString('es-CO', { day: '2-digit', month: 'short', year: 'numeric' }) : '—'

export const hoyISO = () => new Date().toISOString().slice(0, 10)

export const cronometro = (desdeIso, ahora = Date.now()) => {
  const ms = Math.max(0, ahora - new Date(desdeIso).getTime())
  const t = Math.floor(ms / 1000)
  const pad = (n) => String(n).padStart(2, '0')
  return `${pad(Math.floor(t / 3600))}:${pad(Math.floor((t % 3600) / 60))}:${pad(t % 60)}`
}
