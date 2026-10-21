import { useCallback, useEffect, useState } from 'react'
import { api, type AlmightyView } from '../api'
import { useI18n } from '../i18n'
import { DirectorFinanceControls } from './DirectorFinanceControls'

export function AlmightyPanel({ slug }: { slug: string }) {
  const { language } = useI18n()
  const ru = language === 'ru'
  const [view, setView] = useState<AlmightyView | null>(null)
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  const [opened, setOpened] = useState(false)
  const refresh = useCallback(async () => {
    try { setView(await api.almighty(slug)); setError('') }
    catch { setError(ru ? 'Связь со Всевышним недоступна.' : 'The director is unavailable.') }
  }, [slug, ru])
  useEffect(() => {
    if (!opened) return
    void refresh()
    const timer = setInterval(() => { void refresh() }, 30000)
    return () => clearInterval(timer)
  }, [opened, refresh])
  const act = async (operation: () => Promise<unknown>) => {
    if (busy) return
    setBusy(true)
    try { await operation(); await refresh() }
    catch { setError(ru ? 'Не удалось сохранить. Условия могли измениться; обнови список.' : 'Could not save. Conditions may have changed; refresh the list.') }
    finally { setBusy(false) }
  }
  const title = (option: string) => ({ vigor_stronger:ru?'Усилить взвар':'Stronger vigor drink',vigor_weaker:ru?'Ослабить взвар':'Weaker vigor drink',vigor_frequent:ru?'Сократить перерыв взвара':'Shorter vigor cooldown',vigor_rare:ru?'Увеличить перерыв взвара':'Longer vigor cooldown',economy_report: ru?'Нужды города':'Village needs', rest: ru?'Передышка':'Recovery window', work_notice: ru?'Нужная работа':'Useful work', zombies_small: ru?'Зомби':'Zombies', skeletons_small: ru?'Скелеты':'Skeletons', dungeon: ru?'Общий поход':'Expedition', skeletons_north:ru?'Скелеты с севера':'Skeletons from north', skeletons_west:ru?'Скелеты с запада':'Skeletons from west', skeletons_south:ru?'Скелеты с юга':'Skeletons from south', none:ru?'Наблюдать':'Observe' } as Record<string,string>)[option] ?? view?.proposals.find(p => p.option === option)?.title ?? option
  const status = (value: string) => ({Pending:ru?'Ждёт твоего решения':'Awaiting your decision',Applied:ru?'Принято':'Applied',Rejected:ru?'Отклонено':'Rejected',Expired:ru?'Истёк срок':'Expired',Superseded:ru?'Условия изменились':'Conditions changed',Sent:ru?'Доставлено':'Delivered',Queued:ru?'В очереди':'Queued',Sending:ru?'Отправляется':'Sending',Unknown:ru?'Доставка не подтверждена':'Delivery unconfirmed',Unavailable:ru?'Связь недоступна':'Unavailable'} as Record<string,string>)[value] ?? value
  const when = (value: string | null) => value ? new Date(value).toLocaleString(language) : '—'
  return <section className="card almighty-panel">
    <button type="button" aria-expanded={opened} onClick={() => setOpened(!opened)}>👁 {ru ? 'Всевышний' : 'The Almighty'} {opened ? '▴' : '▾'}</button>
    {opened && <>
      {error && <p role="alert">{error}</p>}
      {!view && !error && <p>{ru ? 'Смотрю за городом…' : 'Watching the village…'}</p>}
      {view && <>
        <p>{view.enabled ? ru ? 'Анализ включён. События требуют твоего согласия.' : 'Analysis is enabled. Events need your approval.' : ru ? 'Серверный анализ пока выключен.' : 'Server analysis is disabled.'}</p>
        <div className="row">{['Propose','Observe','ShadowEconomy','AutoEconomy','Off'].map((mode, i) => <button key={mode} type="button" disabled={busy} className={view.state.mode === mode ? 'active' : ''} onClick={() => void act(() => api.almightyMode(slug, mode))}>{(ru ? ['Предложения','Наблюдение','Теневая экономика','Автоэкономика','Выключить'] : ['Proposals','Observe','Shadow economy','Auto economy','Off'])[i]}</button>)}</div>
        <DirectorFinanceControls slug={slug} ru={ru}/>
        <p><strong>{ru ? 'Замысел:' : 'Intent:'}</strong> {view.state.intent}</p>
        <p className="small">{ru ? 'Показатели раз в минуту. ИИ раз в 15 минут; важные изменения — от 5 минут, максимум 4 анализа в час. Предложения — не чаще раза в 30 минут.' : 'Metrics every minute. AI every 15 minutes; important changes after 5 minutes, at most 4 reviews/hour. Proposals at most every 30 minutes.'}</p>
        <p className="small">{ru ? 'Последний анализ' : 'Last review'}: {when(view.state.lastReviewAt)}<br/>{ru ? 'Следующая плановая проверка' : 'Next scheduled review'}: {when(view.state.nextReviewAt)}<br/>{ru ? 'Анализов за реальные сутки' : 'Reviews today'}: {view.state.callsToday}/96</p>
        {view.state.lastError && <p role="status">{view.state.lastError}</p>}
        {view.state.restUntil && <p>{ru ? 'Передышка до' : 'Rest until'} {when(view.state.restUntil)} ({ru ? 'реальное время' : 'real time'})</p>}
        <h3>{ru ? 'Предложения и решения' : 'Proposals and decisions'}</h3>
        {view.proposals.length === 0 && <p>{ru ? 'Пока нет предложений. Он может решить, что вмешиваться не нужно.' : 'No proposals yet. The director can choose not to intervene.'}</p>}
        {view.proposals.map(p => <article className="card" key={p.id}>
          <strong>{title(p.option)}</strong><p>{p.reason}</p>{p.effect && <p className="small">{ru?"Последствия":"Effects"}: {p.effect}</p>}
          <p className="small">{status(p.status)} · {ru ? 'Батюшка' : 'Chaplain'}: {status(p.noticeState)} · {ru ? 'Действует до' : 'Expires'} {when(p.expiresAt)}</p>
          {p.result && <p role="status">{p.result}</p>}
          {p.status === 'Pending' && Date.parse(p.expiresAt) > Date.now() && <div className="row"><button type="button" disabled={busy} onClick={() => void act(() => api.almightyVote(slug, p.id, true))}>✓ {ru ? 'Согласиться' : 'Approve'}</button><button type="button" disabled={busy} onClick={() => void act(() => api.almightyVote(slug, p.id, false))}>✕ {ru ? 'Отклонить' : 'Reject'}</button></div>}
        </article>)}
        <details><summary>{ru ? 'Журнал наблюдений' : 'Observation log'}</summary>{view.observations.map(o => <p key={o.id}><time>{when(o.at)}</time> · {title(o.option)}<br/>{o.reason}</p>)}</details>
        <button type="button" disabled={busy} onClick={() => void refresh()}>{ru ? 'Обновить' : 'Refresh'}</button>
      </>}
    </>}
  </section>
}
