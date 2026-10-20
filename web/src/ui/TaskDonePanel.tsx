import { ActionEnergy } from './ActionEnergy'
import type { ReactNode } from 'react'
import type { Catalog, Citizen, CycleReport, FightReport } from '../api'
import { useI18n } from '../i18n'
import { NotificationToggle } from './NotificationToggle'
import { ResourceIcon, TaskIcon } from './TileIcon'
import { seconds } from './texts'

/** A beast that attacked on the way: who won, the gear used, what broke and what was taken from the carcass. */
export function FightSummary({ fight }: { fight: FightReport }) {
  const i18n = useI18n()
  const { t } = i18n
  const enemy = i18n.enemy(fight.enemy).toLowerCase()
  const gear = [fight.weapon, fight.armor].filter((kind): kind is string => kind !== null).map((kind) => i18n.resource(kind))
  return (
    <div className={`fight-report${fight.won ? ' won' : ''}`}>
      <strong>{t(fight.won ? 'fight.won' : 'fight.lost', { enemy })}</strong>
      <div className="small">
        {t('fight.power', { power: i18n.amount(fight.power) })} / {i18n.amount(fight.enemyPower)} ·{' '}
        {gear.length > 0 ? t('fight.gear', { gear: gear.join(', ') }) : t('fight.unarmed')}
      </div>
      {fight.loot.length > 0 && <div className="small">{t('fight.loot', { goods: i18n.goods(fight.loot) })}</div>}
      {fight.broken.length > 0 && (
        <div className="small warning">{t('fight.broken', { gear: fight.broken.map((kind) => i18n.resource(kind)).join(', ') })}</div>
      )}
      {fight.injured && fight.injuredUntil && (
        <div className="small warning">
          {t('fight.wounded', {
            time: new Date(fight.injuredUntil).toLocaleTimeString(i18n.language, { hour: '2-digit', minute: '2-digit' }),
          })}
        </div>
      )}
    </div>
  )
}

/** Pops up when the player's own cycle finishes; can be hidden to keep watching the village. */
export function TaskDonePanel({
  report,
  citizen,
  catalog,
  now,
  pending,
  error,
  onRenew,
  onChoose,
  onHide,
  hands,
}: {
  /** "In your hands" card, shown when storage was full. */
  hands?: ReactNode
  report: CycleReport
  citizen: Citizen
  catalog: Catalog
  now: number
  pending: boolean
  error: string | null
  onRenew: () => void
  onChoose: () => void
  onHide: () => void
}) {
  const i18n = useI18n()
  const { t } = i18n
  const task = catalog.tasks.find((candidate) => candidate.id === report.taskId)
  const graceEndsAt = citizen.graceEndsAt ? Date.parse(citizen.graceEndsAt) : 0
  const inGrace = now <= graceEndsAt

  return (
    <div className="done-panel" role="dialog" aria-label={t('done.title')}>
      <header className="modal-header">
        <h2>
          <TaskIcon taskId={report.taskId} /> {t('done.title')}
        </h2>
        <button className="close" onClick={onHide} title={t('done.hide')}>
          ✕
        </button>
      </header>
      <div className="done-body">
        <p>
          <strong>{i18n.task(report.taskId)}</strong>
        </p>
        {report.fight && <FightSummary fight={report.fight} />}
        <ActionEnergy citizen={citizen} action={report.taskId} />
        <ul className="done-results">
          {(report.toolsBroken ?? []).map((kind) => (
            <li key={`broke-${kind}`} className="warning">
              {t('done.toolBroke', { tool: i18n.resource(kind) })}
            </li>
          ))}
          {report.produced.map((item) => {
            const lost = report.lost?.find((good) => good.resource === item.resource)?.amount ?? 0
            const carried = report.carried.some((good) => good.resource === item.resource)
            return (
              <li key={item.resource}>
                <ResourceIcon kind={item.resource} scale={1} sharpen={item.sharpen ?? 0} /> {i18n.produced(item.resource, item.amount, item.amount - lost)}
                {(task?.effect === 'Deliver' || report.siteId) && ` ${t('done.toProject')}`}
                {(lost > 0 || carried) && (
                  <span className="warning"> {t(lost > 0 ? 'done.handsFull' : 'done.storageFull')}</span>
                )}
              </li>
            )
          })}
          {report.projectContribution > 0 && report.produced.length === 0 && (
            <li>{t('done.contribution', { amount: i18n.amount(report.projectContribution) })}</li>
          )}
          {report.moraleGained > 0 && <li>{t('done.morale', { value: i18n.amount(report.moraleGained * 100) })}</li>}
          {report.primaryExperience > 0 && (
            <li>
              {t('done.experience', { stat: i18n.stat(report.primary), amount: i18n.amount(report.primaryExperience) })}
              {report.secondaryExperience > 0 &&
                `, ${i18n.stat(report.secondary)} +${i18n.amount(report.secondaryExperience)}`}
            </li>
          )}
          {report.skillExperience > 0 && (
            <li>{t('done.skill', { skill: i18n.skill(report.skill), amount: i18n.amount(report.skillExperience) })}</li>
          )}
          {report.buildingCompleted && (
            <li className="highlight">{t('done.built', { building: i18n.building(report.buildingCompleted).name })}</li>
          )}
          {report.workedHungry && <li className="warning">{t('done.hungry')}</li>}
          {report.hungry && !report.workedHungry && <li className="warning">{t('done.mealMissed')}</li>}
          {report.levelsGained > 0 && <li className="highlight">{t('done.levelUp', { stat: i18n.stat(report.primary) })}</li>}
          {report.skillLevelsGained > 0 && (
            <li className="highlight">{t('done.skillUp', { skill: i18n.skill(report.skill) })}</li>
          )}
          {report.renownGained > 0 && <li>{t('done.renown', { amount: i18n.amount(report.renownGained) })}</li>}
          {report.rareFind && <li className="highlight">{t('done.rare')}</li>}
          {report.tired && <li className="warning">{t('done.tired')}</li>}
          {report.repaired > 0 && <li>{t('report.repaired', { amount: i18n.amount(report.repaired) })}</li>}
          {report.overflowed && report.carried.length === 0 && !(report.fight && !report.fight.won) && (
            <li className="warning">{t('done.overflow')}</li>
          )}
        </ul>
        {hands}
        {inGrace ? (
          <p className="small">{t('done.grace', { seconds: seconds(graceEndsAt - now) })}</p>
        ) : (
          <p className="small muted">{t('done.streakLost')}</p>
        )}
        {error && <p className="error">{error}</p>}
        <NotificationToggle compact />
      </div>
      <div className="done-actions">
        <button className="primary" disabled={pending} onClick={onRenew}>
          {t('done.again')}
        </button>
        <button disabled={pending} onClick={onChoose}>
          {t('done.other')}
        </button>
        <button onClick={onHide}>{t('done.hide')}</button>
      </div>
    </div>
  )
}
