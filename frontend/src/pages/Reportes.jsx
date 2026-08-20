import { useCallback, useEffect, useState } from 'react'
import { api, horas } from '../api'

const MESES = [
  'Enero', 'Febrero', 'Marzo', 'Abril', 'Mayo', 'Junio',
  'Julio', 'Agosto', 'Septiembre', 'Octubre', 'Noviembre', 'Diciembre',
]

export default function Reportes({ empleados }) {
  const ahora = new Date()
  const [anio, setAnio] = useState(ahora.getFullYear())
  const [mes, setMes] = useState(ahora.getMonth() + 1)
  const [empleadoId, setEmpleadoId] = useState('')
  const [reporte, setReporte] = useState(null)
  const [error, setError] = useState(null)
  const [cargando, setCargando] = useState(false)

  const generar = useCallback(async () => {
    setCargando(true)
    setError(null)
    try {
      setReporte(await api.reportes.mensual(anio, mes, empleadoId || undefined))
    } catch (e) {
      setError(e.message)
    } finally {
      setCargando(false)
    }
  }, [anio, mes, empleadoId])

  useEffect(() => {
    generar()
  }, [generar])

  const anios = Array.from({ length: 6 }, (_, i) => ahora.getFullYear() - 3 + i)

  return (
    <>
      <div className="encabezado-seccion">
        <div>
          <h1 className="titulo">Reporte de tiempo laborado</h1>
          <p className="subtitulo">
            Horas por franja, diferencias contra la jornada pactada e indicador de ausentismo.
          </p>
        </div>
        <div className="acciones">
          <a
            className="btn"
            href={api.reportes.urlCsv(anio, mes, empleadoId || undefined)}
            style={{ textDecoration: 'none' }}
          >
            Descargar CSV
          </a>
          <button className="btn" onClick={() => window.print()}>Imprimir</button>
        </div>
      </div>

      <div className="tarjeta" style={{ marginBottom: 18 }}>
        <div className="fila-campos">
          <label className="campo">
            <span className="rotulo">Mes</span>
            <select value={mes} onChange={(e) => setMes(Number(e.target.value))}>
              {MESES.map((m, i) => (
                <option key={m} value={i + 1}>{m}</option>
              ))}
            </select>
          </label>
          <label className="campo">
            <span className="rotulo">Año</span>
            <select value={anio} onChange={(e) => setAnio(Number(e.target.value))}>
              {anios.map((a) => (
                <option key={a} value={a}>{a}</option>
              ))}
            </select>
          </label>
          <label className="campo">
            <span className="rotulo">Empleado</span>
            <select value={empleadoId} onChange={(e) => setEmpleadoId(e.target.value)}>
              <option value="">Todo el equipo</option>
              {empleados.map((e) => (
                <option key={e.id} value={e.id}>{e.nombreCompleto}</option>
              ))}
            </select>
          </label>
        </div>
      </div>

      {error && <div className="aviso aviso-error">{error}</div>}
      {cargando && <p className="rotulo">Calculando…</p>}

      {reporte && !cargando && (
        <>
          <div className="metricas">
            <div className="metrica">
              <span className="rotulo">Periodo</span>
              <span className="metrica-valor" style={{ fontSize: 18 }}>{reporte.periodo}</span>
            </div>
            <div className="metrica">
              <span className="rotulo">Horas trabajadas</span>
              <span className="metrica-valor">{reporte.resumen.horasTrabajadas.toFixed(1)}</span>
            </div>
            <div className="metrica">
              <span className="rotulo">Horas esperadas</span>
              <span className="metrica-valor">{reporte.resumen.horasEsperadas.toFixed(1)}</span>
            </div>
            <div className="metrica">
              <span className="rotulo">Horas extra</span>
              <span className="metrica-valor">{reporte.resumen.horasExtra.toFixed(1)}</span>
            </div>
            <div className="metrica">
              <span className="rotulo">Días perdidos</span>
              <span className="metrica-valor">{reporte.resumen.diasPerdidos}</span>
            </div>
            <div className="metrica">
              <span className="rotulo">Ausentismo</span>
              <span className="metrica-valor">{reporte.resumen.indiceAusentismoGeneral.toFixed(2)}%</span>
            </div>
          </div>

          {reporte.alertas.map((a, i) => (
            <div className="aviso" key={i}>{a}</div>
          ))}

          {!reporte.filas.length ? (
            <div className="vacio">No hay datos para ese periodo.</div>
          ) : (
            <div className="tabla-envoltura">
              <table className="tabla">
                <thead>
                  <tr>
                    <th>Documento</th>
                    <th>Empleado</th>
                    <th>Cargo</th>
                    <th style={{ textAlign: 'right' }}>Días</th>
                    <th style={{ textAlign: 'right' }}>Esperadas</th>
                    <th style={{ textAlign: 'right' }}>Trabajadas</th>
                    <th style={{ textAlign: 'right' }}>Diferencia</th>
                    <th style={{ textAlign: 'right' }}>Ordinarias</th>
                    <th style={{ textAlign: 'right' }}>Extra día</th>
                    <th style={{ textAlign: 'right' }}>Extra noche</th>
                    <th style={{ textAlign: 'right' }}>Rec. noct.</th>
                    <th style={{ textAlign: 'right' }}>Rec. dom/fest</th>
                    <th style={{ textAlign: 'right' }}>Días aus.</th>
                    <th style={{ textAlign: 'right' }}>Ausentismo</th>
                  </tr>
                </thead>
                <tbody>
                  {reporte.filas.map((f) => (
                    <tr key={f.empleadoId}>
                      <td className="num">{f.documento}</td>
                      <td>{f.empleado}</td>
                      <td>{f.cargo || '—'}</td>
                      <td className="num">{f.diasTrabajados}</td>
                      <td className="num">{f.horasEsperadas.toFixed(1)}</td>
                      <td className="num">{f.horasTrabajadas.toFixed(1)}</td>
                      <td className={`num ${f.diferencia < 0 ? 'negativo' : 'positivo'}`}>
                        {f.diferencia > 0 ? '+' : ''}{f.diferencia.toFixed(1)}
                      </td>
                      <td className="num">{f.ordinarias.toFixed(1)}</td>
                      <td className="num">{f.extraDiurnas.toFixed(1)}</td>
                      <td className="num">{f.extraNocturnas.toFixed(1)}</td>
                      <td className="num">{f.recargoNocturno.toFixed(1)}</td>
                      <td className="num">
                        {(f.recargoDominicalFestivo + f.recargoNocturnoDominicalFestivo).toFixed(1)}
                      </td>
                      <td className="num">{f.diasAusencia}</td>
                      <td className="num">{f.indiceAusentismo.toFixed(2)}%</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}

          <p className="subtitulo" style={{ marginTop: 16 }}>
            Días hábiles del periodo: <strong className="cifra">{reporte.diasHabiles}</strong>. El índice de
            ausentismo es días perdidos sobre días programados, siguiendo la fórmula habitual de los indicadores
            de SST. Las horas se entregan por franja: los porcentajes de recargo los aplica nómina con los
            valores vigentes.
          </p>
        </>
      )}
    </>
  )
}
