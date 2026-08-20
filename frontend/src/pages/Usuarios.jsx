import { useCallback, useEffect, useState } from 'react'
import { api } from '../api'

const ROLES = ['Admin', 'Supervisor', 'Empleado']

const enBlanco = () => ({
  id: null,
  email: '',
  nombreVisible: '',
  password: '',
  rol: 'Empleado',
  empleadoId: '',
  activo: true,
})

export default function Usuarios({ empleados }) {
  const [usuarios, setUsuarios] = useState([])
  const [formulario, setFormulario] = useState(null)
  const [aviso, setAviso] = useState(null)

  const cargar = useCallback(async () => {
    try {
      setUsuarios(await api.usuarios.listar())
    } catch (e) {
      setAviso({ tipo: 'error', texto: e.message })
    }
  }, [])

  useEffect(() => { cargar() }, [cargar])

  const set = (campo, valor) => setFormulario((f) => ({ ...f, [campo]: valor }))

  const guardar = async () => {
    const dto = {
      email: formulario.email.trim().toLowerCase(),
      nombreVisible: formulario.nombreVisible.trim(),
      password: formulario.password,
      rol: formulario.rol,
      empleadoId: formulario.empleadoId ? Number(formulario.empleadoId) : null,
    }

    try {
      if (formulario.id) await api.usuarios.actualizar(formulario.id, dto)
      else await api.usuarios.crear(dto)
      setAviso({ tipo: 'ok', texto: 'Usuario guardado.' })
      setFormulario(null)
      cargar()
    } catch (e) {
      setAviso({ tipo: 'error', texto: e.message })
    }
  }

  const desactivar = async (u) => {
    if (!confirm(`Desactivar a ${u.nombreVisible}?`)) return
    try {
      const r = await api.usuarios.desactivar(u.id)
      setAviso({ tipo: 'ok', texto: r.mensaje })
      cargar()
    } catch (e) {
      setAviso({ tipo: 'error', texto: e.message })
    }
  }

  const etiquetaRol = (rol) => {
    if (rol === 'Admin') return 'etiqueta-activa'
    if (rol === 'Supervisor') return ''
    return 'etiqueta-ausencia'
  }

  return (
    <>
      <div className="encabezado-seccion">
        <div>
          <h1 className="titulo">Usuarios</h1>
          <p className="subtitulo">{usuarios.filter(u => u.activo).length} usuarios activos.</p>
        </div>
        <button className="btn" onClick={() => { setFormulario(enBlanco()); setAviso(null) }}>
          Agregar usuario
        </button>
      </div>

      {aviso && (
        <div className={`aviso ${aviso.tipo === 'error' ? 'aviso-error' : 'aviso-ok'}`}>
          {aviso.texto}
        </div>
      )}

      {formulario && (
        <div className="tarjeta" style={{ marginBottom: 18 }}>
          <div className="fila-campos">
            <label className="campo">
              <span className="rotulo">Nombre visible</span>
              <input
                value={formulario.nombreVisible}
                onChange={(e) => set('nombreVisible', e.target.value)}
                placeholder="Ej. Juan Pérez"
              />
            </label>
            <label className="campo">
              <span className="rotulo">Correo electrónico</span>
              <input
                type="email"
                value={formulario.email}
                onChange={(e) => set('email', e.target.value)}
              />
            </label>
            <label className="campo">
              <span className="rotulo">
                {formulario.id ? 'Nueva contraseña (dejar vacío para no cambiar)' : 'Contraseña'}
              </span>
              <input
                type="password"
                value={formulario.password}
                onChange={(e) => set('password', e.target.value)}
                required={!formulario.id}
              />
            </label>
            <label className="campo">
              <span className="rotulo">Rol</span>
              <select value={formulario.rol} onChange={(e) => set('rol', e.target.value)}>
                {ROLES.map((r) => <option key={r} value={r}>{r}</option>)}
              </select>
            </label>
            <label className="campo">
              <span className="rotulo">Empleado vinculado (opcional)</span>
              <select
                value={formulario.empleadoId}
                onChange={(e) => set('empleadoId', e.target.value)}
              >
                <option value="">— Sin vincular —</option>
                {empleados.map((e) => (
                  <option key={e.id} value={e.id}>{e.nombreCompleto}</option>
                ))}
              </select>
            </label>
          </div>

          <div className="acciones" style={{ marginTop: 16 }}>
            <button className="btn btn-entrada" onClick={guardar}>Guardar</button>
            <button className="btn" onClick={() => setFormulario(null)}>Cancelar</button>
          </div>
        </div>
      )}

      {!usuarios.length ? (
        <div className="vacio">No hay usuarios registrados.</div>
      ) : (
        <div className="tabla-envoltura">
          <table className="tabla">
            <thead>
              <tr>
                <th>Nombre</th>
                <th>Correo</th>
                <th>Rol</th>
                <th>Empleado vinculado</th>
                <th>Estado</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {usuarios.map((u) => {
                const emp = empleados.find((e) => e.id === u.empleadoId)
                return (
                  <tr key={u.id} style={{ opacity: u.activo ? 1 : 0.5 }}>
                    <td>{u.nombreVisible}</td>
                    <td>{u.email}</td>
                    <td>
                      <span className={`etiqueta ${etiquetaRol(u.rol)}`}>{u.rol}</span>
                    </td>
                    <td>{emp ? emp.nombreCompleto : <span style={{ color: 'var(--tinta-suave)' }}>—</span>}</td>
                    <td>
                      {u.activo
                        ? <span className="etiqueta etiqueta-activa">Activo</span>
                        : <span className="etiqueta">Inactivo</span>}
                    </td>
                    <td>
                      <div className="acciones">
                        <button
                          className="btn btn-menor"
                          onClick={() => {
                            setFormulario({ ...u, password: '', empleadoId: u.empleadoId ?? '' })
                            setAviso(null)
                          }}
                        >
                          Editar
                        </button>
                        {u.activo && (
                          <button
                            className="btn btn-menor btn-peligro"
                            onClick={() => desactivar(u)}
                          >
                            Desactivar
                          </button>
                        )}
                      </div>
                    </td>
                  </tr>
                )
              })}
            </tbody>
          </table>
        </div>
      )}
    </>
  )
}
