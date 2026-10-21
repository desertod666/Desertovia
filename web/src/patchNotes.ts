import type { Language } from './i18n/translator'

/** Implemented changes in this build. Deployment evidence is kept in docs/dezertovia. */
export interface PatchNote {
  version: string
  date: string
  title: Record<Language, string>
  changes: Record<Language, readonly string[]>
}

export const PATCH_NOTES: readonly PatchNote[] = [
  {
    version:'0.7.17',date:'2026-10-21',
    title:{ru:'Топоры и кирки',en:'Axes and pickaxes'},
    changes:{
      ru:['Деревянные, каменные, железные и стальные инструменты можно изготовить и взять со склада. Подходящий лучший инструмент используется автоматически.',
        'Каменные и более прочные инструменты увеличивают добычу за работу. Время работы прежнее; деревянные инструменты дают обычную добычу.',
        'Износ сохраняется в руках и на общем складе. Видно оставшееся число применений и сообщение о поломке. Добыча золота ограничена остатком жилы.'],
      en:['Craft wooden, stone, iron and steel tools and take them from storage. The best matching tool is used automatically.',
        'Stone and stronger tools increase yield per job. Job duration stays unchanged; wooden tools give ordinary yield.',
        'Wear persists in hands and communal storage, with remaining uses and breakage feedback. Gold extraction respects the remaining vein.']}
  },
  {
    version:'0.7.16',date:'2026-10-19',
    title:{ru:'Исправления обращений и стройки',en:'Feedback and construction fixes'},
    changes:{
      ru:['Снимки отчётов сохраняют карту с постройками в Safari. Значок «!» перенесён в верхний левый угол; отмена удаляет черновик.',
        'После отказа проверки форму можно исправить и убрать снимок. При сетевом сбое повтор не создаёт дубликат. Обращения входят в ежедневную сводку батюшки без отдельных сообщений владельцу.',
        'Нетронутую разрушенную разметку можно убрать без ремонта. Вложенные материалы по-прежнему требуют разборки.',
        'Завершение работы не возвращает оружие на склад посреди набега. Обычная поломка оружия после боя сохраняется.',
        'Нажатие, открывшее карточку, не активирует её кнопки. Плитки ресурсов используют читаемую тёмную тему.'],
      en:['Safari report screenshots preserve the map and buildings. The small ! button moves to the top left; cancel discards the draft.',
        'Validation failures allow correcting the form or removing the image. Network retries keep the original report ID. Reports enter the chaplain’s daily digest without individual owner messages.',
        'Untouched ruined blueprints can be withdrawn without repairs. Invested sites still require dismantling.',
        'Finishing work no longer returns equipment to storage during an active raid. Normal post-battle breakage remains.',
        'The gesture opening a card cannot activate its controls. Resource tiles use a readable dark surface.']}
  },
  {
    version:'0.7.15',date:'2026-10-18',
    title:{ru:'Обратная связь из игры',en:'In-game feedback'},
    changes:{
      ru:['Маленькая кнопка «!» доступна поверх карты и меню, в том числе в подземелье. Можно отправить ошибку, жалобу или предложение.',
        'К обращению прикладываются текущий экран, персонаж, версия игры и данные диагностики. Снимок можно посмотреть и убрать перед отправкой.',
        '«Отмена» и крестик удаляют черновик: до отправки снимок и данные нигде не сохраняются. После отправки батюшка передаёт владельцу единый отчёт.'],
      en:['A small ! button is available above the map and menus, including dungeons, for bugs, complaints and suggestions.',
        'Reports include the current game screen, character, game version and diagnostic context. Preview or remove the screenshot before sending.',
        'Cancel and close discard the draft without storing it anywhere. After submission, the chaplain delivers one report to the owner.']}
  },
  {
    version:'0.7.14',date:'2026-10-18',
    title:{ru:'Укрепления и отмена чертежей',en:'Fortifications and blueprint cancellation'},
    changes:{
      ru:['Пустой чертёж можно отменить, даже если после него есть стройки с вложенной работой. Их материалы и вклад сохраняются.',
        'Жители видят задания улучшения укреплений и могут приносить материалы и работать.',
        'В Стройке есть раздел «Забор и ворота»: уровни, технологии, прогресс и объяснение ремонта. Начатое улучшение можно разобрать с подтверждением и обычным возвратом материалов.'],
      en:['Untouched blueprints can be cancelled when later sites contain work; their materials and contributions remain.',
        'Residents can see fortification upgrade jobs, deliver materials and work.',
        'Construction includes Fence and gates: tiers, technologies, progress and repair guidance. Invested upgrades use the existing confirmed dismantling and salvage rules.']}
  },
  {
    version:'0.7.13',date:'2026-10-18',
    title:{ru:'Отклик в подземельях и значки ресурсов',en:'Dungeon response and resource icons'},
    changes:{
      ru:['Быстрые нажатия в подземелье сохраняют последнее намерение вместо длинной очереди устаревших команд.',
        'Движение участников больше не ждёт записи результатов выбывшего героя. При ошибке сохранения награды остаются доступны для повторной записи.',
        'Проверка больницы перед действием касается только управляющего героем игрока. Новые ранения по-прежнему запрещают бой.',
        'Значки ресурсов на узком экране снова помещаются в ячейки и не перекрывают числа. Заточка +N и прежний размер значков на ПК сохранены.'],
      en:['Rapid dungeon clicks keep the latest intent instead of a backlog of outdated orders.',
        'Fighters keep moving while another hero’s results are saved. Failed persistence retains the run and rewards for retry.',
        'An action checks the acting player’s hospital status only. Fresh injuries still prevent combat.',
        'Resource icons fit narrow-screen slots without overlapping amounts. Sharpening badges and desktop icon sizes remain.']}
  },
  {
    version:'0.7.12',date:'2026-10-18',
    title:{ru:'Заточка, Мёртвые земли и чтение деревни',en:'Weapon edges, the Dead Lands and a clearer village'},
    changes:{
      ru:['В кузнице точат один меч, копьё или стрелу за работу, до +3. Каждая ступень прибавляет 1 к силе удара и стоит 1 уголь. Заточка остаётся на стопке и показывается как +N. Лук не точится: точатся стрелы. В бой берётся самое острое подходящее оружие.',
        'Мёртвые земли — один поход из трёх этажей: мёртвый лес, погост и король кургана. Упавший герой остаётся жив и ранен; кто ещё стоит, идёт дальше.',
        'Пропавшие трупы в подземелье не появляются снова. Обводка выбранного здания совпадает со спрайтом.',
        'Тёмный склад читается как чернила на пергаменте. Голод режет опыт в начале цикла. Счётчики костра и золота считают вклады. Дорога не срезает воду, камыш не кончается.',
        'В деревне звучат гусли, в набеге — боевая тема. Громкость и выключение — в настройках. После сворачивания окна Telegram деревня догоняет состояние.'],
      en:['At the smithy, one sword, spear or arrow is ground a step sharper, up to +3. Each step adds 1 fight power and costs 1 coal. The edge stays on the stack and is shown as +N. A bow is not sharpened; the arrows are. Fights take the sharpest fitting weapon.',
        'The Dead Lands are one run of three floors: the dead woods, the graveyard and the barrow king. A fallen hero lives and stays wounded; whoever is still standing goes on.',
        'Faded dungeon corpses do not appear again. The selected building outline matches the sprite.',
        'Dark storage reads as ink on parchment. Hunger cuts experience at the start of the cycle. Campfire and gold counters count contributions. Paths do not cut water, and reeds do not run out.',
        'Gusli plays in the village and a battle theme during a raid. Volume and mute are in Settings. After the Telegram window is collapsed, the village catches up.']}
  },
  {
    version:'0.7.11',date:'2026-10-17',
    title:{ru:'Набеги, спасение и развитие поселения',en:'Raids, rescue and settlement progression'},
    changes:{
      ru:['Раненого можно подобрать и физически доставить в больницу в любой момент до выздоровления. В палате оставшееся время лечения сокращается до четверти; полезная доставка даёт славу один раз.',
        'Редактор царя: БОСС-ФАЙТ, смешанный бой и орда, собственные пресеты и настройка состава, волн, силы и ущерба. Оценку можно посмотреть до запуска.',
        'Сложность учитывает фактическое сопротивление и результаты боёв. Ущерб зданиям отделён от тяжёлого разгрома; кражи и расходники учитываются отдельно.',
        'Здания получили постоянные номера внутри своего типа. Единственная постройка типа показывается без номера; координаты, владельцы и история сохранены.',
        'Деревянный забор можно усилить до бревенчатого, затем до каменного за материалы и работу. Повреждения сохраняются; готовое усиление ждёт окончания всех волн. Добавлены дневные и ночные текстуры в стиле карты.',
        'Еда расходуется раз в три законченных рабочих цикла. Улучшения жилья и складов увеличивают вместимость; меню показывает условия расширения царства.',
        'Реальные удары в набегах дают ограниченный опыт силы и ловкости. Очки исследований можно однократно вложить в выбранную технологию, сохраняя требования к участникам.',
        'Каша и похлёбка готовятся из существующих ресурсов в пекарне и корчме. Один временный эффект: −10% расхода сил на работу или +5% урона в набеге; HP и силы мгновенно не восстанавливаются.',
        'Явные предложения владельца батюшке действительно сохраняются в предложке; повтор сообщения не создаёт дубликат.'],
      en:['A wounded resident can be physically carried to a hospital at any time before recovery. Admission reduces remaining recovery to a quarter; useful rescue awards renown once.',
        'Owner raid editor: boss, mixed and horde presets, saved configurations, composition, waves, power and loss controls, with a preview before launch.',
        'Difficulty uses actual resistance and battle outcomes. Property damage is separate from a heavy defeat; theft and consumables are accounted for.',
        'Buildings have permanent numbers within each type. A sole building hides its suffix; coordinates, ownership and history are preserved.',
        'Upgrade wooden fences to logs and stone through paid construction. Damage remains; completed reinforcement waits for all waves to end. Matching day and night map textures added.',
        'Food is consumed every three completed work cycles. Housing and storage upgrades offer more capacity; the menu explains kingdom expansion.',
        'Actual raid hits grant bounded strength and dexterity experience. Research points can support the elected technology once, retaining participant requirements.',
        'Cook porridge or stew at the bakery and tavern using existing resources. One temporary effect: −10% work energy cost or +5% raid damage; neither instantly restores HP or energy.',
        'Explicit owner suggestions to the chaplain are durably recorded without duplicate messages.']}
  },
  {
    version:'0.7.9',date:'2026-10-17',
    title:{ru:'Разорение деревни, орда и боссы',en:'Property raids, hordes and bosses'},
    changes:{
      ru:['Враги прежде всего разоряют имущество и запасы: распределяют цели, обходят толпу и делают проломы. Недострои тоже уязвимы.',
        'Директор может предложить орду слабых, смешанный отряд, элиту или от одного до трёх живучих боссов в пределах готовности деревни.',
        'Боссы предупреждают тяжёлый удар областью на земле и меняют фазу при потере половины здоровья.',
        'Добычу из выбранного склада, в том числе личного, уносит только сбежавший живой враг. Ущерб и кража ограничены; потери рюкзака при поражении не добавлены.',
        'Общие алгоритмы выбора целей и обхода толпы используются также в подземелье. Команды игроков, ранения и прогресс сохраняются.'],
      en:['Raiders primarily attack property and supplies, distribute targets, avoid crowds and breach barriers. Unfinished buildings remain vulnerable.',
        'The director can propose weak hordes, mixed groups, elites or one to three durable bosses within the village readiness budget.',
        'Bosses mark heavy strikes on the ground and change phase below half health.',
        'Only a living escaping carrier takes goods from its selected storage, including private storage. Damage and theft are bounded; no backpack death penalty was added.',
        'Dungeons share target selection and crowd avoidance. Player orders, injuries and progress are retained.']}
  },
  {
    version:'0.7.8',date:'2026-10-17',
    title:{ru:'Всевышний: предложения набегов',en:'Director raid proposals'},
    changes:{
      ru:['Недострой, повреждения и отдельные пациенты больше не дают деревне бессрочный иммунитет при достаточном числе боеспособных игроков онлайн.',
        'После часа без боя директор предлагает ограниченный набег. Каждая заявка даёт два часа паузы для всех вариантов; отказ нельзя обойти сменой врага.',
        'Нападение требует согласия царя и повторной проверки условий. Реальные передышки, защита новичков и восстановление после проигрыша сохраняются.'],
      en:['Unfinished buildings, damage and individual patients no longer grant blanket immunity when enough capable residents are online.',
        'After a quiet hour the director proposes a bounded raid. Every proposal pauses the whole category for two hours; switching enemies cannot bypass rejection.',
        'Raids still require owner approval and fresh readiness checks. Approved rest, newcomer protection and recovery after defeat remain.']}
  },
  {
    version:'0.7.7',date:'2026-10-17',
    title:{ru:'Питание, жильё и адресная работа',en:'Meals, homes and selected work targets'},
    changes:{
      ru:['Здания получили постоянные порядковые номера вместо координат в названиях.',
        'Персонаж ест свою еду из рук и личного запаса при личной и общественной работе. В инвентаре голодного персонажа есть кнопка «Поесть».',
        'Ремонт восстанавливает 10% и чинит выбранное здание. Выбранный склад принимает груз без отправки в другой; остаток остаётся в руках.',
        'Владелец может заселиться в собственный готовый дом без заявки царю. В карточке видны владелец и дата постройки.',
        'Окно результата работы больше не переключает карту на 8 кадров/с. Рабочие маршруты и прогулки обходят воду.',
        'Перед добычей видно назначение продукции; в работах указаны обе развиваемые характеристики. Личные материалы можно бесплатно отдать в общий запас.'],
      en:['Buildings have persistent sequential numbers instead of coordinates in their displayed names.',
        'Own food in hands and personal inventory is used during both private and public work. Hungry residents can eat from Inventory.',
        'Repair restores 10% of the selected building. A selected storage receives the load without redirecting it; overflow remains carried.',
        'Owners can move into their own completed homes without royal approval. Home cards show the owner and completion date.',
        'The work-result panel no longer throttles the visible map to 8 FPS. Work and roaming routes avoid water.',
        'Gathering explains the output destination, work lists both trained stats, and personal materials can be donated to communal stock.']}
  },
  {
    version:'0.7.6',date:'2026-10-16',
    title:{ru:'Очередь операций во время боя',en:'Combat operation contention'},
    changes:{
      ru:['Приказ атаки проверяет лечение самого атакующего, не запрашивая принудительно состояние всей деревни. Свежие травмы и недоступность медицинского сервиса по-прежнему запрещают атаку.',
        'Зачёт работ освобождает общую очередь после каждого жителя. В диагностике боя видны длительность удержания и ожидания блокировки по серверным операциям.'],
      en:['Attack orders refresh only the acting player’s clinical state. New injuries and unavailable clinical authority still block combat.',
        'Work settlement releases the village lock between residents. Combat diagnostics attribute lock wait and hold times to server operations.'],
    },
  },
  {
    version:'0.7.5',date:'2026-10-16',
    title:{ru:'Плавное движение в бою',en:'Smooth combat movement'},
    changes:{
      ru:['Убрано округление координат бойцов до целых пикселей карты: медленное движение больше не разбивается на крупные шаги.',
        'Переходы между серверными координатами учитывают неравномерную доставку обновлений. Игроки, враги и полосы здоровья используют общее сглаживание без выхода за подтверждённую позицию.'],
      en:['Fighter coordinates keep subpixel precision, avoiding large steps during slow movement.',
        'Movement pacing accounts for uneven snapshot delivery. Players, enemies and health bars share interpolation without extrapolating beyond confirmed positions.']}
  },
  {
    version:'0.7.4',date:'2026-10-16',
    title:{ru:'Свободный бой и диагностика',en:'Free combat and diagnostics'},
    changes:{
      ru:['Дозоры и участки обороны убраны. Выбирай врага или место на карте самостоятельно; без цели персонаж отвечает только на врага в пределах удара.',
        'Бой больше не привязывает персонажа к костру. Возврат в бой сохраняет потраченное здоровье и лечение.',
        'Исправлен откат медленных бойцов к предыдущей клетке и рывок при наложении снимков движения. Боевые пакеты больше не обновляют всех мирных персонажей.',
        'В настройках можно скачать диагностику рывков: время кадров, обновлений и доставки данных. Сервер отдельно измеряет расчёт боя, поиск пути и сохранение.'],
      en:['Guard duties and defense sectors are removed. Choose enemies and ground yourself; without a target, characters defend only within attack range.',
        'Combat no longer ties players to the camp. Re-entry preserves spent health and healing.',
        'Fixed slow actors returning to a previous cell and overlapping movement snapshots. Combat packets no longer rebuild all civilian actors.',
        'Settings can export combat frame, update and delivery timings. The server also measures simulation, pathfinding and persistence.']}
  },
  {
    version:'0.7.3',date:'2026-10-16',
    title:{ru:'Личный бой и последствия осады',en:'Personal combat and siege consequences'},
    changes:{
      ru:['HP и силы собраны в компактном блоке в стиле ресурсов. Стройка и голосование доступны из меню; дублирующая кнопка выхода убрана.',
        'Клик по врагу отправляет приказ атаки без карточки. В личном бою доступны отход и лечение: одно лекарство за 20% HP, откат 60 секунд.',
        'Бойцы движутся по серверным снимкам без конкурирующего движения мирного персонажа.',
        'Перед добровольным сносом дома жители получают конкретные подходящие места. Причины отказа и состояние заявки понятны в игре и Telegram.',
        'Нападающие повреждают недострои. Полностью разрушенные здания и мост не работают до восстановления; прежние материалы и прогресс сохраняются.',
        'Новые поселения начинают с замка. Потеря замка заканчивает оборону и останавливает следующие волны, сохраняя персонажей и путь восстановления.',
        'За мостом действует монастырский обмен припасов на лекарство. Короткий адрес открывает основной мир; старые ссылки сохраняются.'],
      en:['Personal health and stamina share the resource-card style. Building and votes stay in the menu; the duplicate exit is removed.',
        'Selecting an enemy sends an attack without a card. Personal combat offers physical retreat and healing: one remedy for 20% HP, with a 60-second cooldown.',
        'Fighters follow server snapshots without competing civilian movement.',
        'Voluntary demolition assigns suitable replacement beds. The game and Telegram explain application refusals and actual status.',
        'Attackers damage unfinished sites. Ruined buildings and the bridge need restoration; previous materials and progress remain.',
        'New settlements start with a castle. Losing it ends defense and cancels later waves while preserving characters and recovery.',
        'The monastery trades supplies for a remedy beyond the bridge. The short address opens the primary world and old links still work.']}
  },
  {
    version:'0.7.2',date:'2026-10-16',
    title:{ru:'Бой, карта и очередь строек',en:'Combat, map and construction queue'},
    changes:{
      ru:['Исправлены лишние пересоздания зданий, освещения и полос здоровья во время боя; движение врагов стало плавнее.',
        'Клик по врагу задаёт автоатаку без четырёх отдельных кнопок. Царь может вступить Воеводой без задания охраны.',
        'Пациенты готовой больницы отображаются в её карточке. Раненые до переноски остаются на поле.',
        'Dcoin, тестовые свечи и ресурсы собраны в компактной верхней панели.',
        'Нельзя подать новую строительную заявку, когда общая очередь заполнена. Причины недоступных улучшений объясняются по-русски.',
        'Батюшка уточняет проект, версию и время отзыва; сведения автора сохраняются отдельно от проверенной версии сервера.'],
      en:['Combat reuses buildings, lighting and health bars; enemy movement is smoother.',
        'Click an enemy to set autoattack without four separate buttons. The Tsar may join as Voivode without guard duty.',
        'Admitted patients appear in the completed hospital card. Wounded awaiting rescue remain on the field.',
        'Dcoin, test candles and resources share a compact top bar.',
        'New construction applications are blocked when their queue is full. Unavailable upgrades explain their reason.',
        'Father clarifies feedback project, version and time, keeping reporter statements separate from verified live versions.']}
  },
  {
    version:'0.7.1',date:'2026-10-16',
    title:{ru:'Управление обороной и удобство карты',en:'Defense controls and map usability'},
    changes:{
      ru:['Заявки посадников на строительство снова приходят царю через батюшку: одобрить или отклонить можно прямо в Telegram.',
        'Отзывы батюшке содержат автора и подтверждённый игровой код для разбора ошибок.',
        'Царь может лично вступить в бой как Воевода. Выбор врага задаёт атаку, приказ движения позволяет отойти.',
        '«Усыпить нежить» останавливает текущую атаку и ожидающие волны. Уже полученные ранения и повреждения сохраняются.',
        'Забор доведён до фактического берега. Старые повреждения и положение ворот сохраняются.',
        'Карточка стройплощадки показывает владельца и дату. Добавлены разборка начатой площадки и согласование старого конфликтного участка царём.',
        'Возврат материалов после разборки хранится до получения владельцем в кошельке.',
        'Исправлены пустые отступы в заявках и положение крестика карточки противника.',
        'На карте видны личные Dcoin и тестовые свечи. Нажатие открывает кошелёк; суммы и цены не изменены.'],
      en:['Governors’ construction requests reach the Tsar through Father again, with Telegram approval and rejection buttons.',
        'Feedback includes its author and verified game account code for troubleshooting.',
        'The Tsar can join defense as the Voivode. Select an enemy to attack, or order movement to withdraw.',
        'Putting undead to sleep stops the active attack and queued waves while preserving existing injuries and damage.',
        'The palisade reaches the actual river bank without resetting damage or gates.',
        'Construction cards show ownership and placement date, with unfinished-site dismantling and explicit approval of legacy conflicting plots.',
        'Recovered materials remain available to their original owner in the wallet until collected.',
        'Fixed excessive application spacing and the enemy card close control.',
        'Personal Dcoin and clearly marked test candles are visible on the map with direct wallet access. Balances and prices are unchanged.']
    }
  },
  {
    version:'0.7',date:'2026-10-16',
    title:{ru:'Живая оборона деревни',en:'Live village defense'},
    changes:{
      ru:['Нападающие приходят с одной стороны после предупреждения. Каждый враг имеет своё здоровье, цель и время удара; мгновенный общий расчёт заменён боем на карте.',
        'Всевышний выбирает подход и состав: больше слабых, меньше сильных или смешанную волну. Бюджет фиксируется при объявлении; подготовка игроков помогает без скрытого усиления врагов.',
        'Охранник атакует ближайшего доступного врага или выбранную на карте цель. Можно держать позицию или перехватывать; частые нажатия не ускоряют удары.',
        'Ворота можно открыть и закрыть. Враги ищут вход или ломают забор; повреждения ремонтируются. Костёр остаётся ориентиром центра.',
        'Ранение приостанавливает текущее дело до выздоровления, сохраняя оплаченные материалы, энергию и вклад. Спасение и перенос в больницу продолжают работать.',
        'Энергия и предупреждение о нехватке сил обновляются без перезагрузки. Старый снимок больше не заменяет более свежий ответ персонажа или боя.',
        'Глаз интерфейса не перекрывает полноэкранный чат. Склады получили сворачиваемые карточки, сводку свободного места и подсказки об улучшениях.',
        'Запрещена частная стройка, частично пересекающая границу царства. Старые дома сохраняются.',
        'Исправлена публикация служебного JSON от батюшки: реплики и решения используют разные форматы ответа.'],
      en:['Enemies arrive from one side after the warning. Each has individual health, targets and attack cooldowns; map combat replaces instant aggregate resolution.',
        'The Almighty chooses the approach and weak, strong or mixed composition. The announcement fixes the threat budget; preparation does not secretly strengthen enemies.',
        'Guards attack the nearest reachable enemy or a selected map target. Hold/intercept orders never bypass attack cooldowns.',
        'Rulers control gates. Raiders use openings or breach repairable palisades; the campfire is only a central landmark.',
        'Wounds pause paid work until recovery without consuming its materials, energy or contribution twice. Wounded transport remains available.',
        'Energy and low-energy warnings update without reloads. Late snapshots cannot rewind newer citizen or battle state.',
        'The interface toggle stays out of full-screen chat. Compact storage cards show room and upgrade hints.',
        'Private footprints cannot overlap kingdom borders; existing homes remain intact.',
        'Service JSON can no longer become chaplain speech; speech and director decisions have separate output contracts.']}
  },
  {
    version: '0.6.1', date: '2026-10-15',
    title: { ru: 'Удобнее на телефоне, восстановление сил и ответы ИИ', en: 'Mobile menus, energy and AI replies' },
    changes: {
      ru: ['Посадник может предложить любое доступное здание, включая больницу и замок. Стройка начинается после одобрения царём.',
        'Исправлено завершение хижин и сохранение вклада: ошибка одного работника больше не откатывает труд других; отчёты отправляются после сохранения.',
        'Вкладки меню в одну строку, нижняя панель с едиными иконками. Кнопка возврата открывает карту без перехода на стартовый экран.',
        'Дома и заявки разделены на сворачиваемые списки. В заявках указаны здания; невозможные улучшения не создают новые заявки.',
        'Ники игроков можно скрыть в настройках. У пациентов на карте остались статус и причина без длинного таймера.',
        'События работы по умолчанию скрыты в чате, одинаковые соседние отчёты объединяются со счётчиком.',
        'Всевышний предлагает настройку силы и перерыва взвара; изменения требуют согласия царя.',
        'Энергетик батюшки восстанавливает все силы за 300 ДЗК или 3 тестовые свечи раз в 12 реальных часов. Не снимает ранения и обморок.',
        'Исправлены таймаут и обрезка ответов ИИ; журнал различает причины сбоев.'],
      en: ['Governors can request any available building, including hospitals and the castle. Construction starts after Tsar approval.',
        'Fixed hut completion and contribution saves: one failed worker no longer rolls back others; reports are published after commit.',
        'Single-row menu tabs and an icon dock; return to map without leaving the game.',
        'Separate collapsible homes and applications. Unsupported upgrades no longer create applications.',
        'Player name visibility setting. Short patient status without a timer on the map.',
        'Work events hidden by default; repeated adjacent reports grouped with counts.',
        'Owner-approved director tuning for vigor drinks.',
        'Full energy for 300 DZK or 3 test candles, once per 12 real hours; no healing or waking.',
        'Aligned AI timeouts, preserved structured replies and specific failure reasons.'],
    },
  },
  {
    version: '0.6', date: '2026-10-15',
    title: { ru: 'Царство, дезкоины и Всевышний', en: 'The kingdom, dezcoins and the Almighty' },
    changes: {
      ru: ['Полное восстановление сил при хороших условиях теперь занимает12 реальных часов. Перед третьей работой с нехваткой энергии показано предупреждение об обмороке; его срок сохранён. Таймер показывает оставшееся реальное время.',
        'Добавлены царский замок и его расширения, личные стройки за границей царства за собственные материалы.',
        'Тестовые дезкоины оплачивают полезную работу. Личное имущество можно передавать и продавать; общий материал сначала покупается у царства. Обмен — только на тестовые свечи с личным лимитом и фондом царства. Налог пока показывает расчёт без списания.',
        'Журнал показывает изменения запасов и назначения расходов. Добавлены упоминания жителей, выключение игровых всплывашек и компактная энергия/сытость.',
        'Основной город и прогресс персонажа защищены от удаления прямым запросом.',
        'Единый ИИ-директор наблюдает за городом, помнит замысел и предлагает события, передышки и нужную работу. Применение — после согласия царя.',
        'Батюшка присылает предложения царю в личку с кнопками согласия и отказа. В «Городе» появились панель Всевышнего, журнал и переключение режима.',
        'Анализ ИИ раз в15 минут, важные изменения — от5 минут; не больше4 анализов в час. Новое предложение — не чаще раза в30 минут.',
        'Направленные подходы скелетов используют серверный маршрут. Охрана выбирает участок; перестроение занимает30 реальных секунд. Против направленной атаки резерв Авто даёт75% личного вклада.',
        'Экономические советы опираются на запасы и наблюдения. Свечи, цены и налоги автоматически не меняются. Возможен добровольный сбор в существующий данж.',
        'Версия в настройках открывает историю обновлений. Враги ждут конца таймера; сроки лечения показывают реальные дни, часы и минуты.'],
      en: ['Full energy recovery in good conditions now takes12 real hours. A third tired-work action warns about fainting; its duration is unchanged. Recovery shows explicit real remaining time.',
        'Added a royal castle with territory upgrades and privately funded construction outside the kingdom.',
        'Test dezcoins reward useful work. Personal goods can be transferred and traded; common stock must first be purchased. Exchange grants only test candles with personal and kingdom limits. Tax remains a preview without deductions.',
        'A journal records stock changes and spending. Added resident mentions, an in-game popup preference and compact energy/hunger.',
        'The primary kingdom and resident progress are protected from destructive direct requests.',
        'One AI director watches the village, remembers its intent and proposes events, recovery windows and useful work. Effects require royal approval.',
        'The chaplain privately sends the owner approval/rejection buttons. Village now has a director panel, journal and mode controls.',
        'AI reviews every15 minutes, important changes after5 minutes; at most4 reviews/hour. New proposals at most every30 minutes.',
        'Directional skeleton attacks use server paths. Guards choose a sector; redeployment takes30 real seconds. Auto reserves contribute75% of personal strength to directional attacks.',
        'Economic advice uses stock observations. Candles, prices and taxes are not changed automatically. Existing dungeons can be proposed for voluntary participation.',
        'Click the Settings version to read release history. Enemies wait for the countdown; recovery displays real days, hours and minutes.'],
    },
  },
  {
    version: '0.5.3', date: '2026-10-15',
    title: { ru: 'Исправления интерфейса и стройки', en: 'Interface and construction fixes' },
    changes: {
      ru: [
        'Чат больше не закрывает карточку выбранного здания и её кнопки.',
        'Характеристики показывают вашего персонажа. Информация о супруге больше не повторяется в каждой строке.',
        'Подписи зданий привязаны к самому зданию или текущей стадии стройки.',
        'Полезный вклад в стройку резервируется: несколько игроков не могут одновременно занять одну оставшуюся работу.',
        'В заданиях видна стоимость энергии с учётом условий. Исправлены полоса энергии и читаемость статуса в тёмной теме.',
      ],
      en: [
        'Chat no longer covers the selected building card or its buttons.',
        'Attributes show your own character. Spouse information no longer repeats in every row.',
        'Building labels follow the building itself or its current construction stage.',
        'Useful construction work is reserved so several players cannot claim the same remaining work.',
        'Tasks show their energy cost including current conditions. Fixed the energy bar and dark-theme status contrast.',
      ],
    },
  },
  {
    version: '0.5.2', date: '2026-10-15',
    title: { ru: 'Больница и совместная жизнь', en: 'Hospital and village life' },
    changes: {
      ru: [
        'Больница: пациенты, причины ранений, сроки лечения и переноска раненых после боя. Дуэльные ранения связаны с батюшкой.',
        'Мост строится этапами. Добыча за закрытой переправой запрещена сервером; за полезную стройку начисляется слава.',
        'Лимиты ресурсов и улучшения складов, общий жилой дом, заявки на заселение и связь браков с батюшкой.',
        'Истощение замедляет работу, а работа без отдыха может привести к обмороку. Добавлены квалифицированное изготовление энергетиков, ремонт и заявки на разборку.',
        'Батюшка реже повторяется и молчит без свежего разговора. Увеличены зомби и скелеты; подключены единые обводки зданий.',
      ],
      en: [
        'Hospital patients, injury reasons, recovery timers and carrying the wounded after combat. Duel injuries are linked to the chaplain.',
        'Phased bridge construction, server-enforced river access and renown for useful construction work.',
        'Resource-specific storage limits and upgrades, shared housing, settlement requests and linked marriages.',
        'Exhaustion slows work and working without rest can cause collapse. Qualified energy-drink crafting, repairs and demolition requests.',
        'The chaplain repeats less and stays quiet without a recent conversation. Larger zombies and skeletons; consistent building outlines.',
      ],
    },
  },
  {
    version: '0.5.1', date: '2026-10-14',
    title: { ru: 'Компактный чат', en: 'Compact chat' },
    changes: {
      ru: ['Кнопка чата стала компактнее. Жители и онлайн перенесены наверх вкладки «Город».', 'Чат и уведомления уступают место игровым окнам; черновик, история и непрочитанные сохраняются.'],
      en: ['A compact chat button; resident and online counts moved to the Village tab.', 'Chat and notifications yield to game windows while preserving drafts, history and unread counts.'],
    },
  },
  {
    version: '0.5', date: '2026-10-14',
    title: { ru: 'Три режима чата', en: 'Three chat modes' },
    changes: {
      ru: ['Скрытый чат с уведомлениями, чат на четверть экрана и полноэкранный режим.', 'Сохранение черновика, история сообщений, счётчик непрочитанных и отображение версии в настройках.'],
      en: ['Hidden chat with notifications, quarter-screen chat and fullscreen chat.', 'Saved drafts, message history, unread counts and the game version in Settings.'],
    },
  },
]
