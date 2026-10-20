import { ResourceIcon } from './TileIcon'

/** Reuses the gold artwork; the monetary account is separate from the Gold material. */
export function EconomicGoldIcon() {
  return <span className="economic-gold-icon" aria-hidden="true"><ResourceIcon kind="Gold" scale={1.5}/></span>
}
