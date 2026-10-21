import { currentEnergy } from '../energy'
import { useEnergy } from './useEnergy'
import type { CSSProperties, ReactNode } from 'react'
import { isInjured, type Catalog, type Citizen, type CitizenSummary, type Structure, type VillageState } from '../api'
import { useI18n } from '../i18n'
import { TABS, TAB_ICONS, tabsFor, type MenuTab } from './menuTabs'
import { NextAction } from './NextAction'
import { ProjectBanner } from './ProjectPanel'
import { ResourceIcon } from './TileIcon'
import { Hint } from './Hint'
import { restHints } from './needHints'
import { resourceHint } from './resourceHint'
import { HUD_RESOURCES } from './texts'
import { WeatherChip } from './WeatherChip'
import { NavIcon } from './NavIcon'

export function TopBar({
  village,
  catalog,
  citizen,
  citizens,
  onForecast,
  wallet,
  rest,
  accountId,
}: {
  village: VillageState
  catalog: Catalog
  citizen: Citizen | null | undefined
  citizens: Map<string, CitizenSummary>
  onForecast: (rain: boolean) => Promise<void>
  accountId?:string
  wallet?: ReactNode
  rest?: ReactNode
}) {
  const i18n = useI18n()
  const { t } = i18n
  const energy = useEnergy(citizen)
  const fighter=village.battles?.filter(b=>!b.finishedAt&&b.phase!=='Warning').flatMap(b=>b.fighters).find(f=>f.isTsar?f.accountId===accountId:f.citizenId===citizen?.id)
  const ru=i18n.language==='ru'
  return (
    <div className="hud-top">
      <div className="hud-top-spacer" />
      <ProjectBanner village={village} />
      <div className="hud-resources" style={{ '--columns': Math.ceil((HUD_RESOURCES.length + 2) / 2) } as CSSProperties}>
        {HUD_RESOURCES.map((kind) => {
          const resource = village.resources.find((r) => r.kind === kind)
          return (
            <span className="resource-chip" key={kind}>
              <Hint text={resourceHint(kind, village, catalog, i18n)} className="resource-hint">
                <span className="hud-resource-icon"><ResourceIcon kind={kind} scale={1.5} /></span>
                <span className="hud-resource-amount">{i18n.amount(resource?.amount ?? 0)}</span>
              </Hint>
            </span>
          )
        })}
        <div className="hud-state-row"><span className="resource-chip">
          <span className="morale-icon" aria-hidden>☺</span><Hint text={t('hint.morale',{value:Math.round(village.morale*100),minimum:i18n.amount(catalog.rules.moraleMinimum*100),maximum:i18n.amount(catalog.rules.moraleMaximum*100)})}>{Math.round(village.morale*100)}</Hint>
        </span>
        {(citizen||accountId)&&<div className="resource-chip personal-vitals" aria-label={ru?'Мои здоровье и силы':'My health and stamina'}>
          <Hint className="vital-line" label={ru?'Показать HP':'Show HP'} text={fighter?`HP: ${Math.ceil(fighter.health)}/${Math.ceil(fighter.maximumHealth)}`:ru?'Боевые HP доступны после вступления в бой.':'Combat HP are available after joining a battle.'}><span aria-hidden>♥</span>{fighter?<progress className="hp" value={fighter.health} max={fighter.maximumHealth} aria-label={ru?'Моё здоровье':'My health'}/>:<span className="vital-empty" aria-hidden/>}</Hint>
          {citizen&&<Hint className="vital-line" label={ru?'Показать силы':'Show stamina'} text={`${ru?'Силы':'Stamina'}: ${Math.floor(energy)}/${Math.floor(citizen.maximumEnergy)}`}><span aria-hidden>⚡</span><progress className="stamina" value={energy} max={citizen.maximumEnergy} aria-label={ru?'Мои силы':'My stamina'}/></Hint>}
        </div>}
        <WeatherChip village={village} catalog={catalog} citizen={citizen} citizens={citizens} onForecast={onForecast} />
        {wallet}
        {rest}
        </div>
      </div>
    </div>
  )
}

/** Header of a HUD card that folds it down to this one line. */
export function FoldHead({ collapsed, onToggle, children }: { collapsed: boolean; onToggle: () => void; children: ReactNode }) {
  const { t } = useI18n()
  return (
    <button
      className="hud-card-head"
      onClick={onToggle}
      aria-expanded={!collapsed}
      title={t(collapsed ? 'hud.expand' : 'hud.collapse')}
    >
      {children}
      <span className="hud-card-fold" aria-hidden>
        {collapsed ? '▸' : '▾'}
      </span>
    </button>
  )
}

export function BottomBar({
  citizen,
  catalog,
  structures,
  now,
  phone,
  tsar,
  ruler,
  hasResult,
  onMenu,
  onInventory,
  onShowResult,
  pending,
  onCancelTask,
  drinks,
  onDrink,
  remedies,
  onHeal,
  inCombat=false,
  journeyBlocked=false,
}: {
  citizen: Citizen | null | undefined
  catalog: Catalog
  /** The village's buildings: hints name only the beds already built. */
  structures: Structure[]
  now: number
  phone: boolean
  tsar: boolean
  /** The Tsar, or a citizen the Tsar appointed Governor: plans buildings and calls votes. */
  ruler: boolean
  hasResult: boolean
  /** "Vote open" while a hidden vote runs, "Call a vote" for the rulers; null hides the button. */
  poll: { label: string; onClick: () => void } | null
  onMenu: (tab: MenuTab) => void
  onInventory: () => void
  onShowResult: () => void
  pending: boolean
  onCancelTask: () => void
  /** Vigor drinks in the citizen's hands and in village storage. */
  drinks: number
  onDrink: () => void
  /** Remedies in the citizen's hands and in village storage. */
  remedies: number
  onHeal: () => void
  inCombat?: boolean
  journeyBlocked?: boolean
}) {
  const { t } = useI18n()
  return (
    <div className="hud-bottom">
      {!inCombat&&!journeyBlocked && <div className="hud-left">
        {!tsar && citizen && !inCombat && (
          <CitizenNeeds
            citizen={citizen}
            catalog={catalog}
            structures={structures}
            now={now}
            drinks={drinks}
            pending={pending}
            onDrink={onDrink}
            remedies={remedies}
            onHeal={onHeal}
          />
        )}
        <div className="hud-status">
          {inCombat?null:!tsar && citizen ? (
            <>
              <NextAction citizen={citizen} catalog={catalog} now={now} compact pending={pending} onCancel={onCancelTask} />
              {hasResult && citizen.state === 'AwaitingRenewal' && (
                <button className="primary" onClick={onShowResult}>
                  {t('hud.results')}
                </button>
              )}
            </>
          ) : !tsar ? (
            <button className="primary" onClick={() => onMenu('character')}>
              {t('hud.join')}
            </button>
          ) : null}
        </div>
      </div>}
      <div className="hud-buttons" style={{ '--hud-button-count': tabsFor(tsar, ruler).filter(tab => tab !== 'settings').length + 1 } as CSSProperties}>
        {phone ? (
          // Settings stay reachable from the tab row inside the menu, keeping the phone bar short.
          tabsFor(tsar, ruler).filter((tab) => tab !== 'settings').map((tab) => (
            <button key={tab} title={t(TABS[tab])} aria-label={t(TABS[tab])} onClick={() => onMenu(tab)}>
              <NavIcon name={TAB_ICONS[tab]}/><span>{tab === 'technologies' ? t('tab.technologies').slice(0, 5) + '.' : t(TABS[tab])}</span>
            </button>
          ))
        ) : (
          <button onClick={() => onMenu('village')}>
            {t('hud.menu')} <kbd>Esc</kbd>
          </button>
        )}
        <button onClick={onInventory}>
          {phone && <NavIcon name="storage"/>}<span>{phone ? t('hud.storage') : t('hud.inventory')}</span> {!phone && <kbd>I</kbd>}
        </button>
      </div>
    </div>
  )
}

/** Food and sleep of the player's citizen, with the numbers behind them. */
function CitizenNeeds({
  citizen,
  catalog,
  structures,
  now,
  drinks,
  pending,
  onDrink,
  remedies,
  onHeal,
}: {
  citizen: Citizen
  catalog: Catalog
  structures: Structure[]
  now: number
  drinks: number
  pending: boolean
  onDrink: () => void
  remedies: number
  onHeal: () => void
}) {
  const i18n = useI18n()
  const { t } = i18n
  const meals = catalog.rules.cyclesPerMeal
  const energy = currentEnergy(citizen, now)
  const drinkReadyAt = citizen.drinkReadyAt ? Date.parse(citizen.drinkReadyAt) : 0
  const cooling = now < drinkReadyAt
  const full = energy >= citizen.maximumEnergy
  const drinkTitle = cooling
    ? t('needs.drinkCooldown', { minutes: Math.ceil((drinkReadyAt - now) / 60_000) })
    : full
      ? t('needs.drinkFull')
      : t('needs.drinkTitle', { energy: citizen.vigorEnergy ?? catalog.rules.vigorDrinkEnergy, minutes: citizen.vigorCooldownMinutes ?? 240 })
  const { rules } = catalog
  const percentOf = (multiplier: number) => i18n.amount(multiplier * 100)
  const mealsDone = Math.min(citizen.cyclesSinceMeal, meals)
  const rest = restHints(citizen, catalog, structures, i18n, energy)

  return (
    <div className="hud-needs">
      <span className={`resource-chip need-summary${citizen.isHungry ? ' needs-warning' : ''}`}>
        <ResourceIcon kind="Food" scale={1.5} />
        <Hint label={t('needs.foodHelp')} text={citizen.isHungry
          ? [t('hint.hungry', {
                  experience: percentOf(rules.hungryExperienceMultiplier),
                  rest: percentOf(rules.hungryRegenerationMultiplier),
                }), t('hint.hungryExperience', { percent: percentOf(rules.hungryExperienceMultiplier) }), t('hint.hungryRest', { percent: percentOf(rules.hungryRegenerationMultiplier) })].join('\n')
          : [t('hint.fed', { cycles: meals }), t('hint.meal', { done: mealsDone, cycles: meals })].join('\n')}>
          {citizen.isHungry ? t('needs.hungry') : `${t('needs.fed')} · ${mealsDone}/${meals}`}
          <span className="hint-mark" aria-hidden>ⓘ</span>
        </Hint>
      </span>
      <span className={`resource-chip need-summary${citizen.cycleTired ? ' needs-warning' : ''}`}>
        <span className="sleep-icon" aria-hidden>
          z<small>z</small>
        </span>
        <Hint label={t('needs.restHelp')} text={[rest.energy, rest.place, rest.perHour, rest.restSpeed].join('\n')}>
          {t(citizen.cycleTired ? 'needs.tiredShort' : 'needs.restShort')} · {t('needs.perHour', { perHour: i18n.amount(citizen.energyPerHour) })}
          <span className="hint-mark" aria-hidden>ⓘ</span>
        </Hint>
      </span>
      {drinks > 0 && (
        <button className="drink-button" disabled={pending || cooling || full} onClick={onDrink} title={drinkTitle}>
          <ResourceIcon kind="VigorDrink" scale={1.5} />
          {t('needs.drink', { energy: citizen.vigorEnergy ?? catalog.rules.vigorDrinkEnergy, count: drinks })}
        </button>
      )}
      {isInjured(citizen, now) && <InjuryChip citizen={citizen} remedies={remedies} pending={pending} onHeal={onHeal} />}
    </div>
  )
}

/** Shown while the citizen is wounded: when they can work again, and the remedy that heals them now. */
export function InjuryChip({
  citizen,
  remedies,
  pending,
  onHeal,
}: {
  citizen: Citizen
  remedies: number
  pending: boolean
  onHeal: () => void
}) {
  const i18n = useI18n()
  const { t } = i18n
  const time = new Date(citizen.injuredUntil!).toLocaleTimeString(i18n.language, { hour: '2-digit', minute: '2-digit' })
  return (
    <>
      <span className="resource-chip needs-warning injury-chip" title={t('injury.hint')}>
        <span className="injury-icon" aria-hidden>
          ✚
        </span>
        {t('injury.title', { time })}
      </span>
      <button
        className="drink-button"
        disabled={pending || remedies === 0}
        onClick={onHeal}
        title={remedies === 0 ? t('injury.noRemedy') : t('injury.hint')}
      >
        <ResourceIcon kind="Remedy" scale={1.5} />
        {t('injury.heal', { count: remedies })}
      </button>
    </>
  )
}
