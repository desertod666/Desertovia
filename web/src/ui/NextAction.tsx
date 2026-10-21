import type { Catalog, Citizen } from '../api'
import { useI18n } from '../i18n'
import { seconds } from './texts'
import {Hint} from './Hint'

/** "Next action in N seconds" status with a progress bar. `onCancel` adds a button that stops the running task. */
export function NextAction({
  citizen,
  catalog,
  now,
  compact,
  pending,
  onCancel,
}: {
  citizen: Citizen
  catalog: Catalog
  now: number
  compact?: boolean
  pending?: boolean
  onCancel?: () => void
}) {
  const i18n = useI18n()
  const { t } = i18n
  if(citizen.encounterRunId)return <div className={`next-action paused ${compact?'compact':''}`} role="status">{i18n.language==='ru'?'Задание ждёт исхода боя':'Work is waiting for the fight outcome'}</div>
  if (citizen.restState === 'Resting') return <div className={`next-action idle ${compact ? 'compact' : ''}`} role="status">☾ {i18n.language === 'ru' ? 'Безопасный отдых' : 'Safe rest'}</div>
  if (citizen.state === 'Paused') return <div className={`next-action paused ${compact ? 'compact' : ''}`} role="status">{citizen.journey?.blockedAt?(i18n.language==='ru'?'Дорога перекрыта · работа ждёт':'Route blocked · work is waiting'):<>✚ {t('hospital.pause')}</>}
    {citizen.journey?.blockedAt&&onCancel&&citizen.restState!=='RestRequested'&&<button disabled={pending} onClick={onCancel} title={t('next.cancelTitle')}>{t('next.cancel')}</button>}
  </div>
  // Construction jobs are not catalog tasks, so the activity is just its id.
  const activity = citizen.taskId
  const endsAt = citizen.cycleEndsAt ? Date.parse(citizen.cycleEndsAt) : 0
  const startedAt = citizen.cycleStartedAt ? Date.parse(citizen.cycleStartedAt) : endsAt - catalog.rules.cycleSeconds * 1000
  const graceEndsAt = citizen.graceEndsAt ? Date.parse(citizen.graceEndsAt) : 0

  if (activity && now < endsAt) {
    const progress = (now - startedAt) / Math.max(1, endsAt - startedAt)
    return (
      <div className={`next-action working ${compact ? 'compact' : ''}`}>
        <div>
          <strong>{i18n.task(activity)}</strong> {t('next.working', { seconds: seconds(endsAt - now) })}
          {citizen.cycleOwnership&&citizen.cycleOwnership!=='Legacy'&&<Hint className="help-button" label={i18n.language==='ru'?'Чья работа и оплата':'Work ownership and pay'} text={citizen.cyclePrivateOutput
            ? (i18n.language==='ru'?'Личные материалы и результат. Зарплаты из казны нет.':'Personal inputs and output. No treasury wages.')
            :citizen.cyclePaymentMode==='Reserved'?(i18n.language==='ru'?'Материалы и результат общине. Оплата принятого цикла уже зарезервирована.':'Community inputs and output. Pay for this accepted cycle is reserved.')
            :citizen.cyclePaymentMode==='Voluntary'?(i18n.language==='ru'?'Добровольная общественная работа без зарплаты. Для оплаты выберите госзаказ до начала работы.':'Voluntary public work without wages. Choose a funded order before starting to receive pay.')
            :(i18n.language==='ru'?'Общественная работа на прежних условиях оплаты.':'Public work under the previous payment terms.')}>ⓘ</Hint>}
          {citizen.cycleTired && <span className="warning"> {t('next.tired')}</span>}
          {onCancel && citizen.restState !== 'RestRequested' && (
            <button className="next-cancel" disabled={pending} onClick={onCancel} title={t('next.cancelTitle')}>
              {t('next.cancel')}
            </button>
          )}
        </div>
        <div className="bar loader">
          <div style={{ width: `${Math.min(100, progress * 100)}%` }} />
        </div>
      </div>
    )
  }

  if (citizen.restState === 'RestRequested') return <div className="next-action" role="status">◷ {i18n.language === 'ru' ? 'Завершаем работу перед отдыхом' : 'Finishing accepted work before rest'}</div>
  if (activity && now <= graceEndsAt) {
    return (
      <div className={`next-action awaiting ${compact ? 'compact' : ''}`}>
        <div>
          <strong>{i18n.task(activity)}</strong> {t('next.awaiting', { seconds: seconds(graceEndsAt - now) })}
        </div>
        <div className="bar running">
          <div />
        </div>
      </div>
    )
  }

  return <div className={`next-action idle ${compact ? 'compact' : ''}`}>{t('next.idle')}</div>
}
