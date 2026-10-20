import { useState } from 'react'
import type { Catalog, Citizen, ResourceAmount, Storage } from '../api'
import { useI18n } from '../i18n'
import { ResourceIcon } from './TileIcon'
import { Hint } from './Hint'

/** Lets the player's citizen take goods out of the building into their hands. */
export interface StorageTake {
  citizen: Citizen
  disabled: boolean
  onTake: (resource: string, amount: number, sharpen?: number, durability?: number) => void
}

/** One storage building: what it takes, how many slots are used and what lies inside. */
export function StorageCard({ storage, catalog, take }: { storage: Storage; catalog: Catalog; take?: StorageTake }) {
  const i18n = useI18n()
  const { t } = i18n
  const info = catalog.storages.find((s) => s.kind === storage.kind)
  const limit = catalog.rules.takeLimit

  return (
    <div className="storage-card">
      {info && (
        <p className="muted small">
          {storage.resourceLimit ? `До ${storage.resourceLimit} каждого ресурса${storage.totalLimit ? ` · всего ${storage.totalLimit}` : ''}` : t('inventory.slots', { used: storage.slots.length, total: info.slots, size: info.slotSize })}
          {' '}
          <Hint className="help-button" label={t('storage.acceptsHelp')} text={[(info.accepts.length === 0
            ? t('storage.acceptsAll')
            : t('storage.accepts', { goods: info.accepts.map((kind) => i18n.resource(kind)).join(', ') })), ...(take ? [t('storage.takeHint', {amount: limit})] : [])].join('\n')}>ⓘ</Hint>
        </p>
      )}
      {storage.goods.length === 0 && <span className="muted small">{t('inventory.emptyBuilding')}</span>}
      {take ? (
        <ul className="storage-take">
          {storage.goods.map((good) => (
            <TakeRow key={`${good.resource}:${good.sharpen ?? 0}:${good.durability ?? 0}`} good={good} limit={limit} take={take} />
          ))}
        </ul>
      ) : (
        <div className="storage-goods">
          {storage.goods.map((good) => (
            <span key={`${good.resource}:${good.sharpen ?? 0}:${good.durability ?? 0}`} className="cost" title={i18n.resource(good.resource)}>
              <ResourceIcon
                kind={good.resource}
                scale={1}
                sharpen={good.sharpen ?? 0}
                durability={good.durability ?? 0}
                maxDurability={good.maxDurability ?? 0}
              />{' '}
              {i18n.amount(good.amount)}
              {(good.maxDurability ?? 0) > 0 && ` ${good.durability ?? 0}/${good.maxDurability}`}
            </span>
          ))}
        </div>
      )}
      {!info && take && storage.goods.length > 0 && <p className="small muted">{t('storage.takeHint', { amount: i18n.amount(limit) })}</p>}
    </div>
  )
}

/** One stored good with a number field that starts at the most the citizen can still carry. */
function TakeRow({ good, limit, take }: { good: ResourceAmount; limit: number; take: StorageTake }) {
  const i18n = useI18n()
  const { t } = i18n
  const held = take.citizen.load
    .filter((carried) => carried.resource === good.resource)
    .reduce((sum, carried) => sum + carried.amount, 0)
  const most = Math.max(0, Math.min(limit - held, good.amount))
  const [chosen, setChosen] = useState<string | null>(null)
  const draft = chosen ?? String(most)
  const amount = Number(draft)
  const valid = draft.trim() !== '' && Number.isInteger(amount) && amount >= 1 && amount <= most

  return (
    <li>
      <ResourceIcon
        kind={good.resource}
        scale={1}
        sharpen={good.sharpen ?? 0}
        durability={good.durability ?? 0}
        maxDurability={good.maxDurability ?? 0}
      />
      <span>
        {i18n.resource(good.resource)}
        {(good.maxDurability ?? 0) > 0 && (
          <span className="muted"> {t('hands.durability', { left: good.durability ?? 0, max: good.maxDurability ?? 0 })}</span>
        )}
      </span>
      <span className="muted">{i18n.amount(good.amount)}</span>
      <input
        type="number"
        className="take-amount"
        min={1}
        max={most}
        step={1}
        value={draft}
        disabled={take.disabled || most < 1}
        aria-label={t('storage.takeAmount')}
        onChange={(event) => {
          setChosen(event.target.value)
        }}
      />
      <button
        disabled={take.disabled || most < 1 || !valid}
        title={t('storage.takeTitle', { amount: i18n.amount(limit) })}
        onClick={() => {
          if (valid) take.onTake(good.resource, amount, good.sharpen ?? 0, (good.maxDurability ?? 0) > 0 ? good.durability ?? 0 : undefined)
        }}
      >
        {t('storage.take')}
      </button>
    </li>
  )
}
