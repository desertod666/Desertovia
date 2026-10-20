import { useEffect, useState } from 'react'
import { inviteLink, type Catalog, type Citizen, type VillageState } from '../api'
import { useCollapsed } from '../hooks'
import { useI18n } from '../i18n'
import { FoldHead } from './Hud'
import type { UiKey } from '../i18n/en'
import type { MenuTab } from './menuTabs'

type Role = 'citizen' | 'tsar'

interface Step {
  id: string
  text: UiKey
  action?: UiKey
  /** Menu tab the action opens; steps without it act on their own (copy a link) or need no action. */
  tab?: MenuTab
}

const STEPS: Record<Role, Step[]> = {
  citizen: [
    { id: 'join', text: 'guide.citizen.join', action: 'guide.citizen.joinAction', tab: 'character' },
    { id: 'task', text: 'guide.citizen.task', action: 'guide.citizen.taskAction', tab: 'tasks' },
    { id: 'renew', text: 'guide.citizen.renew' },
    { id: 'needs', text: 'guide.citizen.needs', action: 'guide.citizen.needsAction', tab: 'village' },
    { id: 'build', text: 'guide.citizen.build', action: 'guide.citizen.buildAction', tab: 'village' },
  ],
  tsar: [
    { id: 'blueprint', text: 'guide.tsar.blueprint', action: 'guide.tsar.blueprintAction', tab: 'build' },
    { id: 'invite', text: 'guide.tsar.invite', action: 'guide.tsar.inviteAction' },
    { id: 'needs', text: 'guide.tsar.needs', action: 'guide.tsar.needsAction', tab: 'village' },
  ],
}

interface Progress {
  done: string[]
  skipped: boolean
}

/** Per browser, not per village: a player learns the game once. */
const storageKey = (role: Role) => `desertod.guide.${role}`

function load(role: Role): Progress {
  try {
    const saved = JSON.parse(window.localStorage.getItem(storageKey(role)) ?? 'null') as Progress | null
    return saved ?? { done: [], skipped: false }
  } catch {
    return { done: [], skipped: false }
  }
}

/** Steps the player has reached by playing; once reached a step stays done even if the state changes back. */
function reached(role: Role, village: VillageState, citizen: Citizen | null | undefined, menuTab: MenuTab | null) {
  if (role === 'tsar') {
    return {
      blueprint: village.sites.length > 0 || village.structures.length > 0 || village.storages.length > 1,
      needs: menuTab === 'village',
    } as Record<string, boolean>
  }
  return {
    join: Boolean(citizen),
    task: Boolean(citizen && (citizen.taskId !== null || citizen.stats.some((s) => s.level > 0 || s.experience > 0))),
    renew: Boolean(citizen && citizen.streak > 0),
    needs: menuTab === 'village',
    build: Boolean(citizen?.siteId),
  } as Record<string, boolean>
}

/** First-session guide: one step at a time, detected from what the player actually does. */
export function Guide({
  slug,
  tsar,
  inviteCode,
  village,
  citizen,
  catalog,
  menuTab,
  onMenu,
  phone = false,
}: {
  slug: string
  tsar: boolean
  inviteCode: string | null
  village: VillageState
  citizen: Citizen | null | undefined
  catalog: Catalog
  /** Open menu tab, or null when the menu is closed. */
  menuTab: MenuTab | null
  onMenu: (tab: MenuTab) => void
  phone?: boolean
}) {
  const { t } = useI18n()
  const role: Role = tsar ? 'tsar' : 'citizen'
  const [saved, setSaved] = useState<Progress>(() => load(role))
  const [copied, setCopied] = useState(false)
  const [collapsed, toggle] = useCollapsed('guide', phone)
  const steps = STEPS[role]

  const conditions = reached(role, village, citizen, menuTab)
  const done = steps.filter((step) => saved.done.includes(step.id) || conditions[step.id]).map((step) => step.id)
  const doneKey = done.join(',')

  useEffect(() => {
    window.localStorage.setItem(storageKey(role), JSON.stringify({ skipped: saved.skipped, done: doneKey ? doneKey.split(',') : [] }))
  }, [role, saved.skipped, doneKey])

  const index = steps.findIndex((step) => !done.includes(step.id))
  if (saved.skipped || index < 0) return null
  const step = steps[index]

  const act = async () => {
    if (step.tab) {
      onMenu(step.tab)
      return
    }
    if (step.id === 'invite') {
      await navigator.clipboard.writeText(inviteLink(slug, village.isPrivate ? inviteCode : null))
      setCopied(true)
      window.setTimeout(() => setCopied(false), 4000)
      setSaved({ skipped: saved.skipped, done: [...done, 'invite'] })
    }
  }

  return (
    <aside className={`guide${collapsed ? ' collapsed' : ''}`} aria-label={t('guide.title')}>
      <FoldHead collapsed={collapsed} onToggle={toggle}>
        <strong>{t('guide.title')}</strong>
        <span className="small">{t('guide.step', { number: index + 1, total: steps.length })}</span>
      </FoldHead>
      {!collapsed && (
        <>
          <p>{t(step.text, { seconds: catalog.rules.graceSeconds })}</p>
          <div className="row">
            {step.action && (
              <button className="primary" onClick={act}>
                {t(step.action)}
              </button>
            )}
            <button className="link-button small" onClick={() => setSaved({ skipped: true, done })}>
              {t('guide.skip')}
            </button>
          </div>
          {copied && <p className="small highlight">{t('guide.copied')}</p>}
        </>
      )}
    </aside>
  )
}
