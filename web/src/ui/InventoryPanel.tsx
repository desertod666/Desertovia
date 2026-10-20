import { buildingNumber } from './buildingLabel'
import { currentEnergy } from '../energy'
import {MealsPanel} from './MealsPanel'
import { EconomyPanel } from './EconomyPanel'
import type { BuildingDefinition, Catalog, Citizen, StorageKind, VillageState } from '../api'
import { useI18n } from '../i18n'
import { citizenTitle } from './citizenTitle'
import { HandsCard } from './HandsCard'
import { StorageCard } from './StorageCard'
import { ResourceIcon } from './TileIcon'
import { HUD_RESOURCES } from './texts'

export function InventoryPanel({
  village,
  citizen,
  catalog,
  now,
  pending,
  error,
  onCarry,
  onDrop,
  onEat,
  onCitizen,
  onTake,
  onClose,
  onPrivatePlace,
}: {
  village: VillageState
  citizen: Citizen | null | undefined
  catalog: Catalog
  now: number
  pending: boolean
  error: string | null
  onCarry: () => void
  onDrop: () => void
  onCitizen?: (c:Citizen)=>void
  onEat?: () => void
  onTake: (plotId: string, resource: string, amount: number, sharpen?: number, durability?: number) => void
  onPrivatePlace?: (building:BuildingDefinition)=>void
  onClose: () => void
}) {
  const i18n = useI18n()
  const { t } = i18n
  const ownFood=(citizen?.load.find(g=>g.resource==='Food')?.amount??0)+(citizen?.personalGoods?.find(g=>g.kind==='Food')?.amount??0)
  const energy = citizen ? currentEnergy(citizen, now) : 0
  const upgrades:Partial<Record<StorageKind,StorageKind>>={WoodPile:'Woodyard',StonePile:'StoneYard',DryingRack:'Granary'}
  const publicStores=village.storages.filter(s=>!s.ownerAccountId)
  const accepts=(kind:StorageKind,resource:string)=>{
    const definition=catalog.storages.find(d=>d.kind===kind)
    return !!definition&&(definition.accepts.length===0||definition.accepts.includes(resource))
  }
  const resources = village.resources.filter((r) => HUD_RESOURCES.includes(r.kind) || r.amount > 0)
  const weaponKinds = ['Sword', 'Spear', 'Bow', 'Arrows']
  const weaponStacks = new Map<string, { kind: string; sharpen: number; amount: number }>()
  for (const storage of village.storages) {
    if (storage.ownerAccountId) continue
    for (const good of storage.goods) {
      if (!weaponKinds.includes(good.resource) || good.amount <= 0) continue
      const sharpen = good.sharpen ?? 0
      const key = `${good.resource}:${sharpen}`
      const row = weaponStacks.get(key) ?? { kind: good.resource, sharpen, amount: 0 }
      row.amount += good.amount
      weaponStacks.set(key, row)
    }
  }
  const weapons = [...weaponStacks.values()].sort((a, b) => a.kind.localeCompare(b.kind) || b.sharpen - a.sharpen)
  const working = citizen?.cycleEndsAt ? now < Date.parse(citizen.cycleEndsAt) : false
  return (
    <div className="modal-backdrop" onPointerDown={(event) => event.target === event.currentTarget && onClose()}>
      <div className="modal inventory" role="dialog" aria-label={t('inventory.title')}>
        <header className="modal-header">
          <h2>{t('inventory.title')}</h2>
          <button className="close" onClick={onClose} title={`${t('close')} (I)`}>
            ✕
          </button>
        </header>
        <div className="modal-body">
          {citizen && <EconomyPanel village={village} catalog={catalog} citizen={citizen} accountId={citizen.accountId} onPrivatePlace={onPrivatePlace}/>}
          {citizen&&<MealsPanel slug={village.slug} citizen={citizen} now={now} pending={pending} onCitizen={onCitizen}/>}
          {citizen?.isHungry&&onEat&&<button disabled={pending||ownFood===0||citizen.state==='Paused'} onClick={onEat}>{i18n.language==='ru'?'Поесть':'Eat'}{ownFood===0?(i18n.language==='ru'?' · Нет еды при себе':' · No food carried'):''}</button>}
      {error && <p className="error">{error}</p>}
          {citizen && (
            <section className="card">
              <h3>
                {citizen.name} <span className="muted">— {citizenTitle(citizen, i18n)}</span>
              </h3>
              <div className="character-summary">
                <div>
                  <span className="muted small">{t('character.energy')}</span>
                  <strong>
                    {Math.floor(energy)} / {citizen.maximumEnergy}
                  </strong>
                </div>
                <div>
                  <span className="muted small">{t('character.renown')}</span>
                  <strong>{citizen.renown}</strong>
                </div>
                <div>
                  <span className="muted small">{t('character.streak')}</span>
                  <strong>{citizen.streak}</strong>
                </div>
              </div>
            </section>
          )}
          {citizen && (
            <HandsCard citizen={citizen} village={village} catalog={catalog} pending={pending} onCarry={onCarry} onDrop={onDrop} />
          )}

          <section className="card">
            <h3>
              {t('inventory.storage')}{' '}
              <span className="muted">{t('inventory.morale', { value: Math.round(village.morale * 100) })}</span>
            </h3>
            <div className="inventory-grid">
              {weapons.map((weapon) => (
                <div className="slot" key={`${weapon.kind}:${weapon.sharpen}`} title={i18n.resource(weapon.kind)}>
                  <ResourceIcon kind={weapon.kind} scale={3} sharpen={weapon.sharpen} />
                  <span className="slot-name">{i18n.resource(weapon.kind)}</span>
                  <span className="slot-amount">{i18n.amount(weapon.amount)}</span>
                </div>
              ))}
              {resources.filter((resource) => !weaponKinds.includes(resource.kind)).map((resource) => (
                <div className="slot" key={resource.kind} title={i18n.resource(resource.kind)}>
                  <ResourceIcon kind={resource.kind} scale={3} />
                  <span className="slot-name">{i18n.resource(resource.kind)}</span>
                  <span className="slot-amount">
                    {i18n.amount(resource.amount)}
                    {resource.capacity !== null && <span className="muted"> / {i18n.amount(resource.capacity)}</span>}
                  </span>
                  {resource.capacity !== null && (
                    <div className="bar thin">
                      <div style={{ width: `${Math.min(100, (resource.amount / Math.max(resource.capacity, 1)) * 100)}%` }} />
                    </div>
                  )}
                </div>
              ))}
            </div>
            <p className="small muted">{t('inventory.shared')}</p>
            <details className="storage-overview"><summary>{i18n.language==='ru'?'Где есть место для ресурсов':'Resource storage space'}</summary>
              <ul className="feed">{resources.filter(r=>r.kind!=='ResearchPoints').map(resource=>{
                const suitable=publicStores.filter(s=>accepts(s.kind,resource.kind))
                const room=resource.capacity===null?null:Math.max(0,resource.capacity-resource.amount)
                return <li key={resource.kind}><strong>{i18n.resource(resource.kind)}</strong>: {room===null?'∞':i18n.amount(room)} {i18n.language==='ru'?'свободно':'free'}
                  {suitable.length===0?<span className="warning"> · {i18n.language==='ru'?'нет подходящего склада':'no suitable storage'}</span>:<span> · {suitable.map((s,index)=><span key={s.plotId}>{index>0?', ':''}<a href={`#store-${s.plotId}`}>{i18n.storage(s.kind)} {buildingNumber(village,s.plotId)}</a></span>)}</span>}
                  {room===0&&suitable.length>0&&<span className="warning"> · {i18n.language==='ru'?'лимит заполнен':'capacity reached'}</span>}
                </li>
              })}</ul>
            </details>
          </section>
          <section className="card">
            <h3>{t('inventory.buildings')}</h3>
            <ul className="storage-list">
              {publicStores.map((storage) => (
                <li key={storage.plotId} id={`store-${storage.plotId}`}>
                  <details><summary><strong>{i18n.storage(storage.kind)}</strong> {buildingNumber(village,storage.plotId)} <span className="muted small"> · {storage.goods.reduce((n,g)=>n+g.amount,0)}{storage.totalLimit?`/${storage.totalLimit}`:''}</span></summary>
                  <StorageCard
                    storage={storage}
                    catalog={catalog}
                    take={
                      citizen
                        ? {
                            citizen,
                            disabled: pending || working,
                            onTake: (resource, amount, sharpen, durability) => onTake(storage.plotId, resource, amount, sharpen, durability),
                          }
                        : undefined
                    }
                  />
                  {upgrades[storage.kind]&&<p className="small muted">{i18n.language==='ru'?'Следующее улучшение':'Next upgrade'}: {i18n.storage(upgrades[storage.kind]!)}
                    {(()=>{const next=catalog.buildings.find(b=>b.storage===upgrades[storage.kind]);const technology=next?.technology
                      return technology&&!village.technologies.some(t=>t.id===technology&&t.discovered)?` · ${i18n.language==='ru'?'нужно изучить':'research required'} ${i18n.technology(technology).name}`:` · ${i18n.language==='ru'?'заявка через карточку здания':'request through the building card'}`})()}
                  </p>}
                  </details>
                </li>
              ))}
            </ul>
          </section>
        </div>
      </div>
    </div>
  )
}
