import { useCallback, useEffect, useState } from 'react'
import { api, cronometro, hora, horas, hoyISO } from '../api'

/**
 * Cinta de jornada: las 24 horas del día como una regla.
 * El bloque sólido es el turno cerrado; el rayado, el turno en curso.
 * La franja gris de fondo marca el horario nocturno.
 */
function CintaJornada({ tramos, ahora }) {
  const pos = (fechaHora) => {
    const d = new Date(fechaHora)
    return ((d.getHours() * 60 + d.getMinutes()) / 1440) * 100
  }

  return (
    <div>
      <div className="cinta">
        <div className="cinta-noche" style={{ left: 0, width: `${(6 / 24) * 100}%` }} />
        <div className="cinta-noche" style={{ left: `${(19 / 24) * 100}%`, right: 0 }} />
        {tramos.map((t) => {
          const inicio = pos(t.entrada)
          const fin = t.salida ? pos(t.salida) : ((ahora.getHours() * 60 + ahora.getMinutes()) / 1440) * 100
          return (
            <div
              key={t.id}
              className="cinta-bloque"
              data-abierto={!t.salida}
              style={{ left: `${inicio}%`, width: `${Math.max(fin - inicio, 0.6)}%` }}
              title={`${hora(t.entrada)} — ${t.salida ? hora(t.salida) : 'en curso'}`}
            />
          )
        })}
      </div>
      <div className="cinta-escala">
        <span>00</span><span>06</span><span>12</span><span>18</span><span>24</span>
      </div>
    </div>
  )
}

export default function Reloj({ empleados, ahora, sesion }) {
  const [estados, setEstados] = useState({})
  const [tramos, setTramos] = useState({})
  const [aviso, setAviso] = useState(null)
  const [ocupado, setOcupado] = useState(null)

  const puedeVerTramos = sesion?.rol !== 'Empleado'

  const refrescar = useCallback(async () => {
    if (!empleados.length) return
    try {
      const hoy = hoyISO()
      const lista = await Promise.all(empleados.map((e) => api.reloj.estado(e.id)))
      setEstados(Object.fromEntries(lista.map((e) => [e.empleadoId, e])))

      if (puedeVerTramos) {
        const marcas = await api.marcaciones.listar({ desde: hoy, hasta: hoy })
        setTramos(
          marcas.reduce((acc, m) => {
            ;(acc[m.empleadoId] ||= []).push(m)
            return acc
          }, {})
        )
      }
    } catch (e) {
      setAviso({ tipo: 'error', texto: e.message })
    }
  }, [empleados, puedeVerTramos])

  useEffect(() => {
    refrescar()
    const id = setInterval(refrescar, 60000)
    return () => clearInterval(id)
  }, [refrescar])

  const marcar = async (empleado, accion) => {
    setOcupado(empleado.id)
    try {
      const r = await api.reloj[accion](empleado.id)
      setAviso({ tipo: 'ok', texto: `${empleado.nombreCompleto}: ${r.mensaje}` })
      await refrescar()
    } catch (e) {
      setAviso({ tipo: 'error', texto: `${empleado.nombreCompleto}: ${e.message}` })
    } finally {
      setOcupado(null)
    }
  }

  const activos = Object.values(estados).filter((e) => e.turnoAbierto).length

  return (
    <>
      <div className="encabezado-seccion">
        <div>
          <h1 className="titulo">Reloj de marcación</h1>
          <p className="subtitulo">
            {activos} de {empleados.length} con turno abierto ahora mismo.
          </p>
        </div>
        <button className="btn btn-menor" onClick={refrescar}>Actualizar</button>
      </div>

      {aviso && (
        <div className={`aviso ${aviso.tipo === 'error' ? 'aviso-error' : 'aviso-ok'}`} role="status">
          {aviso.texto}
        </div>
      )}

      {!empleados.length ? (
        <div className="vacio">
          Todavía no hay empleados. Agrega el primero desde la pestaña <strong>Equipo</strong>.
        </div>
      ) : (
        <div className="rejilla">
          {empleados.map((e) => {
            const estado = estados[e.id]
            const abierto = estado?.turnoAbierto
            return (
              <article key={e.id} className="ficha" data-activo={abierto ? 'true' : 'false'}>
                <div>
                  <div className="ficha-nombre">{e.nombreCompleto}</div>
                  <div className="ficha-cargo">{e.cargo || 'Sin cargo asignado'}</div>
                </div>

                <div>
                  <span className="rotulo">{abierto ? 'En turno desde ' + hora(estado.desdeHora) : 'Trabajado hoy'}</span>
                  <div className="ficha-tiempo">
                    {abierto ? cronometro(estado.desdeHora, ahora.getTime()) : horas(estado?.horasHoy ?? 0)}
                  </div>
                </div>

                <CintaJornada tramos={tramos[e.id] ?? []} ahora={ahora} />

                <div className="acciones">
                  <button
                    className="btn btn-entrada"
                    disabled={abierto || ocupado === e.id}
                    onClick={() => marcar(e, 'entrada')}
                  >
                    Marcar entrada
                  </button>
                  <button
                    className="btn btn-salida"
                    disabled={!abierto || ocupado === e.id}
                    onClick={() => marcar(e, 'salida')}
                  >
                    Marcar salida
                  </button>
                </div>

                <div className="rotulo">Semana: {horas(estado?.horasSemana ?? 0)}</div>
              </article>
            )
          })}
        </div>
      )}
    </>
  )
}
