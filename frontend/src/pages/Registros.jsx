import { useCallback, useEffect, useState } from 'react'
import { api, hora, horas, hoyISO } from '../api'

const inicioDeMes = () => `${hoyISO().slice(0, 8)}01`

const paraInput = (iso) => (iso ? iso.slice(0, 16) : '')

export default function Registros({ empleados }) {
  const [filtros, setFiltros] = useState({ empleadoId: '', desde: inicioDeMes(), hasta: hoyISO() })
  const [lista, setLista] = useState([])
  const [aviso, setAviso] = useState(null)
  const [edicion, setEdicion] = useState(null)

  const cargar = useCallback(async () => {
    try {
      setLista(await api.marcaciones.listar(filtros))
    } catch (e) {
      setAviso({ tipo: 'error', texto: e.message })
    }
  }, [filtros])

  useEffect(() => {
    cargar()
  }, [cargar])

  const nuevo = () =>
    setEdicion({
      id: null,
      empleadoId: empleados[0]?.id ?? '',
      entrada: `${hoyISO()}T08:00`,
      salida: `${hoyISO()}T17:00`,
      observacion: '',
    })

  const guardar = async () => {
    const dto = {
      empleadoId: Number(edicion.empleadoId),
      entrada: edicion.entrada,
      salida: edicion.salida || null,
      observacion: edicion.observacion,
      usuario: 'admin',
    }

    try {
      if (edicion.id) await api.marcaciones.actualizar(edicion.id, dto)
      else await api.marcaciones.crear(dto)
      setAviso({ tipo: 'ok', texto: edicion.id ? 'Marcación corregida.' : 'Marcación registrada.' })
      setEdicion(null)
      cargar()
    } catch (e) {
      setAviso({ tipo: 'error', texto: e.message })
    }
  }

  const eliminar = async (m) => {
    if (!confirm(`Eliminar la marcación de ${m.empleado} del ${m.fechaJornada}?`)) return
    try {
      await api.marcaciones.eliminar(m.id)
      setAviso({ tipo: 'ok', texto: 'Marcación eliminada.' })
      cargar()
    } catch (e) {
      setAviso({ tipo: 'error', texto: e.message })
    }
  }

  return (
    <>
      <div className="encabezado-seccion">
        <div>
          <h1 className="titulo">Registros de marcación</h1>
          <p className="subtitulo">Consulta el histórico y corrige lo que quedó mal marcado.</p>
        </div>
        <button className="btn" onClick={nuevo} disabled={!empleados.length}>
          Registrar manualmente
        </button>
      </div>

      {aviso && (
        <div className={`aviso ${aviso.tipo === 'error' ? 'aviso-error' : 'aviso-ok'}`}>{aviso.texto}</div>
      )}

      <div className="tarjeta" style={{ marginBottom: 18 }}>
        <div className="fila-campos">
          <label className="campo">
            <span className="rotulo">Empleado</span>
            <select
              value={filtros.empleadoId}
              onChange={(ev) => setFiltros({ ...filtros, empleadoId: ev.target.value })}
            >
              <option value="">Todos</option>
              {empleados.map((e) => (
                <option key={e.id} value={e.id}>{e.nombreCompleto}</option>
              ))}
            </select>
          </label>
          <label className="campo">
            <span className="rotulo">Desde</span>
            <input type="date" value={filtros.desde} onChange={(ev) => setFiltros({ ...filtros, desde: ev.target.value })} />
          </label>
          <label className="campo">
            <span className="rotulo">Hasta</span>
            <input type="date" value={filtros.hasta} onChange={(ev) => setFiltros({ ...filtros, hasta: ev.target.value })} />
          </label>
        </div>
      </div>

      {edicion && (
        <div className="tarjeta" style={{ marginBottom: 18 }}>
          <h2 className="titulo" style={{ fontSize: 18 }}>
            {edicion.id ? 'Corregir marcación' : 'Nueva marcación'}
          </h2>
          <div className="fila-campos" style={{ marginTop: 12 }}>
            <label className="campo">
              <span className="rotulo">Empleado</span>
              <select value={edicion.empleadoId} onChange={(ev) => setEdicion({ ...edicion, empleadoId: ev.target.value })}>
                {empleados.map((e) => (
                  <option key={e.id} value={e.id}>{e.nombreCompleto}</option>
                ))}
              </select>
            </label>
            <label className="campo">
              <span className="rotulo">Entrada</span>
              <input type="datetime-local" value={edicion.entrada} onChange={(ev) => setEdicion({ ...edicion, entrada: ev.target.value })} />
            </label>
            <label className="campo">
              <span className="rotulo">Salida</span>
              <input type="datetime-local" value={edicion.salida} onChange={(ev) => setEdicion({ ...edicion, salida: ev.target.value })} />
            </label>
            <label className="campo">
              <span className="rotulo">Motivo del ajuste</span>
              <input
                type="text"
                placeholder="Olvidó marcar la salida"
                value={edicion.observacion ?? ''}
                onChange={(ev) => setEdicion({ ...edicion, observacion: ev.target.value })}
              />
            </label>
          </div>
          <div className="acciones" style={{ marginTop: 14 }}>
            <button className="btn btn-entrada" onClick={guardar}>Guardar</button>
            <button className="btn" onClick={() => setEdicion(null)}>Cancelar</button>
          </div>
        </div>
      )}

      {!lista.length ? (
        <div className="vacio">No hay marcaciones en ese rango.</div>
      ) : (
        <div className="tabla-envoltura">
          <table className="tabla">
            <thead>
              <tr>
                <th>Fecha</th>
                <th>Empleado</th>
                <th>Entrada</th>
                <th>Salida</th>
                <th style={{ textAlign: 'right' }}>Horas</th>
                <th>Origen</th>
                <th>Nota</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {lista.map((m) => (
                <tr key={m.id}>
                  <td className="num">{m.fechaJornada}</td>
                  <td>{m.empleado}</td>
                  <td className="num">{hora(m.entrada)}</td>
                  <td className="num">
                    {m.estaAbierta ? <span className="etiqueta etiqueta-activa">en curso</span> : hora(m.salida)}
                  </td>
                  <td className="num">{m.estaAbierta ? '—' : horas(m.horasTotales)}</td>
                  <td>
                    <span className="etiqueta">{m.origen === 'Reloj' ? 'reloj' : 'manual'}</span>
                  </td>
                  <td style={{ whiteSpace: 'normal', maxWidth: 220 }}>{m.observacion || ''}</td>
                  <td>
                    <div className="acciones">
                      <button
                        className="btn btn-menor"
                        onClick={() =>
                          setEdicion({
                            id: m.id,
                            empleadoId: m.empleadoId,
                            entrada: paraInput(m.entrada),
                            salida: paraInput(m.salida),
                            observacion: m.observacion ?? '',
                          })
                        }
                      >
                        Corregir
                      </button>
                      <button className="btn btn-menor btn-peligro" onClick={() => eliminar(m)}>
                        Eliminar
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
