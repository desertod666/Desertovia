# Desertod — Game Design: Stats, Skills, Tasks, Progression

Status: draft v0.1. All numbers are tuning parameters, collected in [Tuning parameters](#10-tuning-parameters).

## 1. Concept recap

- A **streamer** hosts a **Village**. Any streamer on the platform can host one.
- **Citizens** are the streamer's subscribers. Each person has one **Account** with one **Character** (name and look), created right after signing up. Opening a Village settles the Character there as a **Citizen**; stats, skills and renown are per Citizen, so every Village starts from scratch.
- Citizens work in a browser page (clicker style): pick a task, it runs for **a few minutes** (3 for gathering, 5 for crafting), then must be renewed or changed.
- The village is rendered as a 2D map in the browser.

## 2. Stats

Five stats, range **0–10**. Stats are generic: they are what a Citizen *is*. What a Citizen *can do* is set by skills (section 3).

| Stat | Meaning | Main uses |
|---|---|---|
| Strength | Raw physical power | Yield of heavy work; Woodcutting, Quarrying, Mining, Smithing; fight power |
| Dexterity | Precision, speed | Gathering, Hunting, Fishing, Exploring, Baking, Carpentry, Weaponcraft; rare finds; fight power |
| Intelligence | Knowledge, planning | Building, Scholarship, Weatherlore; engineer supervision |
| Charisma | Social influence | Entertaining, Trading |
| Endurance | Stamina | Size of the energy pool (how long a Citizen works at full efficiency); Farming, Hauling, Guarding; captain's command |

### 2.1 Character creation

- Everyone starts wild: every stat at **0**, grown only by working. Creation picks only the name and the look.

### 2.2 Talent cap

- The sum of all five stats cannot exceed **30** (out of a theoretical 50).
- A Citizen can be great at two things, decent at one, or average at everything, but never great at everything.
- Because every skill level needs stats (section 3.1), the cap also decides which crafts a Citizen can master. This makes Citizens depend on each other, which is the whole point of a community game.
- When a stat's experience would push the total over the cap, that experience is converted into **Renown** (prestige currency for cosmetics, titles, house decorations). Effort is never wasted, but power does not grow.

## 3. Skills and professions

Professions are **not** a class choice. Every task trains one **skill** (a craft) and needs its own level of that skill; a profession is the title a Citizen earns at a level of the craft. A Citizen may perform any task they have the skill level for and can switch every cycle. The highest profession held is shown as the Citizen's title.

### 3.1 Skills rest on stats

Every skill has a primary and a secondary stat. Level L of a skill needs:

```text
Primary   ≥ L − 1
Secondary ≥ ⌊(L − 1) / 2⌋
```

| Skill | Primary / Secondary | Skill | Primary / Secondary |
|---|---|---|---|
| Woodcutting | Strength / Endurance | Carpentry | Dexterity / Strength |
| Quarrying | Strength / Endurance | Weaponcraft | Dexterity / Strength |
| Mining | Strength / Endurance | Farming | Endurance / Dexterity |
| Smithing | Strength / Dexterity | Hauling | Endurance / Strength |
| Gathering | Dexterity / Intelligence | Guarding | Endurance / Strength |
| Hunting | Dexterity / Endurance | Building | Intelligence / Strength |
| Fishing | Dexterity / Endurance | Scholarship | Intelligence / Charisma |
| Exploring | Dexterity / Endurance | Weatherlore | Intelligence / Endurance |
| Baking | Dexterity / Endurance | Entertaining | Charisma / Dexterity |
| | | Trading | Charisma / Intelligence |

Examples: Smithing 2 needs Strength 1; Hunting 4 needs Dexterity 3 and Endurance 1; a master craft (level 7) needs 6 and 3.

When a skill has the experience for its next level but not the stats, the level **waits**: experience up to that one level is kept, the rest is lost, and the level comes as soon as the stats catch up. Since a stat gets the same experience as the skill trained with it, a specialist rarely waits; the gate bites when the talent cap stops a stat from growing. A Citizen with Strength, Dexterity and Endurance at 10 cannot get past Scholarship 1.

### 3.2 Professions

Three tiers. A task's skill level is its only gate besides the village's buildings and discoveries; the profession is a title and the group its tasks are listed under.

| Tier | Craft level | Typical time to reach (section 5.3) |
|---|---|---|
| Basic (Laborer) | — | immediately |
| Journeyman | 3 | ~2 days of energy |
| Master | 7 | ~6 days of energy |

| Craft | Journeyman (3) | Master (7) | Work by required level |
|---|---|---|---|
| Mining | Miner | Deep miner | Dig in the cave, Mine coal 0 · Mine iron ore 3 · Mine gold 7 |
| Smithing | Smith | Master smith | Smelt iron 0 · Forge swords 3 · Forge chains 7 |
| Carpentry | Carpenter | Artisan | Saw planks 0 · Make shields 3 · Build mechanisms 7 |
| Scholarship | Scholar | Alchemist | Measure the river, Read the chronicles, Brew vigor drinks 0 · Research 3 · Brew remedies 7 |
| Building | Engineer | Architect, Foreman | Build, Repair 0 · Survey, Supervise 3 · Draft plans, Lead a crew 7 (not available yet) |
| Guarding | Guard | Captain | Keep watch 0 · Patrol 3 · Command 7 |
| Exploring | — | Scout | Roam, Explore the land 0 · Go on an expedition 3 · Long expedition 7 |
| Trading | Merchant | Envoy | Trade 0 · Negotiate 7 (both not available yet) |
| Hunting | Hunter | — | Rabbits 0 · Deer 2 · Birds 3 |
| Farming | Farmer | — | Tend, water, harvest, turnips 0 · carrots 2 · cabbage 3 · flax 4 · rye 5 |
| Baking | Baker | — | Bake bread 0 |
| Entertaining | Bard | — | Tend the fire, Chat at the tavern, Tell stories 0 · Play the gusli 3 |
| Weatherlore | Synoptic | — | Read the sky 0 |
| Weaponcraft | — | — | Spears, leather armor, arrows 0 · Bows 2 |
| Woodcutting, Quarrying, Gathering, Fishing, Hauling | — | — | All work at 0 |

Every craft has work at level 0, so anyone can start it. Professions with a discovery (Miner and Deep miner: Mining; Smith: Smelting; Master smith: Forging; Carpenter: Carpentry; Artisan: Mechanics; Baker: Baking; Scholar: Writing; Alchemist: Alchemy; Bard: Music; Guard and Captain: Warfare; Synoptic: Weather lore) open their work only once the village has made it.

Crafts with work at 0, 3 and 7 always have a task that teaches in full (section 5.4). Building has no working master task yet, so Building above 6 learns at a quarter; the same goes for Trading and Entertaining above 6, and for single-task crafts above 3.

## 4. Tasks

### 4.1 Task cycle

- A task (one *cycle*) runs for **a few minutes**, so a day of play is many short visits rather than one long session. The duration is measured on the server; the client only displays it.

  | Kind | Work time |
  |---|---|
  | Gathering, farming, mining, roaming, expeditions, hunting rabbits, hauling to a site | 3 minutes |
  | Crafting, construction labor, research, project work (deliver, build, survey, supervise), camp work (tend the fire, tell stories, repair) | 5 minutes |
  | Guard shifts (keep watch, patrol, command) | 20 minutes |
  | Hunting deer / birds | 6 minutes / 90 seconds |
  | Carry to storage | 15 seconds |

  Walking to the workplace and to the storage is added on top.
- When a cycle completes, its result is **banked automatically**. Work already done is never lost.
- After completion the player has a **60 second grace window** to renew or change the task.
- After the grace window the Citizen goes **idle** until the player picks a task again.
- Only **one active task** per Citizen. A new cycle can only start after the previous one completes. Clicking faster gives nothing: every task costs energy (section 5.5).

### 4.2 Task definition

Each task has:

| Field | Example (Mine coal) |
|---|---|
| Profession (where it is listed) | Miner |
| Skill and required level | Mining 0 |
| Primary stat | Strength |
| Secondary stat | Endurance |
| Base yield per cycle | 2 coal |
| Inputs consumed | none |
| Energy cost | 1 |

### 4.3 Task catalog (first version)

| Task | Profession | Skill level | Primary / Secondary | Base yield | Inputs |
|---|---|---|---|---|---|
| Chop wood | Laborer | Woodcutting 0 | Strength / Endurance | 3 wood | — |
| Quarry stone | Laborer | Quarrying 0 | Strength / Endurance | 2 stone | — |
| Forage | Laborer | Gathering 0 | Dexterity / Intelligence | 2 food | — |
| Tend fields | Laborer | Farming 0 | Endurance / Dexterity | 2 food | — |
| Read old books | Laborer | Scholarship 0 | Intelligence / Charisma | 0.5 research point | — |
| Chat at the tavern | Laborer | Entertaining 0 | Charisma / Intelligence | +morale (half of Perform) | — |
| Haul | Laborer | Hauling 0 | Endurance / Strength | +1 project progress | — |
| Mine coal | Miner | Mining 0 | Strength / Endurance | 2 coal | — |
| Mine iron ore | Miner | Mining 3 | Strength / Endurance | 1 iron ore | — |
| Smelt ingot | Smith | Smithing 0 | Strength / Dexterity | 1 iron ingot | 1 iron ore, 1 coal |
| Saw planks | Carpenter | Carpentry 0 | Dexterity / Strength | 2 planks | 2 wood |
| Hunt (pick the prey) | Laborer | Hunting 0 / 2 / 3 for rabbits / deer / birds | Dexterity / Endurance (birds: Intelligence) | see [cooperative-building.md](cooperative-building.md), Wildlife | — |
| Research | Scholar | Scholarship 3 | Intelligence / Charisma | 1 research point | — |
| Survey | Engineer | Building 3 | Intelligence / Dexterity | +1 survey progress | — |
| Supervise | Engineer | Building 3 | Intelligence / Charisma | enables construction (see 6.2) | — |
| Trade | Merchant | Trading 0 | Charisma / Intelligence | exchange at better rate | resources |
| Perform | Bard | Entertaining 3 | Charisma / Dexterity | +morale | — |
| Keep watch | Laborer | Guarding 0 | Endurance / Strength | defends the camp during a raid (section 12) | — |
| Repair buildings | Laborer | Building 0 | Strength / Dexterity | mends 25 damage of the most damaged building | 2 wood |
| Patrol | Guard | Guarding 3 | Endurance / Strength | defends the camp, ×1.25 (section 12) | — |
| Mine gold | Deep Miner | Mining 7 | Strength / Endurance | 0.5 gold | — |
| Forge chain | Master Smith | Smithing 7 | Strength / Dexterity | 1 chain | 2 iron ingot, 1 coal |
| Build mechanism | Artisan | Carpentry 7 | Dexterity / Intelligence | 1 mechanism | 2 planks, 1 iron ingot |
| Explore | Scout | Exploring 7 | Dexterity / Endurance | discovery roll | — |
| Design | Architect | Building 7 | Intelligence / Charisma | +1 blueprint progress | — |
| Brew remedy | Alchemist | Scholarship 7 | Intelligence / Dexterity | 1 remedy | 2 food |
| Lead crew | Foreman | Building 7 | Charisma / Intelligence | group bonus (see 6.3) | — |
| Negotiate | Envoy | Trading 7 | Charisma / Intelligence | +relation with another village | — |
| Command | Captain | Guarding 7 | Endurance / Strength | defends the camp; +4% defense per Endurance (section 12) | — |

### 4.4 Presence

- A citizen is **present** while their village page is open, and for **5 minutes** after it closes or after their last action.
- A citizen who is not present is **away**. They still live in the village and keep everything; they are back as soon as they open the page or act.
- Away citizens are not counted in votes: the poll closes once more than half of the present citizens voted. A ballot cast before going away still counts.
- An away citizen stays on the map while a task they started is running, and disappears once it ends.

## 5. Progression math

### 5.1 Experience gain per cycle

```text
XP_primary   = 10 × Challenge × Fatigue × Overflow
XP_secondary = 0.3 × XP_primary
```

- **Every task gives the same base experience (10).** Higher-tier tasks give better *yield*, not more experience. This removes the "find the single best experience task and spam it" exploit.
- The multipliers are anti-abuse rules (sections 5.4–5.6).

### 5.2 Experience curve

```text
Skill:  XP_to_next(0) = 50,  XP_to_next(L) = 270 + 20 × L       for L = 1..9   (290 … 450)
Stat:   XP_to_next(L) = round(0.6 × skill XP_to_next(L))                       (30, 174 … 270)
```

Experience is tracked per stat and per skill. At level 10 a stat is maxed and further experience becomes Renown.

**Pacing rule:** a day of energy (about 36 tasks, section 5.5) gives about **one level of the profession** and **about two stat points**, so every day opens something new. The first level comes in the first few tasks. Citizens start with every stat at 0.

### 5.3 Pacing (calculated)

Calculated by `dotnet run tools/Balance/balance.cs` from the live catalogs. Assumptions: the player renews within 5 seconds, is fed, switches crafts before the challenge rule bites, has no streak, morale or tool bonus, and drops goods at the camp (no storage building nearby).

**One task in real time.** A gathering task is 3 minutes of work + a one-way walk to where the goods are dropped + renewing: about 3.5 minutes, 17 tasks an hour. Crafting and camp work is about 5 minutes, 12 an hour. A day of energy (36 tasks) is about 2–3 hours of work, for example **a task every 30 minutes from morning to night**.

| Level | Skill XP | Skill tasks | Days of energy | Stat XP | Stat tasks (main stat) |
|---|---|---|---|---|---|
| 1 | 50 | 5 | 0.1 | 30 | 3 |
| 2 | 340 | 34 | 0.9 | 204 | 21 |
| 3 (journeyman) | 650 | 65 | 1.8 | 390 | 39 |
| 4 | 980 | 98 | 2.7 | 588 | 59 |
| 5 | 1 330 | 133 | 3.7 | 798 | 80 |
| 6 | 1 700 | 170 | 4.7 | 1 020 | 102 |
| 7 (master) | 2 090 | 209 | 5.8 | 1 254 | 126 |
| 8 | 2 500 | 250 | 6.9 | 1 500 | 150 |
| 9 | 2 930 | 293 | 8.1 | 1 758 | 176 |
| 10 | 3 380 | 338 | 9.4 | 2 028 | 203 |

The main stat stays ahead of the skill, so the stat gate (section 3.1) holds back only a craft whose stats are spent elsewhere. The talent cap takes 11 days of energy spread as 6 in every stat, or 13 days as 10/10/10 (a full task gives 10 main and 3 side stat experience).

**Energy sets the daily budget** (full-experience tasks per day, section 5.5; beds from section 4 of [cooperative-building.md](cooperative-building.md)):

| Endurance | Pool | Under the trees (× 0.8) | Lean-to (× 0.9) | Hut (× 1) |
|---|---|---|---|---|
| 0 | 36 | 29 | 32 | 36 |
| 4 | 40 | 32 | 36 | 40 |
| 10 | 46 | 37 | 41 | 46 |

**Calendar time** (sleeping in a hut; under the trees it is a day or two later):

| Player | Journeyman (3) | Master (7) | Main stat 10 | Skill 10 |
|---|---|---|---|---|
| A task every 30 minutes, 7:00–23:00 | day 3 | day 7 | day 7 | day 11 |
| 1 h every day | day 4 | day 13 | day 12 | day 20 |
| 3 h every day | day 2 | day 6 | day 6 | day 9 |
| 8 h every day | day 2 | day 6 | day 6 | day 9 |
| Bot, 24 h a day | day 1 | day 5 | day 5 | day 8 |
| One 3 h stream a week | day 8 | day 36 | day 29 | day 50 |
| Three 3 h streams a week | day 3 | day 12 | day 10 | day 17 |

Playing longer than the energy lasts gains almost nothing: a bot is only a day or two ahead of a player who checks in every half hour.

**Discoveries.** A fresh citizen qualifies as a researcher in 0.6 days of energy for the Camp era, 1.1–1.6 days for the Village era and 2.2–3.8 days for the Town era (Mechanics and Forging are the slowest).

**Village work** (person-tasks with gathering the material, stats 0 to 3): a hut, granary, library or barracks 54–99 each; palisade 142–225; Bell Tower 235–318; Great Granary 279–429; Tsar Bridge 303–460. The whole story chain is 1 170–1 740 tasks and the buildings and research of the discovery tree 630–880 more: 10–15 days for 5 citizens, 5–7 days for 10, 2.5–3.5 days for 20, if each spends a day of energy.

Findings:

- Tired work still counts for the village: results round to whole units with a minimum of 1, so a tired, weak citizen does a full unit of construction labor, building, survey or research per task. Tiredness bites only on gathering, where it costs about half.
- The pool caps a weekly stream at about 37 tasks (2 hours); the rest of the stream earns no experience. Stream-only players grow a profession level per stream.
- Short tasks (birds 90 s, carry to storage 15 s) cost the same energy, so experience stays bounded.

### 5.4 Challenge: no farming trivial tasks

```text
Challenge = 1.0   if Skill ≤ TaskSkillLevel + 3
Challenge = 0.25  otherwise
```

The challenge compares the Citizen's level in the task's skill with the level the task needs. A Mining 4 Citizen digging coal (Mining 0) gets a quarter of the experience. To keep growing they must move up to iron ore (Mining 3), and at Mining 7 to gold. Crafts with work at 0, 3 and 7 always have a full-experience task; the crafts that lack one are listed at the end of section 3.2. Construction jobs count as skill level 0. Researching a technology still compares the researcher's primary stat with the technology's researcher requirement.

### 5.5 Fatigue: rolling energy pool (main anti-bot and anti-no-life rule)

```text
EnergyMax   = 36 + Endurance                (37 at Endurance 1, 46 at Endurance 10)
EnergyRegen = EnergyMax / 24 per hour       (empty to full in a day, continuous) × bed (0.8 under the trees, 0.9 lean-to, 1 hut)
Each cycle costs 1 energy.
```

| State | Condition | Experience | Yield |
|---|---|---|---|
| Fresh | Energy > 0 | × 1.0 | × 1.0 |
| Tired | Energy = 0 | × 0 | × 0.25 |

- Regeneration is **continuous**, not a daily reset, so there is no "reset at midnight" exploit and no timezone advantage.
- Steady state: about 36 full tasks a day for everyone (29 for a newcomer under the trees, 46 at Endurance 10 in a hut). Endurance and beds change the budget only a little on purpose.
- Tired Citizens can still work and contribute a little (it stays fun during a long stream), but they stop gaining power.
- **Result:** a bot running 24/7 progresses at the same speed as a dedicated human. There is nothing to gain from automation.

### 5.6 Overflow: work must be useful

> Being replaced by storage buildings and carried goods: see [streamer-mode.md](streamer-mode.md), section 3.

Each village resource has a storage cap (raised by building warehouses).

```text
Overflow = 0.5   if the task's output resource is at storage cap
Overflow = 1.0   otherwise
```

When storage is full, yield is wasted and experience is halved. This pushes Citizens toward whatever the village actually needs instead of mindless grinding, and stops resource inflation.

### 5.7 Skills: getting better at a craft

Besides stats, every task, construction job and research trains one **skill** (a craft). Baking bread at the Bakery trains Baking; chopping wood and felling trees on a site train Woodcutting.

| Skill | Trained by |
|---|---|
| Woodcutting | Gather wood, Fell the trees |
| Quarrying | Gather stones |
| Gathering | Forage berries, Cut reeds, Study plants |
| Hunting | Hunt rabbits, deer and birds |
| Fishing | Fish from the pier |
| Farming | Tend, sow, water and harvest the fields, Plough the field |
| Mining | Dig for gold, Mine coal, iron ore and gold, Dig the shaft |
| Smithing | Smelt iron, Forge chains |
| Carpentry | Saw planks, Build mechanisms |
| Baking | Bake bread |
| Scholarship | Measure the river, Read the chronicles, Research, Brew remedies, researching a technology |
| Building | Build, Survey, Supervise, Draft plans, Lead a crew, construction labor |
| Hauling | Carry to storage, Deliver goods, Bring materials to a site |
| Entertaining | Tend the fire, Chat at the tavern, Tell stories, Play the gusli |
| Exploring | Expeditions, Roam the land, Explore the land |
| Trading | Trade, Negotiate (not available yet) |
| Guarding | Keep watch, Patrol, Command, and every fight with a beast or raider |
| Weaponcraft | Make spears, Sew leather armor, Make bows, Fletch arrows |

- A skill gets the same experience as the task's primary stat, on the same curve (section 5.2), up to level 10. Skills do **not** count toward the talent cap; experience past level 10 is lost (no Renown).
- **Stat gate:** each skill level needs its stats (section 3.1). A level the stats do not allow yet waits with at most one level of experience banked, and comes once the stats grow.
- **Task gate:** every task needs its skill level (section 3.2): journeyman work 3, master work 7, a few steps in between (deer 2, bows 2, better seeds 2–5).
- **Bonus:** every skill level above what the task needs gives +5% yield and −4% work time (walking is not shortened), for up to 5 levels: +25% yield and −20% work time at most. Experience per cycle stays the same, so a skilled citizen also levels faster per hour.

## 6. Yield math

### 6.1 Yield per cycle

```text
EffectiveStat = 0.7 × Primary + 0.3 × Secondary
StatFactor    = 0.4 + 0.12 × EffectiveStat      (0.52 at 1, 1.0 at 5, 1.6 at 10)
Yield         = BaseYield × StatFactor × Fatigue × Morale × Streak × ToolBonus
```

- Spread between weakest and strongest Citizen is about **3×**. Strong Citizens matter, but newcomers are never useless. This is the soft gate for basic labor.
- Fractional yield accumulates per Citizen (0.6 + 0.6 → 1 delivered, 0.2 carried over).

### 6.2 Streak

```text
Streak = 1 + 0.01 × ConsecutiveRenewals, capped at 1.20
```

Renewing within the grace window keeps the streak; missing it resets the streak to 0. **The streak affects yield only, never experience**, so it rewards attention without speeding up power progression.

### 6.3 Morale (village-wide)

```text
Morale ∈ [0.8, 1.2], starts at 1.0
```

Raised by Bards, food surplus and finished projects. Lowered by food shortage and lost raids. Slowly drifts back to 1.0.

### 6.4 Tools

Axes and pickaxes are personal goods. A citizen uses the best matching tool in their hands while gathering. An axe speeds wood. A pickaxe speeds stone, coal, iron ore, and gold. The tool is not taken from storage automatically.

| Tier | Uses | Gathering speed |
|------|------|-----------------|
| Wood | 5 | ×1 (same as bare hands) |
| Stone | 8 | ×1.25 |
| Iron | 12 | ×1.5 |
| Steel | 20 | ×1.75 |

Each use spends one point of durability. At 0 the tool leaves the hands. Stacks of the same tool with different wear stay separate in the hands and in communal storage, so storing a tool does not repair it. A personal warehouse still stores a count only, and a tool taken back from there is fresh.

Wooden tools are made at the camp. Stone tools need the Stone tools discovery. Iron and steel tools are forged at the smithy; steel is folded iron (two ingots) and needs Forging plus Smithing 3. The Stone tools discovery still adds its own ×1.25 to chopping and quarrying, on top of a tool in hand.

### 6.5 Discovery rolls (Scout, Deep Miner)

```text
RareChance = 1% + 0.5% × Dexterity            per cycle
```

Rolled on the server only. The rare find is announced in the village.

## 7. Projects (co-operative goals)

Projects are what make the village a community instead of 50 solo clickers.

### 7.1 Structure

Each project has phases. Each phase has requirements:

- **Resources:** delivered from village storage.
- **Labor:** total cycles of a given task.
- **Role presence:** some phases only progress while a qualified Citizen is actively working on it.

### 7.2 Example: Bridge

| Phase | Requirements |
|---|---|
| 1. Survey | 30 cycles of *Survey* (Engineer) |
| 2. Materials | 400 wood, 200 stone, 120 planks, 40 iron ingots, 10 chains |
| 3. Construction | 300 cycles of *Build*/*Haul*, **only progress while at least one Engineer is on *Supervise*** |

Supervision rule:

```text
LaborersSupervised = 3 + Intelligence of the Engineer   (per Engineer)
```

A single Intelligence 4 Engineer can supervise 7 laborers; extra laborers' cycles do not count. This turns "you can't build a bridge if you're not smart" into a concrete need: smart Citizens are the bottleneck.

### 7.3 Foreman bonus

```text
CrewBonus = 1 + 0.02 × Charisma of the Foreman   (only the best Foreman counts)
```

Applied to the yield of everyone working on the same project.

### 7.4 Contribution cap

- No single Citizen may provide more than **20%** of any single requirement of a phase (resources or labor).
- This stops one dedicated player (or bot) from soloing projects and keeps the "we need more people" pressure.
- Project completion rewards (Renown, title, name on the bridge) are split by contribution share.

### 7.5 Weather and the Synoptic

- A **day** lasts 4 real hours (configurable with `Game:DayLength`): the first half is daytime, the second half night. Days count from the Unix epoch, so every server agrees on them. The map darkens at night, with a short twilight around dusk and dawn; development and local deployments (`Game:DevTools`) show a toggle that previews daylight or night on the client only.
- Every daytime and every night draws its own sky as it begins: **Clear**, **Rain**, **Wind** or **Thunderstorm**. Rain comes with a chance rolled between **5% and 10%** per period. Wind and thunderstorms are prepared but have a chance of 0 until they get effects; a thunderstorm will count as rain. The draw is derived from the village id and the period index, so every read agrees without a worker.
- **Synoptic** is a Journeyman profession (Weatherlore 3) unlocked by the tech **Weather lore** (Village era, Granary, after Agriculture). Its skill is **Weatherlore**, trained by the laborer task "Read the sky".
- A rested Synoptic (idle past the grace window after sleeping) may forecast the **next** daytime or night: rain or a clear sky. The first forecast of a period wins. The forecast comes out the other way round with a probability of 60% at Weatherlore 0, rising to 95% at the cap; otherwise the sky does what it would have done anyway.
- Forecasts are kept for 7 days, so past weather never changes.
- Rain effects: beds count as watered (the Water task is refused); fishing ×1.25; research and tavern work ×1.15; felled forest regrows twice as fast; outdoor tasks cost ×1.5 energy; sleeping under the trees regenerates at half speed; "Tend the fire" gives half morale; building, hauling and surveying ×0.75 (mud); hunting and roaming ×0.8.
- The weather of a cycle is the weather at the moment the cycle started.

## 8. Anti-abuse summary

| Abuse | Countermeasure |
|---|---|
| Fast clicking / scripted requests | Server-authoritative task length (minutes), every task costs energy, results computed from server timestamps |
| Bots renewing 24/7 | Rolling energy pool: experience stops when energy is empty, so bots do not outpace humans |
| Farming the easiest task | Same base experience for all tasks + Challenge multiplier |
| Grinding useless resources | Storage caps + Overflow multiplier |
| One player carrying everything | Talent cap (30 total stat levels), project contribution cap (20%) |
| Rushing levels | Daily energy budget and challenge multiplier: about one profession level and two stat points a day however long one plays |
| Daily reset exploits / timezones | Continuous energy regeneration instead of daily reset |
| Multiple accounts | One Citizen per Account per Village; Accounts are tied to a verified identity (platform login or streamer invite) |
| Suspicious automation patterns | Server heuristic: very low variance in renewal delay over hours → flag for the streamer, optional confirmation challenge |
| Client-side cheating | Client only sends "start task X" and "renew"; all randomness, yields and experience are calculated on the server |

## 9. Subscription lifecycle

| Event | Effect |
|---|---|
| New subscriber | Citizen creation unlocked; arrival shown in the village |
| Subscription lapses | Citizen becomes **Dormant**: no tasks, stats and items frozen (no decay) |
| Resubscribe | Citizen wakes up, keeps everything |
| Tier upgrades | Cosmetics, house upgrades, extra title slots. **Never stats, experience or energy.** |

## 10. Tuning parameters

| Parameter | Value |
|---|---|
| Stat range / start | 0–10 / 0 |
| Talent cap (sum of stats) | 30 |
| Skill level L needs | primary stat L − 1, secondary ⌊(L − 1) / 2⌋ |
| Journeyman / master craft level | 3 / 7 |
| Task length: gathering / crafting / guard shift | 3 min / 5 min / 20 min |
| Grace window | 60 s |
| Presence window (away after) | 5 minutes |
| Base experience per cycle | 10 |
| Secondary experience share | 0.3 |
| Skill experience curve | 50 for the first level, then 270 + 20 × L |
| Stat experience curve | 0.6 × the skill curve |
| Skill maximum level | 10 (no talent cap) |
| Skill bonus per level above the task's need | +5% yield, −4% work time, up to 5 levels |
| Challenge threshold / penalty | task skill level + 3 / × 0.25 |
| Energy max | 36 + Endurance |
| Energy full regeneration time | 24 hours |
| Bed regeneration: under the trees / lean-to / hut | × 0.8 / × 0.9 / × 1 |
| Vigor drink (tavern, "Vigor drinks" tech) | +8 energy, once per 2 hours; brewed from 2 grain + 1 food |
| Tired experience / yield | × 0 / × 0.25 |
| Overflow experience | × 0.5 |
| Stat factor | 0.4 + 0.12 × EffectiveStat |
| Streak | +1% per renewal, max +20% |
| Morale range | 0.8–1.2 |
| Tool bonus | +10% per tier, max +30% |
| Rare find chance | 1% + 0.5% × Dexterity |
| Engineer supervision | 3 + Intelligence laborers |
| Foreman crew bonus | +2% per Charisma |
| Project contribution cap | 20% per requirement |

## 11. Open questions

- **Stat balance:** decided to keep all five stats. Endurance (energy) and Charisma (two crafts, both with work not available yet) are weaker than the others; to be revisited in the hardening pass (less experience and energy, higher build requirements).
- **Respec:** should Citizens be able to lower one stat to raise another (for example, a costly "retraining" project)? Without it, the talent cap is permanent.
- **Seasons:** do villages reset periodically (fresh competition, new streamers can catch up) or persist forever?
- **Failure/risk:** decided for the wild: hunts and expeditions can be ambushed and wound the citizen (section 12). Should deep mining get a cave-in chance too?

## 12. Fights, enemies and weapons

### 12.1 Enemies

| Enemy | Power | Ambush weight | Slain leaves | One raider steals | Building damage | Wound |
|---|---|---|---|---|---|---|
| Wolf | 3 | 5 | 1 leather | 3 food | 0 | 30 min |
| Boar | 5 | 3 | 6 food, 1 leather | 4 food, 2 grain | 10 | 40 min |
| Bear | 12 | 1 | 15 food, 3 leather | 12 food | 25 | 60 min |
| Zombie (undead) | 4 | — | — | — | 15 | 30 min |
| Skeleton (undead) | 6 | — | 1 iron ore | 1 gold | 20 | 40 min |

### 12.2 Fight power and the armory

- **Fight power** = 2 (bare hands) + 0.6 × Strength + 0.4 × Dexterity + 0.3 × Guarding skill + the weapon's power.
- Weapons and armor are goods in the shared storage (the Storehouse takes them). A citizen starting a dangerous task (hunt, roam, expedition, exploration) or a guard task takes the best weapon and armor available — from their own hands first, then storage — and puts them back when the task ends. If storage has no room, the gear stays in their hands.

| Gear | Effect | Breaks per fight | Made by | Needs |
|---|---|---|---|---|
| Spear | +3 power | 10% | Make spears: 2 wood + 1 stone | Weaponcraft |
| Bow | +4 power, shoots 2 arrows per fight; useless without arrows | 5% | Make bows: 3 wood + 1 leather | Archery |
| Arrows | 6 per quiver | used up | Fletch arrows: 1 wood + 2 feathers → 6 | Archery |
| Sword | +6 power | 3% | Forge swords (Smith): 2 iron + 1 coal + 1 leather | Warfare |
| Leather armor | stops 30% of wounds | 8% | Sew leather armor: 3 leather | Weaponcraft |
| Shield | stops 40% of wounds | 5% | Make shields (Carpenter): 2 planks + 1 leather | Warfare |

Discoveries: **Weaponcraft** (Camp era, at the stone pile, after Stone tools), **Archery** (Camp era, at the drying rack, after Tracking and Weaponcraft), **Warfare** (Town era, at the smithy, after Smelting and Weaponcraft; opens the Guard and Captain roles and the Barracks).

### 12.3 Ambushes in the wild

- Every dangerous task has a chance per cycle that a beast attacks: hunt rabbits 4% (wolf), hunt deer 8% (wolf, boar or bear), hunt birds 3%, roam and explore the land 4%, expedition and long expedition 6%. At night the chance is ×1.5. The beast is picked by its ambush weight.
- Win chance = power / (power + beast power). A win gives the carcass loot and Guarding experience, with a 10% chance of a wound anyway. A loss wounds the citizen and the cycle yields nothing (overflow experience).
- New villages are left in peace for the first day (`Game:PeacefulTime`, 0 in development).

### 12.4 Night raids and the undead

- Each night there is a 35% chance beasts attack the camp, between 15% and 85% of the night. The tribe sees them coming 15 minutes before they strike. Wolves only in the Camp era; boars join in the Village era; bears in the Town era. The pack grows with the number of active citizens.
- The Tsar can call an **undead event** for the stream: zombies or skeletons in 1, 2 or 3 waves (small, medium, large), 3 minutes after the call and 2 minutes apart. The Tsar can lay them to rest before they strike.
- **Defense** = (Σ power × task factor of everyone on a guard task at the strike + 3 for the campfire + 5 with the palisade) × the best captain's command (1 + 0.04 × Endurance) × 1.25 with the palisade × 1.1 with the Barracks. Task factor: Keep watch 1, Patrol 1.25, Command 1. Luck varies the defense by ±25%.
- **Repelled** (defense ≥ attack): defenders slay most raiders, share 2 renown per kill, and the slain leave loot; each defender has a 15% chance of a wound. Morale +3%.
- **Lost:** a few raiders die, the rest steal goods, damage random buildings and wound citizens resting at the camp (50% each); each defender has a 60% wound chance. Morale −8%.
- Gear wears out in raids as in ambushes.

### 12.5 Wounds, healing and repairs

- A wounded citizen cannot start any work until the wound heals. A **Remedy** (Brew remedies, or Make poultices from 2 food with Herb lore) heals it at once; the citizen's hands first, then storage.
- A damaged building (0 to 100) works slower: yield × (1 − damage / 200). **Repair buildings** mends the most damaged one.

## 13. Dungeons

A dungeon is a small separate map beyond the edge of the land that the tribe fights through together, live, on the stream.
The first one is the **Dead Lands** (`src/Desertod.Domain/Dungeons/DungeonCatalog.cs`): 48 × 32 tiles of dead woods, a
fenced graveyard and the barrow of a dead king, 19 undead, 15 minutes.

### 13.1 Gathering and march

- Only the Tsar opens a dungeon (one at a time per village). A **60-second gathering** starts; every citizen gets a pop-up.
  Closing it declines; answering opens the **class picker**. Citizens may switch class or stay home until the gathering ends;
  the Tsar may call it off.
- Inactive and wounded citizens cannot join. Joining **cancels the running task** (like Cancel task).
- The class takes its gear from storage at once (the citizen's own hands first) and puts it back if they leave.
- The Tsar always comes as the **Voivode**: 80 health, 8 damage, no gear, no wound and no rewards.
- When the gathering ends, every hero walks to the west edge of the land (this reveals the strip of land along the way).
  Each player's page moves to `/v/:slug/dungeon/:runId` once their figure arrives. The fight starts when the last hero
  arrives, at most 45 seconds after the march began. Anyone signed in may watch the run.

### 13.2 Classes

Health = (20 + 4 × Endurance) × health factor. Damage of one blow = the fight power of section 12.2 with the class weapon × damage factor.

| Class | Needs | Takes | Reach (tiles) | Blow every | Health | Damage | Trains |
|---|---|---|---|---|---|---|---|
| Brawler | — | leather armor if any | 0.9 | 1.0 s | × 1 | × 1 | Strength |
| Spearman | a spear in storage | spear, leather armor | 1.5 | 1.1 s | × 1 | × 1 | Strength |
| Swordsman | a sword, Guarding 2 | sword, leather armor | 1.0 | 1.0 s | × 1 | × 1.1 | Strength |
| Archer | a bow, Hunting 2 | bow, up to 12 arrows, leather armor | 5 | 1.4 s | × 0.9 | × 1 | Dexterity |
| Shieldbearer | a shield, Guarding 1 | shield | 0.9 | 1.2 s | × 1.5 | × 0.7 | Endurance |
| Healer | a remedy, Scholarship 2 | 1 remedy, leather armor | 3 | 1.5 s | × 1 | × 0.6 | Intelligence |

- Armor stops a share of every blow: leather armor 30%, a shield 40%.
- An archer uses one arrow a shot; without arrows they fight at melee reach for half damage.
- A healer heals an ally 8 health per use. The remedy is used up once they have healed anyone.

### 13.3 The fight

- The server runs the fight 10 times a second; players only send orders. Heroes **never attack on their own**: the player
  clicks a foe to attack it (the hero walks up and keeps striking until it dies), the ground to walk, an ally to heal.
- Foes stand still until a fighting hero comes within their **aggro range**. Then they chase the nearest hero and never flee.
  A foe a hero strikes first chases that hero.
- **Every kill rewards every hero still fighting** (not the Voivode): the foe's experience and a random roll of its loot.

| Foe | Health | Damage | Blow every | Speed | Aggro range | Reach | Experience | Loot |
|---|---|---|---|---|---|---|---|---|
| Grave rat | 8 | 2 | 1.0 s | 2.6 | 4 | 0.9 | 4 | 1–2 food 50%, 1 leather 25% |
| Zombie | 22 | 4 | 1.6 s | 1.0 | 5 | 0.9 | 8 | 1 leather 30%, 1 iron ore 20% |
| Skeleton | 18 | 5 | 1.3 s | 1.6 | 6 | 0.9 | 8 | 1–2 iron ore 60%, 1 gold 10% |
| Imp | 12 | 3 | 0.8 s | 2.4 | 6 | 0.9 | 6 | 1–2 coal 60%, 1 remedy 10% |
| Ghoul | 30 | 6 | 1.4 s | 1.8 | 5 | 1.0 | 12 | 1–2 leather 50%, 1 iron ore 30% |
| Wraith | 16 | 4 | 1.8 s | 1.4 | 7 | 4 | 10 | 1 remedy 20%, 1 gold 15% |
| Barrow King | 120 | 10 | 1.6 s | 1.2 | 6 | 1.2 | 40 | 2–4 gold, 2–3 iron, a sword 40%, a shield 30% |

The new foes live only in dungeons; the Tsar's undead event still calls only zombies and skeletons.

### 13.4 Falling, winning and losing

- A hero at 0 health is **knocked out**: they run for the entrance with a skull over their head and go home **wounded** for
  the killer's wound time: zombie 30 and skeleton 40 minutes (section 12.1), grave rat 20, imp 30, wraith 40, ghoul 45,
  Barrow King 60. They keep what they earned until then.
- **Victory**: every foe is slain. **Defeat**: no hero is left fighting, or 15 minutes pass (a timeout wounds nobody).
- Leaving heroes are settled at once: experience goes to the class stat and to Guarding; loot goes to storage, and what does
  not fit stays in their hands; gear wears out once per foe hit as in section 12; the remaining arrows go back; then they walk
  home. The result stays on the village banner for 2 minutes.
- A server restart abandons a run under way: everyone comes home with what was already settled.
