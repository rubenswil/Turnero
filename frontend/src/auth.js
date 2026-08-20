const CLAVE = 'turnero_sesion'

export function guardarSesion(datos) {
  localStorage.setItem(CLAVE, JSON.stringify(datos))
}

export function sesion() {
  try { return JSON.parse(localStorage.getItem(CLAVE)) } catch { return null }
}

export function cerrarSesion() {
  localStorage.removeItem(CLAVE)
}

export function tokenActual() {
  return sesion()?.token ?? null
}
