import { currentEnergy } from '../energy'
import type { Catalog, Citizen, StatRequirement, Structure } from '../api'
import { useI18n } from '../i18n'
import { CharacterPortrait } from './CharacterPortrait'
import { citizenTitle } from './citizenTitle'
import { Hint } from './Hint'
import { restHints } from './needHints'
import { ResourceIcon } from './TileIcon'
import { formatRecoveryTime } from './recoveryTime'

export function CharacterSheet({ citizen, catalog, structures, now = Date.now() }: { citizen: Citizen; catalog: Catalog; structures: Structure[]; now?: number }) {
  const i18n = useI18n()
  const { t } = i18n
  const energy = citizen ? currentEnergy(citizen, now) : 0
  const describeNeeds = (needs: StatRequirement[]) => needs.map((r) => `${i18n.stat(r.stat)} ${r.minimum}`).join(', ')
  const { rules } = catalog
  const rest = restHints(citizen, catalog, structures, i18n, energy)
  const streakBonus = Math.min(citizen.streak * rules.streakPerRenewal, rules.streakMaximumBonus)
  const medicalReasons:Record<string,string>={breakfast:'Испорченный завтрак: ходьба −25%, запас сил −20%.',cast:'Гипс не по размеру: ходьба −20%, груз −25%.',snorers:'Храпящие соседи: расход энергии +20%.',cups:'Лечение банками: груз −20%.'}
  return (
    <section className="card">
      {citizen.recovery?.until&&Date.parse(citizen.recovery.until)>Date.now()?<p className="warning">На лечении после дуэли · до {new Date(citizen.recovery.until).toLocaleString()}</p>:citizen.injuredUntil&&Date.parse(citizen.injuredUntil)>Date.now()&&<p className="warning">Ранен после боя · до {new Date(citizen.injuredUntil).toLocaleString()}</p>}
      {citizen.exhaustedUntil && Date.parse(citizen.exhaustedUntil) > now && <p className="warning">{t('character.exhaustionCountdown', { time: formatRecoveryTime(citizen.exhaustedUntil, now, i18n.language) })}</p>}
      {!!citizen.exhaustedCycles&&<p className="small warning">Работа на последнем дыхании: {citizen.exhaustedCycles}/3. Затем обморок на 12 игровых часов.</p>}
      {citizen.recovery?.effectKind&&citizen.recovery.effectUntil&&Date.parse(citizen.recovery.effectUntil)>Date.now()&&<p className="warning">{medicalReasons[citizen.recovery.effectKind]??'Больничное осложнение'} До {new Date(citizen.recovery.effectUntil).toLocaleString()}</p>}
      {citizen.homePlotId&&<p className="small">Жильё: {citizen.homePlotId}</p>}
      {citizen.spouseName&&<p className="small">Супруг: {citizen.spouseName}</p>}
      <h3 className="character-title">
        {citizen.appearance && <CharacterPortrait appearance={citizen.appearance} scale={3} />}
        <span>
          {t('character.mine')} {citizen.name} <span className="muted">— {citizenTitle(citizen, i18n)}</span>
        </span>
      </h3>
      <div className="character-summary">
        <div>
          <span className="muted small">
            <Hint text={rest.energy}>{t('character.energy')}</Hint>
          </span>
          <div className="bar energy">
            <div style={{ width: `${(energy / citizen.maximumEnergy) * 100}%` }} />
          </div>
          <span className="small">
            {Math.floor(energy)} / {citizen.maximumEnergy}
          </span>
        </div>
        <div>
          <span className="muted small">
            <Hint text={t('hint.renown', { statMaximum: rules.statMaximum })}>{t('character.renown')}</Hint>
          </span>
          <strong>{citizen.renown}</strong>
        </div>
        <div>
          <span className="muted small">
            <Hint
              text={t('hint.streak', {
                grace: rules.graceSeconds,
                per: i18n.amount(rules.streakPerRenewal * 100),
                maximum: i18n.amount(rules.streakMaximumBonus * 100),
                bonus: i18n.amount(streakBonus * 100),
              })}
            >
              {t('character.streak')}
            </Hint>
          </span>
          <strong>{citizen.streak}</strong>
        </div>
        <div>
          <span className="muted small">
            <Hint text={t('hint.talent', { total: citizen.statTotal, cap: rules.talentCap })}>{t('character.talent')}</Hint>
          </span>
          <strong>
            {citizen.statTotal} / {catalog.rules.talentCap}
          </strong>
        </div>
        <div>
          <span className="muted small">{t('character.sleeps')}</span>
          <strong>
            <Hint text={rest.place}>{rest.underTrees ? t('character.underTrees') : t('character.inShelter')}</Hint>
          </strong>
          <span className="small muted">
            <Hint text={rest.restSpeed}>{t('character.rest', { percent: rest.percent })}</Hint>
          </span>
        </div>
      </div>
      {citizen.isHungry ? (
        <p className="warning small">{t('character.hungry')}</p>
      ) : (
        <p className="small muted"><Hint text={t('character.eating', { cycles: catalog.rules.cyclesPerMeal })}>{t('character.fed')} ⓘ</Hint></p>
      )}
      {citizen.injuredUntil && Date.parse(citizen.injuredUntil) > Date.parse(citizen.serverNow) && (
        <p className="warning small">
          {t('injury.title', {
            time: new Date(citizen.injuredUntil).toLocaleTimeString(i18n.language, { hour: '2-digit', minute: '2-digit' }),
          })}
          . {t('injury.hint')}
        </p>
      )}
      <h4>{t('fight.title')}</h4>
      <p className="small">
        <Hint text={t('fight.powerHint')}>{t('fight.power', { power: i18n.amount(citizen.power) })} ⓘ</Hint> ·{' '}
        {citizen.weapon && <ResourceIcon kind={citizen.weapon} scale={2} sharpen={citizen.weaponSharpen ?? 0} />}
        {citizen.arrows > 0 && <ResourceIcon kind="Arrows" scale={2} sharpen={citizen.arrowSharpen ?? 0} />}
        {citizen.armor && <ResourceIcon kind={citizen.armor} scale={2} />}{' '}
        {citizen.weapon || citizen.armor
          ? t('fight.gear', {
              gear: [citizen.weapon, citizen.armor]
                .filter((kind): kind is string => kind !== null)
                .map((kind) => i18n.resource(kind))
                .concat(citizen.arrows > 0 ? [t('fight.arrows', { count: citizen.arrows })] : [])
                .join(', '),
            })
          : t('fight.unarmed')}
      </p>
      <table className="stats">
        <tbody>
          {citizen.stats.map((stat) => (
            <tr key={stat.stat}>
              <td>
                <Hint text={i18n.statHint(stat.stat)}>{i18n.stat(stat.stat)}</Hint>
              </td>
              <td className="stat-value">{stat.level}</td>
              <td className="stat-progress">
                {stat.experienceToNext > 0 ? (
                  <>
                    <div className="bar">
                      <div style={{ width: `${(stat.experience / stat.experienceToNext) * 100}%` }} />
                    </div>
                    <span className="muted small">
                      {stat.experience} / {stat.experienceToNext}
                    </span>
                  </>
                ) : (
                  <span className="muted small">{t('character.maximum')}</span>
                )}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
      <h4>{t('character.skills')}</h4>
      {citizen.skills.length === 0 ? (
        <p className="small muted">{t('character.noSkills')}</p>
      ) : (
        <table className="stats">
          <tbody>
            {citizen.skills.map((skill) => {
              const needs = skill.nextLevel.filter((r) => r.minimum > 0)
              return (
                <tr key={skill.skill} title={needs.length > 0 ? t('character.nextLevelNeeds', { stats: describeNeeds(needs) }) : undefined}>
                  <td>{i18n.skill(skill.skill)}</td>
                  <td className="stat-value">{skill.level}</td>
                  <td className="stat-progress">
                    {skill.experienceToNext > 0 ? (
                      <>
                        <div className="bar">
                          <div style={{ width: `${(skill.experience / skill.experienceToNext) * 100}%` }} />
                        </div>
                        <span className="muted small">
                          {skill.experience} / {skill.experienceToNext}
                        </span>
                        {skill.waitsForStats && (
                          <span className="warning small"> {t('character.waitsForStats', { stats: describeNeeds(needs) })}</span>
                        )}
                      </>
                    ) : (
                      <span className="muted small">{t('character.maximum')}</span>
                    )}
                  </td>
                </tr>
              )
            })}
          </tbody>
        </table>
      )}
      {citizen.cycleTired && <p className="warning small">{t('character.tired')}</p>}
    </section>
  )
}
