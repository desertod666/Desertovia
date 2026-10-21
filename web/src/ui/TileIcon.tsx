import { tileStyle, type TileRef } from '../game/sheets'
import { RESOURCE_ICONS, taskIcon } from '../game/worldMap'

export function TileIcon({ tile, scale = 2, title }: { tile: TileRef | undefined; scale?: number; title?: string }) {
  if (!tile) return <span style={{ width: 16 * scale, height: 16 * scale, display: 'inline-block' }} />
  return <span className="tile-icon" style={tileStyle(tile, scale)} title={title} />
}

export const ResourceIcon = ({
  kind,
  scale = 2,
  sharpen = 0,
  durability = 0,
  maxDurability = 0,
}: {
  kind: string
  scale?: number
  sharpen?: number
  durability?: number
  maxDurability?: number
}) => {
  // Keep ordinary resource tiles direct children: the compact HUD scales them to 16px.
  // A sharpen or durability badge needs the positioned wrapper.
  if (sharpen <= 0 && maxDurability <= 0) return <TileIcon tile={RESOURCE_ICONS[kind]} scale={scale} />
  return (
    <span className="resource-icon">
      <TileIcon tile={RESOURCE_ICONS[kind]} scale={scale} />
      {sharpen > 0 && <span className="sharpen-badge">+{sharpen}</span>}
      {maxDurability > 0 && (
        <span className={`durability-badge${durability <= 2 ? ' low' : ''}`} title={`${durability}/${maxDurability}`}>
          {durability}
        </span>
      )}
    </span>
  )
}

export const TaskIcon = ({ taskId, scale = 2 }: { taskId: string; scale?: number }) => (
  <TileIcon tile={taskIcon(taskId)} scale={scale} />
)
