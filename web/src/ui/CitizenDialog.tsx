import { useState } from 'react'
import { api, createOperationId, CITIZEN_SUBTITLE_MAXIMUM, type CitizenSummary } from '../api'
import { useI18n } from '../i18n'

/** The Tsar clicked a citizen on the map: appoints them Governor or dismisses them, and writes the line under their name. */
export function CitizenDialog({
  slug,
  citizen,
  canRule,
  admissionEnabled,
  onEnableAdmission,
  onSaved,
  onClose,
}: {
  slug: string
  citizen: CitizenSummary
  canRule: boolean
  admissionEnabled: boolean
  onEnableAdmission: ()=>Promise<void>
  onSaved: (citizen: CitizenSummary) => void
  onClose: () => void
}) {
  const i18n = useI18n()
  const { t } = i18n
  const [subtitle, setSubtitle] = useState(citizen.subtitle ?? '')
  const [pending, setPending] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const act = async (action: () => Promise<CitizenSummary>, close: boolean) => {
    setPending(true)
    setError(null)
    try {
      onSaved(await action())
      if (close) onClose()
    } catch (exception) {
      setError(i18n.error(exception))
    } finally {
      setPending(false)
    }
  }
  const save = (next: string | null) => act(() => api.setSubtitle(slug, citizen.id, next), true)

  return (
    <div className="modal-backdrop" onPointerDown={(event) => event.target === event.currentTarget && onClose()}>
      <div className="modal" role="dialog" aria-label={citizen.name}>
        <header className="modal-header">
          <h2>{citizen.name}</h2>
          <button className="close" onClick={onClose} title={`${t('close')} (Esc)`}>
            ✕
          </button>
        </header>
        <form
          className="modal-body"
          onSubmit={(event) => {
            event.preventDefault()
            if(canRule)void save(subtitle.trim() || null)
          }}
        >
          {canRule&&<label className="check-field">
            <input
              type="checkbox"
              checked={citizen.isGovernor}
              disabled={pending}
              onChange={(event) => {
                const appoint = event.target.checked
                void act(() => api.setGovernor(slug, citizen.id, appoint), false)
              }}
            />
            <span>
              <strong>{t('governor.appoint')}</strong>
              <span className="small muted"> — {t('governor.appointHint')}</span>
            </span>
          </label>}
          {canRule&&!admissionEnabled&&<button type="button" disabled={pending} onClick={()=>void act(async()=>{await onEnableAdmission();return citizen},false)}>{i18n.language==='ru'?'Общие грядки — только назначенным фермерам':'Limit shared beds to appointed farmers'}</button>}
          <label className="check-field farm-access-field"><input type="checkbox" checked={!!citizen.isFarmer} disabled={pending} onChange={event=>void act(()=>api.setFarmAccess(slug,citizen.id,{requestId:createOperationId(),isFarmer:event.target.checked}),false)}/><span>{i18n.language==='ru'?'Назначен фермером':'Appointed farmer'}</span></label>
          {canRule&&citizen.isGovernor&&<label className="check-field farm-access-field"><input type="checkbox" checked={!!citizen.canManageFarm} disabled={pending} onChange={event=>void act(()=>api.setFarmAccess(slug,citizen.id,{requestId:createOperationId(),canManageFarm:event.target.checked}),false)}/><span>{i18n.language==='ru'?'Разрешить назначать фермеров':'May appoint farmers'}</span></label>}
          {canRule&&<><p className="small">{t('subtitle.hint')}</p>
          <label className="field">
            {t('subtitle.label')}
            <input
              value={subtitle}
              onChange={(event) => setSubtitle(event.target.value)}
              maxLength={CITIZEN_SUBTITLE_MAXIMUM}
              placeholder={t('subtitle.placeholder')}
              autoFocus
            />
          </label>
          <div className="chips">
            <button type="submit" className="primary" disabled={pending}>
              {t('subtitle.save')}
            </button>
            {citizen.subtitle && (
              <button type="button" disabled={pending} onClick={() => void save(null)}>
                {t('subtitle.remove')}
              </button>
            )}
          </div></>}
          {error && <p className="error">{error}</p>}
        </form>
      </div>
    </div>
  )
}
