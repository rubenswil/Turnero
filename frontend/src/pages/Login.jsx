import { useState } from 'react'
import { api } from '../api'
import { guardarSesion } from '../auth'

export default function Login() {
  const [form, setForm] = useState({ email: '', password: '' })
  const [error, setError] = useState(null)
  const [cargando, setCargando] = useState(false)

  async function enviar(e) {
    e.preventDefault()
    setCargando(true)
    setError(null)
    try {
      const datos = await api.auth.login(form.email, form.password)
      guardarSesion(datos)
      window.location.reload()
    } catch (err) {
      setError(err.message)
    } finally {
      setCargando(false)
    }
  }

  return (
    <div className="login-pagina">
      <div className="login-tarjeta">
        <div className="login-marca">
          <span className="login-titulo">Turn<span>ero</span></span>
          <span className="login-subtitulo">Control de tiempo laboral</span>
        </div>

        {error && <div className="aviso aviso-error">{error}</div>}

        <form onSubmit={enviar} className="login-form">
          <div className="campo">
            <label className="rotulo">Correo electrónico</label>
            <input
              type="email"
              autoComplete="email"
              value={form.email}
              onChange={(e) => setForm((f) => ({ ...f, email: e.target.value }))}
              required
            />
          </div>
          <div className="campo">
            <label className="rotulo">Contraseña</label>
            <input
              type="password"
              autoComplete="current-password"
              value={form.password}
              onChange={(e) => setForm((f) => ({ ...f, password: e.target.value }))}
              required
            />
          </div>
          <button type="submit" className="btn btn-entrada" disabled={cargando}>
            {cargando ? 'Ingresando…' : 'Ingresar'}
          </button>
        </form>
      </div>
    </div>
  )
}
