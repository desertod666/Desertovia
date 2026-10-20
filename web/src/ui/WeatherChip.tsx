import { useEffect, useLayoutEffect, useRef, useState, type CSSProperties } from 'react'
import {
  RAIN,
  unlockedTasks,
  type Catalog,
  type Citizen,
  type CitizenSummary,
  type Forecast,
  type VillageState,
  type WeatherKind,
} from '../api'
import { useI18n } from '../i18n'
import { useSinglePopup } from './Hint'

const percent = (multiplier: number) => Math.round(Math.abs(multiplier - 1) * 100)

const KIND_TEXT = {
  Clear: 'weather.sun',
  Rain: 'weather.rain',
  Wind: 'weather.wind',
  Thunderstorm: 'weather.thunderstorm',
} as const satisfies Record<WeatherKind, string>

/**
 * The sky of the current daytime or night in the top bar. Opens the forecasts, what rain does, and for a Synoptic
 * who has just slept the two announcements; the weather then tends to do the opposite.
 */
export function WeatherChip({
  village,
  catalog,
  citizen,
  citizens,
  onForecast,
}: {
  village: VillageState
  catalog: Catalog
  /** The player's citizen; null for the Tsar and watchers. */
  citizen: Citizen | null | undefined
  citizens: Map<string, CitizenSummary>
  onForecast: (rain: boolean) => Promise<void>
}) {
  const i18n = useI18n()
  const { t } = i18n
  const { weather } = village
  const [open, setOpen] = useState(false)
  const [pending, setPending] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const chip = useRef<HTMLButtonElement>(null)
  const [place, setPlace] = useState<CSSProperties>()
  const root = useRef<HTMLSpanElement>(null)
  useSinglePopup(open, () => setOpen(false))

  useEffect(() => {
    if (!open) return
    const onPointerDown = (event: PointerEvent) => {
      if (!root.current?.contains(event.target as Node)) setOpen(false)
    }
    document.addEventListener('pointerdown', onPointerDown, true)
    return () => document.removeEventListener('pointerdown', onPointerDown, true)
  }, [open])

  useLayoutEffect(() => {
    if (!open) return
    const measure = () => {
      const rect = chip.current?.getBoundingClientRect()
      if (!rect) return
      const margin = 8
      const width = Math.min(320, window.innerWidth - 2 * margin)
      const left = Math.max(margin, Math.min(rect.right - width, window.innerWidth - width - margin))
      setPlace({ top: rect.bottom + 6, left, width, maxHeight: window.innerHeight - rect.bottom - 6 - margin })
    }
    measure()
    window.addEventListener('resize', measure)
    return () => window.removeEventListener('resize', measure)
  }, [open])

  const sky = weather.kind === 'Clear' && weather.night ? t('weather.clearNight') : t(KIND_TEXT[weather.kind])
  const nameOf = (forecast: Forecast) => citizens.get(forecast.citizenId)?.name ?? t('weather.someone')
  const minutesLeft = Math.max(0, Math.ceil((Date.parse(weather.periodEndsAt) - Date.now()) / 60_000))
  const changesIn =
    minutesLeft >= 60 ? t('weather.inHours', { hours: Math.floor(minutesLeft / 60), minutes: minutesLeft % 60 }) : t('weather.inMinutes', { minutes: minutesLeft })
  const coming = weather.night ? t('weather.comingDay') : t('weather.comingNight')

  const unlocked = unlockedTasks(village, catalog)
  const indoor = catalog.tasks.some(
    (task) => (task.requiredBuilding === 'Library' || task.requiredBuilding === 'Tavern') && unlocked.has(task.id),
  )
  const synoptic = citizen?.forecastReliability != null ? citizen : null
  const rested =
    synoptic !== null &&
    (synoptic.state === 'Idle' || (synoptic.state === 'AwaitingRenewal' && synoptic.graceEndsAt !== null && Date.now() > Date.parse(synoptic.graceEndsAt)))

  const forecast = async (rain: boolean) => {
    setPending(true)
    setError(null)
    try {
      await onForecast(rain)
    } catch (exception) {
      setError(i18n.error(exception))
    } finally {
      setPending(false)
    }
  }

  return (
    <span ref={root} className="weather">
      <button
        ref={chip}
        className={`resource-chip weather-chip weather-${weather.kind.toLowerCase()}${weather.raining ? ' raining' : ''}${weather.night ? ' night' : ''}`}
        onClick={() => setOpen(!open)}
        title={t('weather.title')}
        aria-label={sky}
        aria-expanded={open}
      >
        <span className="weather-icon" aria-hidden />
        <span className="weather-name">{sky}</span>
      </button>
      {open && (
        <div className="weather-panel" style={place}>
          <div className="weather-row">
            <strong>{t('weather.today', { sky })}</strong>
            <span className="muted small">{t(weather.night ? 'weather.dawnIn' : 'weather.nightIn', { time: changesIn })}</span>
          </div>
          {weather.current && (
            <p className="small">
              {t(weather.current.saysRain ? 'weather.promisedRain' : 'weather.promisedSun', { name: nameOf(weather.current) })}{' '}
              {weather.current.saysRain === weather.raining ? t('weather.cameTrue') : t('weather.cameWrong')}
            </p>
          )}
          <p className="small">
            {weather.next
              ? t(weather.next.saysRain ? 'weather.promisesRain' : 'weather.promisesSun', { name: nameOf(weather.next), when: coming })
              : t('weather.noForecast')}
          </p>

          <p className="small weather-effects-title">{t('weather.effectsTitle')}</p>
          <ul className="weather-effects small">
            {unlocked.has('water-fields') && <li className="good">{t('weather.effectFields')}</li>}
            {unlocked.has('fish') && <li className="good">{t('weather.effectFishing', { percent: percent(RAIN.fishingYield) })}</li>}
            {indoor && <li className="good">{t('weather.effectIndoor', { percent: percent(RAIN.indoorYield) })}</li>}
            <li className="good">{t('weather.effectForest', { times: RAIN.forestRegrowth })}</li>
            <li className="bad">{t('weather.effectEnergy', { percent: percent(RAIN.outdoorEnergyCost) })}</li>
            <li className="bad">{t('weather.effectSleep', { percent: percent(RAIN.underTreesRegeneration) })}</li>
            {unlocked.has('tend-fire') && <li className="bad">{t('weather.effectFire', { percent: percent(RAIN.fireMorale) })}</li>}
            <li className="bad">{t('weather.effectMud', { percent: percent(RAIN.mudYield) })}</li>
            <li className="bad">{t('weather.effectHunting', { percent: percent(RAIN.huntingYield) })}</li>
          </ul>

          {synoptic && (
            <div className="weather-synoptic">
              <p className="small">
                {t('weather.synoptic')}
              </p>
              {weather.next ? null : rested ? (
                <div className="weather-buttons">
                  <button disabled={pending} onClick={() => forecast(false)}>
                    {t('weather.saySun')}
                  </button>
                  <button disabled={pending} onClick={() => forecast(true)}>
                    {t('weather.sayRain')}
                  </button>
                </div>
              ) : (
                <p className="muted small">{t('weather.sleepFirst')}</p>
              )}
              {error && <p className="error small">{error}</p>}
            </div>
          )}
        </div>
      )}
    </span>
  )
}
