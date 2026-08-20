import { useCallback, useEffect, useState } from 'react'
import { api } from './api'
import { sesion, cerrarSesion } from './auth'
import Login from './pages/Login'
import Usuarios from './pages/Usuarios'
import Reloj from './pages/Reloj'
import Registros from './pages/Registros'
import Ausencias from './pages/Ausencias'
import Reportes from './pages/Reportes'
import Equipo from './pages/Equipo'
import Ajustes from './pages/Ajustes'

const SECCIONES = [
  { id: 'reloj',     rotulo: 'Reloj',     roles: ['Admin', 'Supervisor', 'Empleado'], Componente: Reloj },
  { id: 'registros', rotulo: 'Registros', roles: ['Admin', 'Supervisor'],             Componente: Registros },
  { id: 'ausencias', rotulo: 'Ausencias', roles: ['Admin', 'Supervisor'],             Componente: Ausencias },
  { id: 'reportes',  rotulo: 'Reportes',  roles: ['Admin', 'Supervisor'],             Componente: Reportes },
  { id: 'equipo',    rotulo: 'Equipo',    roles: ['Admin'],                           Componente: Equipo },
  { id: 'usuarios',  rotulo: 'Usuarios',  roles: ['Admin'],                           Componente: Usuarios },
  { id: 'ajustes',   rotulo: 'Ajustes',   roles: ['Admin'],                           Componente: Ajustes },
]

export default function App() {
  const sesionActual = sesion()

  if (!sesionActual) return <Login />

  return <AppAutenticado sesionActual={sesionActual} />
}

function AppAutenticado({ sesionActual }) {
  const rol = sesionActual.rol
  const seccionesVisibles = SECCIONES.filter((s) => s.roles.includes(rol))

  const [seccion, setSeccion] = useState(seccionesVisibles[0]?.id ?? 'reloj')
  const [empleados, setEmpleados] = useState([])
  const [errorCarga, setErrorCarga] = useState(null)
  const [ahora, setAhora] = useState(new Date())

  const cargarEmpleados = useCallback(async () => {
    try {
      setEmpleados(await api.empleados.listar())
      setErrorCarga(null)
    } catch (e) {
      setErrorCarga(e.message)
    }
  }, [])

  useEffect(() => { cargarEmpleados() }, [cargarEmpleados])

  useEffect(() => {
    const id = setInterval(() => setAhora(new Date()), 1000)
    return () => clearInterval(id)
  }, [])

  // Los empleados de rol Empleado solo ven su propia ficha
  const empleadosVisibles =
    rol === 'Empleado' && sesionActual.empleadoId
      ? empleados.filter((e) => e.id === sesionActual.empleadoId)
      : empleados

  const seccionActual = seccionesVisibles.find((s) => s.id === seccion) ?? seccionesVisibles[0]
  const { Componente } = seccionActual ?? {}

  return (
    <div className="app">
      <header>
        <div className="barra">
          <span className="barra-marca">
            Turnero<span className="barra-descriptor">Control de tiempo</span>
          </span>
          <div className="barra-derecha">
            <span className="barra-reloj">
              {ahora.toLocaleDateString('es-CO', { weekday: 'short', day: '2-digit', month: 'short' })}
              {'  '}
              {ahora.toLocaleTimeString('es-CO', { hour12: false })}
            </span>
            <span className="barra-usuario">
              <span className="barra-usuario-nombre">{sesionActual.nombreVisible}</span>
              <span className="barra-usuario-rol">{rol}</span>
            </span>
            <button
              className="btn-cerrar-sesion"
              title="Cerrar sesión"
              onClick={() => { cerrarSesion(); window.location.reload() }}
            >
              Salir
            </button>
          </div>
        </div>
        <nav className="nav">
          {seccionesVisibles.map((s) => (
            <button
              key={s.id}
              className="nav-item"
              aria-current={seccionActual?.id === s.id ? 'page' : undefined}
              onClick={() => setSeccion(s.id)}
            >
              {s.rotulo}
            </button>
          ))}
        </nav>
      </header>

      <main className="contenido">
        {errorCarga && (
          <div className="aviso aviso-error">
            No se pudo conectar con la API. Verifica que el backend esté corriendo en{' '}
            <code>http://localhost:5080</code>. Detalle: {errorCarga}
          </div>
        )}
        {Componente && (
          <Componente
            empleados={empleadosVisibles}
            recargarEmpleados={cargarEmpleados}
            ahora={ahora}
            sesion={sesionActual}
          />
        )}
      </main>
    </div>
  )
}
