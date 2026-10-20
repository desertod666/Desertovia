# Дезертовия 1.0 — сверка исходников, 20.10.2026

Статус: **предварительный аудит кода; входной пакет ещё не получен**.
Это чтение исходников и сохранённых квитанций. Игра и бот не запускались,
сервер не опрашивался и не менялся. Наличие тестового файла ниже означает
найденное покрытие, а не новый успешный прогон.

## Зафиксированные источники

| Источник | Проверенный ref / SHA | Как использован |
| --- | --- | --- |
| Рабочая игра | `desertod666/Desertovia`, main `b9b4b32ef1a3a8c7821a72126ea84ab64fc41b09` | Текущая адаптация и документы общей фермы |
| Оригинальная основа Вадима | `buhaiov-vs/Desertovia`, main `e23bc943bac17e55d4d5a0b0805d07d53de02108` | Проверен общий предок: этот ref является merge-base с рабочей игрой |
| Продолжение Вадима | `buhaiov-vs/Desertovia-1`, main `c42973fe38a8934fdfdabe1ab65512d3f13aa949` | Уже предок рабочей игры; расхождение с ней — последующие документы фермы |
| Инструменты Вадима | `Desertovia-1`, `feat/tiered-tools`, `b617f8d9f38fcc700400dbf2e7239f931d8d2127` | Новая отдельная ветка; просмотрен diff и ToolCatalog, НЕ интегрирована |
| Заточка Вадима | `Desertovia-1`, `feat/weapon-sharpening`, `e30fc988c957b339ba1e7a172d5d70b0d3f59025` | Уже входит в историю рабочей игры; повторно переносить не нужно |
| Батюшка | `desertod666/TG-bots`, default `claude/telegram-bot-chat-variety-wci5ph`, `76de289b00a99c8e40fb2c061beab6e7af216b5c` | Текущая bot-ветка: bridge, директор, подписки, уведомления |
| Последний сохранённый выпуск | [RELEASE_0_7_16_RESULT.json](RELEASE_0_7_16_RESULT.json) | Квитанция от 19.10; game `4bb125f2dc05711ebaf4dac2723f3ee19eae3ea9`, bot payload `8dbbc3a000367a84c12869f6aed0a2b12318c148` |
| Запрошенный внешний пакет | `00_START_HERE.txt` и остальные документы | **Не найден; путь запрошен у владельца. Не прочитан.** Дату подготовки и его полный состав установить пока нельзя |

Рабочие checkout находятся рядом: `Desertovia/` и `TG-bots/`. Отдельный
`desertod666/Sramoslav` не принимался за источник действующего игрового bridge:
он обнаружен при поиске, но здесь исследован именно актуальный `TG-bots`.

Квитанция 0.7.16 сообщает об успешных 537 Domain, 288 API, 238 browser и двух
штатных skip, сохранности 143 профилей и режиме Propose. Это историческое
доказательство выпуска, не свежая проверка production и не проверка 1.0.
В ней явно нет проверки физического телефона и естественного замера боя.

## Карта существующих систем

Пути в таблице относительны корню игры; `TG-bots:` означает соседний checkout
по зафиксированному выше SHA. Сверка ограничена указанными файлами и поиском
символов в `src`, `web/src`, `tests`, `sramoslav`; она не является полным аудитом
безопасности всех модулей или доказательством поведения работающего сервера.

| Область | Что подтверждено исходниками | Чего это ещё не даёт для 1.0 | Точки интеграции и найденные проверки |
| --- | --- | --- | --- |
| Экономика | `GameWallet`, `KingdomEconomy`, `EconomyReceipt`, `LedgerEntry`, `MarketLot`, личный inventory, себестоимость и полезная работа уже существуют. Оплата в `PayUsefulWorkAsync` идемпотентна; базовые WorkPay 10 / ConstructionPay 20 берутся через настройки | Нет учёта нового коллективного урожая/обязательств фермеров. Налог — preview, а не исполняемый сбор | `src/Desertod.Domain/Economy/Economy.cs`; `src/Desertod.Api/Game/GameService.Economy.cs`, `GameService.Accounting.cs`; `KingdomEconomyTests.cs`, `KingdomEconomyApiTests.cs`, `web/e2e/kingdom-economy.spec.ts` |
| Питание и отдых | `FoodRules.Eat`: еда из ноши → личного запаса → общего, если разрешено. `CycleEngine.Eat`: одна Food через 3 завершённых цикла, голодный пробует после каждого. Еда не восстанавливает энергию. Энергия восстанавливается отдельно, базовый полный срок 12ч | Новые доли фермы ещё не включены в общий баланс. Старый комментарий «36 задач в день» не заменяет расчёт цены каждой работы, жилья, голода и реального расписания | `Rules/{FoodRules,Energy,WorkEnergy,PreparedMeals,GameRules,CycleEngine}.cs`; `ProgressionAndMealsTests.cs`, `EnergyAndYieldTests.cs`, API и browser progression/mobile-needs |
| Строительство | Размещение, стадии, резерв полезной работы, ремонт, заявки, разборка, salvage, уровни укреплений и отмена уже есть. При поражении `ValidatePlacement` ограничивает расширение | Нужно проверить достижимость восстановления после потери производств, включая ферму; расширение карты нельзя начинать простой заменой границ | `Buildings/ConstructionRules.cs`, `Rules/WorkContext.cs`, `GameService.Civic.cs`, `CivicNotificationService.cs`; Construction/WorkReservation/FortificationUpgrade/Cooperative тесты |
| Ферма | `FieldBed` хранит plot/index/crop/growth/timestamps. 3 грядки на ферму, 5 культур, серверный рост с дождём, сухая скорость 0.25, полив до 3 грядок на 30мин. `Sow/Harvest` берут первую подходящую грядку; выбор ограничен владельцем фермы. `FieldsCard` показывает состояние | Нет адресного действия на выбранной грядке, назначений фермеров, удобрения, вкладов, продуктовых требований к выдаче и 90/10. `tend-fields` отдельно производит Food и оплачивается как полезный ресурсный цикл | `Farming/{FieldBed,FarmCatalog,FarmRules}.cs`, `Catalog/GameCatalog.cs:121`, `CycleEngine.cs:558`, `GameService.Economy.cs:61`, `web/src/ui/{FieldsCard,TargetPopup,TaskList}.tsx`; `FarmingTests.cs`, `WeatherTests.cs` |
| Бой | Есть живой RaidBattle, управление жителями/царём, типы врагов, адаптивные бюджеты, цели имущества, проломы, ранения, спасение, снаряжение и диагностика. Заточка уже перенесена | Новые атмосферные сценарии, экономика длительного восстановления и потери рюкзака не следуют из наличия боя. Лаги физического телефона не объявлять устранёнными без замера | `Combat/`, `GameService.{Battles,RaidEditor,CombatDiagnostics}.cs`, `web/src/game/VillageWorld.ts`; LiveRaid/MonsterBehavior/FreeCombat/RaidBalanceSimulation и browser live-combat/combat-motion |
| Данжи | `DungeonRun`/фазы Gathering→Marching→Running→Ended, классы/экипировка, приглашения, карты/этажи, выход и выдача результата. Runtime боя в памяти, фазовые/геройские результаты в БД; имеется `Abandoned` после остановки | Полное продолжение той же симуляции после рестарта не подтверждено. Не записывать persistent resume как готовую функцию | `Dungeons/{DungeonRun,DungeonRules,DungeonSimulation,DungeonCatalog}.cs`, `GameService.Dungeon.cs`, `DungeonRuntime.cs`, `DungeonWorker.cs`, `DungeonHub.cs`; Domain Dungeon и API Dungeon/DungeonLatency, `web/tests/dungeonOrders.test.ts` |
| Большая карта | Уже есть карта окрестностей, wilderness, fog/exploration, путь, река/мост и границы царства по замку. `WorldMap.Land` = 120×90, fog использует размер этой области | Мировая карта регионов, межрегиональные маршруты и их отдельные состояния в просмотренном коде не обнаружены. Увеличение Land меняет индексацию fog, а не только отрисовку | `World/{WorldMap,Wilderness,Exploration,BankAccess,AuthoredTerrain}.cs`, `Buildings/KingdomRules.cs`, `web/src/game/{worldMap,VillageWorld}.ts`; Exploration/Wilderness/AuthoredTerrain/BankAccess тесты |
| Походы | Обычная задача `expedition`, таблица Loot и редкая находка уже есть; групповой Dungeon имеет сбор и переход | Длительная группа с провиантом, маршрутом, лагерем и возвратом из регионов — новый слой. Нельзя приравнять к существующему мгновенному settlement задачи | `Rules/Expeditions.cs`, `CycleEngine.cs:643`, `GameCatalog.cs`, `web/src/ui/GameMenu.tsx`; CycleEngine/Project тесты и Dungeon lifecycle |
| Восстановление царства | `RuinRules.Collapse` выводит здания/склады из строя, разрушает мост, освобождает жильё и выставляет `DefeatedAt` при падении общего замка. `CombatRules.Repair` вызывает `RuinRules.Restored`, восстановление замка снимает поражение. Монастырь имеет ограниченный реальный обмен материалов на Remedy | Полный проверенный сценарий «всё разрушено/нет еды/склад полон/больница недоступна → восстановление» ещё не доказан этой сверкой. Баланс восстановления остаётся задачей | `Combat/{RuinRules,CombatRules}.cs`, `Buildings/{KingdomRules,ConstructionRules}.cs`, `GameService.Monastery.cs`; OwnerCorrection/FreeCombat/Cooperative/KingdomEconomy тесты |
| Самостоятельный Всевышний | Один директор, snapshots/observations/lease/предложения, лимиты модели и повторная серверная проверка. `AlmightyPolicy.IsMode` допускает только Off/Observe/Propose. `DecideAlmightyCoreAsync` проверяет владельца; бот явно требует согласия царя | Автономного режима исполнения нет. Уже исполняемые после одобрения raid/rest/dungeon/vigor не означают произвольную власть ИИ; налог/настоящие деньги не разрешены | `Director/Almighty.cs:143`, `GameService.Almighty.cs:176,230`, `AlmightyService.cs`; `TG-bots:sramoslav/{almighty,ai_budget}.py`; Almighty Domain/API/browser и `tests/test_almighty.py` |
| Интерфейс | React/Pixi/SignalR, TargetPopup, мобильный HUD, чат, настройки, отчёты «!», дневные/ночные спрайты уже есть | Нет нового фермерского окна и региональной карты. Не нужен второй renderer или новый shell. Старые сообщения о UI-регрессиях не считать свежими воспроизведениями | `web/src/pages/GamePage.tsx`, `web/src/ui/`, `web/src/game/`, i18n; browser cooperative/post-071/player-feedback/in-game-reports |
| Dcoin | UI-переименование ДЗК и кошелёк есть. В `exchange` списываются Coins и добавляются TestCandles; лимиты по UTC-дню, базово 100:1, 5 на игрока, 50 на царство, с возможными overrides | Нет показанного перевода на реальный свечной счёт батюшки. Нельзя умножать остатки повторно или считать TestCandles настоящими | `GameService.Economy.cs:40,263`, `Economy/Economy.cs`, `web/src/ui/EconomyPanel.tsx`; KingdomEconomy Domain/API/browser |
| Подписки | В батюшке есть каталог уровней YouTube и событийные свечные бонусы, привязка получателя. `Memberships.confirm` подтверждает название уровня, не статус зрителя | Новый игровой entitlement, подписка на саму игру, срок/отзыв/возврат и bridge в .NET в просмотренных модулях не найдены. Модель монетизации из отсутствующего пакета неизвестна | `TG-bots:sramoslav/{memberships,membership_studio,donations}.py`; `tests/test_membership_panel.py`, `tests/test_membership_studio.py` и membership-сценарии `tests/test_bot.py`. Не путать с подпиской на уведомления |
| Уведомления | Клиент `notifications.ts` показывает Notification при открытой фоновой вкладке после opt-in; GamePage получает события. Серверные civic-уведомления имеют claim/receipt/retry и owner callback. Отчёты сохраняются в общей памяти/существующей сводке. У legacy `town_push.py` есть Web Push | Web Push закрытого текущего .NET-клиента не следует из наличия legacy push. Новым событиям фермы/походов нужны свои предпочтения, дедупликация и актуальный источник | `web/src/notifications.ts`, `GamePage.tsx`, `CivicNotificationService.cs`; `TG-bots:sramoslav/{civic_notifications,game_feedback,town_push}.py`; CivicNotification API, bot тесты |
| Музыка | Один HTMLAudioElement, 7 спокойных mp3, battle.mp3 при начавшемся набеге; mute/volume/разблокировка жестом/visibility. Есть browser sound tests | Региональные/походные/данжевые темы и их приоритеты ещё проектировать, если они требуются пакетом. Не создавать второй одновременно играющий плеер | `web/src/game/backgroundMusic.ts`, `web/src/ui/BackgroundMusic.tsx`, `gamePreferences.ts`, `web/e2e/sound.spec.ts` |

## Свежие изменения и устаревшие описания

Сравнение «после подготовки внешнего пакета» **заблокировано неизвестной датой
и содержимым пакета**. Ниже достоверная хронология репозитория, а не выдуманный
diff с ним:

1. 18.10, 0.7.12: уже перенесены заточка/снаряжение и музыка; затем 0.7.13 —
   очередь команд/тик данжей и мобильные иконки; 0.7.14 — отмена укреплений
   и видимость их работ. Сведения о выпуске — соответствующие RELEASE-документы.
2. 18–19.10, 0.7.15/16: отчёты из игры, карта на снимке WebKit, отмена пустых
   руин-чертежей, сохранность снаряжения в бою и защита от открывающего клика.
   Доставка отчёта теперь в память/общую сводку, прежние описания ZIP/ЛС
   относятся к истории. Известные 400/413 исправлены; исходный частный сбой
   report144 не объявлен полностью воспроизведённым/закрытым.
3. 20.10, D-102: личный огород заменён моделью общей фермы. Общие земля/семена,
   допуск от владельца/заместителя, 90/10 Food по полезному вкладу; фонд WorkPay
   на урожай не увеличивается. Это документы, НЕ реализация.
4. 20.10, upstream `b617f8d`: новые семейства топоров/кирок, прочность,
   перенос/хранение/новая миграция. `SpeedMultiplier` 1.0/1.25/1.5/1.75
   **умножает количество ресурса в CycleEngine**, а не сокращает время цикла.
   Совместимость с нашей медленной экономикой, cost basis, рынком и заточкой
   ещё проверить; автоматического merge/cherry-pick не выполнялось.
5. В текущем bot ref есть независимые изменения новостей от 20.10. Game payload
   из квитанции 19.10 — не полный образ текущего бота. Будущая синхронизация
   не должна возвращать старые newsroom/бюджеты/настройки.

`CURRENT_MASTER_PLAN.md` всё ещё содержит старый baseline 0.5.1 и пакет 0.5.2,
`TODO.md` — старую запись о 0.7.11/12. Они сохраняются как история под новым
указателем. Для очереди 1.0 читать [UNIFIED_PLAN_1_0.md](UNIFIED_PLAN_1_0.md),
для статуса этой сверки — [PLANNING_PROGRESS_1_0.md](PLANNING_PROGRESS_1_0.md).

## Как воспроизвести сверку без запуска игры

- `git rev-parse HEAD`, `git merge-base <game-sha> vadim/main`;
  `git log <game-sha>..vadim-next/feat/tiered-tools`;
  `git diff <game-sha>...vadim-next/feat/tiered-tools`.
- Читать реальные символы из таблицы, отдельно от описаний и квитанций.
- После получения пакета записать его состав/дату/контрольные суммы и связать
  каждый пункт с ID плана. Пока пакет не прочитан, не ставить `package_reconciled`.
- Не запускать игровые entrypoint бота, deployment workflow или миграции для
  проверки Markdown. Последующие игровые проверки перечислены как будущие.

## Итог локальной проверки документации

На этом этапе: четыре новых документа, 16 локальных Markdown-ссылок и 23 ID
задач проверены; арифметика сценариев сходится. `progress.json` разбирается,
старые поля прогресса/релизов сохранены побайтно по смыслу JSON, текущие
ограничения AGENTS/CLAUDE совпадают. `git diff --check` прошёл. В изменениях
только документация; TG-bots чист. Дополнительно сравнение игровых деревьев
`4bb125f` и `b9b4b32` по src/web/tests не показывает различий: нынешний main
добавляет документы, это не новый проверенный runtime. Workspace с обоими
репозиториями и этим планом подтверждён через `code --status`.

Игровые тесты и симуляция не запускались; production не проверялся. Все эти
успешные проверки относятся к документации, не к готовности самой 1.0.
