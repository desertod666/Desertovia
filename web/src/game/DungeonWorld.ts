import { registerReportCanvas } from '../reports/capture'
import { Application, CanvasSource, Container, Graphics, Sprite, Text, Texture, type Ticker } from 'pixi.js'
import {
  DUNGEON_TILES,
  type Appearance,
  type DungeonEvent,
  type DungeonRunView,
  type DungeonSnapshot,
  type DungeonTile,
  type HeroClass,
  type HeroState,
} from '../api'
import { appearanceKey, CharacterPainter } from './appearance'
import { enemyContext, enemyHeight } from './enemies'
import { dungeon as dungeonTile, TILE, TileSet } from './sheets'
import { VILLAGER_SPRITES } from './worldMap'

const LABEL_FONT = 'system-ui, "Segoe UI", sans-serif'
/** About this many tiles across the screen at the default zoom. */
const DEFAULT_VIEW_TILES = 22
/** At least48px across the selection circle on a narrow screen. */
const MINIMUM_DEFAULT_TILE_PIXELS = 32
const MAXIMUM_SCALE = 6
/** Figures glide toward the latest snapshot; further than this many tiles they jump. */
const JUMP_TILES = 3
const GLIDE_RATE = 12
const FLOAT_SECONDS = 1
const ARROW_SECONDS = 0.25
const CORPSE_SECONDS = 4
/** A click this close to a figure, in tiles, picks it. */
const PICK_RADIUS = 0.75

const WALKABLE: DungeonTile[] = ['Ground', 'DeadGrass', 'Bones', 'Entrance']

export interface DungeonPickHandlers {
  onFoe: (foeId: number) => void
  onHero: (heroId: string) => void
  onGround: (x: number, y: number) => void
}

interface Figure {
  view: Container
  body: Container
  bar: Graphics
  /** In tiles. */
  x: number
  y: number
  targetX: number
  targetY: number
  health: number
  maximumHealth: number
  phase: number
}

interface HeroFigure extends Figure {
  id: string
  heroClass: HeroClass
  state: HeroState
  label: Text
  skull: Container
}

interface FoeFigure extends Figure {
  id: number
  dead: boolean
  diedAt: number
}

interface Effect {
  view: Container
  startedAt: number
  seconds: number
  update: (share: number) => void
}

function hash(x: number, y: number) {
  let value = Math.imul(x, 0x27d4eb2d) ^ Math.imul(y, 0x165667b1)
  value = Math.imul(value ^ (value >>> 15), 0x85ebca6b)
  return (value ^ (value >>> 13)) >>> 0
}

function hashText(text: string): number {
  let value = 2166136261
  for (let index = 0; index < text.length; index++) {
    value ^= text.charCodeAt(index)
    value = Math.imul(value, 16777619)
  }
  return value >>> 0
}

/** Dead ground: grey-brown earth, withered grass, bones, graves and bare trees, drawn in code. */
function drawTile(graphics: Graphics, tile: DungeonTile, column: number, row: number) {
  const x = column * TILE
  const y = row * TILE
  const noise = hash(column, row)
  const earth = [0x4a4238, 0x463e35, 0x4e453a][noise % 3]
  graphics.rect(x, y, TILE, TILE).fill({ color: tile === 'Entrance' ? 0x6a5e4c : earth })
  // Specks of grit on every tile.
  for (let index = 0; index < 3; index++) {
    const speck = hash(column * 7 + index, row * 13 + index)
    graphics.rect(x + (speck % 15), y + ((speck >> 4) % 15), 1, 1).fill({ color: 0x3a332b })
  }
  switch (tile) {
    case 'DeadGrass':
      for (let index = 0; index < 4; index++) {
        const tuft = hash(column * 31 + index, row * 17 + index)
        const tx = x + 2 + (tuft % 12)
        const ty = y + 4 + ((tuft >> 5) % 10)
        graphics.moveTo(tx, ty).lineTo(tx - 1, ty - 3).moveTo(tx, ty).lineTo(tx + 1, ty - 4)
      }
      graphics.stroke({ color: 0x8a7a50, width: 1 })
      break
    case 'Bones':
      graphics.rect(x + 3, y + 9, 7, 1).fill({ color: 0xd8d0b8 })
      graphics.rect(x + 2, y + 8, 2, 3).fill({ color: 0xd8d0b8 })
      graphics.rect(x + 9, y + 8, 2, 3).fill({ color: 0xd8d0b8 })
      graphics.circle(x + 11, y + 4, 2).fill({ color: 0xe0d8c0 })
      graphics.rect(x + 10, y + 4, 1, 1).fill({ color: 0x2a241c })
      break
    case 'Entrance':
      graphics.rect(x, y + 2, TILE, 1).fill({ color: 0x7a6e5a })
      graphics.rect(x, y + 13, TILE, 1).fill({ color: 0x5a4e3e })
      break
    case 'DeadTree': {
      graphics.ellipse(x + 8, y + 15, 5, 1.5).fill({ color: 0x1b1206, alpha: 0.35 })
      graphics.rect(x + 7, y + 4, 2, 11).fill({ color: 0x3a2e24 })
      graphics
        .moveTo(x + 8, y + 7)
        .lineTo(x + 3, y + 2)
        .moveTo(x + 8, y + 6)
        .lineTo(x + 13, y + 1)
        .moveTo(x + 5, y + 4)
        .lineTo(x + 4, y)
        .moveTo(x + 11, y + 3)
        .lineTo(x + 14, y + 4)
        .stroke({ color: 0x3a2e24, width: 1 })
      break
    }
    case 'Grave':
      graphics.rect(x + 3, y + 12, 10, 3).fill({ color: 0x3a3228 })
      graphics.roundRect(x + 4, y + 2, 8, 11, 3).fill({ color: 0x7a7a80 })
      graphics.rect(x + 7, y + 4, 2, 6).fill({ color: 0x5a5a60 })
      graphics.rect(x + 5, y + 6, 6, 2).fill({ color: 0x5a5a60 })
      break
    case 'Fence':
      graphics.rect(x, y + 6, TILE, 2).fill({ color: 0x5a4632 })
      graphics.rect(x, y + 11, TILE, 2).fill({ color: 0x5a4632 })
      graphics.rect(x + 2, y + 3, 2, 12).fill({ color: 0x4a3828 })
      graphics.rect(x + 11, y + 4, 2, 11).fill({ color: 0x4a3828 })
      break
    case 'Mound':
      graphics.ellipse(x + 8, y + 11, 8, 5).fill({ color: 0x5a4e3a })
      graphics.ellipse(x + 8, y + 9, 6, 4).fill({ color: 0x665840 })
      graphics.rect(x + 7, y + 4, 2, 4).fill({ color: 0x8a8a90 })
      break
    case 'Rock':
      graphics.ellipse(x + 8, y + 10, 7, 5).fill({ color: 0x5a5a5e })
      graphics.ellipse(x + 7, y + 8, 5, 3.5).fill({ color: 0x6e6e74 })
      graphics.rect(x + 5, y + 6, 2, 1).fill({ color: 0x8a8a90 })
      break
    default:
      break
  }
}

/** A sign of the class beside the hero: spear, sword, bow, shield, healer's cross or the Voivode's crown. */
function drawClassSign(heroClass: HeroClass): Graphics {
  const graphics = new Graphics()
  switch (heroClass) {
    case 'Spearman':
      graphics.moveTo(5, -2).lineTo(5, -15).stroke({ color: 0x8a6a40, width: 1 })
      graphics.poly([4, -15, 6, -15, 5, -18]).fill({ color: 0xc0c0c8 })
      break
    case 'Swordsman':
      graphics.rect(5, -13, 1, 8).fill({ color: 0xd0d0d8 })
      graphics.rect(3, -6, 5, 1).fill({ color: 0x8a6a40 })
      break
    case 'Archer':
      graphics.arc(4, -8, 5, -Math.PI / 2, Math.PI / 2).stroke({ color: 0x8a6a40, width: 1 })
      graphics.moveTo(4, -13).lineTo(4, -3).stroke({ color: 0xe0e0e0, width: 0.5 })
      break
    case 'Shieldbearer':
      graphics.roundRect(3, -10, 5, 6, 2).fill({ color: 0x8a5a2a }).stroke({ color: 0xc0a060, width: 1 })
      break
    case 'Healer':
      graphics.rect(4, -12, 1, 5).fill({ color: 0xff5050 })
      graphics.rect(2, -10, 5, 1).fill({ color: 0xff5050 })
      break
    case 'Voivode':
      graphics.poly([-4, -17, -4, -19, -2, -18, 0, -20, 2, -18, 4, -19, 4, -17]).fill({ color: 0xffd23a })
      break
    default:
      break
  }
  return graphics
}

function drawSkullBubble(): Container {
  const view = new Container()
  const bubble = new Graphics()
  bubble.roundRect(-6, -13, 12, 11, 3).fill({ color: 0xfff8e7 }).stroke({ color: 0x1b1206, width: 1 })
  bubble.poly([-2, -2, 2, -2, 0, 1]).fill({ color: 0xfff8e7 })
  bubble.circle(0, -9, 3.2).fill({ color: 0x2a241c })
  bubble.rect(-2, -7, 4, 2).fill({ color: 0x2a241c })
  bubble.rect(-2, -10, 1, 1).fill({ color: 0xfff8e7 })
  bubble.rect(1, -10, 1, 1).fill({ color: 0xfff8e7 })
  view.addChild(bubble)
  return view
}

function drawBar(bar: Graphics, share: number, color: number) {
  bar.clear()
  bar.rect(-6, 0, 12, 2).fill({ color: 0x1b1206 })
  bar.rect(-6, 0, 12 * Math.max(0, Math.min(1, share)), 2).fill({ color })
}

/**
 * A dungeon run drawn from the server's snapshots: the dead-land map, the heroes with their health and class, the
 * undead, blows, arrows and heals. Clicking a foe attacks it, an ally heals them, the ground walks there.
 */
export class DungeonWorld {
  private reportPreviousFps = 0
  private reportObscured = false
  private reportCaptureCleanup: (() => void) | null = null
  private readonly app: Application
  private readonly tiles: TileSet
  private readonly painter: CharacterPainter
  private readonly lookTextures = new Map<string, Texture>()
  private readonly world = new Container()
  private readonly ground = new Graphics()
  private readonly marks = new Graphics()
  private readonly objects = new Container()
  private readonly effects = new Container()
  private readonly heroes = new Map<string, HeroFigure>()
  private readonly foes = new Map<number, FoeFigure>()
  private readonly running: Effect[] = []
  private view: DungeonRunView | null = null
  private walkable: boolean[] = []
  private width = 0
  private height = 0
  private floor = 0
  private myHeroId: string | null = null
  private handlers: DungeonPickHandlers | null = null
  private elapsed = 0
  private scale = 1
  /** Centre of the view, in tiles. */
  private centerX = 0
  private centerY = 0
  /** The camera follows the player's hero until they drag the map. */
  private following = true
  private dragging: { x: number; y: number; centerX: number; centerY: number; moved: boolean } | null = null

  private constructor(app: Application, tiles: TileSet, painter: CharacterPainter) {
    this.app = app
    this.reportCaptureCleanup = registerReportCanvas(app.canvas, () => app.renderer.extract.canvas({ target: app.stage, frame: app.screen, resolution: 1 }) as HTMLCanvasElement, visible => {
      if (visible === this.reportObscured) return
      this.reportObscured = visible
      if (visible) { this.reportPreviousFps = app.ticker.maxFPS; app.ticker.maxFPS = 8 }
      else app.ticker.maxFPS = this.reportPreviousFps
    })
    this.tiles = tiles
    this.painter = painter
    this.objects.sortableChildren = true
    this.world.addChild(this.ground, this.marks, this.objects, this.effects)
    app.stage.addChild(this.world)
    app.ticker.add((ticker) => this.update(ticker))
    app.renderer.on('resize', () => this.layoutCamera())
    this.bindPointer()
  }

  static async create(host: HTMLElement): Promise<DungeonWorld> {
    const [tiles, painter, app] = await Promise.all([TileSet.load(), CharacterPainter.load(), Promise.resolve(new Application())])
    await app.init({
      resizeTo: host,
      background: '#1a1714',
      antialias: false,
      roundPixels: true,
      autoDensity: true,
      resolution: Math.min(window.devicePixelRatio || 1, 2),
    })
    host.appendChild(app.canvas)
    return new DungeonWorld(app, tiles, painter)
  }

  destroy() {
    this.reportCaptureCleanup?.()
    this.app.canvas.remove()
    this.app.destroy(true, { children: true })
    for (const texture of this.lookTextures.values()) texture.destroy(true)
  }

  setHandlers(handlers: DungeonPickHandlers | null) {
    this.handlers = handlers
  }

  /** The map once per run. Heroes stay off it until their march to the edge is over. */
  setRun(view: DungeonRunView) {
    const firstTime = this.view?.runId !== view.runId
    this.view = view
    this.myHeroId = view.myHeroId
    if (firstTime) {
      this.floor = 0
      this.showMap(view.width, view.height, view.tiles, view.entrance)
    }
    for (const [id, figure] of this.heroes) {
      if (!this.onTheLand(id)) figure.view.visible = false
    }
    this.layoutCamera()
  }

  /** A hero is on the dungeon land only after the fight has started and their own arrival time has passed. */
  private onTheLand(heroId: string) {
    const view = this.view
    if (!view || view.run.phase !== 'Running') return false
    const listed = view.heroes.find((hero) => hero.id === heroId)
    if (!listed) return false
    const marcher = listed.citizenId === null
      ? view.run.heroes.find((hero) => hero.citizenId === null)
      : view.run.heroes.find((hero) => hero.citizenId === listed.citizenId)
    return marcher?.arrivesAt == null || Date.parse(marcher.arrivesAt) <= Date.now()
  }

  private showMap(width: number, height: number, tiles: string, entrance: { x: number; y: number }) {
    this.width = width
    this.height = height
    this.walkable = [...tiles].map((digit) => WALKABLE.includes(DUNGEON_TILES[Number(digit)] ?? 'Rock'))
    this.ground.clear()
    ;[...tiles].forEach((digit, index) =>
      drawTile(this.ground, DUNGEON_TILES[Number(digit)] ?? 'Rock', index % width, Math.floor(index / width)),
    )
    this.centerX = entrance.x + 0.5
    this.centerY = entrance.y + 0.5
    this.scale = this.defaultScale()
    this.layoutCamera()
  }

  private clearFoes() {
    for (const foe of this.foes.values()) foe.view.destroy({ children: true })
    this.foes.clear()
  }

  setSnapshot(snapshot: DungeonSnapshot) {
    const floorChanged = snapshot.floor !== this.floor
    if (floorChanged) {
      this.floor = snapshot.floor
      this.clearFoes()
      this.showMap(snapshot.width, snapshot.height, snapshot.tiles, snapshot.entrance)
    }
    for (const hero of snapshot.heroes) {
      if (!this.onTheLand(hero.id)) {
        const waiting = this.heroes.get(hero.id)
        if (waiting) waiting.view.visible = false
        continue
      }
      let figure = this.heroes.get(hero.id)
      if (!figure) {
        const listed = this.view?.heroes.find((candidate) => candidate.id === hero.id)
        if (!listed) continue
        figure = this.addHero(listed.id, listed.name, listed.class, listed.appearance, hero.x, hero.y)
        figure.maximumHealth = listed.maximumHealth || figure.maximumHealth
      }
      figure.view.visible = true
      figure.health = hero.health
      figure.state = hero.state
      const fell = hero.state !== 'Fighting' || hero.health <= 0
      if (floorChanged && fell) {
        figure.view.visible = false
        continue
      }
      if (hero.state !== 'Left') {
        if (floorChanged) {
          figure.x = hero.x
          figure.y = hero.y
          figure.view.position.set(hero.x * TILE, hero.y * TILE)
        }
        figure.targetX = hero.x
        figure.targetY = hero.y
      }
    }
    for (const foe of snapshot.foes) {
      let figure = this.foes.get(foe.id)
      if (!figure) figure = this.addFoe(foe.id, foe.kind, foe.x, foe.y)
      figure.health = foe.health
      figure.maximumHealth = foe.maximumHealth
      if (foe.health <= 0) {
        if (!figure.dead) {
          figure.dead = true
          figure.diedAt = this.elapsed
        }
        continue
      }
      figure.dead = false
      figure.targetX = foe.x
      figure.targetY = foe.y
    }
    for (const event of snapshot.events) this.showEvent(event)
    this.drawMarks(snapshot)
  }

  zoomBy(factor: number) {
    const minimum = this.minimumScale()
    this.scale = Math.min(MAXIMUM_SCALE, Math.max(minimum, this.scale * factor))
    this.layoutCamera()
  }

  /** Resume following after inspecting another part of the map. No movement order is sent. */
  focusHero() {
    this.following = true
    const mine = this.myHeroId ? this.heroes.get(this.myHeroId) : null
    if (mine) {
      this.centerX = mine.x
      this.centerY = mine.y
      this.layoutCamera()
    }
  }

  /** Rings under the player's hero, their target and whom they heal. */
  private drawMarks(snapshot: DungeonSnapshot) {
    this.marks.clear()
    const mine = snapshot.heroes.find((hero) => hero.id === this.myHeroId)
    if (!mine || mine.state !== 'Fighting') return
    const ring = (figure: Figure | undefined, color: number) => {
      if (!figure) return
      this.marks.ellipse(figure.targetX * TILE, figure.targetY * TILE, 7, 3).stroke({ color, width: 1.5, alpha: 0.9 })
    }
    if (mine.targetFoe !== null) ring(this.foes.get(mine.targetFoe), 0xff4030)
    if (mine.healTarget) ring(this.heroes.get(mine.healTarget), 0x60ff80)
  }

  private lookTexture(id: string, appearance: Appearance | null): Texture {
    if (!appearance) return this.tiles.get(dungeonTile(VILLAGER_SPRITES[hashText(id) % VILLAGER_SPRITES.length]))
    const key = appearanceKey(appearance)
    let texture = this.lookTextures.get(key)
    if (!texture) {
      texture = new Texture({ source: new CanvasSource({ resource: this.painter.paint(appearance), scaleMode: 'nearest' }) })
      this.lookTextures.set(key, texture)
    }
    return texture
  }

  private addHero(id: string, name: string, heroClass: HeroClass, appearance: Appearance | null, x: number, y: number): HeroFigure {
    const view = new Container()
    const body = new Container()
    const shadow = new Graphics().ellipse(0, 0, 5, 1.5).fill({ color: 0x000000, alpha: 0.35 })
    const sprite = new Sprite(this.lookTexture(id, appearance))
    sprite.anchor.set(0.5, 1)
    body.addChild(sprite, drawClassSign(heroClass))
    const bar = new Graphics()
    bar.position.set(0, -20)
    const label = new Text({
      text: name,
      style: { fontFamily: LABEL_FONT, fontSize: 12, fill: id === this.myHeroId ? 0xffe066 : 0xffffff, stroke: { color: 0x000000, width: 3 } },
    })
    label.anchor.set(0.5, 1)
    label.scale.set(0.3)
    label.position.set(0, -21)
    const skull = drawSkullBubble()
    skull.position.set(0, -24)
    skull.visible = false
    view.addChild(shadow, body, bar, label, skull)
    view.position.set(x * TILE, y * TILE)
    this.objects.addChild(view)
    const figure: HeroFigure = {
      id,
      heroClass,
      state: 'Fighting',
      view,
      body,
      bar,
      label,
      skull,
      x,
      y,
      targetX: x,
      targetY: y,
      health: 0,
      maximumHealth: 1,
      phase: 0,
    }
    this.heroes.set(id, figure)
    return figure
  }

  private addFoe(id: number, kind: Parameters<typeof enemyContext>[0], x: number, y: number): FoeFigure {
    const view = new Container()
    const body = new Container()
    body.addChild(new Graphics(enemyContext(kind)))
    const bar = new Graphics()
    bar.position.set(0, -enemyHeight(kind) - 4)
    view.addChild(body, bar)
    view.position.set(x * TILE, y * TILE)
    this.objects.addChild(view)
    const figure: FoeFigure = {
      id,
      view,
      body,
      bar,
      x,
      y,
      targetX: x,
      targetY: y,
      health: 1,
      maximumHealth: 1,
      phase: id,
      dead: false,
      diedAt: 0,
    }
    this.foes.set(id, figure)
    return figure
  }

  private showEvent(event: DungeonEvent) {
    const hero = event.heroId ? this.heroes.get(event.heroId) : undefined
    const foe = event.foeId !== null ? this.foes.get(event.foeId) : undefined
    switch (event.kind) {
      case 'Hit':
        if (hero && foe && event.ranged) this.arrow(hero, foe)
        if (foe) this.floatText(foe, `-${Math.round(event.amount)}`, 0xffffff)
        break
      case 'Struck':
        if (hero && foe && event.ranged) this.arrow(foe, hero)
        if (hero) {
          this.floatText(hero, `-${Math.round(event.amount)}`, 0xff6050)
          hero.body.tint = 0xff8080
          window.setTimeout(() => (hero.body.tint = 0xffffff), 150)
        }
        break
      case 'Healed':
        if (hero) this.floatText(hero, `+${Math.round(event.amount)}`, 0x60ff80)
        break
      default:
        break
    }
  }

  private floatText(figure: Figure, text: string, color: number) {
    const label = new Text({
      text,
      style: { fontFamily: LABEL_FONT, fontSize: 14, fontWeight: '700', fill: color, stroke: { color: 0x000000, width: 3 } },
    })
    label.anchor.set(0.5, 1)
    label.scale.set(0.35)
    const startX = figure.view.x + (Math.random() - 0.5) * 6
    const startY = figure.view.y - 18
    label.position.set(startX, startY)
    this.effects.addChild(label)
    this.running.push({
      view: label,
      startedAt: this.elapsed,
      seconds: FLOAT_SECONDS,
      update: (share) => {
        label.position.set(startX, startY - share * 10)
        label.alpha = 1 - share
      },
    })
  }

  private arrow(from: Figure, to: Figure) {
    const line = new Graphics()
    const startX = from.view.x
    const startY = from.view.y - 8
    const endX = to.view.x
    const endY = to.view.y - 6
    this.effects.addChild(line)
    this.running.push({
      view: line,
      startedAt: this.elapsed,
      seconds: ARROW_SECONDS,
      update: (share) => {
        const x = startX + (endX - startX) * share
        const y = startY + (endY - startY) * share
        const angle = Math.atan2(endY - startY, endX - startX)
        line.clear()
        line.moveTo(x - Math.cos(angle) * 5, y - Math.sin(angle) * 5).lineTo(x, y).stroke({ color: 0xe8e0c8, width: 1 })
      },
    })
  }

  private update(ticker: Ticker) {
    const seconds = Math.min(ticker.deltaMS / 1000, 0.1)
    this.elapsed += seconds
    const glide = Math.min(1, seconds * GLIDE_RATE)
    const move = (figure: Figure) => {
      const dx = figure.targetX - figure.x
      const dy = figure.targetY - figure.y
      const distance = Math.hypot(dx, dy)
      if (distance > JUMP_TILES) {
        figure.x = figure.targetX
        figure.y = figure.targetY
      } else {
        figure.x += dx * glide
        figure.y += dy * glide
      }
      const moving = distance > 0.05
      if (moving) {
        figure.phase += seconds * 10
        if (Math.abs(dx) > 0.02) figure.body.scale.x = dx < 0 ? -1 : 1
      }
      const hop = moving ? -Math.abs(Math.sin(figure.phase)) : 0
      figure.view.position.set(Math.round(figure.x * TILE), Math.round(figure.y * TILE))
      figure.body.y = hop
      figure.view.zIndex = figure.y
    }

    for (const hero of this.heroes.values()) {
      if (hero.state !== 'Left' && hero.view.visible) move(hero)
      drawBar(hero.bar, hero.health / Math.max(1, hero.maximumHealth), hero.id === this.myHeroId ? 0xffd23a : 0x60c040)
      hero.skull.visible = hero.state === 'Fleeing' && hero.view.visible
      if (hero.state === 'Left') hero.view.visible = false
      hero.view.alpha = hero.view.visible ? 1 : Math.max(0, hero.view.alpha - seconds)
    }
    for (const foe of this.foes.values()) {
      if (foe.dead) {
        const since = this.elapsed - foe.diedAt
        foe.body.rotation = Math.PI / 2
        foe.bar.visible = false
        foe.view.alpha = Math.max(0, 1 - since / CORPSE_SECONDS)
        // Slain foes stay in every snapshot until the floor changes. The faded sprite stays
        // listed so a later snapshot does not spawn that corpse and play the death again.
        if (since > CORPSE_SECONDS) foe.view.visible = false
        continue
      }
      move(foe)
      foe.bar.visible = foe.health < foe.maximumHealth
      drawBar(foe.bar, foe.health / Math.max(1, foe.maximumHealth), 0xd03030)
    }

    for (let index = this.running.length - 1; index >= 0; index--) {
      const effect = this.running[index]
      const share = (this.elapsed - effect.startedAt) / effect.seconds
      if (share >= 1) {
        effect.view.destroy()
        this.running.splice(index, 1)
        continue
      }
      effect.update(share)
    }

    if (this.following && this.myHeroId) {
      const mine = this.heroes.get(this.myHeroId)
      if (mine?.view.visible && mine.state !== 'Left') {
        this.centerX = mine.x
        this.centerY = mine.y
        this.layoutCamera()
      }
    }
  }

  private minimumScale() {
    const { width, height } = this.app.screen
    if (!this.width || !this.height) return 1
    return Math.min(width / (this.width * TILE), height / (this.height * TILE))
  }

  private defaultScale() {
    return Math.max(this.minimumScale(), MINIMUM_DEFAULT_TILE_PIXELS / TILE, this.app.screen.width / (DEFAULT_VIEW_TILES * TILE))
  }

  /** Keeps the map on screen: centred when it is smaller than the screen, clamped to its edges otherwise. */
  private layoutCamera() {
    const { width, height } = this.app.screen
    this.scale = Math.min(MAXIMUM_SCALE, Math.max(this.minimumScale(), this.scale))
    this.world.scale.set(this.scale)
    const mapWidth = this.width * TILE * this.scale
    const mapHeight = this.height * TILE * this.scale
    const place = (center: number, screen: number, map: number) =>
      map <= screen ? (screen - map) / 2 : Math.min(0, Math.max(screen - map, screen / 2 - center * TILE * this.scale))
    this.world.position.set(Math.round(place(this.centerX, width, mapWidth)), Math.round(place(this.centerY, height, mapHeight)))
  }

  private toTiles(screenX: number, screenY: number) {
    const point = this.world.toLocal({ x: screenX, y: screenY })
    return { x: point.x / TILE, y: point.y / TILE }
  }

  private pick(x: number, y: number) {
    if (!this.handlers) return
    let bestFoe: FoeFigure | null = null
    let bestDistance = PICK_RADIUS
    for (const foe of this.foes.values()) {
      if (foe.dead) continue
      // Figures stand on their feet: the body reaches about half a tile above the point.
      const distance = Math.hypot(foe.x - x, foe.y - 0.35 - y)
      if (distance < bestDistance) {
        bestFoe = foe
        bestDistance = distance
      }
    }
    if (bestFoe) return this.handlers.onFoe(bestFoe.id)
    let bestHero: HeroFigure | null = null
    bestDistance = PICK_RADIUS
    for (const hero of this.heroes.values()) {
      if (hero.id === this.myHeroId || hero.state !== 'Fighting') continue
      const distance = Math.hypot(hero.x - x, hero.y - 0.4 - y)
      if (distance < bestDistance) {
        bestHero = hero
        bestDistance = distance
      }
    }
    if (bestHero) return this.handlers.onHero(bestHero.id)
    const column = Math.floor(x)
    const row = Math.floor(y)
    if (column < 0 || row < 0 || column >= this.width || row >= this.height) return
    if (!this.walkable[row * this.width + column]) return
    this.following = true
    this.handlers.onGround(x, y)
  }

  private bindPointer() {
    const canvas = this.app.canvas
    canvas.style.touchAction = 'none'
    const pointers = new Map<number, { x: number; y: number }>()
    let pinching = false
    let previousPinch: { distance: number; x: number; y: number } | null = null
    const local = (event: PointerEvent | WheelEvent) => {
      const bounds = canvas.getBoundingClientRect()
      return { x: event.clientX - bounds.left, y: event.clientY - bounds.top }
    }
    const pinch = () => {
      const [a, b] = [...pointers.values()]
      return a && b ? { distance: Math.hypot(a.x - b.x, a.y - b.y), x: (a.x + b.x) / 2, y: (a.y + b.y) / 2 } : null
    }
    canvas.addEventListener('pointerdown', (event) => {
      if (event.button !== 0) return
      if (pointers.size === 0) { pinching = false; previousPinch = null }
      pointers.set(event.pointerId, local(event))
      canvas.setPointerCapture(event.pointerId)
      if (pointers.size > 1) {
        pinching = true
        this.dragging = null
        previousPinch = pinch()
        return
      }
      this.dragging = { x: event.clientX, y: event.clientY, centerX: this.centerX, centerY: this.centerY, moved: false }
    })
    canvas.addEventListener('pointermove', (event) => {
      if (pointers.has(event.pointerId)) pointers.set(event.pointerId, local(event))
      if (pinching) {
        const next = pinch()
        if (next && previousPinch && previousPinch.distance > 0 && next.distance > 0) {
          const anchor = this.toTiles(previousPinch.x, previousPinch.y)
          this.following = false
          this.zoomBy(next.distance / previousPinch.distance)
          const moved = this.toTiles(next.x, next.y)
          // Near an edge the displayed centre differs from the requested hero centre.
          // Anchor against the displayed map, otherwise the first pinch jumps sideways.
          const displayed = this.toTiles(this.app.screen.width / 2, this.app.screen.height / 2)
          this.centerX = Math.min(this.width, Math.max(0, displayed.x + anchor.x - moved.x))
          this.centerY = Math.min(this.height, Math.max(0, displayed.y + anchor.y - moved.y))
          this.layoutCamera()
        }
        previousPinch = next
        return
      }
      if (!this.dragging) {
        if (event.pointerType !== 'mouse') return
        const point = local(event)
        const tiles = this.toTiles(point.x, point.y)
        const overFoe = [...this.foes.values()].some((foe) => !foe.dead && Math.hypot(foe.x - tiles.x, foe.y - 0.35 - tiles.y) < PICK_RADIUS)
        canvas.style.cursor = overFoe ? 'crosshair' : ''
        return
      }
      if (Math.hypot(event.clientX - this.dragging.x, event.clientY - this.dragging.y) > 6) this.dragging.moved = true
      if (!this.dragging.moved) return
      this.following = false
      this.centerX = this.dragging.centerX - (event.clientX - this.dragging.x) / (this.scale * TILE)
      this.centerY = this.dragging.centerY - (event.clientY - this.dragging.y) / (this.scale * TILE)
      this.centerX = Math.min(this.width, Math.max(0, this.centerX))
      this.centerY = Math.min(this.height, Math.max(0, this.centerY))
      this.layoutCamera()
    })
    canvas.addEventListener('pointerup', (event) => {
      if (event.button !== 0) return
      if (!pointers.delete(event.pointerId)) return
      const dragging = this.dragging
      this.dragging = null
      // Lifting either finger, including the last one, must never issue an attack or move.
      if (pinching || !dragging || dragging.moved) { previousPinch = pinch(); return }
      const point = local(event)
      const tiles = this.toTiles(point.x, point.y)
      this.pick(tiles.x, tiles.y)
    })
    const cancel = (event: PointerEvent) => {
      if (!pointers.delete(event.pointerId)) return
      pinching = true
      previousPinch = null
      this.dragging = null
    }
    canvas.addEventListener('pointercancel', cancel)
    canvas.addEventListener('lostpointercapture', cancel)
    canvas.addEventListener('contextmenu', (event) => event.preventDefault())
    canvas.addEventListener(
      'wheel',
      (event) => {
        event.preventDefault()
        this.zoomBy(Math.exp(-Math.max(-100, Math.min(100, event.deltaY)) * 0.0015))
      },
      { passive: false },
    )
  }
}
