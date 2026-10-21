import { useState } from 'react'
import { storageBuildingsFor, type Catalog, type Citizen, type VillageState } from '../api'
import { useI18n } from '../i18n'
import { ResourceIcon } from './TileIcon'
/**
 * Goods the citizen carries until they take them to storage.
 * "Tell the Tsar" copies a ready-made line for the player to paste into the stream chat.
 */
export function HandsCard({
  citizen,
  village,
  catalog,
  pending,
  onCarry,
  onDrop,
}: {
  citizen: Citizen
  village: VillageState
  catalog: Catalog
  pending: boolean
  onCarry: () => void
  onDrop: () => void
}) {
  const i18n = useI18n()
  const { t } = i18n
  const [copied, setCopied] = useState(false)
  if (citizen.load.length === 0) return null

  const tell = async () => {
    const lines = citizen.load.map((good) => {
      const stock = village.resources.find((r) => r.kind === good.resource)
      const building = storageBuildingsFor(catalog, good.resource)[0]
      const parameters = {
        resource: i18n.resource(good.resource),
        amount: stock?.amount ?? 0,
        capacity: stock?.capacity ?? 0,
        building: building ? i18n.building(building.kind).name : '',
      }
      return t(building ? 'tell.build' : 'tell.more', parameters)
    })
    await navigator.clipboard.writeText(lines.join(' '))
    setCopied(true)
    window.setTimeout(() => setCopied(false), 4000)
  }

  return (
    <section className="card hands">
      <h3>{t('hands.title')}</h3>
      <ul className="done-results">
        {citizen.load.map((good) => (
          <li key={`${good.resource}:${good.sharpen ?? 0}:${good.durability ?? 0}`}>
            <ResourceIcon
              kind={good.resource}
              scale={1}
              sharpen={good.sharpen ?? 0}
              durability={good.durability ?? 0}
              maxDurability={good.maxDurability ?? 0}
            />{' '}
            {i18n.held(good.resource, good.amount)}
            {(good.maxDurability ?? 0) > 0 && (
              <span className="muted"> {t('hands.durability', { left: good.durability ?? 0, max: good.maxDurability ?? 0 })}</span>
            )}
          </li>
        ))}
      </ul>
      <p className="small muted">{t('hands.hint')}</p>
      <div className="row">
        <button className="primary" disabled={pending} onClick={onCarry}>
          {t('hands.carry')}
        </button>
        <button onClick={tell}>{t('hands.tell')}</button>
        <button disabled={pending} onClick={onDrop}>
          {t('hands.drop')}
        </button>
      </div>
      {copied && <p className="small highlight">{t('hands.copied')}</p>}
    </section>
  )
}
