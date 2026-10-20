import { createRoot } from 'react-dom/client'
import { useDungeonRun, useLiveVillage } from '../src/hooks'

function Dungeon() {
  const live = useDungeonRun('run-a')
  return <><pre id="state">{JSON.stringify({phase: live.view?.run.phase, hero: live.view?.myHeroId, elapsed: live.snapshot?.elapsed, outcome: live.snapshot?.outcome, error: !!live.error})}</pre>
    <button onClick={() => live.attack(1)}>Attack</button><button onClick={() => live.moveTo(4, 5)}>Move</button><button onClick={() => live.heal('ally')}>Heal</button></>
}
function Village() {
  const live = useLiveVillage('test-world', false)
  return <pre id="state">{JSON.stringify(live.state)}</pre>
}
createRoot(document.getElementById('app')!).render(location.search.includes('village') ? <Village/> : <Dungeon/> )
