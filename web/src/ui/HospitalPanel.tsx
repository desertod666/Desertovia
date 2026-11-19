import { useCallback, useEffect, useState } from 'react'
import { api, type HospitalPayment, type HospitalStatus, type HospitalTier, type VillageState } from '../api'
import { CharacterPortrait } from './CharacterPortrait'
import { useI18n } from '../i18n'
import type { UiKey } from '../i18n/en'
import { formatRecoveryTime } from './recoveryTime'
import dayImage from '../assets/night/hospital.png'

const paymentKey = (slug: string, actor: string) => `desertovia.hospital.payment.${slug}.${actor}`


/** A pending receipt stays bound to the same player, patient and offer across refresh/reconnect. */
export function HospitalPanel({ slug, actorId, citizenId, tsar, village, onVillage, onClose, onUpdated, serverOffsetMs = 0, initialPatientId }:
  { slug: string; actorId: string; citizenId?: string; tsar: boolean; village: VillageState
    onVillage: (state: VillageState) => void; onClose: () => void; onUpdated: () => void; serverOffsetMs?: number; initialPatientId?:string|null }) {
  const i18n = useI18n(), { t } = i18n
  const [status, setStatus] = useState<HospitalStatus | null>(null)
  const [patientId, setPatientId] = useState<string | null>(initialPatientId ?? citizenId ?? null)
  const [confirmation, setConfirmation] = useState<HospitalTier | null>(null)
  const [message, setMessage] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const [now, setNow] = useState(() => Date.now() + serverOffsetMs)
  const [payment, setPayment] = useState<HospitalPayment | null>(() => {
    try { return JSON.parse(localStorage.getItem(paymentKey(slug, actorId)) ?? 'null') } catch { return null }
  })
  const refresh = useCallback(async () => {
    try { setStatus(await api.hospital(slug)) }
    catch { setMessage(t('hospital.unavailable')) }
  }, [slug, t])
  useEffect(() => {
    void refresh()
    const poll = window.setInterval(() => { void refresh() }, 15000)
    const clock = window.setInterval(() => setNow(Date.now() + serverOffsetMs), 1000)
    return () => { clearInterval(poll); clearInterval(clock) }
  }, [refresh, serverOffsetMs])
  const selected = status?.patients.find(p => p.id === patientId)
  const recovery = selected?.recovery ?? (patientId === citizenId ? status?.recovery : null)
  const admitted = !!recovery?.until && Date.parse(recovery.until) > now
  const submit = async (request: HospitalPayment) => {
    if (busy) return
    setBusy(true); setMessage(null)
    try {
      // Persist BEFORE network; storage failure must not create an untrackable purchase.
      localStorage.setItem(paymentKey(slug, actorId), JSON.stringify(request)); setPayment(request)
      const receipt = await api.hospitalHeal(slug, request)
      if (receipt.state !== 'pending') {
        localStorage.removeItem(paymentKey(slug, actorId)); setPayment(null)
      }
      const errorKey = `hospital.${receipt.error}` as UiKey
      setMessage(receipt.state === 'completed' ? t('hospital.completed') : receipt.state === 'refunded' ? t('hospital.refunded')
        : receipt.state === 'pending' ? t('hospital.pending') : t(errorKey) === errorKey ? t('hospital.error') : t(errorKey))
      setConfirmation(null); await refresh(); onUpdated()
    } catch { setMessage(t('hospital.pending')) }
    finally { setBusy(false) }
  }
  const applyRemedy = async () => {
    if (busy) return
    setBusy(true)
    try { await api.heal(slug); await refresh(); onUpdated() }
    catch (e) { setMessage(i18n.error(e)) }
    finally { setBusy(false) }
  }
  const hospital = village.structures.find(s => s.kind === 'Hospital')
  return <div className="modal-backdrop" onPointerDown={e => e.target === e.currentTarget && onClose()}>
    <section className="modal hospital-panel" role="dialog" aria-modal="true" aria-label={t('hospital.title')}>
      <header className="modal-header"><h2>{t('hospital.title')}</h2><button className="close" aria-label={t('close')} onClick={onClose}>✕</button></header>
      <div className="modal-body">
        <div className="hospital-heading"><div className="hospital-portrait" style={{ backgroundImage: `url(${dayImage})` }} />
          <div><strong>{t('hospital.level', { level: status?.level ?? 0 })}</strong><p className="small muted">{t('hospital.balance', { count: status?.balance ?? '—' })}</p></div></div>
        {!hospital && <p className="small muted">{t('hospital.notBuilt')}</p>}
        {(!status || !status.available) && <p role="status" className="warning">{t('hospital.unavailable')}</p>}
        <label className="field">{t('hospital.help')}<select value={patientId ?? ''} onChange={e => { setPatientId(e.target.value); setConfirmation(null) }} disabled={busy || !!payment}>
          {citizenId && <option value={citizenId}>{t('hospital.self')}</option>}
          {!citizenId && <option value="">{t('hospital.help')}</option>}
          {status?.patients.filter(p => p.id !== citizenId).map(p => <option key={p.id} value={p.id}>{p.name}</option>)}
        </select></label>
        <section className="card hospital-recovery">{admitted&&<div className="hospital-bed-patient" aria-label="Пациент лежит на больничной койке">{selected?.appearance?<CharacterPortrait appearance={selected.appearance} scale={3}/>:<span>🛏️</span>}</div>}<strong>{admitted ? recovery?.cause==='battle'?'На лечении после боя':t('hospital.duel') : t('hospital.available')}</strong>
          {admitted && <><p className="hospital-countdown">{t('hospital.until', { time: formatRecoveryTime(recovery?.until, now, i18n.language) })}</p>
          <p className="small">{t('hospital.pause')}</p><p className="small muted">{t('hospital.bonus', { hospital: recovery?.hospitalPercent ?? 0, skill: recovery?.skillPercent ?? 0 })}</p></>}
        </section>
        {message && <p role="status" className="warning">{message}</p>}
        {admitted && recovery?.cause === 'battle' ? <section className="card">
          <p>Ранение после боя лечится снадобьем или проходит со временем. Переноска не меняет срок.</p>
          {patientId === citizenId && <button disabled={busy} onClick={() => void applyRemedy()}>Использовать снадобье</button>}
        </section> : payment ? <button className="primary" disabled={busy} onClick={() => void submit(payment)}>{t(busy ? 'hospital.wait' : 'hospital.retry')}</button>
          : confirmation ? <section className="card" aria-label={t('hospital.confirm', { price: confirmation.price })}>
            <strong>{t('hospital.confirm', { price: confirmation.price })}</strong><p className="small">{t('hospital.tiers')}</p>
            <div className="hospital-actions"><button className="primary" disabled={busy || !patientId || !recovery?.episodeId} onClick={() => {
              if (patientId && recovery?.episodeId) void submit({ patientId, code: confirmation.code, requestId: crypto.randomUUID(), expectedPrice: confirmation.price, episodeId: recovery.episodeId })
            }}>{t('hospital.buy', { price: confirmation.price })}</button><button onClick={() => setConfirmation(null)}>{t('hospital.cancel')}</button></div>
          </section> : <div className="hospital-tiers">{status?.tiers.map(tier => <button className="hospital-tier" key={tier.code}
            disabled={!status.available || !admitted || busy || !!tier.blocked || (status.balance ?? 0) < tier.price} onClick={() => setConfirmation(tier)}>
            <strong>{tier.title}</strong><span>{t('hospital.effect', { min: tier.minHours, max: tier.maxHours })}</span><span>{tier.price} 🪙</span>
          </button>)}</div>}
        <section className="hospital-patients"><h3>{t('hospital.patients', { count: status?.patients.length ?? 0 })}</h3>
          {status?.patients.map(p => <button type="button" className="hospital-patient-card" disabled={busy||!!payment} key={p.id} aria-label={`Пациент ${p.name}`}
            onClick={()=>{setPatientId(p.id);setConfirmation(null)}}>
            <span className="patient-mini-bed">{p.appearance?<CharacterPortrait appearance={p.appearance} scale={2}/>:<span>🛏️</span>}</span>
            <span className="patient-card-info"><strong>{p.name}</strong><span>На лечении {p.recovery.cause==='battle'?'после боя':'после дуэли'}</span><small>{t('hospital.until', { time: formatRecoveryTime(p.recovery.until, now, i18n.language) })}</small></span>
          </button>)}
        </section>
        {tsar && hospital && (status?.level ?? 3) < 3 && !village.sites.some(s => s.plotId === hospital.plotId) &&
          <button disabled={busy} onClick={async () => { setBusy(true); try { onVillage(await api.hospitalUpgrade(slug, hospital.plotId)); await refresh() } catch(e) { setMessage(i18n.error(e)) } finally { setBusy(false) } }}>
            {t('hospital.upgrade', { level: (status?.level ?? 0) + 1 })}</button>}
      </div>
    </section>
  </div>
}
