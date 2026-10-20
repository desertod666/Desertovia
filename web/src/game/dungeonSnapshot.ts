/** Elapsed is cumulative across floors; a delayed JoinRun reply cannot rewind live combat. */
export function latestDungeonSnapshot<T extends { runId: string; elapsed: number; outcome: string }>(previous: T | null, next: T): T {
  if (!previous || previous.runId !== next.runId) return next
  if (next.elapsed < previous.elapsed) return previous
  // A terminal frame may have the same elapsed time as the last running frame.
  if (next.elapsed === previous.elapsed && previous.outcome !== 'Pending' && next.outcome === 'Pending') return previous
  return next
}
