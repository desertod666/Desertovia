import { MAP_HEIGHT, MAP_LEFT, MAP_TOP, MAP_WIDTH, Terrain } from './worldMap'
const TERRAIN_COLORS: Record<Terrain, [number, number, number]> = {
  [Terrain.Grass]: [126, 200, 80],
  [Terrain.Tree]: [47, 107, 42],
  [Terrain.Water]: [79, 184, 232],
  [Terrain.Stone]: [156, 156, 156],
  [Terrain.Bush]: [140, 70, 60],
  [Terrain.Reeds]: [156, 138, 58],
  [Terrain.Clearing]: [176, 150, 100],
  [Terrain.Mountain]: [104, 96, 92],
  [Terrain.Ravine]: [52, 38, 28],
  [Terrain.Thicket]: [36, 80, 31],
  [Terrain.Rockfall]: [130, 122, 114],
  [Terrain.Marsh]: [95, 122, 69],
  [Terrain.Cave]: [28, 23, 18],
  [Terrain.Lookout]: [200, 190, 175],
  [Terrain.Trail]: [156, 127, 90],
}

export interface MinimapMarkers {
  buildings: Array<{ x: number; y: number; width: number; height: number; finished: boolean }>
  citizens: Array<{ x: number; y: number; highlighted: boolean }>
  camp: { x: number; y: number }
}

interface Rectangle {
  x: number
  y: number
  width: number
  height: number
}

/**
 * Small overview of the whole map in a corner. Shows the land, buildings, citizens and the visible area;
 * a click or drag moves the view there. Coordinates are tiles.
 */
export class Minimap {
  private readonly root = document.createElement('div')
  private readonly base = document.createElement('canvas')
  private readonly overlay = document.createElement('canvas')
  private view: Rectangle = { x: 0, y: 0, width: 0, height: 0 }
  private markers: MinimapMarkers | null = null
  /** Fog of war, one pixel per tile, drawn over the land. */
  private fog: HTMLCanvasElement | null = null

  constructor(host: HTMLElement, onPick: (tileX: number, tileY: number) => void) {
    this.root.className = 'minimap'
    this.base.width = MAP_WIDTH
    this.base.height = MAP_HEIGHT
    this.overlay.width = MAP_WIDTH * 4
    this.overlay.height = MAP_HEIGHT * 4
    this.root.append(this.base, this.overlay)
    host.appendChild(this.root)

    const pick = (event: PointerEvent) => {
      // Match the drawn map, excluding the frame. On a112px phone minimap its2px border spans multiple tiles.
      const bounds = this.base.getBoundingClientRect()
      const tileX = MAP_LEFT + ((event.clientX - bounds.left) / bounds.width) * MAP_WIDTH
      const tileY = MAP_TOP + ((event.clientY - bounds.top) / bounds.height) * MAP_HEIGHT
      onPick(tileX, tileY)
    }
    this.root.addEventListener('pointerdown', (event) => {
      event.stopPropagation()
      this.root.setPointerCapture(event.pointerId)
      pick(event)
    })
    this.root.addEventListener('pointermove', (event) => {
      if (this.root.hasPointerCapture(event.pointerId)) pick(event)
    })
    this.root.addEventListener('wheel', (event) => event.stopPropagation())
  }

  destroy() {
    this.root.remove()
  }

  setTerrain(terrain: Uint8Array) {
    const context = this.base.getContext('2d')
    if (!context) return
    const image = context.createImageData(MAP_WIDTH, MAP_HEIGHT)
    terrain.forEach((kind, index) => {
      const [red, green, blue] = TERRAIN_COLORS[kind as Terrain] ?? TERRAIN_COLORS[Terrain.Grass]
      image.data.set([red, green, blue, 255], index * 4)
    })
    context.putImageData(image, 0, 0)
  }

  /** The canvas is read again on every redraw, so changes to it show up with the next markers. */
  setFog(fog: HTMLCanvasElement) {
    this.fog = fog
    this.draw()
  }

  setView(view: Rectangle) {
    this.view = view
    this.draw()
  }

  setMarkers(markers: MinimapMarkers) {
    this.markers = markers
    this.draw()
  }

  private draw() {
    const context = this.overlay.getContext('2d')
    if (!context) return
    const unit = this.overlay.width / MAP_WIDTH
    const x = (tile: number) => (tile - MAP_LEFT) * unit
    const y = (tile: number) => (tile - MAP_TOP) * unit
    context.clearRect(0, 0, this.overlay.width, this.overlay.height)
    if (this.fog) {
      context.imageSmoothingEnabled = true
      context.drawImage(this.fog, 0, 0, this.overlay.width, this.overlay.height)
    }

    const markers = this.markers
    if (markers) {
      for (const building of markers.buildings) {
        context.fillStyle = building.finished ? '#f4d35e' : 'rgba(244, 211, 94, 0.45)'
        context.strokeStyle = '#3a2a1a'
        context.lineWidth = 1
        context.fillRect(x(building.x), y(building.y), building.width * unit, building.height * unit)
        context.strokeRect(x(building.x), y(building.y), building.width * unit, building.height * unit)
      }
      context.fillStyle = '#ff7b1c'
      context.beginPath()
      context.arc(x(markers.camp.x), y(markers.camp.y), unit * 1.2, 0, Math.PI * 2)
      context.fill()
      for (const citizen of markers.citizens) {
        context.fillStyle = citizen.highlighted ? '#ffe066' : '#ffffff'
        context.strokeStyle = '#1b1206'
        context.beginPath()
        context.arc(x(citizen.x), y(citizen.y), citizen.highlighted ? unit * 1.3 : unit * 0.9, 0, Math.PI * 2)
        context.fill()
        context.stroke()
      }
    }

    context.strokeStyle = '#ffffff'
    context.lineWidth = 2
    context.strokeRect(x(this.view.x), y(this.view.y), this.view.width * unit, this.view.height * unit)
  }
}
