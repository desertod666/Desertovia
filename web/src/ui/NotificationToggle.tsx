import { useI18n } from '../i18n'
import { useNotificationSetting } from '../notifications'
import {useEffect,useState} from 'react'
import {NOTIFICATION_CATEGORIES,type NotificationCategory} from '../notificationPolicy'
import type {NotificationDraft} from '../notificationContext'
import {Hint} from './Hint'

/** `compact`: a single button, shown only while notifications are off. */
export function NotificationToggle({ compact = false }: { compact?: boolean }) {
  const { t,language } = useI18n(),text=(ru:string,en:string)=>language==='ru'?ru:en
  const { state, enable, disable,settings,ready,busy,error,save,refresh } = useNotificationSetting()
  const [draft,setDraft]=useState<NotificationDraft|null>(null),[dirty,setDirty]=useState(false),[saved,setSaved]=useState(false)
  useEffect(()=>{
    if(!ready||dirty)return
    const {revision,browserEnabled,categories,quietEnabled,quietStartMinute,quietEndMinute,timeZoneId}=settings
    setDraft({revision,browserEnabled,categories,quietEnabled,quietStartMinute,quietEndMinute,timeZoneId:settings.configured?timeZoneId:Intl.DateTimeFormat().resolvedOptions().timeZone})
  },[settings,ready,dirty])
  const change=(values:Partial<NotificationDraft>)=>{setDirty(true);setSaved(false);setDraft(current=>current?{...current,...values}:current)}
  const labels:Record<NotificationCategory,string>={work:text('Работа и задания','Work and quests'),building:text('Стройка и открытия','Buildings and discoveries'),farm:text('Созревший урожай','Ripe crops'),adventure:text('Сбор и возврат из похода','Expedition invitations and return'),danger:text('Нападения и исход боя','Attacks and battle results'),civic:text('Голосования','Votes')}
  const time=(minute:number)=>Number.isFinite(minute)?`${String(Math.floor(minute/60)).padStart(2,'0')}:${String(minute%60).padStart(2,'0')}`:''
  const minute=(value:string)=>value?Number(value.slice(0,2))*60+Number(value.slice(3)):NaN
  const valid=!!draft&&Number.isInteger(draft.quietStartMinute)&&Number.isInteger(draft.quietEndMinute)
    &&(!draft.quietEnabled||draft.quietStartMinute!==draft.quietEndMinute)&&!!draft.timeZoneId
    &&(()=>{try{new Intl.DateTimeFormat('en',{timeZone:draft.timeZoneId});return true}catch{return false}})()

  if (compact) {
    if (state !== 'off') return null
    return (
      <button className="link-button small" disabled={!ready||busy} onClick={() => void enable()}>
        🔔 {t('notify.enableShort')}
      </button>
    )
  }

  return (
    <section className="card notification-settings">
      <h3>{t('notify.title')}</h3>
      <div className="row"><span className="small muted">{text('На устройстве, пока игра открыта','On this device while the game is open')}</span><Hint className="help-button" label={text('О доставке уведомлений','About notification delivery')} text={text('Категории и тихие часы сохраняются в аккаунте. Разрешение браузера нужно отдельно на каждом устройстве. Отчёты и события игры сохраняются даже при отключённых уведомлениях.','Categories and quiet hours are saved to your account. Browser permission is separate on each device. Reports and game events remain available when notifications are off.')}>ⓘ</Hint></div>
      {error&&<p className="error" role="alert">{error}</p>}
      {!ready&&<p className="small">{t('loading')}</p>}
      {state === 'unsupported' && <p className="small warning">{t('notify.unsupported')}</p>}
      {state === 'denied' && <p className="small warning">{t('notify.denied')}</p>}
      {state === 'off' && (
        <button className="primary" disabled={!ready||busy||dirty} onClick={() => void enable()}>
          {text('Разрешить на этом устройстве','Enable on this device')}
        </button>
      )}
      {state === 'on' && (
        <div className="row">
          <span className="small highlight">✓ {t('notify.on')}</span>
        </div>
      )}
      {settings.browserEnabled&&<button disabled={busy} onClick={()=>void disable()}>{text('Отключить для аккаунта','Disable for account')}</button>}
      {ready&&draft&&<details><summary>{text('События и тихие часы','Events and quiet hours')}</summary>
        <fieldset className="notification-options" disabled={busy}>
        <label className="notification-choice"><input type="checkbox" checked={draft.browserEnabled} onChange={e=>change({browserEnabled:e.target.checked})}/>{text('Браузерные уведомления для аккаунта','Browser notifications for this account')}</label>
        <fieldset><legend>{text('Какие события','Which events')}</legend>{NOTIFICATION_CATEGORIES.map(category=><label key={category} className="notification-choice"><input type="checkbox" checked={draft.categories.includes(category)} onChange={e=>change({categories:NOTIFICATION_CATEGORIES.filter(c=>c===category?e.target.checked:draft.categories.includes(c))})}/>{labels[category]}</label>)}</fieldset>
        <label className="notification-choice"><input type="checkbox" checked={draft.quietEnabled} onChange={e=>change({quietEnabled:e.target.checked})}/>{text('Не беспокоить ночью','Use quiet hours')}</label>
        <div className="notification-times"><label>{text('Не беспокоить с','Quiet from')}<input type="time" value={time(draft.quietStartMinute)} onChange={e=>change({quietStartMinute:minute(e.target.value)})}/></label><label>{text('До','Until')}<input type="time" value={time(draft.quietEndMinute)} onChange={e=>change({quietEndMinute:minute(e.target.value)})}/></label></div>
        <label>{text('Часовой пояс','Time zone')}<input type="text" maxLength={64} value={draft.timeZoneId} placeholder="Europe/Oslo" onChange={e=>change({timeZoneId:e.target.value})}/></label>
        <p className="small muted">{text('Новые события объединяются. Старые тревоги после тихих часов не возвращаются.','New events are grouped. Expired alerts do not return after quiet hours.')}</p>
        <div className="row"><button disabled={busy||!dirty||!valid} onClick={async()=>{try{const result=await save(draft);setDraft(result);setDirty(false);setSaved(true)}catch{/* The shared error preserves the draft and request. */}}}>{text('Сохранить уведомления','Save notifications')}</button>
          <button disabled={busy} onClick={async()=>{try{const result=await refresh();setDraft(result);setDirty(false);setSaved(false)}catch{/* Keep the previous draft. */}}}>{text('Загрузить настройки аккаунта','Reload account settings')}</button></div>
        {saved&&<p className="small" role="status">{text('Настройки сохранены','Settings saved')}</p>}
        </fieldset>
      </details>}
    </section>
  )
}
