import { useCallback, useEffect, useState } from 'react'
import { api, fecha, hoyISO } from '../api'

const enBlanco = (empleados, tipos) => ({
  id: null,
  empleadoId: empleados[0]?.id ?? '',
  tipoAusenciaId: tipos[0]?.id ?? '',
  fechaInicio: hoyISO(),
  fechaFin: hoyISO(),
  horasParciales: '',
  justificada: true,
  numeroSoporte: '',
  observacion: '',
})

export default function Ausencias({ empleados }) {
  const [tipos, setTipos] = useState([])
  const [lista, setLista] = useState([])
  const [filtroEmpleado, setFiltroEmpleado] = useState('')
  const [formulario, setFormulario] = useState(null)
  const [aviso, setAviso] = useState(null)

  const cargar = useCallback(async () => {
    try {
      const [t, l] = await Promise.all([
        api.ausencias.tipos(),
        api.ausencias.listar({ empleadoId: filtroEmpleado }),
      ])
      setTipos(t)
      setLista(l)
    } catch (e) {
      setAviso({ tipo: 'error', texto: e.message })
    }
  }, [filtroEmpleado])

  useEffect(() => {
    cargar()
  }, [cargar])

  const tipoActual = tipos.find((t) => t.id === Number(formulario?.tipoAusenciaId))

  const guardar = async () => {
    const dto = {
      empleadoId: Number(formulario.empleadoId),
      tipoAusenciaId: Number(formulario.tipoAusenciaId),
      fechaInicio: formulario.fechaInicio,
      fechaFin: formulario.fechaFin,
      horasParciales: formulario.horasParciales ? Number(formulario.horasParciales) : null,
      justificada: formulario.justificada,
      numeroSoporte: formulario.numeroSoporte || null,
      rutaSoporte: null,
      observacion: formulario.observacion || null,
      usuario: 'admin',
    }

    try {
      if (formulario.id) await api.ausencias.actualizar(formulario.id, dto)
      else await api.ausencias.crear(dto)
      setAviso({ tipo: 'ok', texto: 'Ausencia guardada.' })
      setFormulario(null)
      cargar()
    } catch (e) {
      setAviso({ tipo: 'error', texto: e.message })
    }
  }

  const eliminar = async (a) => {
    if (!confirm(`Eliminar la ausencia de ${a.empleado}?`)) return
    try {
      await api.ausencias.eliminar(a.id)
      cargar()
    } catch (e) {
      setAviso({ tipo: 'error', texto: e.message })
    }
  }

  return (
    <>
      <div className="encabezado-seccion">
        <div>
          <h1 className="titulo">Ausencias</h1>
          <p className="subtitulo">
            Incapacidades, permisos, vacaciones y todo lo que resta días a la jornada.
          </p>
        </div>
        <button
          className="btn"
          disabled={!empleados.length || !tipos.length}
          onClick={() => setFormulario(enBlanco(empleados, tipos))}
        >
          Registrar ausencia
        </button>
      </div>

      {aviso && (
        <div className={`aviso ${aviso.tipo === 'error' ? 'aviso-error' : 'aviso-ok'}`}>{aviso.texto}</div>
      )}

      {formulario && (
        <div className="tarjeta" style={{ marginBottom: 18 }}>
          <div className="fila-campos">
            <label className="campo">
              <span className="rotulo">Empleado</span>
              <select
                value={formulario.empleadoId}
                onChange={(e) => setFormulario({ ...formulario, empleadoId: e.target.value })}
              >
                {empleados.map((e) => (
                  <option key={e.id} value={e.id}>{e.nombreCompleto}</option>
                ))}
              </select>
            </label>

            <label className="campo">
              <span className="rotulo">Tipo</span>
              <select
                value={formulario.tipoAusenciaId}
                onChange={(e) => setFormulario({ ...formulario, tipoAusenciaId: e.target.value })}
              >
                {tipos.map((t) => (
                  <option key={t.id} value={t.id}>{t.codigo} — {t.nombre}</option>
                ))}
              </select>
            </label>

            <label className="campo">
              <span className="rotulo">Desde</span>
              <input
                type="date"
                value={formulario.fechaInicio}
                onChange={(e) => setFormulario({ ...formulario, fechaInicio: e.target.value })}
              />
            </label>

            <label className="campo">
              <span className="rotulo">Hasta</span>
              <input
                type="date"
                value={formulario.fechaFin}
                onChange={(e) => setFormulario({ ...formulario, fechaFin: e.target.value })}
              />
            </label>

            <label className="campo">
              <span className="rotulo">Horas (solo permisos parciales)</span>
              <input
                type="number"
                step="0.5"
                min="0"
                placeholder="Déjalo vacío si es día completo"
                value={formulario.horasParciales}
                onChange={(e) => setFormulario({ ...formulario, horasParciales: e.target.value })}
              />
            </label>

            <label className="campo">
              <span className="rotulo">
                Número de soporte {tipoActual?.requiereSoporte ? '(requerido)' : '(opcional)'}
              </span>
              <input
                type="text"
                value={formulario.numeroSoporte}
                onChange={(e) => setFormulario({ ...formulario, numeroSoporte: e.target.value })}
              />
            </label>
          </div>

          <label className="campo" style={{ marginTop: 12 }}>
            <span className="rotulo">Observación</span>
            <textarea
              rows={2}
              value={formulario.observacion}
              onChange={(e) => setFormulario({ ...formulario, observacion: e.target.value })}
            />
          </label>

          <label style={{ display: 'flex', gap: 8, alignItems: 'center', marginTop: 12 }}>
            <input
              type="checkbox"
              checked={formulario.justificada}
              onChange={(e) => setFormulario({ ...formulario, justificada: e.target.checked })}
            />
            <span>La ausencia está justificada</span>
          </label>

          <div className="acciones" style={{ marginTop: 14 }}>
            <button className="btn btn-entrada" onClick={guardar}>Guardar</button>
            <button className="btn" onClick={() => setFormulario(null)}>Cancelar</button>
          </div>
        </div>
      )}

      <div className="tarjeta" style={{ marginBottom: 18 }}>
        <label className="campo" style={{ maxWidth: 320 }}>
          <span className="rotulo">Filtrar por empleado</span>
          <select value={filtroEmpleado} onChange={(e) => setFiltroEmpleado(e.target.value)}>
            <option value="">Todos</option>
            {empleados.map((e) => (
              <option key={e.id} value={e.id}>{e.nombreCompleto}</option>
            ))}
          </select>
        </label>
      </div>

      {!lista.length ? (
        <div className="vacio">No hay ausencias registradas.</div>
      ) : (
        <div className="tabla-envoltura">
          <table className="tabla">
            <thead>
              <tr>
                <th>Empleado</th>
                <th>Tipo</th>
                <th>Desde</th>
                <th>Hasta</th>
                <th style={{ textAlign: 'right' }}>Días</th>
                <th style={{ textAlign: 'right' }}>Horas</th>
                <th>Soporte</th>
                <th>Estado</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {lista.map((a) => (
                <tr key={a.id}>
                  <td>{a.empleado}</td>
                  <td>
                    <span className="etiqueta etiqueta-ausencia">{a.tipoCodigo}</span> {a.tipoNombre}
                  </td>
                  <td className="num">{fecha(a.fechaInicio)}</td>
                  <td className="num">{fecha(a.fechaFin)}</td>
                  <td className="num">{a.horasParciales ? '—' : a.diasCalendario}</td>
                  <td className="num">{a.horasParciales ?? '—'}</td>
                  <td>{a.numeroSoporte || '—'}</td>
                  <td>{a.justificada ? 'Justificada' : <span style={{ color: 'var(--alerta)' }}>Sin justificar</span>}</td>
                  <td>
                    <div className="acciones">
                      <button
                        className="btn btn-menor"
                        onClick={() =>
                          setFormulario({
                            id: a.id,
                            empleadoId: a.empleadoId,
                            tipoAusenciaId: a.tipoAusenciaId,
                            fechaInicio: a.fechaInicio,
                            fechaFin: a.fechaFin,
                            horasParciales: a.horasParciales ?? '',
                            justificada: a.justificada,
                            numeroSoporte: a.numeroSoporte ?? '',
                            observacion: a.observacion ?? '',
                          })
                        }
                      >
                        Editar
                      </button>
                      <button className="btn btn-menor btn-peligro" onClick={() => eliminar(a)}>Eliminar</button>
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
