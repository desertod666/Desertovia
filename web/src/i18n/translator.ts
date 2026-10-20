import {
  ApiError,
  type CycleReport,
  type DungeonRun,
  type Era,
  type HeroClass,
  type ProjectPhase,
  type RaidReport,
  type RoleTier,
  type Skill,
  type Stat,
} from '../api'
import { en, type Dictionary, type UiKey } from './en'
import { ru } from './ru'

export type Language = 'en' | 'ru'

/** English first: it is the default and the fallback for anything missing. */
export const LANGUAGES: Array<{ code: Language; label: string }> = [
  { code: 'en', label: 'English' },
  { code: 'ru', label: 'Русский' },
]

export const DICTIONARIES: Record<Language, Dictionary> = { en, ru }
export const LANGUAGE_STORAGE_KEY = 'desertod.language'
type Parameters = Record<string, string | number>

function isLanguage(value: string | null): value is Language {
  return value !== null && value in DICTIONARIES
}

/** `?lang=` wins, then the saved choice, then the browser language, then English. */
export function initialLanguage(): Language {
  const fromQuery = new URLSearchParams(window.location.search).get('lang')
  if (isLanguage(fromQuery)) return fromQuery
  const stored = window.localStorage.getItem(LANGUAGE_STORAGE_KEY)
  if (isLanguage(stored)) return stored
  const browser = (navigator.languages ?? [navigator.language]).map((tag) => tag.slice(0, 2).toLowerCase())
  return browser.find(isLanguage) ?? 'en'
}

function format(template: string, parameters?: Parameters) {
  if (!parameters) return template
  return template.replace(/\{(\w+)\}/g, (match, name: string) =>
    parameters[name] === undefined ? match : String(parameters[name]),
  )
}

/** Farm work overflows when another citizen took the bed or the water first, not because storage is full. */
const FARM_TASKS = /^(sow-|water-fields$|harvest$)/

const amountFormats = new Map<string, Intl.NumberFormat>()

function numberFormat(language: Language, options: Intl.NumberFormatOptions) {
  const key = `${language}:${JSON.stringify(options)}`
  let formatter = amountFormats.get(key)
  if (!formatter) {
    formatter = new Intl.NumberFormat(language, options)
    amountFormats.set(key, formatter)
  }
  return formatter
}

/** Large amounts are shortened (12K, 12 тыс.); small fractions keep one decimal, written the way the language writes it. */
export function formatAmount(value: number, language: Language = 'en') {
  if (value >= 10000) return numberFormat(language, { notation: 'compact', maximumFractionDigits: 0 }).format(value)
  return numberFormat(language, { maximumFractionDigits: value < 10 ? 1 : 0 }).format(value)
}

export function createTranslator(language: Language) {
  const dictionary = DICTIONARIES[language]
  const t = (key: UiKey, parameters?: Parameters) => format(dictionary.ui[key] ?? en.ui[key], parameters)
  const amount = (value: number) => formatAmount(value, language)
  const held = (kind: string, value: number) =>
    t('amount.held', { amount: amount(value), resource: resource(kind).toLowerCase() })
  const resource = (kind: string) => dictionary.resources[kind] ?? en.resources[kind] ?? kind
  const animal = (kind: string) => dictionary.animals[kind] ?? en.animals[kind] ?? kind
  const animalHint = (kind: string) => dictionary.animalHints[kind] ?? en.animalHints[kind] ?? ''
  const enemy = (kind: string) => dictionary.enemies[kind] ?? en.enemies[kind] ?? kind
  const crop = (kind: string) => dictionary.crops[kind] ?? en.crops[kind] ?? kind
  const cropHint = (kind: string) => dictionary.cropHints[kind] ?? en.cropHints[kind] ?? ''
  /** "+3 wood" in English, "дерево +3" in Russian: the word order avoids number agreement. */
  /** "+2 food"; with `kept` below `value` some was lost: "+2(0) food". */
  const produced = (kind: string, value: number, kept = value) =>
    t('amount.produced', {
      amount: kept < value ? `${amount(value)}(${amount(kept)})` : amount(value),
      resource: resource(kind).toLowerCase(),
    })
  const role = (id: string) => dictionary.roles[id] ?? en.roles[id] ?? id
  const job = (id: string) => dictionary.jobs[id] ?? en.jobs[id] ?? id
  /** Tasks and construction jobs share one name space in reports and the feed. */
  const task = (id: string) => dictionary.tasks[id] ?? en.tasks[id] ?? (id in en.jobs ? job(id) : id)
  const project = (id: string) => dictionary.projects[id] ?? en.projects[id] ?? { name: id, description: '' }
  const building = (kind: string) => dictionary.buildings[kind] ?? en.buildings[kind] ?? { name: kind, description: '' }
  const stage = (id: string) => dictionary.stages[id] ?? en.stages[id] ?? id
  const stat = (value: Stat) => dictionary.stats[value] ?? en.stats[value] ?? value
  const skill = (value: Skill) => dictionary.skills[value] ?? en.skills[value] ?? value
  const technology = (id: string) =>
    dictionary.technologies[id] ?? en.technologies[id] ?? { name: id, description: '' }
  const era = (value: Era) => dictionary.eras[value] ?? en.eras[value]
  /** "Intelligence 5, Charisma 2". */
  const requirements = (list: Array<{ stat: Stat; minimum: number }>) =>
    list.map((r) => `${stat(r.stat)} ${r.minimum}`).join(', ')
  const storage = (kind: string) => dictionary.storages[kind] ?? en.storages[kind] ?? kind
  const questText = (quest: { kind: string; resource: string | null }) => {
    const entry = dictionary.quests[quest.kind] ?? en.quests[quest.kind] ?? { title: quest.kind, story: '' }
    const parameters = { resource: quest.resource ? resource(quest.resource).toLowerCase() : '' }
    return { title: format(entry.title, parameters), story: format(entry.story, parameters) }
  }
  /** The rulers' wording speaks about citizens' work; its numbers, if any, follow it in brackets. */
  const objective = (id: string, ruler: boolean, parameters?: Parameters & { current?: string; required?: string }) => {
    const entry = dictionary.objectives[id] ?? en.objectives[id]
    if (!entry) return id
    if (!ruler) return format(entry.citizen, parameters)
    const text = format(entry.ruler, parameters)
    return parameters?.current !== undefined && parameters.required !== undefined
      ? `${text} (${parameters.current} / ${parameters.required})`
      : text
  }

  /** Server problems carry a code and arguments; identifiers in the arguments are translated too. */
  const error = (exception: unknown) => {
    if (exception instanceof ApiError && exception.code) {
      const template = exception.code === 'storage.missingInput' && typeof exception.parameters.missing === 'number'
        ? dictionary.errors['storage.missingAmount'] ?? en.errors['storage.missingAmount']
        : dictionary.errors[exception.code] ?? en.errors[exception.code]
      if (template) {
        const parameters: Parameters = { ...exception.parameters }
        if (typeof parameters.resource === 'string') parameters.resource = resource(parameters.resource).toLowerCase()
        if (typeof parameters.taskId === 'string') parameters.task = task(parameters.taskId)
        if (typeof parameters.roleId === 'string') parameters.role = role(parameters.roleId)
        if (typeof parameters.animal === 'string') parameters.animal = animal(parameters.animal)
        if (typeof parameters.crop === 'string') parameters.crop = crop(parameters.crop)
        if (typeof parameters.building === 'string') parameters.building = building(parameters.building).name
        if (typeof parameters.project === 'string') parameters.project = project(parameters.project).name
        if (typeof parameters.requires === 'string') {
          parameters.requires = parameters.requires
            .split(',')
            .map((kind) => building(kind).name)
            .join(', ')
        }
        if (typeof parameters.stage === 'string') parameters.stage = stage(parameters.stage).toLowerCase()
        if (typeof parameters.technology === 'string') parameters.technology = technology(parameters.technology).name
        if (typeof parameters.readyAt === 'string') {
          parameters.readyAt = new Date(parameters.readyAt).toLocaleTimeString(language, { hour: '2-digit', minute: '2-digit' })
        }
        if (typeof parameters.technologies === 'string') {
          parameters.technologies = parameters.technologies
            .split(',')
            .map((id) => technology(id).name)
            .join(', ')
        }
        if (typeof parameters.stat === 'string') parameters.stat = stat(parameters.stat as Stat)
        if (typeof parameters.skill === 'string') parameters.skill = skill(parameters.skill as Skill)
        return format(template, parameters)
      }
    }
    return exception instanceof Error ? exception.message : String(exception)
  }

  const describeReport = (report: CycleReport) => {
    const parts: string[] = []
    if (report.fight) {
      const name = enemy(report.fight.enemy).toLowerCase()
      parts.push(t(report.fight.won ? 'report.fightWon' : 'report.fightLost', { enemy: name }))
    }
    for (const item of report.produced) parts.push(produced(item.resource, item.amount))
    for (const kind of report.toolsBroken ?? []) parts.push(t('done.toolBroke', { tool: resource(kind) }))
    if (report.repaired > 0) parts.push(t('report.repaired', { amount: amount(report.repaired) }))
    if (report.moraleGained > 0) parts.push(t('report.morale', { value: amount(report.moraleGained * 100) }))
    if (report.projectContribution > 0 && report.produced.length === 0) {
      parts.push(t('report.contribution', { amount: amount(report.projectContribution) }))
    }
    if (report.levelsGained > 0) parts.push(t('report.level', { stat: stat(report.primary) }))
    if (report.skillLevelsGained > 0) parts.push(t('report.skill', { skill: skill(report.skill) }))
    if (report.rareFind) parts.push(t('report.rare'))
    if (report.buildingCompleted) parts.push(t('done.built', { building: building(report.buildingCompleted).name }))
    if (report.technologyId) {
      const name = technology(report.technologyId).name
      parts.push(report.technologyDiscovered ? t('done.discovered', { technology: name }) : t('report.research', { technology: name }))
    }
    return `${report.citizenName} — ${task(report.taskId)}${parts.length ? `: ${parts.join(', ')}` : ''}`
  }

  const goods = (list: Array<{ resource: string; amount: number }>) =>
    list.map((item) => held(item.resource, item.amount)).join(', ')

  const heroClass = (value: HeroClass) => t(`class.${value}`)
  const heroClassHint = (value: HeroClass) => t(`classHint.${value}`)

  /** One line for the chronicle about a dungeon run that just ended. */
  const describeDungeon = (run: DungeonRun) => {
    const values = { slain: run.slain, foes: run.foes }
    if (run.outcome === 'Victory') return t('feed.dungeon.Victory', values)
    if (run.outcome === 'Defeat') return t('feed.dungeon.Defeat', values)
    return t('feed.dungeon.Abandoned', values)
  }

  /** One line for the chronicle: the outcome, then what it cost or brought. */
  const describeRaid = (report: RaidReport) => {
    const name = enemy(report.enemy)
    const parts = [
      report.outcome==='PropertyDamaged' ? t('raid.propertyDamaged', {enemy:name}) : report.repelled
        ? t('raid.repelled', { killed: report.killed, enemy: name.toLowerCase() })
        : t('raid.lost', { enemy: name }),
    ]
    if (report.waves > 1) parts.push(t('raid.wave', { wave: report.wave, waves: report.waves }))
    if (report.loot.length > 0) parts.push(t('raid.loot', { goods: goods(report.loot) }))
    if (report.stolen.length > 0) parts.push(t('raid.stolen', { goods: goods(report.stolen) }))
    const wounded = [...report.defenders.filter((defender) => defender.injured), ...report.victims].map((citizen) => citizen.name)
    if (wounded.length > 0) parts.push(t('raid.injured', { names: wounded.join(', ') }))
    if (report.damagedPlots.length > 0) parts.push(t('raid.damaged', { count: report.damagedPlots.length }))
    return parts.join(' · ')
  }

  const speechFor = (report: CycleReport) => {
    if (report.fight) return t(report.fight.won ? 'speech.fightWon' : 'speech.fightLost')
    if (report.buildingCompleted) return t('speech.built', { building: building(report.buildingCompleted).name })
    if (report.technologyDiscovered && report.technologyId) {
      return t('speech.discovered', { technology: technology(report.technologyId).name })
    }
    if (report.rareFind) return t('speech.gold')
    if (report.levelsGained > 0) return t('speech.level', { stat: stat(report.primary) })
    if (report.skillLevelsGained > 0) return t('speech.skill', { skill: skill(report.skill) })
    if (report.tired) return t('speech.tired')
    if (report.overflowed && report.produced.length === 0 && FARM_TASKS.test(report.taskId)) return t('speech.fieldTaken')
    if (report.overflowed) return t('speech.full')
    if (report.hungry) return t('speech.hungry')
    if (report.technologyId) return t('speech.research')
    if (report.siteId && report.produced.length === 0) return t('speech.done')
    if (report.taskId === 'carry-to-storage') return t('speech.carried')
    if (report.taskId === 'build') return t('speech.build')
    if (report.taskId === 'survey') return t('speech.survey')
    if (report.taskId === 'supervise') return t('speech.supervise')
    if (report.taskId === 'perform') return t('speech.perform')
    const first = report.produced[0]
    if (first) return produced(first.resource, first.amount)
    return t('speech.done')
  }

  return {
    language,
    t,
    amount,
    produced,
    /** An amount of goods held or carried: "3 wood" in English, "дерево: 3" in Russian. */
    held,
    /** "3 wood, 1 leather". */
    goods,
    resource,
    animal,
    animalHint,
    enemy,
    crop,
    cropHint,
    role,
    task,
    job,
    project,
    building,
    stage,
    stat,
    skill,
    technology,
    era,
    requirements,
    storage,
    quest: questText,
    objective,
    statHint: (value: Stat) => dictionary.statHints[value],
    tier: (value: RoleTier) => dictionary.tiers[value],
    phase: (value: ProjectPhase) => dictionary.phases[value],
    priestLines: dictionary.priestLines,
    error,
    describeReport,
    describeRaid,
    describeDungeon,
    heroClass,
    heroClassHint,
    speechFor,
  }
}

export type Translator = ReturnType<typeof createTranslator>
