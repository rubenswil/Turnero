import { useState } from 'react'
import { api, fecha, hoyISO } from '../api'

const enBlanco = () => ({
  id: null,
  documento: '',
  nombres: '',
  apellidos: '',
  cargo: '',
  area: '',
  email: '',
  fechaIngreso: hoyISO(),
  fechaRetiro: '',
  jornadaSemanalHoras: 42,
  pin: '',
  activo: true,
})

export default function Equipo({ empleados, recargarEmpleados }) {
  const [formulario, setFormulario] = useState(null)
  const [aviso, setAviso] = useState(null)

  const guardar = async () => {
    const dto = {
      documento: formulario.documento.trim(),
      nombres: formulario.nombres.trim(),
      apellidos: formulario.apellidos.trim(),
      cargo: formulario.cargo || null,
      area: formulario.area || null,
      email: formulario.email || null,
      fechaIngreso: formulario.fechaIngreso,
      fechaRetiro: formulario.fechaRetiro || null,
      jornadaSemanalHoras: Number(formulario.jornadaSemanalHoras),
      pin: formulario.pin || null,
      activo: formulario.activo,
    }

    try {
      if (formulario.id) await api.empleados.actualizar(formulario.id, dto)
      else await api.empleados.crear(dto)
      setAviso({ tipo: 'ok', texto: 'Empleado guardado.' })
      setFormulario(null)
      recargarEmpleados()
    } catch (e) {
      setAviso({ tipo: 'error', texto: e.message })
    }
  }

  const desactivar = async (e) => {
    if (!confirm(`Dar de baja a ${e.nombreCompleto}? Su historial se conserva.`)) return
    try {
      const r = await api.empleados.desactivar(e.id)
      setAviso({ tipo: 'ok', texto: r.mensaje })
      recargarEmpleados()
    } catch (err) {
      setAviso({ tipo: 'error', texto: err.message })
    }
  }

  return (
    <>
      <div className="encabezado-seccion">
        <div>
          <h1 className="titulo">Equipo</h1>
          <p className="subtitulo">{empleados.length} personas activas.</p>
        </div>
        <button className="btn" onClick={() => setFormulario(enBlanco())}>Agregar empleado</button>
      </div>

      {aviso && (
        <div className={`aviso ${aviso.tipo === 'error' ? 'aviso-error' : 'aviso-ok'}`}>{aviso.texto}</div>
      )}

      {formulario && (
        <div className="tarjeta" style={{ marginBottom: 18 }}>
          <div className="fila-campos">
            <label className="campo">
              <span className="rotulo">Documento</span>
              <input value={formulario.documento} onChange={(e) => setFormulario({ ...formulario, documento: e.target.value })} />
            </label>
            <label className="campo">
              <span className="rotulo">Nombres</span>
              <input value={formulario.nombres} onChange={(e) => setFormulario({ ...formulario, nombres: e.target.value })} />
            </label>
            <label className="campo">
              <span className="rotulo">Apellidos</span>
              <input value={formulario.apellidos} onChange={(e) => setFormulario({ ...formulario, apellidos: e.target.value })} />
            </label>
            <label className="campo">
              <span className="rotulo">Cargo</span>
              <input value={formulario.cargo} onChange={(e) => setFormulario({ ...formulario, cargo: e.target.value })} />
            </label>
            <label className="campo">
              <span className="rotulo">Área</span>
              <input value={formulario.area} onChange={(e) => setFormulario({ ...formulario, area: e.target.value })} />
            </label>
            <label className="campo">
              <span className="rotulo">Correo</span>
              <input type="email" value={formulario.email} onChange={(e) => setFormulario({ ...formulario, email: e.target.value })} />
            </label>
            <label className="campo">
              <span className="rotulo">Fecha de ingreso</span>
              <input type="date" value={formulario.fechaIngreso} onChange={(e) => setFormulario({ ...formulario, fechaIngreso: e.target.value })} />
            </label>
            <label className="campo">
              <span className="rotulo">Jornada semanal (horas)</span>
              <input type="number" step="0.5" value={formulario.jornadaSemanalHoras} onChange={(e) => setFormulario({ ...formulario, jornadaSemanalHoras: e.target.value })} />
            </label>
            <label className="campo">
              <span className="rotulo">PIN del reloj</span>
              <input maxLength={6} value={formulario.pin} onChange={(e) => setFormulario({ ...formulario, pin: e.target.value })} placeholder="Últimos 4 del documento" />
            </label>
          </div>

          <div className="acciones" style={{ marginTop: 14 }}>
            <button className="btn btn-entrada" onClick={guardar}>Guardar</button>
            <button className="btn" onClick={() => setFormulario(null)}>Cancelar</button>
          </div>
        </div>
      )}

      {!empleados.length ? (
        <div className="vacio">Agrega al primer empleado para empezar a marcar tiempo.</div>
      ) : (
        <div className="tabla-envoltura">
          <table className="tabla">
            <thead>
              <tr>
                <th>Documento</th>
                <th>Nombre</th>
                <th>Cargo</th>
                <th>Área</th>
                <th>Ingreso</th>
                <th style={{ textAlign: 'right' }}>Jornada</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {empleados.map((e) => (
                <tr key={e.id}>
                  <td className="num">{e.documento}</td>
                  <td>{e.nombreCompleto}</td>
                  <td>{e.cargo || '—'}</td>
                  <td>{e.area || '—'}</td>
                  <td className="num">{fecha(e.fechaIngreso)}</td>
                  <td className="num">{e.jornadaSemanalHoras} h</td>
                  <td>
                    <div className="acciones">
                      <button
                        className="btn btn-menor"
                        onClick={() =>
                          setFormulario({
                            ...e,
                            cargo: e.cargo ?? '',
                            area: e.area ?? '',
                            email: e.email ?? '',
                            fechaRetiro: e.fechaRetiro ?? '',
                            pin: '',
                          })
                        }
                      >
                        Editar
                      </button>
                      <button className="btn btn-menor btn-peligro" onClick={() => desactivar(e)}>
                        Dar de baja
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </>
  )
}
