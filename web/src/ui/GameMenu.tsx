import { EconomyPanel } from './EconomyPanel'
import { DefensePanel } from './DefensePanel'
import { AlmightyPanel } from './AlmightyPanel'
import { CivicPanel } from './CivicPanel'
import { useState } from 'react'
import {
  blockedByLoad,
  builtKinds,
  discoveredTechnologies,
  isProjectTask,
  projectWants,
  type BuildingDefinition,
  type Catalog,
  type Citizen,
  type Me,
  type Task,
  type VillageState,
} from '../api'
import { UndeadPanel } from './RaidBanner'
import { DungeonPanel } from './Dungeon'
import type { FeedEntry } from '../hooks'
import { useI18n } from '../i18n'
import type { UiKey } from '../i18n/en'
import { BuildPanel } from './BuildPanel'
import { CharacterSheet } from './CharacterSheet'
import { JoinVillageForm } from './CreateCharacterForm'
import { TABS, tabsFor, type MenuTab } from './menuTabs'
import { NextAction } from './NextAction'
import { ProjectPanel } from './ProjectPanel'
import { QuestPanel } from './QuestPanel'
import { SettingsPanel } from './SettingsPanel'
import { SitesPanel, type JobChoice } from './SitesPanel'
import { TaskList } from './TaskList'
import { TechnologyPanel } from './TechnologyPanel'

type TaskFilter = 'all' | 'gather' | 'project' | 'crafts'

const FILTERS: Record<TaskFilter, { label: UiKey; matches: (task: Task) => boolean }> = {
  all: { label: 'filter.all', matches: () => true },
  gather: {
    label: 'filter.gather',
    matches: (task) => (task.effect === 'Resources' && task.inputs.length === 0) || task.effect === 'Roam',
  },
  project: { label: 'filter.project', matches: isProjectTask },
  crafts: { label: 'filter.crafts', matches: (task) => task.inputs.length > 0 || task.effect === 'Morale' },
}

export interface MenuProps {
  slug: string
  tab: MenuTab
  onTab: (tab: MenuTab) => void
  onHospital: () => void
  onMonastery?:()=>void
  onClose: () => void
  village: VillageState
  population: { total: number; online: number }
  /** The village owner: rules and plans, never works, sees no storage numbers. */
  tsar: boolean
  /** The Tsar, or a citizen the Tsar appointed Governor: plans buildings and calls votes. */
  ruler: boolean
  /** Only known to the owner of a private village. */
  inviteCode: string | null
  onVillage: (state:VillageState)=>void
  onCitizen?: (citizen:Citizen)=>void
  onNewInviteCode: () => Promise<void>
  onPrivatePlace?: (definition: BuildingDefinition) => void
  onPlace: (definition: BuildingDefinition) => void
  onWithdraw: (plotId: string) => Promise<void>
  catalog: Catalog
  me: Me
  citizen: Citizen | null | undefined
  now: number
  pending: boolean
  error: string | null
  onStart: (task: Task) => void
  onStartJob: (choice: JobChoice) => void
  onCancelTask: () => void
  /** Opens the open vote, or lets the rulers call one. */
  onPoll: () => void
  onResearch: () => void
  /** Reloads the account after signing out. */
  onAccountChanged: () => void
  /** Moves the account's character into this village. */
  onJoin: (inviteCode: string | null) => void
  joining: boolean
  joinError: string | null
  feed: FeedEntry[]
  onCancelUndead: () => void
  undeadError: string | null
  onOpenDungeon: () => void
  onCancelDungeon: () => void
  dungeonError: string | null
}

function JoinPrompt(props: MenuProps) {
  const { t } = useI18n()
  if (props.citizen === undefined) return <section className="card">{t('loading')}</section>
  if (props.village.isPrivate) {
    return <JoinVillageForm joining={props.joining} error={props.joinError} onJoin={props.onJoin} />
  }
  return (
    <section className="card">
      {props.joinError ? (
        <>
          <p className="error">{props.joinError}</p>
          <button className="primary" disabled={props.joining} onClick={() => props.onJoin(null)}>
            {t('join.submit')}
          </button>
        </>
      ) : (
        t('join.joining')
      )}
    </section>
  )
}

export function GameMenu(props: MenuProps) {
  const { tab, onTab, onClose, village, catalog, citizen, now, pending, error, onStart, onStartJob, tsar, ruler } = props
  const i18n = useI18n()
  const { t } = i18n
  const [filter, setFilter] = useState<TaskFilter>('all')
  const working = citizen?.cycleEndsAt ? now < Date.parse(citizen.cycleEndsAt) : false
  const disabled = pending || working || !!citizen?.restState && citizen.restState !== 'Active'
  const built = new Set<string>([...builtKinds(village, catalog), ...village.structures.filter(s=>s.ownerAccountId===props.me.id).map(s=>s.kind), ...village.completedProjects])
  const discovered = discoveredTechnologies(village)
  // Project tasks only show while a project runs, and only the ones it has use for.
  const visibleTask = (task: Task) => task.id!=='rescue-wounded' && projectWants(village.project, task)
  const projectTasks = village.project ? catalog.tasks.filter((task) => isProjectTask(task) && visibleTask(task)) : []
  const expedition = catalog.tasks.find((task) => task.id === 'expedition')
  const roam = catalog.tasks.find((task) => task.id === 'roam')
  const sites = citizen && (
    <SitesPanel catalog={catalog} village={village} citizen={citizen} disabled={disabled} onStartJob={onStartJob} />
  )

  return (
    <div className="modal-backdrop" onPointerDown={(event) => event.target === event.currentTarget && onClose()}>
      <div className="modal menu" role="dialog" aria-label={t('menu.title')}>
        <header className="modal-header">
          <nav className="tabs">
            {tabsFor(tsar, ruler).map((key) => (
              <button key={key} className={`tab ${tab === key ? 'active' : ''}`} onClick={() => onTab(key)}>
                {t(TABS[key])}
              </button>
            ))}
          </nav>
          <button className="close" onClick={onClose} aria-label={t('close')} title={`${t('close')} (Esc)`}>
            ✕
          </button>
        </header>

        <div className="modal-body">
          {tab === 'village' && ruler && <DefensePanel slug={props.slug} village={village} ruler tsar={tsar} onVillage={props.onVillage}/>}
          {tab === 'village' && tsar && <AlmightyPanel slug={props.slug}/>}
          {tab === 'village' && (tsar || citizen) && <EconomyPanel village={village} catalog={catalog} citizen={citizen} accountId={props.me.id} onPrivatePlace={props.onPrivatePlace} onCitizen={props.onCitizen}/>}
          {tab==='village'&&(village.bridgeDamage??0)>0&&<p className="error small">{i18n.language==='ru'?'Царский мост повреждён. Восстановление — работа «Ремонт зданий» в заданиях.':'The royal bridge is damaged. Use Repair buildings in Tasks to restore it.'} {village.bridgeDamage}/100</p>}
          {tab === 'village' && <div className="row"><button onClick={props.onHospital}>🏥 Больница</button>{props.onMonastery&&<button onClick={props.onMonastery}>{i18n.language==='ru'?'Монастырь за рекой':'Monastery across the river'}</button>}</div>}
          {tab === 'village' && <section className="card village-population" title={t('chat.populationHelp')}>
            {t('chat.population', props.population)} · ☺ {Math.round(village.morale*100)}% · 👑 {village.castleLevel??0}/3
          </section>}
          {citizen && <NextAction citizen={citizen} catalog={catalog} now={now} pending={pending} onCancel={props.onCancelTask} />}
          {error && <p className="error">{error}</p>}

          {tab === 'village' && (
            <>
              <CivicPanel village={village} tsar={tsar} ruler={ruler} actorId={props.me.id} citizen={citizen} onVillage={props.onVillage} />
              <QuestPanel
                village={village}
                catalog={catalog}
                tsar={tsar}
                ruler={ruler}
                citizen={citizen}
                disabled={disabled}
                onStart={onStart}
                onPlan={() => onTab('build')}
                onHelpBuild={() => document.getElementById('sites')?.scrollIntoView({ behavior: 'smooth' })}
              />
              {citizen && (
                <div className="big-actions">
                  <button
                    className="big"
                    onClick={() => {
                      setFilter('gather')
                      onTab('tasks')
                    }}
                  >
                    {t('action.gather')}
                  </button>
                  <button
                    className="big"
                    disabled={disabled || !roam || blockedByLoad(roam, citizen, village.resources, catalog.rules.takeLimit) !== null}
                    onClick={() => roam && onStart(roam)}
                  >
                    {t('action.roam')}
                  </button>
                  <button
                    className="big"
                    onClick={() => document.getElementById('sites')?.scrollIntoView({ behavior: 'smooth' })}
                  >
                    {t('action.help')}
                  </button>
                  <button
                    className="big"
                    disabled={disabled || !expedition || blockedByLoad(expedition, citizen, village.resources, catalog.rules.takeLimit) !== null}
                    onClick={() => expedition && onStart(expedition)}
                  >
                    {t('action.expedition')}
                  </button>
                </div>
              )}
              {village.project && (
                <ProjectPanel project={village.project} completed={village.completedProjects} />
              )}
              {tsar ? (
                <section className="card">
                  <h3>{t('tsar.title')}</h3>
                  <p className="small">{t('tsar.role')}</p>
                  <button className="primary" onClick={() => onTab('build')}>
                    {t('hud.build')}
                  </button>
                </section>
              ) : citizen ? (
                <section className="card" id="sites">
                  <h3>{t('site.title')}</h3>
                  {sites}
                  {projectTasks.length > 0 && (
                    <>
                      <h3>{t('menu.helpBuild')}</h3>
                      <TaskList
                        tasks={projectTasks}
                        citizen={citizen}
                        catalog={catalog}
                        built={built}
                        discovered={discovered}
                        wildlife={village.wildlife}
                        goldVein={village.goldVeinRemaining}
                        fields={village.fields}
                        resources={village.resources}
                        disabled={disabled}
                        onStart={onStart}
                      />
                    </>
                  )}
                </section>
              ) : (
                <JoinPrompt {...props} />
              )}
              {tsar && (
                <UndeadPanel
                  catalog={catalog}
                  village={village}
                  pending={pending}
                  error={props.undeadError}
                  onVillage={props.onVillage}
                  onCancel={props.onCancelUndead}
                />
              )}
              {tsar && (
                <DungeonPanel
                  village={village}
                  pending={pending}
                  error={props.dungeonError}
                  onOpen={props.onOpenDungeon}
                  onCancel={props.onCancelDungeon}
                />
              )}
              <section className="card">
                <h3>{t('menu.chronicle')}</h3>
                {props.feed.length === 0 ? (
                  <p className="muted small">{t('menu.quiet')}</p>
                ) : (
                  <ul className="feed">
                    {props.feed.map((entry) => (
                      <li key={entry.key}>
                        {entry.kind === 'cycle'
                          ? i18n.describeReport(entry.report)
                          : entry.kind === 'raid'
                            ? i18n.describeRaid(entry.report)
                            : entry.kind === 'dungeon'
                              ? i18n.describeDungeon(entry.run)
                              : t('feed.built', { project: i18n.project(entry.completion.projectId).name })}
                      </li>
                    ))}
                  </ul>
                )}
              </section>
            </>
          )}

          {tab === 'settings' && (
            <SettingsPanel
              village={village}
              me={props.me}
              isOwner={tsar}
              inviteCode={props.inviteCode}
              onNewInviteCode={props.onNewInviteCode}
              onAccountChanged={props.onAccountChanged}
            />
          )}

          {tab === 'build' && ruler && (
            <BuildPanel tsar={tsar} catalog={catalog} village={village} onPlace={props.onPlace} onWithdraw={props.onWithdraw}
              defense={<DefensePanel slug={props.slug} village={village} ruler tsar={tsar} onVillage={props.onVillage}/>} />
          )}

          {tab === 'technologies' && (
            <TechnologyPanel
              catalog={catalog}
              village={village}
              citizen={citizen}
              ruler={ruler}
              disabled={disabled}
              onPoll={props.onPoll}
              onResearch={props.onResearch}
              onVillage={props.onVillage}
            />
          )}

          {tab === 'character' &&
            (citizen ? <CharacterSheet citizen={citizen} catalog={catalog} structures={village.structures} now={now} /> : <JoinPrompt {...props} />)}

          {tab === 'tasks' &&
            (citizen ? (
              <>
                {village.workContractsEnabled&&<p className="small">{i18n.language==='ru'?'Общественная работа оплачивается по госзаказу. Без заказа — добровольно.':'Public work is paid through funded orders. Work without an order is voluntary.'} <button onClick={()=>props.onTab('village')}>{i18n.language==='ru'?'Госзаказы':'Work orders'}</button></p>}
                <div className="chips">
                  {(Object.keys(FILTERS) as TaskFilter[]).map((key) => (
                    <button key={key} className={`chip ${filter === key ? 'active' : ''}`} onClick={() => setFilter(key)}>
                      {t(FILTERS[key].label)}
                    </button>
                  ))}
                </div>
                {(filter === 'all' || filter === 'project') && (
                  <section className="card">
                    <h3>{t('site.title')}</h3>
                    {sites}
                  </section>
                )}
                {(['Basic', 'Journeyman', 'Master'] as const).map((tier) => {
                  const roleIds = new Set(catalog.roles.filter((r) => r.tier === tier).map((r) => r.id))
                  const tasks = catalog.tasks.filter(
                    (task) => roleIds.has(task.roleId) && visibleTask(task) && FILTERS[filter].matches(task),
                  )
                  if (tasks.length === 0) return null
                  return (
                    <section key={tier} className="card">
                      <h3>{i18n.tier(tier)}</h3>
                      <TaskList
                        tasks={tasks}
                        citizen={citizen}
                        catalog={catalog}
                        built={built}
                        discovered={discovered}
                        wildlife={village.wildlife}
                        goldVein={village.goldVeinRemaining}
                        fields={village.fields}
                        resources={village.resources}
                        disabled={disabled}
                        onStart={onStart}
                      />
                    </section>
                  )
                })}
              </>
            ) : (
              <JoinPrompt {...props} />
            ))}
        </div>
        <footer className="modal-footer muted small">{t('menu.hint')}</footer>
      </div>
    </div>
  )
}
