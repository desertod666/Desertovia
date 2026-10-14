> D-032: [план боя и командира нападений](BATTLE_PLAN.md) — разведка, доступные
> направления, настоящая локальная оборона, отряды и будущий режиссёр. PLAN,
> не реализовано и не выложено; существующие нападения/данжи сохраняются.

> Выпущен только первый этап входа/хостинга: [TELEGRAM_LAUNCH.md](TELEGRAM_LAUNCH.md).
> Исторические требования ниже не означают готовый полный перенос. DZ-02 —
> auth реализован; полное согласование политики доступа остаётся отдельным этапом.

> Последнее решение D-013/D-014: ВСЕ функции автора сохраняются, новые не
> отключаем. Старые ограничения набегов/данжей/других деревень не применяются
> как причина отключить готовую функцию основы. Чат адаптируется в режимы0/¼/full.
> Актуальная стратегия — CURRENT_ADAPTATION_LIST.md и DECISIONS.md.

> Историческая техническая матрица основы492cf291. После нового обновления
> приоритет имеет [CURRENT_ADAPTATION_LIST.md](CURRENT_ADAPTATION_LIST.md):
> чат/присутствие/Governors/набеги/данжи теперь уже есть, времена изменились.
> Строки об отсутствии функций здесь не описывают свежийe23bc94.

# Матрица минимальной адаптации существующей основы

Ни одна строка не означает, что перенос уже сделан. `Сохранить` — инженерный
механизм; его конфликтующие числа/права проверяются отдельно. `Расширить` —
добавить поля/правила в существующий слой, а не создать параллельный движок.
Все имена новых классов ниже — предложения, не существующие файлы.

Слои: D = `src/Desertod.Domain`, I = `src/Desertod.Infrastructure`,
A = `src/Desertod.Api`, W = `web/src`. В планировании сверяться с DZ-ID из
RULES_AND_SOURCES.md, а в реализации — читать указанную существующую функцию.

| ID | Существующая точка основы | Что сохраняем | Минимальная адаптация под нас | Проверка завершения |
| --- | --- | --- | --- | --- |
| DZ-01 | D/Entities/Village.cs; A/Game/GameService.cs | Village aggregate, сервис/DTO/EF/SignalR | Один настроенный slug; ограничить создание новых миров и чужое управление, не удалять всю multi-village модель | Игрок не создаёт второй мир и не меняет чужой; данные/сигналы не смешиваются |
| DZ-02 | A/Auth/TelegramLoginVerifier.cs, AuthEndpoints.cs; I/Accounts/AccountService.cs | Account/ExternalLogin, cookie auth, действующий Telegram verifier | Добавить MiniApp initData или подписанный короткий bot-ticket; связь по числовому provider ID. Проверка прихода/ограничений через узкий доверенный bridge | Поддельный/старый ticket и чужой ID отвергнуты; повторный вход ведёт к одному Citizen |
| DZ-03 | D/Catalog/GameCatalog.cs, Definitions.cs; D/Entities/Citizen.cs | Каталог ролей/задач и проверку их условий | Selection/cooldown роли на Citizen; наши 8 ролей доступны по зданиям; роль задачи проверяется при старте/очереди. Старые технологии не должны создать второй запрет новичку | Смена через 7199с отклонена, через 7200с доступна без текущей работы; повтор выбора не продлевает срок |
| DZ-04 | D/Rules/CycleEngine.cs: Start/Begin; GameRules.cs; SkillRules | Начало/завершение и WorkSeconds в TaskDefinition | Каталог наших сроков/выдачи/энергии и согласованная политика бонусов. Работа+путь укладываются в утверждённый общий срок, не прибавляются дважды | Дерево/камень/стройка совпадают с контрактом; изменение каталога не меняет уже начатый цикл |
| DZ-05 | A/Game/CycleSettlementWorker.cs; GameService.SettleOneAsync/StartAsync; D CycleSettled | Фоновую обработку и единственный путь Settle | Стабильный идентификатор принятого цикла/receipt для новых наград и расходов; доменные эффекты и ledger сохраняются атомарно, доставка уведомления отдельно | Два worker/API-вызова/рестарт завершают одну работу с одной зарплатой |
| DZ-06 | D Citizen current-task fields; A StartAsync/SettleOneAsync; W/ui/NextAction.tsx | Текущее поручение и индикатор NextAction | Одна следующая команда (task/site/deposit/scope), queue revision/error. После Settle повторная валидация и запуск в том же lifecycle; без auto-renew streak-фарма | Место освобождается при старте; провал оставляет понятную паузу, без потери груза/дублирования |
| DZ-07 | D/Rules/WorkContext.cs; D/Buildings/ConstructionRules.cs; A LoadContextAsync | Расчёт занятости/стадий и ограничения проекта | Наши слоты/напарник, snapshot heartbeat; конфигурировать старый надзор инженера, не навешивать его дополнительно на наш двухролёвый барьер | Параллельно мост/забор; переполнение мест и лишняя последняя работа невозможны |
| DZ-08 | D/Storage/StorageRules.cs, StorageBuilding.cs; D Citizen.Load; I DbContext | Реальные руки/груз, вместимость и склады | Расширить владение/scope и резерв входов/места под принятые результаты; не терять обещанный груз при заполнении склада | Конкурентные задания не превышают резерв; результат в руках не превращается в новую добычу |
| DZ-09 | D/World/Forest.cs, WorldMap.cs; D/Rules/WorkSpots.cs | Геометрию, серверную проверку точки и живые деревья | Конечные участки дерева/камня/руды с запасом/updatedAt/скоростью регена; клиент видит данные, не создаёт ресурс кликом по рисунку | 216/162/36; 12/12/4ч от пустого; после рестарта/смены суток прирост не дублируется |
| DZ-10 | D TaskEffect.Deliver/Unload, ConstructionRules; D WorldMap.Walk | Доставку существующего груза и маршруты | Курьерский тип задания без смены роли, источник/получатель/резерв; дороги влияют на доставку, не увеличивают производство | Нельзя доставить несуществующее или повторить принятый груз |
| DZ-11 | D/Projects/ProjectCatalog.cs, ProjectRules.cs; Buildings/ConstructionSite.cs; A blueprint methods | Стадийную стройку и размещение | Наши зависимости и расход; bridge 60/фence 12/walls 20, параллельные задания разных объектов. Прогресс блокирует уже законченные этапы | Не открывается берег раньше моста; забор работает параллельно, стены после дерева |
| DZ-12 | D Buildings/BuildingCatalog.cs, ConstructionRules.cs; Village.Structures | Постройки и их стадии | Level/Condition и jobs подготовки/расширения/обслуживания; существующий pipeline Start/Settle. Износ от выполненного производства, не декоративного времени | 1–3, подготовка/20/40 работ, condition ≥30, ремонт не выдаёт два завершения |
| DZ-13 | D Catalog TaskDefinition Inputs/Outputs, ResourceKind.cs; CycleEngine.Start/Settle | Каталог/проверку рецептов и существующие цепочки | Добавить недостающие ресурсы и наши рецепты/требования зданий; различить Bread/Meals/Fish/Flour, не свести всё к Food | Производства потребляют ровно резерв и выдают результат однократно; изготовление видно в отчёте |
| DZ-14 | D/Rules/Energy.cs, VigorDrinks.cs; CycleEngine | Хранение энергии и TimeProvider | D-024 PLAN: энергия0 замедляет работу, не ходьбу; перегруз → обморок/восстановление. Адаптировать существующие VigorDrink/рецепт под высокий уровень и продажу; учитывать тестовое окно | Серверные состояния/очередь/reconnect не обходят отдых; нет дублирования расходов/наград |
| DZ-15 | D/Skills/SkillRules.cs, Progression.cs, StatBlock; Citizen.Skills | Структуры навыков/опыта и вычисления progression | Наши профессии сохраняют независимый опыт. Сопоставить уровни, силу/ум/харизму и вещи; временно ограничить несогласованные boosts основы | Смена роли не обнуляет опыт, интеллект не расширяет queue, бонусы не меняют текущий job |
| DZ-16 | D WorldMap, Village, Citizen.Load; D StorageRules.Take; I DbContext | Проекцию/коллизии/размещение и физический груз | KingdomBoundary + ownership/funding policy для каждого reserve/take/deliver/build. Клиентский scope лишь запрос, сервер выбирает допустимый | Игрок снаружи не тратит treasury через crafting, очередь, courier или forged coordinates |
| DZ-17 | D ConstructionRules.PlaceBlueprint/ConstructionSite; A blueprint endpoints | Проверки свободной земли и стадии строительства | Личное владение, заявки pending/approved/ready, дом/социальное жильё, уровень и возврат резерва через существующие сервисы | Чужой дом не строится/не продаётся; отклонение возвращает резерв один раз |
| DZ-18 | A GameService.GovernAsync; Village.OwnerAccountId; Auth AccountClaims | Владельца и проверяемые серверные права | Assistant assignment/proposal entities; bridge от бота с owner auth. Помощник готовит запрос, только царь исполняет решение | Бывший помощник/повторное одобрение не исполняют действие; «owner=true» из JSON бесполезен |
| DZ-19 | D/I ownership, A GameEndpoints/GameService | Один API mutation path и EF concurrency | Расширение market/stock-order: товар/owner/количество/цена/state и атомарная сделка+ledger | Два покупателя получают не больше одного лота; деньги и товар вместе либо вместе откат |
| DZ-20/21 | D ResourceKind.Gold; I DbContext; A AfterSettleAsync | Модель ресурса Gold не переименовываем в деньги | Отдельные wallet/ledger сущности ДЗК и test candles. Награда через CycleReport receipt; миграция ×10 только при импорте старого номинала, не при чистом мире | 10/20/50 по типу; отдых/переход 0; imported flag защищает повтор |
| DZ-22 | A HTTP auth/TimeProvider + новый exchange handler; I migration | Существующие auth/ошибки/транзакции | RATE/LIMIT/TZ + уникальный replay key; debit/credit/receipt атомарны; no real bot wallet | 100→1, до5/сутки; replay/параллельные запросы не обходят квоту |
| DZ-23 | D CycleReport; A AfterSettleAsync; I mappings | Доменные reports и публикацию событий | События расхода/изготовления, contribution totals, историческая cost basis known/unknown | Сводки равны журналу; откат/доставка/retry не новый доход |
| DZ-24 | A authorisation + отдельный reporting endpoint; I ledger | Авторизацию/проектные DTO | Owner report из точных данных; AI через существующий бот или ограниченный сервис, read-only, budget/cache. Tax только preview | Рекомендация не меняет БД; чужой игрок отчёт не читает; лимит модели проверяется |
| DZ-25 | A/Game/VillageHub.cs, VillageNotifier; W hooks/api/i18n | SignalR, обновления и нормальные React-компоненты | Chat entity/API/events, антифлуд, stickers; батюшка через bridge, короткие ответы с лимитом; только нужные UI controls | Участник видит сообщения без reload; replay не дублирует, права проверены, модель не выполняет команды |
| DZ-26 | D Citizen.Status/IdleSpot/current-cycle; W/game/VillageWorld.ts | PixiJS renderer, движение и внешний вид автора | Heartbeat/visibility predicate; быстрый подход визуально, работа на месте/возврат по этапам, не серверный speed exploit | online idle виден, offline working виден до конца, offline idle скрыт; domain presence не прекращает работу |
| DZ-27 | D/GameRuleException.cs; A/Game/GameExceptionHandler.cs; W i18n/TaskList/NextAction | Коды ошибок и локализацию/React state | Перевод наших кратких ошибок и stable selection/draft между SignalR snapshots, без нового дизайна | Android picker не размонтируется, action/site/deposit сохраняются, причина видна без hover |
| DZ-28 | A notifier/settlement + новый уведомительный bridge; W/notifications.ts | Отдельные события клиенту и согласие | Opt-in subscription, отправка после commit через outbox/delivery state, bot UID mapping | Job не заканчивается дважды из-за повторной доставки; физические устройства проверены отдельно |
| DZ-29 | D/Quests/QuestRules.cs, StoryQuests; D Rules/Expeditions, Weather; W signals | Существующие механизмы квестов/событий/эмоций | Настроить наши мирные события/разведку/достижения; безопасная neighbor emote, не health damage | Не появляется новый бой/пассивная выплата; событие завершено один раз |
| DZ-30 | D Projects/ConstructionRules + KingdomBoundary | Те же проекты/стройка/карта | D-024 PLAN/P1: повысить приоритет царского замка/расширений; ночной work multiplier остаётся отдельным прежним планом | Не сбрасывать мир, параметры/расположение выбираются перед реализацией |
| DZ-31/33 | D entities/quests + future combat/recovery modules | Слои Domain/Api/EF, не отдельный игровой сервер | ПОЗЖЕ: текстовый бой/лечение; дуэльный срок через ID и подписанное событие, не общий перенос bot DB | Не вводить в мирный этап; запланированные награды без дубля/сброса роли |
| DZ-32 | D technologies/projects/catalog | Существующие технологии и уровни | ПОЗЖЕ: регионы/типы материалов/станки, связать с царскими общими целями | Не включать все 7 тиров из предложения как принятый баланс |
| DZ-34/35 | Все несогласованные feature entry points | Код расширяемости и графику автора | Сценарии кланов/атак скрыты/недоступны в нашей конфигурации; без нового рисования | Endpoint тоже запрещает сценарий, не только отсутствует кнопка |

## Карта данных при переносе: проект, не готовые EF-сущности

| Старое | Существующая основа | Минимальное расширение / замечание |
| --- | --- | --- |
| town_world/generation | Village.Id/Slug | Версия импорта + legacy world ID; один настроенный мир |
| town_players.uid | Account / ExternalLogin / Citizen.AccountId | Telegram provider ID → Account GUID → Citizen GUID, unique provider key |
| town_jobs | Citizen.CurrentTaskId/Start/End/CycleSettled | Принятый receipt/фиксированные outputs, owner/scope, policy version |
| town_queue | Нет настоящей очереди; NextAction только UI | Один next-command с revision/validation error |
| town_stock(scope) | Village.Storages / Citizen.Load | Собственность и внешний stock; груз/склад разные места одного запаса |
| town_skills | Citizen.Skills / Stats | Явная таблица соответствия ролей/уровней/эффектов, не silent conversion |
| town_sites/development | Village.Sites/Structures/Projects | Наши levels/condition/prepared, стабильный mapping проектов |
| town_properties/housing | Sites/Structures, частично shelter | Владелец/заявка/финансирование/уровень/социальная заявка |
| town_looks/walkers | Appearance / IdleSpot / WorldMap | Mapping внешности/координат по текущей карте, не сырые пиксели |
| town_market | Нет торгового ledger этой модели | Лоты/ownership/atomic execution |
| town_profiles.gold/treasury | ResourceKind.Gold НЕ кошелёк | Wallet accounts/treasury/ledger; деньги целыми единицами |
| candle_wallet/exchange/income/basis | Нет | Раздельные экономические сущности; не основной аккаунт батюшки |
| assistants/proposals/chat/events | OwnerAccountId/notifier/quests | Расширения существующего EF и API, bot bridge; без личных данных в git |
| sessions/login/push | Auth cookies/external provider, notifications | Старые session secrets НЕ импортируются как credentials; новые сессии/согласие |

Существующие owned JSON структуры и rowversion сохраняются там, где достаточны.
Кошелёк/ledger/receipt с уникальными ключами могут требовать отдельных таблиц,
но это обычные EF migrations в том же PostgreSQL, не SQLite-сервис рядом.

## Контракты между слоями

- Клиент посылает intent: taskId/siteId/depositId/amount/queueRevision/requestId.
  Сервер определяет роль, цену, source scope, срок, награду и право одобрения.
- Domain валидирует правило и результат, не вызывает Telegram/ИИ/HTTP.
- Application/GameService связывает authorised identity, WorkContext, EF, запись
  результата и уведомления; не переносить все правила в контроллер.
- После принятия job хранит существенные условия. Новая конфигурация влияет на
  новые поручения, не пересчитывает оплаченные и уже начатые.
- SignalR переносит подтверждённое состояние, а не принимает игровой результат
  от клиента. Каждая подписка/команда проверяет мир и ограничения доступа.
- После commit outbox/notifier доставляет событие. Повторная доставка не означает
  повторную зарплату. Retry защищён unique receipt и database transaction.
- Bridge батюшки аутентифицирован сервером; username и клиентский owner-флаг
  не доказывают права. Проверка бота не превращает модель ИИ в администратора.


### D-018 — ночные здания: PREPARED, не LIVE

| Требование | Подготовлено | Интеграция | Проверка |
| --- | --- | --- | --- |
| Свет внутри и слабый наружный свет у всех новых зданий | 26 RGBA PNG, атлас и сравнение день/ночь в `art/night-buildings` | Отдельные дневной/ночной слои в существующем Pixi renderer; сначала масштаб и ground anchor каждой пары; плавный crossfade | 26 файлов, прозрачность, отсутствие видимого обрезания по границам, просмотр галереи; runtime не изменён |


### D-019 — интеграция ночных зданий и общение батюшки

| Поручение | Точка адаптации | Изменение | Проверка |
| --- | --- | --- | --- |
| 26 пар день/ночь без скачка | TileSet, VillageWorld, BuildingLighting | Совместная загрузка, регистрация по видимым границам, единый premultiplied shader, сохранение текущего уровня при rebuild | Браузер: неизменные bounds/позиция и непрозрачные стены для всех26 |
| Батюшка в общем чате | ChatService, SramoslavService, ChaplainChatWorker | AI ответы на обращение и редкие ambient; отдельная таблица NPC без аккаунта/роли | History/доступ, restart gap и hourly cap; клиенты не считают NPC своим сообщением |
| Обрезанный текст в Telegram | Hud phone CSS | Описание во всю ширину над кнопками | 320px, шрифт130% |
| Количество еды сбрасывается | StorageCard.TakeRow | Строковый draft без принудительной подстановки; проверка перед Take | Пустой ввод,2, обновление склада, недопустимое число |


### D-020 — стройплощадки, пилот хижины выпущен

| Объём | Точки адаптации | Проверки при реализации |
| --- | --- | --- |
| Хижина: стадии, леса, лестница, материалы, ночь | VillageWorld.siteView/drawBuildings, BuildingLighting, существующие Site/Catalog | Совмещение,94%→готово, доставка без progress, мировой свет/lifecycle, телефон/ПК |
| Остальные здания и проекты после хижины | Отдельные типы и setVillage/PROJECT_SITES, прежний fallback | Не потерять открытые конструкции, мост/пирс/ограды/памятник и их этапы |

Полная спецификация: [CONSTRUCTION_VISUAL_PLAN.md](CONSTRUCTION_VISUAL_PLAN.md). Пилот Hut и общие исправления выпущены; изображения остальных типов пока PLAN.


### D-021 — чат0/¼/full, выпущено0.5

| Требование | Адаптация | Проверка |
| --- | --- | --- |
| Режимы и удобные кнопки | ChatPanel, HUD CSS, GamePage obscured | Телефон/ПК, реальные границы, draft/preference/scroll |
| Уведомления и непрочитанные | useLiveVillage chatArrivals, ChatPanel bounded toasts | History/own исключены, live/burst/expiry, jump |
| Клавиатура и сохранение функций | VisualViewport, fullscreen inert/Tab/Escape, события/членство/SignalR без смены API | Resize, отправка/ошибки, прежний browser suite |

Счётчик: жители/citizen presence,5мин grace; не NPC/не видимые working offline.
Версия: web/package.json0.5.0→SettingsPanel0.5, источник единственный.
Выпуск и проверки D-021: [CHAT_MODES.md](CHAT_MODES.md).

D-022 /0.5.1: компактная ChatPanel.launch, население GameMenu.village,
interrupted для всех окон + modal-backdrop stacking. Регрессии размера,
голосования, unread и настоящего GameMenu проверяются в chat-modes.spec.ts.

D-022 RELEASED/VERIFIED14.10.2026:0.5.1, SHA ad88be344908a7fb42bc434ce89aad94c92d1665,
CI38571676265 (474 backend/55 browser passed/1 skipped), deploy38573019933,
check38573303603; публичные release header и bundle проверены.

## D-024: последняя сверка владельца (PLAN/BUG REPORT, не реализация)

Подписи/карточки/кириллица: VillageWorld/CSS/React panels, A-25/27, P1.
Мост: author Tsar river crossing и отдельный RopeBridge/ravine, A-26, P0,
жалоба требует phone/PC воспроизведения; вертикальная ориентация не доказывает
невозможность ПК/телефона. Castle отсутствует, A-28, P1, существующие projects/sites.
DZ-31 отдельно повышен до PLAN/P2: дуэльная больница через ID/узкий статус,
существующие patients/heal_tiers/payment у бота. Настоящие свечи остаются у бота;
срок24 реальных часа не превращать в12 игровых часов обморока. DZ-33/прочие
боевые идеи не меняются. Подробнее NEXT_TODO.md.

D-025 /DZ-31/A-29 current focus: PLAN + hospital ART PROPOSAL. HOSPITAL_PLAN.md
описывает staged external status, pause/recovery, existing bot treatment/deep link,
durable idempotency перед embedded heal payment, common hospital construction.
Native Injury/Remedy/CombatRules сохраняются; external duel срок отдельно.
Графика art/hospital-proposal не интегрирована. Никакого deployment/списаний.

D-026 /DZ-31 PLAN: hospital-level + patient Recovery skill time bonus, bounded,
snapshot once per admission and shared authoritative bot expiry. Recommendation
2/4/6% + skill up to5%, cap10%, not approved balance. No code/payment change.
