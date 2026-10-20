import {useEffect} from 'react'
export {NotificationProvider,useNotificationSetting,useGameNotifications} from './notificationContext'

/** Marks the tab title while the player is away, so a pinned or crowded tab still shows that work is done. */
export function flagTitle(mark: string, baseTitle: string) {
  if (document.visibilityState === 'visible' && document.hasFocus()) return
  document.title = `${mark} ${baseTitle}`
}

export function useTitleReset(baseTitle: string) {
  useEffect(() => {
    const reset = () => {
      if (document.visibilityState === 'visible') document.title = baseTitle
    }
    document.title = baseTitle
    window.addEventListener('focus', reset)
    document.addEventListener('visibilitychange', reset)
    return () => {
      window.removeEventListener('focus', reset)
      document.removeEventListener('visibilitychange', reset)
    }
  }, [baseTitle])
}
