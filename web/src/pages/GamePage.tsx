import { SramoslavPanel } from '../ui/SramoslavPanel'
import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router'
import {
  api,
  ApiError,
  CARRY_TO_STORAGE,
  comingRaids,
  dungeonPath,
  myArrival,
  myHero,
  INVITE_PARAMETER,
  KEEP_WATCH,
  isStoryQuest,
  takesSpot,
  type BuildingDefinition,
  type Catalog,
  type Citizen,
  type CycleReport,
  type Me,
  type Task,
  type TilePoint,
  type VillageState,
} from '../api'
import type { JobChoice } from '../ui/SitesPanel'
import { inForestArea, tileBuiltOver, type WorldTarget } from '../game/worldMap'
import { TargetPopup } from '../ui/TargetPopup'
import { watchable, WatchPopup } from '../ui/WatchPopup'
import { flagTitle, notify, useTitleReset } from '../notifications'
import { GameCanvas, type GameCanvasHandle, type Placement } from '../game/GameCanvas'
import { HandsCard } from '../ui/HandsCard'
import { PRIEST_ID, TSAR_ID } from '../game/VillageWorld'
import { useLiveVillage, useMediaQuery, useServerNow } from '../hooks'
import { GameMenu } from '../ui/GameMenu'
import type { MenuTab } from '../ui/menuTabs'
import { BottomBar, TopBar } from '../ui/Hud'
import { ChatPanel } from '../ui/ChatPanel'
import { InventoryPanel } from '../ui/InventoryPanel'
import { useI18n } from '../i18n'
import { TaskDonePanel } from '../ui/TaskDonePanel'
import { Guide } from '../ui/Guide'
import { QuestTracker } from '../ui/QuestPanel'
import { CitizenDialog } from '../ui/CitizenDialog'
import { PollPanel, PollSetup } from '../ui/PollPanel'
import { ServerClock } from '../ui/ServerClock'
import { LoginPanel } from '../ui/LoginPanel'
import { CreateCharacterForm } from '../ui/CreateCharacterForm'
import { RaidBanner } from '../ui/RaidBanner'
import { DungeonBanner, DungeonInvite } from '../ui/Dungeon'
import { declinedDungeon, declineDungeon } from '../ui/dungeonInvites'

type Panel = 'none' | 'menu' | 'inventory' | 'done'

const ZOOM_STEP = 1.25
/** Results of a closed vote pop up for players who come within this time. */
const POLL_RESULTS_MS = 10 * 60 * 1000
const POLL_CLOCK_MS = 15_000
const RAID_CLOCK_MS = 5_000
const seenPollKey = (slug: string) => `desertod.poll-seen.${slug}`
/** The village page sends the player to a run once; coming back from it leaves them in the village. */
const sentToDungeonKey = (runId: string) => `desertod.dungeon-sent.${runId}`
const UI_HIDDEN_KEY = 'desertod.ui-hidden'
/** Long enough to read a chat line over the author's head: 4 s plus about one second per 15 characters. */
const chatBubbleSeconds = (text: string) => Math.min(12, 4 + text.length / 15)

/** Villages are for signed-in players only: visitors sign in before the map loads. */
export function GamePage() {
  const { t } = useI18n()
  const [me, setMe] = useState<Me | null | undefined>(undefined)
  const loadMe = useCallback(async () => setMe(await api.me()), [])

  useEffect(() => {
    api.me().then(setMe)
  }, [])

  if (me === undefined) return <div className="splash">{t('loading')}</div>
  if (!me) {
    return (
      <div className="menu-screen">
        <LoginPanel onLoggedIn={loadMe} />
        <Link to="/">{t('toHome')}</Link>
      </div>
    )
  }
  if (!me.character) {
    return (
      <div className="menu-screen">
        <CreateCharacterForm suggestedName={me.displayName} onCreated={setMe} />
      </div>
    )
  }
  return <VillageView key={me.id} me={me} onAccountChanged={loadMe} />
}

function VillageView({ me, onAccountChanged }: { me: Me; onAccountChanged: () => void }) {
  const { slug = '' } = useParams()
  const navigate = useNavigate()
  const i18n = useI18n()
  const [catalog, setCatalog] = useState<Catalog | null>(null)
  const [ownCitizen, setCitizen] = useState<Citizen | null | undefined>(undefined)
  const [placing, setPlacing] = useState<{ definition: BuildingDefinition } | null>(null)
  /** Citizen the Tsar clicked: appoints them Governor or writes their subtitle. */
  const [subtitleFor, setSubtitleFor] = useState<string | null>(null)
  /** Place on the map the player clicked to send their citizen there. */
  const [picked, setPicked] = useState<{ target: WorldTarget; screen: { x: number; y: number } } | null>(null)
  const [notice, setNotice] = useState<string | null>(null)
  const [serverOffset, setServerOffset] = useState(0)
  const [chaplainOpen, setChaplainOpen] = useState(false)
  const [chatFull, setChatFull] = useState(false)
  const [panel, setPanel] = useState<Panel>('none')
  const [tab, setTab] = useState<MenuTab>('village')
  const [report, setReport] = useState<CycleReport | null>(null)
  const [pending, setPending] = useState(false)
  const [error, setError] = useState<string | null>(null)
  /** The Tsar or a Governor is ticking discoveries for a new vote. */
  const [pollSetup, setPollSetup] = useState(false)
  /** Open vote the player hid; the next one pops up again. */
  const [hiddenPoll, setHiddenPoll] = useState<string | null>(null)
  /** Closed vote whose results the player has seen. */
  const [seenPoll, setSeenPoll] = useState(() => localStorage.getItem(seenPollKey(slug)))
  const [pollError, setPollError] = useState<string | null>(null)
  /** Development only: the day-night toggle forces daylight or night; null follows the village sky. */
  const [nightOverride, setNightOverride] = useState<boolean | null>(null)
  /** Everything but the map and the popups of clicked objects is hidden. */
  const [uiHidden, setUiHidden] = useState(() => localStorage.getItem(UI_HIDDEN_KEY) === '1')
  const toggleUi = () => {
    const hidden = !uiHidden
    localStorage.setItem(UI_HIDDEN_KEY, hidden ? '1' : '0')
    setUiHidden(hidden)
    if (hidden) setPanel((current) => (current === 'menu' || current === 'inventory' ? 'none' : current))
  }
  const phone = useMediaQuery('(max-width: 720px)')
  const pollClock = useServerNow(serverOffset, POLL_CLOCK_MS)
  const canvas = useRef<GameCanvasHandle>(null)
  const citizenId = useRef<string | null>(null)
  const applyCitizen = useCallback((next: Citizen | null) => {
    setCitizen(next)
    if (next) setServerOffset(Date.parse(next.serverNow) - Date.now())
  }, [])

  const loadCitizen = useCallback(async () => applyCitizen(await api.myCitizen(slug)), [slug, applyCitizen])

  useEffect(() => {
    api.myCitizen(slug).then(applyCitizen)
    api.catalog().then(setCatalog)
  }, [slug, applyCitizen])

  const titleRef = useRef('Desertovia')
  const catalogRef = useRef<Catalog | null>(null)

  const live = useLiveVillage(slug, !!ownCitizen, {
    onCitizenUpdated: (summary) => {
      if (summary.id === citizenId.current) void loadCitizen()
    },
    onCycleCompleted: (finished) => {
      canvas.current?.say(finished.citizenId, i18n.speechFor(finished))
      if (finished.buildingCompleted) {
        const building = i18n.building(finished.buildingCompleted).name
        notify(i18n.t('notify.builtTitle', { building }), i18n.t('notify.builtBody'), `built-${finished.siteId ?? building}`)
        flagTitle('★', titleRef.current)
      }
      if (finished.technologyDiscovered && finished.technologyId) {
        const technology = i18n.technology(finished.technologyId).name
        notify(
          i18n.t('notify.discoveredTitle', { technology }),
          i18n.t('notify.discoveredBody'),
          `discovered-${finished.technologyId}`,
        )
        flagTitle('★', titleRef.current)
      }
      if (finished.citizenId === citizenId.current) {
        setReport(finished)
        setError(null)
        setPanel((current) => (current === 'none' ? 'done' : current))
        if (!finished.buildingCompleted && !finished.technologyDiscovered) {
          notify(
            i18n.t('notify.doneTitle', { task: i18n.task(finished.taskId) }),
            i18n.t('notify.doneBody', { seconds: catalogRef.current?.rules.graceSeconds ?? 60 }),
            'cycle-done',
          )
        }
        flagTitle('✓', titleRef.current)
      }
    },
    onProjectCompleted: (completion) => {
      const project = i18n.project(completion.projectId).name
      canvas.current?.say(PRIEST_ID, i18n.t('priest.completed', { project }))
      notify(i18n.t('notify.builtTitle', { building: project }), i18n.t('notify.builtBody'), `project-${completion.projectId}`)
      flagTitle('★', titleRef.current)
    },
    onChatMessage: (message) => {
      canvas.current?.say(message.isChaplain ? PRIEST_ID : message.citizenId ?? TSAR_ID, message.text, chatBubbleSeconds(message.text))
    },
    onRaidFought: (fought) => {
      const line = i18n.describeRaid(fought)
      setNotice(line)
      window.setTimeout(() => setNotice(null), 8000)
      notify(i18n.t('notify.raidOverTitle'), line, `raid-${fought.raidId}`)
      flagTitle(fought.repelled ? '★' : '!', titleRef.current)
    },
    onDungeonEnded: (run) => {
      if (run.outcome === 'Cancelled') return
      setNotice(i18n.describeDungeon(run))
      window.setTimeout(() => setNotice(null), 8000)
    },
    onQuestCompleted: (completion) => {
      const done = i18n.t(isStoryQuest(completion.kind) ? 'quest.storyDone' : 'quest.done', {
        quest: i18n.quest(completion).title,
      })
      const renown = citizenId.current ? completion.renownByCitizen[citizenId.current] : undefined
      setNotice(renown ? `${done} ${i18n.t('quest.renown', { renown: i18n.amount(renown) })}` : done)
      window.setTimeout(() => setNotice(null), 6000)
      notify(done, i18n.t('notify.questBody'), `quest-${completion.questId}`)
      flagTitle('★', titleRef.current)
    },
    onVillageDeleted: () => navigate('/'),
  })

  const brand = i18n.t('brand')
  const title = live.state ? `${live.state.name} — ${brand}` : brand
  useEffect(() => {
    titleRef.current = title
    catalogRef.current = catalog
  }, [title, catalog])
  useTitleReset(title)


  // The village owner is the Tsar: rules and plans, never works. Governors are citizens the Tsar lets plan too.
  const tsar = live.isOwner
  const citizen = tsar ? null : ownCitizen
  const ruler = tsar || citizen?.isGovernor === true

  const [joining, setJoining] = useState(false)
  const [joinError, setJoinError] = useState<string | null>(null)
  const join = useCallback(
    async (inviteCode: string | null) => {
      setJoining(true)
      setJoinError(null)
      try {
        applyCitizen(await api.joinVillage(slug, inviteCode))
      } catch (exception) {
        // The session may belong to another account than the one this tab loaded.
        if (exception instanceof ApiError && exception.code === 'character.missing') {
          onAccountChanged()
          return
        }
        setJoinError(i18n.error(exception))
      } finally {
        setJoining(false)
      }
    },
    [slug, applyCitizen, i18n, onAccountChanged],
  )
  // The account's character settles in on the first visit; a private village waits for the invite code.
  const triedJoin = useRef(false)
  const isPrivate = live.state?.isPrivate
  const linkInvite = new URLSearchParams(window.location.search).get(INVITE_PARAMETER)
  useEffect(() => {
    if (triedJoin.current || ownCitizen !== null || isPrivate === undefined || tsar) return
    if (isPrivate && !linkInvite) return
    triedJoin.current = true
    void join(isPrivate ? linkInvite : null)
  }, [ownCitizen, isPrivate, tsar, linkInvite, join])
  const placingRef = useRef(placing)
  const subtitleForRef = useRef(subtitleFor)
  const pickedRef = useRef(picked)
  /** Closes the vote screen when it is shown. */
  const closePollRef = useRef<(() => void) | null>(null)
  useEffect(() => {
    citizenId.current = citizen?.id ?? null
    placingRef.current = placing
    subtitleForRef.current = subtitleFor
    pickedRef.current = picked
  })

  // Calls everyone to the vote when it opens, and shares the result when it closes.
  const lastPoll = useRef<{ id: string; open: boolean } | null | undefined>(undefined)
  const poll = live.state?.poll ?? null
  useEffect(() => {
    if (!live.state) return
    const previous = lastPoll.current
    lastPoll.current = poll ? { id: poll.id, open: poll.closedAt === null } : null
    if (previous === undefined || !poll) return
    if (poll.closedAt === null && previous?.id !== poll.id) {
      notify(i18n.t('notify.pollTitle'), i18n.t('notify.pollBody'), `poll-${poll.id}`)
      flagTitle('?', titleRef.current)
    } else if (poll.winner && previous?.id === poll.id && previous.open) {
      notify(
        i18n.t('notify.pollClosedTitle', { technology: i18n.technology(poll.winner).name }),
        i18n.t('notify.pollClosedBody'),
        `poll-${poll.id}`,
      )
      flagTitle('★', titleRef.current)
    }
  }, [live.state, poll, i18n])

  // Warns everyone once when an attack comes into sight.
  const raidClock = useServerNow(serverOffset, RAID_CLOCK_MS)
  const warnedRaids = useRef(new Set<string>())
  useEffect(() => {
    if (!live.state) return
    for (const raid of comingRaids(live.state.raids, raidClock)) {
      if (warnedRaids.current.has(raid.id)) continue
      warnedRaids.current.add(raid.id)
      notify(
        i18n.t('notify.raidTitle', { enemy: i18n.enemy(raid.enemy) }),
        i18n.t('notify.raidBody', { minutes: Math.max(1, Math.ceil((Date.parse(raid.strikesAt) - raidClock) / 60_000)) }),
        `raid-coming-${raid.id}`,
      )
      flagTitle('!', titleRef.current)
    }
  }, [live.state, raidClock, i18n])

  // The Tsar's call to a dungeon pops up for every citizen once; closing it declines.
  const dungeon = live.state?.dungeon ?? null
  const [inviteFor, setInviteFor] = useState<string | null>(null)
  const invitedRuns = useRef(new Set<string>())
  const citizenReady = !!citizen
  useEffect(() => {
    if (!dungeon || dungeon.phase !== 'Gathering' || !citizenReady) return
    if (invitedRuns.current.has(dungeon.id)) return
    invitedRuns.current.add(dungeon.id)
    if (declinedDungeon(dungeon.id)) return
    setInviteFor(dungeon.id)
    notify(i18n.t('notify.dungeonTitle'), i18n.t('notify.dungeonBody', { seconds: 60 }), `dungeon-${dungeon.id}`)
    flagTitle('!', titleRef.current)
  }, [dungeon, citizenReady, i18n])
  const closeInvite = () => {
    if (inviteFor && dungeon && !myHero(dungeon, citizen?.id ?? null, false)) declineDungeon(inviteFor)
    setInviteFor(null)
  }

  // Heroes go to the run's page once their figure reaches the edge of the land.
  const arrival = dungeon && (dungeon.phase === 'Marching' || dungeon.phase === 'Running') ? myArrival(dungeon, citizen?.id ?? null, tsar) : null
  const ownHero = dungeon ? myHero(dungeon, citizen?.id ?? null, tsar) : null
  const sendable = ownHero !== null && !ownHero.settled && !ownHero.knockedOut
  useEffect(() => {
    if (!dungeon || arrival === null || !sendable) return
    if (sessionStorage.getItem(sentToDungeonKey(dungeon.id))) return
    const go = () => {
      sessionStorage.setItem(sentToDungeonKey(dungeon.id), '1')
      navigate(dungeonPath(slug, dungeon.id))
    }
    const wait = arrival - (Date.now() + serverOffset)
    if (wait <= 0) {
      go()
      return
    }
    const timer = window.setTimeout(go, wait)
    return () => window.clearTimeout(timer)
  }, [dungeon, arrival, sendable, serverOffset, slug, navigate])

  useEffect(() => {
    const onKey = (event: KeyboardEvent) => {
      if (event.key === 'Escape' && subtitleForRef.current) {
        event.preventDefault()
        setSubtitleFor(null)
        return
      }
      if (event.key === 'Escape' && closePollRef.current) {
        event.preventDefault()
        closePollRef.current()
        return
      }
      const target = event.target as HTMLElement | null
      if (target && ['INPUT', 'TEXTAREA', 'SELECT'].includes(target.tagName)) {
        if (event.key === 'Escape') target.blur()
        return
      }
      if (event.key === 'Escape') {
        event.preventDefault()
        if (placingRef.current) {
          setPlacing(null)
          return
        }
        if (pickedRef.current) {
          setPicked(null)
          return
        }
        setPanel((current) => (current === 'none' ? 'menu' : 'none'))
      } else if (event.code === 'KeyI') {
        event.preventDefault()
        setPanel((current) => (current === 'inventory' ? 'none' : 'inventory'))
      } else if (['Equal', 'NumpadAdd'].includes(event.code)) {
        event.preventDefault()
        canvas.current?.zoom(ZOOM_STEP)
      } else if (['Minus', 'NumpadSubtract'].includes(event.code)) {
        event.preventDefault()
        canvas.current?.zoom(1 / ZOOM_STEP)
      }
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [])

  const run = async (action: () => Promise<Citizen>) => {
    setPending(true)
    setError(null)
    try {
      applyCitizen(await action())
      setPanel('none')
      setPicked(null)
    } catch (exception) {
      setError(i18n.error(exception))
    } finally {
      setPending(false)
    }
  }

  const start = (task: Task, spot: TilePoint | null = null) => run(() => api.startTask(slug, task.id, spot))
  const startJob = (choice: JobChoice) => run(() => api.startJob(slug, choice.plotId, choice.stage, choice.jobId))
  const carry = () => run(() => api.startTask(slug, CARRY_TO_STORAGE))
  const research = () => run(() => api.research(slug))

  const renew = (finished: CycleReport) => {
    if (finished.technologyId) {
      if (finished.technologyDiscovered) openMenu('technologies')
      else void research()
      return
    }
    if (finished.siteId) {
      const site = live.state?.sites.find((s) => s.plotId === finished.siteId)
      const stage = citizen?.siteStage ?? site?.stage ?? 0
      void startJob({ plotId: finished.siteId, stage, jobId: finished.taskId })
      return
    }
    const task = catalog?.tasks.find((t) => t.id === finished.taskId)
    if (task) void start(task, againAt(task))
  }

  /** The same spot again, unless the picked camp tree has fallen; then the nearest tree is chopped. */
  const againAt = (task: Task) => {
    const spot = citizen?.spot
    if (!spot || !takesSpot(task)) return null
    const [x, y] = [Math.floor(spot.x), Math.floor(spot.y)]
    const state = live.state
    const felled =
      task.workplace === 'forest' &&
      inForestArea(x, y) &&
      (!state?.trees.some((tree) => tree.x === x && tree.y === y) || (state !== null && tileBuiltOver(state, x, y)))
    return felled ? null : spot
  }

  const openMenu = (next: MenuTab) => {
    setTab(next)
    setPanel('menu')
    setPicked(null)
  }

  const drop = async () => {
    setPending(true)
    setError(null)
    try {
      applyCitizen(await api.dropLoad(slug))
    } catch (exception) {
      setError(i18n.error(exception))
    } finally {
      setPending(false)
    }
  }

  const drink = async () => {
    setPending(true)
    try {
      applyCitizen(await api.drink(slug))
    } catch (exception) {
      setNotice(i18n.error(exception))
      window.setTimeout(() => setNotice(null), 5000)
    } finally {
      setPending(false)
    }
  }

  const heal = async () => {
    setPending(true)
    try {
      applyCitizen(await api.heal(slug))
      setNotice(i18n.t('injury.healed'))
    } catch (exception) {
      setNotice(i18n.error(exception))
    } finally {
      setPending(false)
      window.setTimeout(() => setNotice(null), 5000)
    }
  }

  const [undeadError, setUndeadError] = useState<string | null>(null)
  const undeadAction = async (action: () => Promise<VillageState>) => {
    setPending(true)
    setUndeadError(null)
    try {
      live.setState(await action())
    } catch (exception) {
      setUndeadError(i18n.error(exception))
    } finally {
      setPending(false)
    }
  }

  const [dungeonError, setDungeonError] = useState<string | null>(null)
  const dungeonAction = async (action: () => Promise<VillageState>) => {
    setPending(true)
    setDungeonError(null)
    try {
      live.setState(await action())
    } catch (exception) {
      setDungeonError(i18n.error(exception))
    } finally {
      setPending(false)
    }
  }

  /** Keeps the storage view open, so the player sees the goods move into their hands. */
  const takeFromStorage = async (plotId: string, resource: string, amount: number) => {
    setPending(true)
    setError(null)
    try {
      applyCitizen(await api.takeFromStorage(slug, plotId, resource, amount))
    } catch (exception) {
      setError(i18n.error(exception))
    } finally {
      setPending(false)
    }
  }

  const cancelTask = async () => {
    setPending(true)
    setError(null)
    try {
      applyCitizen(await api.cancelTask(slug))
    } catch (exception) {
      setNotice(i18n.error(exception))
      window.setTimeout(() => setNotice(null), 5000)
    } finally {
      setPending(false)
    }
  }

  const { setState: setVillageState } = live
  const placement = useMemo<Placement | null>(() => {
    if (!placing) return null
    return {
      kind: placing.definition.kind,
      onPick: async (x, y) => {
        const building = i18n.building(placing.definition.kind).name
        setPlacing(null)
        try {
          setVillageState(await api.placeBlueprint(slug, placing.definition.kind, x, y))
          setNotice(i18n.t('build.placed', { building }))
        } catch (exception) {
          setNotice(i18n.error(exception))
        }
        window.setTimeout(() => setNotice(null), 5000)
      },
    }
  }, [placing, slug, i18n, setVillageState])

  const pollAction = async (action: () => Promise<VillageState>, after?: () => void) => {
    setPending(true)
    setPollError(null)
    try {
      setVillageState(await action())
      after?.()
    } catch (exception) {
      setPollError(i18n.error(exception))
    } finally {
      setPending(false)
    }
  }

  const pollOpen = poll !== null && poll.closedAt === null
  const pollShown = pollOpen
    ? hiddenPoll !== poll.id
    : poll !== null &&
      poll.winner !== null &&
      seenPoll !== poll.id &&
      poll.closedAt !== null &&
      pollClock - Date.parse(poll.closedAt) < POLL_RESULTS_MS
  const canCallPoll = ruler && !pollOpen && (live.state?.technologies.some((state) => state.available) ?? false)

  const showPoll = () => {
    setPanel('none')
    setPollError(null)
    if (pollOpen) setHiddenPoll(null)
    else if (ruler) setPollSetup(true)
  }

  const closePoll = () => {
    setPollError(null)
    if (pollSetup) {
      setPollSetup(false)
    } else if (poll && pollOpen) {
      setHiddenPoll(poll.id)
    } else if (poll) {
      localStorage.setItem(seenPollKey(slug), poll.id)
      setSeenPoll(poll.id)
    }
  }
  useEffect(() => {
    closePollRef.current = pollSetup || pollShown ? closePoll : null
  })

  const withdraw = async (plotId: string) => {
    try {
      setVillageState(await api.cancelBlueprint(slug, plotId))
    } catch (exception) {
      setNotice(i18n.error(exception))
      window.setTimeout(() => setNotice(null), 5000)
    }
  }

  const hands = (owner: Citizen) => (
    <HandsCard citizen={owner} village={live.state!} catalog={catalog!} pending={pending} onCarry={carry} onDrop={drop} />
  )

  const villageRef = useRef(live.state)
  useEffect(() => {
    villageRef.current = live.state
  })
  const citizens = useMemo(() => [...live.citizens.values()], [live.citizens])
  const pickCitizen = useMemo(() => (id: string) => { if (id === PRIEST_ID) setChaplainOpen(true); else if (tsar) setSubtitleFor(id) }, [tsar])
  // Only a citizen can be sent to work; the Tsar watches and walks around, players without a character just look.
  const canWork = !tsar && !!citizen
  const working = citizen?.state === 'Working'
  const ownId = citizen?.id
  /** Away citizens stay on the map only while a task they started is still running. */
  const shownCitizens = useMemo(
    () => citizens.filter((c) => c.id === ownId || c.state === 'Working' || !live.away.has(c.id)),
    [citizens, live.away, ownId],
  )
  const pickWorld = useMemo(() => {
    /** Water and mountains stop walkers; says so instead of asking the server. */
    const blocked = (walkerId: string, point: TilePoint) => {
      const block = canvas.current?.wayBlock(walkerId, point)
      if (!block) return false
      setNotice(i18n.t(`walk.${block}`))
      window.setTimeout(() => setNotice(null), 5000)
      return true
    }
    if (tsar) {
      return (target: WorldTarget | null, screen: { x: number; y: number }, point: TilePoint) => {
        const watched = target !== null && villageRef.current !== null && watchable(target, villageRef.current)
        setPicked(watched ? { target, screen } : null)
        if (watched || blocked(TSAR_ID, point)) return
        api.tsarGoTo(slug, point).then(setVillageState, (exception) => {
          setNotice(i18n.error(exception))
          window.setTimeout(() => setNotice(null), 5000)
        })
      }
    }
    if (!canWork || !ownId) return null
    return (target: WorldTarget | null, screen: { x: number; y: number }, point: TilePoint) => {
      setError(null)
      setPicked(target ? { target, screen } : null)
      // Empty ground: "come here". A citizen busy with a task stays at work.
      if (target || working || blocked(ownId, point)) return
      api.goTo(slug, point).then(applyCitizen, (exception) => {
        setNotice(i18n.error(exception))
        window.setTimeout(() => setNotice(null), 5000)
      })
    }
  }, [tsar, canWork, working, ownId, slug, applyCitizen, setVillageState, i18n])
  const worldPickable = useMemo(
    () => (tsar ? (target: WorldTarget) => villageRef.current !== null && watchable(target, villageRef.current) : null),
    [tsar],
  )
  const showPicked = picked !== null && canWork && panel === 'none'
  const showWatched = picked !== null && tsar && panel === 'none' && live.state !== null && watchable(picked.target, live.state)
  const subtitleCitizen = subtitleFor ? live.citizens.get(subtitleFor) : undefined

  /** On phones the minimap sits right under the top bar, whose height changes with the banner and the wrapped chips. */
  const topBarObserver = useRef<ResizeObserver | null>(null)
  const watchTopBar = useCallback((hud: HTMLDivElement | null) => {
    topBarObserver.current?.disconnect()
    topBarObserver.current = null
    const top = hud?.querySelector<HTMLElement>('.hud-top')
    const root = hud?.parentElement
    if (!top || !root) return
    const measure = () => {
      // Hidden with the rest of the interface: the minimap and its buttons stay where they were.
      if (top.offsetHeight === 0) return
      root.style.setProperty('--hud-top-bottom', `${top.getBoundingClientRect().bottom - root.getBoundingClientRect().top}px`)
    }
    topBarObserver.current = new ResizeObserver(measure)
    topBarObserver.current.observe(top)
    measure()
  }, [])

  if (live.error) {
    return (
      <div className="splash">
        <p className="error">{i18n.error(live.error)}</p>
        <Link to="/">{i18n.t('toHome')}</Link>
      </div>
    )
  }

  if (!live.state || !catalog) return <div className="splash">{i18n.t('loading')}</div>
  const village = live.state
  const shownNight = nightOverride ?? village.weather.night
  // Switching back to what the sky shows hands the light back to the clock.
  const dayNight = catalog.devTools
    ? { night: shownNight, onToggle: () => setNightOverride(!shownNight === village.weather.night ? null : !shownNight) }
    : null

  return (
    <div className={`game-root${uiHidden ? ' ui-hidden' : ''}${chatFull ? ' chat-full-active' : ''}`}>
      <GameCanvas
        ref={canvas}
        village={live.state}
        citizens={shownCitizens}
        highlightId={citizen?.id ?? null}
        nightOverride={catalog.devTools ? nightOverride : null}
        obscured={chatFull || panel !== 'none' || chaplainOpen || pollSetup || subtitleFor !== null}
        catalog={catalog}
        placement={placement}
        onCitizenPick={pickCitizen}
        onWorldPick={pickWorld}
        worldPickable={worldPickable}
        selection={showPicked || showWatched ? picked.target.area : null}
      />

      <button
        className="ui-toggle"
        onClick={toggleUi}
        title={i18n.t(uiHidden ? 'hud.showInterface' : 'hud.hideInterface')}
        aria-label={i18n.t(uiHidden ? 'hud.showInterface' : 'hud.hideInterface')}
        aria-pressed={uiHidden}
      >
        <EyeIcon closed={uiHidden} />
      </button>

      {dayNight && (
        <button
          className="night-toggle"
          onClick={dayNight.onToggle}
          title={i18n.t('hud.dayNightTitle')}
          aria-pressed={dayNight.night}
        >
          {dayNight.night ? i18n.t('hud.day') : i18n.t('hud.night')}
        </button>
      )}

      {showWatched && (
        <ServerClock offsetMs={serverOffset}>
          {(now) => (
            <WatchPopup
              target={picked.target}
              screen={picked.screen}
              phone={phone}
              village={village}
              catalog={catalog}
              citizens={citizens}
              now={now}
              onClose={() => setPicked(null)}
            />
          )}
        </ServerClock>
      )}

      {showPicked && citizen && (
        <ServerClock key={`${picked.screen.x},${picked.screen.y}`} offsetMs={serverOffset}>
          {(now) => (
            <TargetPopup
              target={picked.target}
              screen={picked.screen}
              phone={phone}
              village={village}
              catalog={catalog}
              citizen={citizen}
              now={now}
              pending={pending}
              error={error}
              onStart={start}
              onStartJob={startJob}
              onTake={takeFromStorage}
              onClose={() => setPicked(null)}
            />
          )}
        </ServerClock>
      )}

      <div className="hud" ref={watchTopBar}>
        <TopBar
          village={live.state}
          catalog={catalog}
          citizen={citizen}
          citizens={live.citizens}
          onForecast={async (rain) => setVillageState(await api.forecast(slug, rain))}
        />
        <ServerClock offsetMs={serverOffset}>
          {(now) => (
            <RaidBanner
              village={village}
              citizens={live.citizens}
              catalog={catalog}
              now={now}
              watch={canWork ? (catalog.tasks.find((task) => task.id === KEEP_WATCH) ?? null) : null}
              pending={pending || working}
              onWatch={(task) => void start(task)}
            />
          )}
        </ServerClock>
        {dungeon && (
          <ServerClock offsetMs={serverOffset}>
            {(now) => (
              <DungeonBanner
                slug={slug}
                run={dungeon}
                now={now}
                canAnswer={!!citizen}
                onAnswer={() => setInviteFor(dungeon.id)}
              />
            )}
          </ServerClock>
        )}
        <div className="hud-side">
          <QuestTracker village={live.state} tsar={tsar} ruler={ruler} phone={phone} onOpen={() => openMenu('village')} />
          {(citizen !== undefined || tsar) && (
            <Guide
              key={tsar ? 'tsar' : 'citizen'}
              slug={slug}
              tsar={tsar}
              inviteCode={live.inviteCode}
              village={live.state}
              citizen={citizen}
              catalog={catalog}
              menuTab={panel === 'menu' ? tab : null}
              onMenu={openMenu}
            />
          )}
        </div>
        <ChatPanel
          key={slug}
          incoming={live.chatArrivals}
          interrupted={panel !== 'none' || chaplainOpen || pollSetup || pollShown || subtitleFor !== null || inviteFor !== null}
          onModeChange={mode => setChatFull(mode === 'full')}
          feed={live.feed}
          messages={live.chat}
          member={live.member}
          tsar={tsar}
          ownCitizenId={citizen?.id ?? null}
          serverOffsetMs={serverOffset}
          phone={phone}
          onSend={live.sendChat}
        />
        {(citizen || tsar) && (
          <button
            className="minimap-locate"
            onClick={() => canvas.current?.locate(citizen?.id ?? TSAR_ID)}
            title={i18n.t('hud.locate')}
            aria-label={i18n.t('hud.locate')}
          >
            <LocateIcon />
          </button>
        )}
        {!phone && (
          <div className="zoom-controls">
            <button onClick={() => canvas.current?.zoom(ZOOM_STEP)} title={`${i18n.t('hud.zoomIn')} (+)`} aria-label={i18n.t('hud.zoomIn')}>
              +
            </button>
            <button onClick={() => canvas.current?.zoom(1 / ZOOM_STEP)} title={`${i18n.t('hud.zoomOut')} (−)`} aria-label={i18n.t('hud.zoomOut')}>
              −
            </button>
          </div>
        )}
        {(placing || notice) && (
          <div className="placement-banner">
            {placing ? (
              <>
                {i18n.t('build.placing', { building: i18n.building(placing.definition.kind).name })}
                <button onClick={() => setPlacing(null)}>{i18n.t('build.cancel')}</button>
              </>
            ) : (
              notice
            )}
          </div>
        )}
        <ServerClock offsetMs={serverOffset}>
          {(now) => (
            <BottomBar
              citizen={citizen}
              catalog={catalog}
              structures={village.structures}
              now={now}
              phone={phone}
              tsar={tsar}
              ruler={ruler}
              hasResult={report !== null}
              poll={
                pollOpen && !pollShown
                  ? { label: i18n.t('poll.open'), onClick: showPoll }
                  : canCallPoll && !pollSetup
                    ? { label: i18n.t('poll.call'), onClick: showPoll }
                    : null
              }
              onMenu={openMenu}
              onInventory={() => setPanel((current) => (current === 'inventory' ? 'none' : 'inventory'))}
              onShowResult={() => setPanel('done')}
              pending={pending}
              onCancelTask={cancelTask}
              drinks={
                (village.resources.find((r) => r.kind === 'VigorDrink')?.amount ?? 0) +
                (citizen?.load.find((good) => good.resource === 'VigorDrink')?.amount ?? 0)
              }
              onDrink={drink}
              remedies={
                (village.resources.find((r) => r.kind === 'Remedy')?.amount ?? 0) +
                (citizen?.load.find((good) => good.resource === 'Remedy')?.amount ?? 0)
              }
              onHeal={heal}
            />
          )}
        </ServerClock>
      </div>

      {chaplainOpen && <SramoslavPanel slug={slug} onClose={() => setChaplainOpen(false)} onReply={text => canvas.current?.say(PRIEST_ID, text, 12)} />}
      {panel === 'menu' && (
        <ServerClock offsetMs={serverOffset}>
          {(now) => (
            <GameMenu
              slug={slug}
              tab={tab}
              onTab={setTab}
              onClose={() => setPanel('none')}
              village={village}
              population={{total:citizens.length,online:citizens.filter(c=>!live.away.has(c.id)).length}}
              tsar={tsar}
              ruler={ruler}
              inviteCode={live.inviteCode}
              onNewInviteCode={async () => live.setInviteCode((await api.newInviteCode(slug)).inviteCode)}
              onPlace={(definition) => {
                setPlacing({ definition })
                setNotice(null)
                setPanel('none')
              }}
              onWithdraw={withdraw}
              catalog={catalog}
              me={me}
              citizen={citizen}
              now={now}
              pending={pending}
              error={error}
              onStart={start}
              onStartJob={startJob}
              onCancelTask={cancelTask}
              onPoll={showPoll}
              onResearch={research}
              onAccountChanged={onAccountChanged}
              onJoin={join}
              joining={joining}
              joinError={joinError}
              feed={live.feed}
              onCallUndead={(enemy, size) => undeadAction(() => api.callUndead(slug, enemy, size))}
              onCancelUndead={() => undeadAction(() => api.cancelUndead(slug))}
              undeadError={undeadError}
              onOpenDungeon={() => dungeonAction(() => api.openDungeon(slug))}
              onCancelDungeon={() => dungeonAction(() => api.cancelDungeon(slug))}
              dungeonError={dungeonError}
            />
          )}
        </ServerClock>
      )}

      {panel === 'inventory' && (
        <ServerClock offsetMs={serverOffset}>
          {(now) => (
            <InventoryPanel
              village={village}
              citizen={citizen}
              catalog={catalog}
              now={now}
              pending={pending}
              error={error}
              onCarry={carry}
              onDrop={drop}
              onTake={takeFromStorage}
              onClose={() => setPanel('none')}
            />
          )}
        </ServerClock>
      )}

      {ruler && pollSetup && !pollOpen ? (
        <PollSetup
          catalog={catalog}
          village={live.state}
          pending={pending}
          error={pollError}
          onStart={(technologyIds) => pollAction(() => api.openPoll(slug, technologyIds), () => setPollSetup(false))}
          onClose={closePoll}
        />
      ) : (
        poll &&
        pollShown && (
          <PollPanel
            poll={poll}
            catalog={catalog}
            citizen={citizen}
            ruler={ruler}
            pending={pending}
            error={pollError}
            onVote={(technologyId) => pollAction(() => api.vote(slug, technologyId))}
            onCancel={() => pollAction(() => api.cancelPoll(slug))}
            onClose={closePoll}
          />
        )
      )}

      {citizen && dungeon && dungeon.phase === 'Gathering' && inviteFor === dungeon.id && (
        <ServerClock offsetMs={serverOffset}>
          {(now) => (
            <DungeonInvite
              slug={slug}
              run={dungeon}
              now={now}
              citizen={citizen}
              catalog={catalog}
              onCitizen={applyCitizen}
              onClose={closeInvite}
            />
          )}
        </ServerClock>
      )}

      {tsar && subtitleCitizen && (
        <CitizenDialog
          key={subtitleCitizen.id}
          slug={slug}
          citizen={subtitleCitizen}
          onSaved={live.updateCitizen}
          onClose={() => setSubtitleFor(null)}
        />
      )}

      {panel === 'done' && report && citizen && (
        <ServerClock offsetMs={serverOffset}>
          {(now) => (
            <TaskDonePanel
              hands={hands(citizen)}
              report={report}
              citizen={citizen}
              catalog={catalog}
              now={now}
              pending={pending}
              error={error}
              onRenew={() => renew(report)}
              onChoose={() => openMenu('tasks')}
              onHide={() => setPanel('none')}
            />
          )}
        </ServerClock>
      )}
    </div>
  )
}

/** Crosshair over a dot: the view jumps to the player's own figure. */
function LocateIcon() {
  return (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2} strokeLinecap="round" aria-hidden>
      <circle cx="12" cy="12" r="7" />
      <circle cx="12" cy="12" r="2" fill="currentColor" />
      <path d="M12 2v3M12 19v3M2 12h3M19 12h3" />
    </svg>
  )
}

/** Open eye while the interface shows, crossed out while it is hidden. */
function EyeIcon({ closed }: { closed: boolean }) {
  return (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2} strokeLinecap="round" strokeLinejoin="round" aria-hidden>
      <path d="M2 12s3.6-7 10-7 10 7 10 7-3.6 7-10 7S2 12 2 12z" />
      <circle cx="12" cy="12" r="3" />
      {closed && <path d="M3 3l18 18" />}
    </svg>
  )
}
