import { useEffect, useState } from 'react'
import { api, fecha } from '../api'

export default function Ajustes() {
  const [p, setP] = useState(null)
  const [festivos, setFestivos] = useState([])
  const [anio, setAnio] = useState(new Date().getFullYear())
  const [aviso, setAviso] = useState(null)

  useEffect(() => {
    api.config.parametros().then(setP).catch((e) => setAviso({ tipo: 'error', texto: e.message }))
  }, [])

  useEffect(() => {
    api.config.festivos(anio).then(setFestivos).catch(() => setFestivos([]))
  }, [anio])

  const guardar = async () => {
    try {
      setP(await api.config.guardarParametros(p))
      setAviso({ tipo: 'ok', texto: 'Parámetros actualizados. Los reportes ya usan los nuevos valores.' })
    } catch (e) {
      setAviso({ tipo: 'error', texto: e.message })
    }
  }

  const generarFestivos = async () => {
    try {
      const r = await api.config.generarFestivos(anio)
      setAviso({ tipo: 'ok', texto: r.mensaje })
      setFestivos(await api.config.festivos(anio))
    } catch (e) {
      setAviso({ tipo: 'error', texto: e.message })
    }
  }

  if (!p) return <p className="rotulo">Cargando parámetros…</p>

  const campo = (clave, rotulo, tipo = 'number', paso = '0.01') => (
    <label className="campo">
      <span className="rotulo">{rotulo}</span>
      <input
        type={tipo}
        step={tipo === 'number' ? paso : undefined}
        value={tipo === 'time' ? String(p[clave] ?? '').slice(0, 5) : (p[clave] ?? '')}
        onChange={(e) => setP({ ...p, [clave]: tipo === 'number' ? Number(e.target.value) : e.target.value })}
      />
    </label>
  )

  return (
    <>
      <div className="encabezado-seccion">
        <div>
          <h1 className="titulo">Ajustes de cálculo</h1>
          <p className="subtitulo">
            Cuando cambien la jornada legal o los recargos, se corrige aquí. No hay que tocar el código.
          </p>
        </div>
        <button className="btn btn-entrada" onClick={guardar}>Guardar cambios</button>
      </div>

      {aviso && (
        <div className={`aviso ${aviso.tipo === 'error' ? 'aviso-error' : 'aviso-ok'}`}>{aviso.texto}</div>
      )}

      <div className="tarjeta" style={{ marginBottom: 18 }}>
        <h2 className="titulo" style={{ fontSize: 18 }}>Jornada</h2>
        <div className="fila-campos" style={{ marginTop: 12 }}>
          {campo('jornadaSemanalHoras', 'Máximo semanal (horas)', 'number', '0.5')}
          {campo('horasOrdinariasDia', 'Ordinarias por día', 'number', '0.5')}
          {campo('descuentoAlmuerzoHoras', 'Almuerzo descontado (horas)', 'number', '0.25')}
          {campo('turnoMinimoParaDescontarAlmuerzo', 'Turno mínimo para descontar almuerzo', 'number', '0.5')}
          {campo('toleranciaIngresoMinutos', 'Tolerancia de ingreso (minutos)', 'number', '1')}
          {campo('maximoHorasTurnoAbierto', 'Máximo de horas por turno', 'number', '1')}
        </div>
      </div>

      <div className="tarjeta" style={{ marginBottom: 18 }}>
        <h2 className="titulo" style={{ fontSize: 18 }}>Franja nocturna y recargos</h2>
        <div className="fila-campos" style={{ marginTop: 12 }}>
          {campo('inicioNocturno', 'Empieza la noche', 'time')}
          {campo('finNocturno', 'Termina la noche', 'time')}
          {campo('recargoNocturno', 'Recargo nocturno (0,35 = 35%)')}
          {campo('recargoDominicalFestivo', 'Recargo dominical y festivo')}
          {campo('recargoExtraDiurna', 'Extra diurna')}
          {campo('recargoExtraNocturna', 'Extra nocturna')}
        </div>
        <p className="subtitulo" style={{ marginTop: 14, marginBottom: 0 }}>
          Estos porcentajes solo se guardan como referencia para nómina: el reporte entrega las horas separadas
          por franja. Confirma los valores vigentes con tu contador o revisor fiscal antes de liquidar.
        </p>
      </div>

      <div className="tarjeta">
        <div className="encabezado-seccion" style={{ marginBottom: 12 }}>
          <h2 className="titulo" style={{ fontSize: 18 }}>Festivos</h2>
          <div className="acciones">
            <input
              type="number"
              value={anio}
              onChange={(e) => setAnio(Number(e.target.value))}
              style={{ width: 100, padding: '8px 10px', border: '1px solid var(--linea)' }}
            />
            <button className="btn btn-menor" onClick={generarFestivos}>Generar año</button>
          </div>
        </div>

        {!festivos.length ? (
          <div className="vacio">No hay festivos cargados para {anio}. Usa «Generar año».</div>
        ) : (
          <ul style={{ columns: 2, listStyle: 'none', padding: 0, margin: 0, fontSize: 14 }}>
            {festivos.map((f) => (
              <li key={f.id} style={{ padding: '3px 0', breakInside: 'avoid' }}>
                <span className="cifra">{fecha(f.fecha)}</span> — {f.nombre}
              </li>
            ))}
          </ul>
        )}
      </div>
    </>
  )
}
