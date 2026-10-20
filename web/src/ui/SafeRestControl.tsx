import { useState } from 'react'
import type { RestState } from '../api'
import { useI18n } from '../i18n'
import { Hint } from './Hint'

export function SafeRestControl({state, onChange}: {state: RestState; onChange: (resting: boolean) => Promise<void>}) {
  const i18n = useI18n(), ru = i18n.language === 'ru'
  const [pending, setPending] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const resting = state !== 'Active'
  return <div className="safe-rest-control" data-state={state}>
    <button type="button" disabled={pending} aria-pressed={resting} onClick={async () => {
      if (pending) return
      setPending(true); setError(null)
      try { await onChange(!resting) } catch (reason) { setError(i18n.error(reason)) } finally { setPending(false) }
    }}>{state === 'Resting' ? (ru ? '☀ Проснуться' : '☀ Wake up') : state === 'RestRequested' ? (ru ? '◷ После работы' : '◷ After work') : (ru ? '☾ Отдых' : '☾ Safe rest')}</button>
    <Hint label={ru ? 'Правила безопасного отдыха' : 'Safe rest rules'} text={ru
      ? 'Начатая работа завершится и будет оплачена один раз. Затем герой защищён от новых боёв, не работает и не получает новый опыт. Рост растений и восстановление сил продолжаются. Из боя сначала отступите; из данжа вернитесь. Повторное нажатие отменяет ожидание отдыха или будит героя.'
      : 'Accepted work finishes and is paid once. Then the hero is protected from new combat and earns no new work or experience. Plants and stamina recovery continue. Retreat from combat and return from the dungeon first. Press again to cancel a rest request or wake up.'}>ⓘ</Hint>
    {error && <span role="alert" className="error">{error}</span>}
  </div>
}
