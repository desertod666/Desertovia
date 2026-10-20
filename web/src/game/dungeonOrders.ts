export type DungeonOrder =
  | { method: 'MoveTo'; args: [number, number] }
  | { method: 'Attack'; args: [number] }
  | { method: 'Heal'; args: [string] }

/** One acknowledged order and at most one newer intent. Never replay old clicks after a reconnect. */
export class DungeonOrders {
  private busy = false
  private pending: DungeonOrder | null = null
  private disposed = false
  private paused = false
  private generation = 0

  private readonly send: (order: DungeonOrder) => Promise<boolean>

  constructor(send: (order: DungeonOrder) => Promise<boolean>) {
    this.send = send
  }

  offer(order: DungeonOrder) {
    if (this.disposed || this.paused) return
    if (this.busy) {
      this.pending = order
      return
    }
    void this.flush(order)
  }

  /** A disconnected run's commands must not leak into its next session. */
  clear() {
    this.pending = null
    this.busy = false
    this.generation++
  }

  /** Ignore clicks until the new connection has confirmed the player's hero. */
  suspend() {
    this.paused = true
    this.clear()
  }

  resume() {
    if (!this.disposed) this.paused = false
  }

  dispose() {
    this.disposed = true
    this.clear()
  }

  private async flush(order: DungeonOrder) {
    const generation = this.generation
    this.busy = true
    try {
      // A dead target can reject the old attack; the newer walk order is still useful.
      // Every order retains the server's own fresh authorization and medical checks.
      await this.send(order)
    } catch {
      if (generation === this.generation) this.clear()
    } finally {
      // A reply from a disconnected session must not clear or flush its successor's queue.
      if (generation === this.generation) {
        this.busy = false
        const next = this.pending
        this.pending = null
        if (next && !this.disposed && !this.paused) void this.flush(next)
      }
    }
  }
}
