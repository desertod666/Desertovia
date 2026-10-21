> T054: отдельная делегация/AutoEconomy, обеспеченные работа/закупки,
> ограниченный выпуск и будущая дань через существующие обработчики.
> 726Domain/39первых API/13mockedBot PASS; расширенные проверки и новый CI нужны.
> Миграция57→58 сохранила старые поля/строки27сравниваемых таблиц; новые
> полномочия пусты. DIRECTOR_FINANCE_1_0.md / VERIFICATION: scoped proof only.
> Полные T026/A11/T054/T055 и вся1.0 остаются активными; D109/D110 сохраняются.
> Публичной выкладки/реальных платежей/включения ИИ и Telegram нет.

> Продолжение T025/T026: объявленный версионированный фонд выпуска в казну,
> общий суточный предел между версиями и подкреплённая квитанцией эмиссия.
> 721Domain/14разных scoped API-boundary-migration сценариев PASS по этапам;
> 56→57 сохранила24сравниваемые таблицы. HTTP issue/pause/restart/replay PASS
> только в собственном QA-мире; новая политика по умолчанию отсутствует.
> Реальная цепочка public/private goods/tax сходится300=136+58+106 на synthetic
> условиях. Это не выбранный баланс или весь A11/A37/T026/T054.
> Точный b27f128 прошёл CI39589892714:721Domain/466API/418browser+2skip;
> frontend/Docker PASS. Следующий функциональный шаг — автономия T054.
> См. TREASURY_FUNDING_1_0.md / VERIFICATION и FUNDED_ECONOMIC_TRACE_1_0.json.
> D109/D110 сохраняются; публичного выпуска/реальных платежей нет.

> Продолжение T025: сверка каждого кошелька/казны/фонда с журналом внутри
> прежней транзакции, явные transfer/legacy issuance/withdrawal и данные для
> Всевышнего.709Domain PASS; локальная55→56 сохранила23сравниваемые таблицы.
> Реальные HTTP/restart повторы оставили4квитанции/7проводок без двойной оплаты.
> 42разных API/boundary/миграционных сценария PASS по этапам; последние9
> проверяют текущую аналитику разрывов. Новый полный CI ещё требуется;
> весь T025/T026/T054 не завершён.
> См. MONETARY_AUDIT_1_0.md и MONETARY_AUDIT_VERIFICATION_1_0.json.
> Родитель923bec6 прошёл CI39579662471:696Domain/454API/418browser+2skip,
> frontend/Docker PASS. Это родительские проверки. D109/D110 сохраняются.

> **D-104:** реализация начата. M0 локально проверен; PKG-T010 добавляет
> совместимые валютные идентификаторы/отображение золота.
> [Точные проверки и ограничения](IMPLEMENTATION_PROGRESS_1_0.json).
> Исторический статус документального D-103 ниже сохранён.

## D-103 · Сводная матрица всей 1.0 — 20.10.2026, документация сверена / runtime PLAN

Полная текущая матрица «область → уже есть/частично/нет → реальные символы →
проверки» находится в [CODE_AUDIT_1_0.md](CODE_AUDIT_1_0.md).
План изменения каждого слоя и приёмка — [UNIFIED_PLAN_1_0.md](UNIFIED_PLAN_1_0.md).

| Прежние области | Новые ID | Состояние этой сверки |
| --- | --- | --- |
| Источники/история | DV1-00/05 | Пакет 23 файла сверён; 53 карточки в едином DAG; новый upstream tools не перенесён |
| DZ-05–08/13–24, питание/права/собственность | DV1-01–04/13 | Существующие lifecycle/economy/storage/food; общая ферма PLAN |
| DZ-11/12/17/18, стройка/помощники | DV1-03/08 | Существующие stages/ruins/repair; полный recovery path требует проверки |
| Боевые системы/данжи, сохранённые D-013 | DV1-06/07 | SOURCE; новые сценарии и гарантии restart отдельно |
| DZ-09/16/29/30/32/33, мир/походы | DV1-09/10 | Окрестности/exploration/expedition есть; региональный слой PLAN |
| Всевышний/экономика/события | DV1-11 | SOURCE Off/Observe/Propose; самостоятельное исполнение PLAN |
| DZ-25/27/28/35, UI/общение/уведомления | DV1-12/15/16 | Существующие React/Pixi/chat/Notification/music; новые связи PLAN |
| Внешние платные подписки | DV1-14 | Bot membership есть; игровая модель/bridge требуют спецификации |
| Сквозная готовность | DV1-17 | Не проверена/не выпущена; игровой код и сервер не меняются |

Старые BASE/LIVE/BRANCH/PLAN ниже относятся к своим датам/движкам. Не копировать
их в текущую 1.0 без аудита. Исторические строки сохранены.

---

## D-102 · Ручное выращивание еды — PLAN / обсуждение, 20.10.2026

| Область | Основа | Предлагаемое расширение | Будущая проверка |
| --- | --- | --- | --- |
| DZ-05/06/13/14/16 | Domain Farming/FieldBed/FarmRules, CycleEngine, TargetPopup/FieldsCard | [Окно выбранной грядки](SHARED_FARM_PLAN.md): посев, полив, новое удобрение, сбор; точная цель plotId+bedIndex вместо первой подходящей фермы. Грядки общие;90%Food общине/10%участникам по вкладу, ограниченный допуск от владельца/заместителя; денежный фонд не растёт | Выбранная грядка на двух фермах; ручные действия, офлайн, повтор запроса/сбор, права назначенных фермеров, общий запас/минимальный паёк, совместный уход, дробные доли без двойного сбора, сохранение посадок |

#0.7.15 УСТАНОВЛЕНА И НЕЗАВИСИМО ПРОВЕРЕНА

Проверка завершена 2026-10-18T21:56:55Z. Gameexact`52f2f6ccb39d276be9a260f027198eb02f165e09`,
CI39168526454 SUCCESS533Domain/287API/226browser+2штатныхskip/4Nodeunit/
lint/build/Docker. Botexact`7963416ae406197089320e3e0d370fb71599dcde`, CI39168529605 SUCCESS1096,
serverSKIPPED. SourcePR69/93 merged, деревья идентичны testedsource.
Actualpreflight39169669850,install39169732307 и отдельныйcheck39170032365 SUCCESS.
44operatorguards и дополнительный desktop/WebKit capture/CancelcheckPASS.
141→141профилей,0потерьпрогресса;38миграций. Аккаунты/привязки/навыки/
характеристики/слава/номеразданий сохранены; полные snapshots/backups вещей
и игрового состояния сохранены. 121otherbotmodules/657othercodeassets и конфиги
сохранены, Propose/бюджеты/карантин не менялись. PGdump/restorecatalog,
SQLiteonlinebackup/integrity/code/config/profile backups созданы.

В игре маленькая «!» поверх карты/любого меню и Dungeon: ошибка/жалоба/
предложение, снимок до открытия формы, персонаж/версия/экран/диагностика.
Отмена/X/Escape очищают память: до Send нет сохранения на сервере/в боте/
браузерном хранилище/файле. Снимок можно убрать. Сервер принимает только
аутентифицированную личность; отдельный outbox, дедупликация,5обращений/час,
лимит вложений256MiB и7дней для доставленных изображений. Батюшка сохраняет
предложку и передаёт владельцу один ZIP. Только явно отправленное — отчёт.
BotruntimeONLY almighty.py,feedback.py,feedback_context.py,newgame_feedback.py.
Никаких синтетических liveсообщений/отчётов, волн, modelprobes, платежей/сбросов.
Отправка покрыта тестами с mockTelegram; настоящую жалобу не имитировали.
Publichealth/login/exactSHA/frontendcontrols/outboxschema/bridge/versionregistry
подтверждены. post0715request=check; НЕ повторятьinstall. История ниже.

# Safari capture follow-up — FINAL CHECKS PENDING, NOT INSTALLED

Visual review found WebKit restarting cloned menu entrance animations, blending
HUD through the screenshot. Capture now disables animations/transitions only in
the detached clone; live UI stays unchanged. Regression checks actual JPEG menu
pixels, not just file existence. Fixture waits for initial account load before
API login/navigation. Prior f0027ff CI39166485172 passed533/287/226+2skip/4unit/
lint/build/Docker, bot6f9e71e CI39166484124 passed1096. Final game source must
pass again after this image fix; no install request or runtime change. History below.

# In-game reports — IN PROGRESS, NOT INSTALLED18Oct2026

Owner: small non-text «!» above map/menus, bug/complaint/suggestion with current
screen and account/version context. Explicit clarification: Cancel/close discards
all draft data; no server/bot/file/browser storage before Send. Capture before
opening the form, preview/remove image, fallback without image; game and Dungeon.
Authenticated server identity, bounded allowlisted context/JPEG, durable PG outbox,
idempotent receipt and owner-only bot ZIP including JSON/text/optional image.
No model calls, gameplay mutations or synthetic live messages. Source/tests only;
no new deployment authorization. Preserve current0.7.14 and parallel bot news work.
See docs/dezertovia/IN_GAME_REPORTS.md. Historical records below.

#0.7.14 УСТАНОВЛЕНА И НЕЗАВИСИМО ПРОВЕРЕНА — 18 октября 2026

Проверено 16:24:08UTC/18:24:08Осло.
Gameexact`a616b6485b9ea469f2272d322c4c9753c90c939a`, CI39143233311SUCCESS:
533Domain/280API/216browser+2штатныхskip/4Nodeunit/lint/build/DockerPASS.
BotONLYfeedback_context exact`6a51c2d6453bba9556d51f5b13525e6d0378ad39`,
CI39143279668SUCCESS1053tests/serverSKIPPED.40operatorguardsPASS.
Actualpreflight39144261041, install39144320651, отдельныйcheck39144753586SUCCESS.
141→141профилей,0потерьпрогресса;37миграций,
аккаунты/привязки/навыки/характеристики и номера зданий сохранены.
122otherbotmodules/658othercodeassets/configs preserved; Propose/бюджеты/
карантин сохранены. ActualPGdump restorecatalog,SQLiteintegrity/code/config/profile
backups созданы. Public/local/gatewayexactSHA, frontend0.7.14 и новые controls
подтверждены. Telegram-вход работает; Production/DevToolsdisabled.

Исправлены отмена чертежа среди строек с вложенной работой и скрытые задания
укреплений. В Стройке «Забор и ворота»: уровни, технологии, прогресс, ремонт.
Начатая работа разбирается с подтверждением и прежним25%возвратом материалов.
Новый движок/баланс/схема не менялись; liveигровые действия не имитировались.
Перед выпуском зависший gateway восстановлен рестартом после SQLite/config/code
snapshot, без изменения кода/версии/конфигов. Точная причина зависания не доказана.
Это не считать исправлением зависаний ботав данном gamepatch.
Не повторятьinstall; текущийpost0714request=check. История подготовки ниже.

#0.7.14 installation authorized — 18 October 2026, NOT INSTALLED

Owner: «Установи». Fortification fix PR67 source9b3fa65 exactCI39140681611
SUCCESS533Domain/280API/216browser+2normal skips, lint/build/Docker/4Nodeunit.
Stamp0.7.14 and patch notes; final exactCI required after stamp. Freshpost0714
operator/candidate, report→backup/deploy→separatecheck; baselinea4bc0ec/0.7.13.
Only feedback_context.py registry may change in bot; preserve current other
modules/assets/configs, Propose/budgets/quarantine and player progress. Do not
replay older requests. Wait for natural Dungeon completion before restart.
No synthetic player messages, model probes, waves, payments or state resets.
See docs/dezertovia/RELEASE_0_7_14.md. Historical records below.

# Fortification cancellation — source fix, NOT INSTALLED, 18 October 2026

Owner reports an uncancellable reinforcement blueprint and unclear fence upgrades.
Read-only live logs confirm EF nested JSON ordinal failure on the second save;
untouched fortification site is between worked sites. Both cancellation routes
now save, detach/reload within the same transaction, then build state. Resident
SiteCard uses actual server stages: the empty static FortificationUpgrade catalog
previously hid all jobs. Construction has Fence and gates guidance/progress;
inline cancellation errors and existing paid dismantling/salvage remain distinct.
Regression: old source fails both new API cases; fixed fortification API 5 PASS.
Local 533Domain/280API PASS; existing fence scenarios 8 PASS, final new UI
checks 4/4 PASS on desktop/Pixel7 after controlled-response test fix. TS/build/
lint PASS; screenshots inspected. Full GitHub CI pending. Live stays exact 0.7.13/a4bc0ec;
no deployment, reset, synthetic message/wave, balance or schema change.
See docs/dezertovia/FORTIFICATION_CANCELLATION.md. Historical release records below.

#0.7.13 установлена и независимо проверена — 18 октября 2026

Проверено в13:56UTC/15:56Осло. Gameexact`a4bc0ec46170b5b3c94356d2c7b2c03f90e7eab3`,
CI39131566902 SUCCESS:533Domain/278API/212browser+2штатныхskip/4Nodeunit,
lint/build/Docker PASS. BotregistryONLY`b400c06`/CI39131646323 SUCCESS1007tests.
Preflight39133399274,install39133438404,отдельныйcheck39133722743 SUCCESS.
139→139профилей,142аккаунта/привязки сохранены;0потерьпрогресса,37миграций,
номеразданийбезошибок.120другихмодулей/656файловбота и конфиги сохранены.
PGdumpс проверкой каталога,SQLiteintegrityok,code/config/profile backups созданы.
Frontend0.7.13,24chunks,exactSHA публичного/локального/gateway совпадают;
Telegram-вход работает,Propose/бюджеты/карантин сохранены.

Исправлены очередь команд и задержки серверного тика подземелий, а также
перекрытие чисел значками ресурсов на Android. Правила боя/баланс/схема прежние.
CI и эмуляция Pixel7 пройдены; задержка в живом подземелье и физический
телефон после установки ещё не измерены. Не обещать подтверждённый FPS/RTT.
Первая попытка остановилась из-за заполненного диска; удалён только неиспользуемый
сборочный кэш и брошенный сборочный контейнер. Базы/резервные копии сохранены.
Параллельный newsmedia-релиз проверен по квитанции и защищён от перезаписи.

Исторические записи подготовки ниже не отражают текущий статус.

> Android HUD icons SOURCE ONLY: ResourceIcon без заточки сохраняет прямой TileIcon, чтобы мобильный CSS уменьшал24→16px. Тесты: геометрия и отсутствие перекрытия чисел при320/390/430/960px, desktop/Pixel7. Результат ANDROID_HUD_ICONS.md; server/version unchanged.

> Dungeon response fix verified_source: exact d9ad3c4, CI39129933021 SUCCESS533Domain/278API/212browser+2skip/4clientunit; PR63 merged с идентичным деревом. Не установлен. Подробнее DUNGEON_INPUT_LATENCY.md.

# Dungeon input latency — SOURCE ONLY, 18Oct2026

| Проблема | Точки | Изменение | Проверка |
| --- | --- | --- | --- |
| Каждый клик опрашивает всех жителей | DungeonHub.Act → CheckDungeonHospitalAsync → SyncHospitalAsync | actorOnly=true при force=true | 4API: обычный/царь, новое ранение/отказ моста |
| Старые клики копятся в SignalR | useDungeonRun → DungeonOrders | один in-flight, одна последняя команда; очистка после disconnect/unmount | 4Node unit: burst/rejection/disconnect/dispose |
| Переходы и выдача наград останавливают все тики | DungeonWorker | отдельная phase loop, один асинхронный settlement на run; retry после неуспеха | 2API: заблокированная деревня и движение; отказ final transaction |

Бой/награды/ранения сохраняют серверные правила. Полный CI pending, LIVE0.7.12.

# D-101:0.7.12 УСТАНОВЛЕНА И НЕЗАВИСИМО ПРОВЕРЕНА18Oct2026

Проверка подтверждена18Oct09:49UTC (11:49Europe/Oslo). Результат установлен,
а не толькоREADY/scheduled. Исторические NOTINSTALLED/HOLD ниже не текущий статус.
Gameexact56a62381eb33b35e401e1766c2f4525d2709b361; botpayloadexactd0f82a617a3a76830862942d3decd4708cfc8b9d,
ТОЛЬКОfeedback_context.py. GameCI39114746891 SUCCESS533Domain/272API/
212browser+2штатныхskip,lint/build/DockerPASS. BotCI39114759689 SUCCESS968tests,
serverSKIPPED;37operator guardsPASS. Upstream e30fc98 принят с исправлениями.
Actualpreflight39116333730, install39116370847 и independentcheck39116734143 SUCCESS.
138→138профилей,0потерь навыков/характеристик/славы;141аккаунт/141привязка
сохранены.37миграций, оба реестра номеров без ошибок, frontend0.7.12/24chunks.
Фактические PGdump/restorecatalog,SQLite integrityok,code/config/profile backups
приватны.115otheractualbotfiles/configs сохранены, включая параллельные picker/
login/Игрища/persona/reader; Propose/AIbudget/quarantine сохранены.
Выпущены заточка+3,3этажаDungeon,музыка,resume,видимостьofflineбойца,
голодныйXPпо старту,контраст/квесты/пути/обводки/перерисовка. Заточка вобщественной
кузнице; личныйторговыйинвентарь пока не хранит+N. Ночные сценариидиректора—PLAN.
Плавность естественногонабега после обновления не измерялась; не обещать FPS.
Текущийpost0712request=check; не переустанавливать и не повторять старые requests.
Новые изменения — отдельный проверенный source/preflight/backups/check; версия
далее0.7.13…0.7.99, без автоматического0.8/1.0. Сообщений/пробных волн/платежей/
сбросов/форсированныхмодельныхпроб не запускалось.

# V-01–V-06 в исходниках, не установлены

17.10.2026: тёмный текст складов, голодный опыт по старту цикла, пропуск перерисовки
неизменного мира, бесконечный камыш, вклады квестов в публикуемом состоянии
и путь без среза воды. Версия и сервер не менялись. LIVE остаётся 0.7.11.

# D-100: редактор набегов с ползунками установлен и независимо проверен

Версия остаётся0.7.11; это UIhotfix D-099 по команде владельца «Грузи».
Проверено2026-10-17T14:21:38.6374261Z. Прежние NOTREADY/NOTINSTALLED ниже — история.
Game exact55094d52755eb074e8074e41374a9ec5fd3a96ed; bot exact092390d83cf9c1e6a110633f5fb02b340742688f.
Exact gameCI39038955924 иbotCI39039077720 attempt1 SUCCESS; game516Domain/
270API/199browser+1штатныйskip, frontend/lint/DockerPASS;
bot903tests/serverSKIPPED;31operator guardsPASS. Mergeдеревья равны testedsource.
Actualpreflight39040135022, install39040182761 и отдельный
independentcheck39040394389 SUCCESS. Новыйraidui0711flow; старые
post0711andolderrequests не повторялись. Currentraidui0711requestactioncheck.

120→120профилей,0потерь навыков/характеристик/славы;
122аккаунтов/122привязок сохранены.
34миграции без изменения схемы. Глобальные номера/семейныйреестр безошибок.
Publicfrontend0.7.11/24JSchunks, новыеsliderJSmarkers
и liveSHAlookupбатюшки подтверждены. FreshPGdump restorecatalog+SQLite/code/
config/profile/identity/combat backups сохранены приватно. Инвентари/профили не
сбрасывались; обычная игра продолжалась. Толькоfeedback_context.py runtimeбота
заменён: mappingновогоSHA;16othermodules/AIbudget/quarantine/config/Propose
сохранены. Серверныйsrc tree равен прежнему; баланс/формулы/DBschema прежние.

Установлены: Босс/Смешанный/Орда, быстрый выбор сложности, свёрнутые advanced;
все числовые поля и доли видов — nativeползунки с единицами/пояснениями ×;
общая сумма100%, Поровну, точные ошибки до preview/save/launch; прежниеmanual
presets/idempotency/ownerlimits сохранены. Все5деревенскихвидов доступнывладельцу,
DungeonOnlyвраги исключены. Новые боевые пробы/модели/сообщения/платежи не запускали.
Не делать performanceзаявлений без естественного боя/замеров телефона.

Не переустанавливать и не подставлятьmain/mergeSHA вместо exacttestedsource.
Новый выпуск требует отдельного свежегоcandidate/preflight/backups/check.
Нельзя автоматически откатывать двигатель через activeRaids/newJSON.

> D-099: source-only редактор набегов — [план/причина/проверка](RAID_EDITOR_USABILITY.md). LIVE0.7.11 прежний.

# 0.7.11 установлена и независимо проверена — 17 октября 2026

D-098: владелец явно поручил «Установи». Проверка завершена в13:25:32UTC
(15:25:32Europe/Oslo). Ранее записанные NOTINSTALLED/дата не выбрана — история.

- Game exact `ba7050f8d0d2efc07d7523d300944e9531c3fb01`, CI39031620418 attempt1
  SUCCESS:516Domain,270API,193browser PASS/1штатныйphonekeyboard skip,
  lint/TypeScript/frontend/Docker PASS. PublicPR54 mergedcc917669, дерево совпало.
- Bot exact `2d5a7ab7759bd7d50322335bbe1772267396ae5d`, CI39031678709 attempt1
  SUCCESS903tests/serverSKIPPED. CanonicalPR52 merged645bc0f, дерево совпало.
-29защитных тестов оператора PASS. Свежий preflight39036214937,
  установка39036254787 и отдельная независимая проверка39036486712 SUCCESS.
-120→120профилей,0потерь прогресса;122аккаунта и122привязки входа сохранены.
  Проверены навыки/характеристики/слава. Старые глобальные номера и счётчик
  корректны; новая нумерация по семействам без пропусков/дублей/пустых значений.
-34миграции применены. Публичный frontend0.7.11/24JSchunks и определение версии
  батюшкой по фактическому liveSHA подтверждены; feedback_origins создана.
- Свежие PostgreSQLdump с проверкой restorecatalog, SQLite/code/config/profile/
  identity/combat backups сохранены приватно. Данные/инвентари не сбрасывались;
  обычные траты/работа во время живой игры не означают неизменность инвентаря.
- Runtimeбота заменены только app/db/helper/feedback/civic_notifications/
  feedback_context.py. app сохраняет reviewedbudgetfix, добавлены4строки захвата
  предложений;11остальных защищённых модулей, настройки, AIbudget/quarantine и
  Propose сохранены. Дозоров/привязки к костру/штрафа потерь рюкзака не добавляли.
- Принудительные волны, модельные пробы, сообщения и платежи не запускались.
  Во время обновления не было активного боя/естественного замера: плавность
  телефона в настоящем набеге требует наблюдения игроков, не объявлена доказанной.

Установлены: физическое спасение и ускорение лечения после доставки; редактор
БОСС/смешанный/орда с настройками и preview; адаптация и фактические итоги боя;
номера зданий по типу; расход еды/вместимость; оплачиваемое улучшение укреплений
дерево→брёвна→камень с пиксельной обводкой и дневной/ночной графикой; боевой XP,
вложение очков исследований и два блюда; сохранение явных предложений батюшке.

Не переустанавливать и не повторять старые requests. Текущий post0711request
имеет actioncheck. Новый runtime уже использует новые JSONполя; автоматический
откат старого двигателя запрещён, особенно при незавершённых RaidBattles.
Полная фактическая квитанция хранится в приватном ops release record.

> **D-091/D-092 выпущены0.7.9:** [RELEASE_0_7_9.md](RELEASE_0_7_9.md), independentcheck39019429338 PASS. Исторические строки «в ветке/не установлено» ниже описывают подготовку.

## D-091 · новые набеги: реализация в ветке, не установлено

| Правило | Точка входа → выход | Проверка |
| --- | --- | --- |
| Разорение имущества, все виды | MonsterBrain → RaidSimulation; общий каталог Dungeon | приоритет, отвлечение, разные цели, DungeonOnly сохранён |
| Толпа/обход/пролом | RaidNavigation A* + MonsterCrowd ORCA → проверенная геометрия |16/32/48, границы, детерминизм, долгий бой |
| Орда/элита/босс | RaidBalance → AlmightyPolicy/PrepareBattleAsync | одинаковый бюджет,1–3 босса, fresh approval |
| Предупреждение/JSON | RaidUnit nullable → EF migration/DTO/Pixi | старый jsonb, HP/стрелы/урон сохранены, неподвижная зона удара |
| Штраф смерти | отдельный дизайн, код не включён | проценты/возврат/receipt ещё не утверждены |

Подробности: [MONSTER_BEHAVIOR_INTEGRATION.md](MONSTER_BEHAVIOR_INTEGRATION.md).

> **LIVE0.7.8, D-089:** [исправление тупика директора установлено и проверено](RELEASE_0_7_8.md).
> install39012337683/check39012556053 SUCCESS;119профилей/121привязка,0потерь.
> Предыдущие READY/source-only записи ниже исторические.

> **D-087 · LIVE0.7.7 установлен и независимо проверен17.10.2026.**
> [Результат](RELEASE_0_7_7.md): install39006543221/check39006763680 SUCCESS,
>119профилей/121привязка сохранены,0потерь. Все прежние READY ниже исторические.

> **LIVE0.7.4:** [фактическая выкладка и проверка](RELEASE_0_7_4.md).
> Exact74adbe8, independentcheck38959583714SUCCESS.119профилей/121привязка сохранены.
> Дозоры/посты/привязка к костру убраны; диагностика включена. Замеры физического
> телефона и разбор оставшихся задержек — по следующему обычному бою. Старые holds ниже — история.

> **D-084 /0.7.4 NOT LIVE:** убраны дозоры/посты, добавлены свободные приказы и
> ограниченная диагностика боя. [Реализация/приёмка](FREE_COMBAT_AND_DIAGNOSTICS.md).
> Текущий установленный runtime остаётся0.7.3; новый rollout ещё не запускали.

> **LIVE0.7.3:** [установленные изменения и фактическая проверка](RELEASE_0_7_3.md).
> Exact672d72e, independentcheck38948699510SUCCESS.119профилей и121привязка сохранены.
> Пакет D081 выполнен и установлен. Предыдущие PLAN/NOTLIVE ниже исторические.
> Текущий HUD: сердце/молния, числа по нажатию; Чат/Отступить/Лечение одним рядом44px.
> Старый замок не перемещали; новый налоговый директор и измерение FPS остаются отдельно.

> **D-081:** owner correction code is implemented in `feat/owner-combat-and-siege`,
> candidate 0.7.3 under final validation; NOT LIVE. See [implementation and checks](OWNER_CORRECTIONS_IMPLEMENTATION.md).
> Earlier PLAN/release entries below are history, not proof of current readiness.

> **D-077 /LIVE0.7.2:** [фактический выпуск и проверки](RELEASE_0_7_2.md).
> PR46+botPR40 установлены, independent checksPASS; прежние holds ниже исторические.

> **D-073 /0.7.1 INSTALLED:** [фактический выпуск](RELEASE_0_7_1.md).
> PR44+45 и парный botPR39 установлены. CI и отдельный preservation check PASS.
> Заявки посадников снова в Telegram, отзывы с проверенным кодом автора.
> Все прежние repository-only/ready записи ниже — история подготовки.

> **D-072:** read-only идентификация автора отзывов, новая ветка, не LIVE.
> [Контракт, приватность, проверки](FEEDBACK_GAME_IDENTITY.md).

> **D-071 ·0.7.1 VERIFIED, repository only:** [CI и состав](RELEASE_0_7_1.md).
> LIVE0.7/0540a2a сохранён; новая установка не выполнена. Старые LIVE/pending ниже исторические.

> **D-064 · LIVE0.6.1:** [итог выпуска](RELEASE_0_6_1.md). Установка и независимая проверка успешны;111→111 профилей,0 потерь. Все прежние candidate/hold ниже исторические.

> D-041/D-042: первоначальная матрица ниже историческая. Реализованный пакет0.5.2 и пути всех18 блоков: [IMPLEMENTATION_LOG.md](IMPLEMENTATION_LOG.md). Сервер не обновлён.

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


## D-051/D-052 — дополнение15.10

| ID | Основа | Изменение | Статус |
| --- | --- | --- | --- |
| D-051 | RaidRules/RaidWorker, ChaplainChatWorker, SramoslavService, DZ-24 | Единый директор/каталог/журнал: ALMIGHTY_DIRECTOR_PLAN.md | PLAN, без автомата |
| D-052 | SettingsPanel, version.ts, modal scroll | PatchNotes, каталог RU/EN и раскрываемая история | BRANCH, без deployment |

| D-053 | EnemyLayer, VillageWorld, HospitalPanel | Появление по strikesAt, общий formatRecoveryTime с реальной шкалой | VERIFIED BRANCH,5 целевых browser PASS, build/lint PASS; без deploy |


## D-059 — исполнение новой очереди

| Источник | Содержание | Статус |
| --- | --- | --- |
| TODO_EXECUTION_GUIDE.md | Восемь задач после сверки TODO; файлы, транзакции, совместимость, критерии готовности и 6.1 SOL Высокое для всего объёма | PLAN/DOCS, runtime не менялся; без выкладки по D-058 |


## D-060 — реализация восьми пунктов

| Контракт | Существующая основа | Изменение | Состояние |
| --- | --- | --- | --- |
| Сохранность/причины | GameService/ProblemDetails | Single-world destructive guard и точные RU/EN отказы | BRANCH, локально проверено |
| Замок/территории | BuildingCatalog/ConstructionRules/Civic/WorldMap | Castle/уровни/owner источник/границы/частная стройка | BRANCH, локально проверено |
| Учёт/инвентарь/торговля | Village transaction/Storage/Citizen/InventoryPanel | Ledger/wallet/receipts/PersonalGoods/market/test exchange | BRANCH, локально проверено; налог preview |
| Чат/мобильный UX | ChatService/ChatPanel/Settings/прежняя карта | stable mentions, popup preference, компактный статус, map placement | BRANCH, целевой browser PASS |
| Всевышний | PR33 + signed bot PR35 | Агрегаты учёта, DB route regression, прежние возможности сохранены | BRANCH; production flag не менялся |


## D-071 — пакет0.7.1 (VERIFIED candidate, not deployed)

| Замечание | Адаптация существующей основы | Проверка |
| --- | --- | --- |
| COMBAT-02D | Terminal Cancelled + штатное завершение фактического участия/состояния | Domain PASS; API/browser CI PASS |
| COMBAT-02B | Account-bound Voivode + persistent RaidFighter + target/move intents | Domain PASS; API/browser CI PASS |
| COMBAT-02A | Visible persisted shoreline end caps, existing damage unchanged | Domain PASS; browser CI PASS |
| LAND-02 | Ownership/approval metadata, slow site-demolish, durable salvage claims | Domain PASS; API CI PASS |
| UI-06/02C | Content-sized civic cards, centered SVG close control | Build PASS; browser CI PASS |
| UI-07 | Dcoin icon, personal wallet HUD, direct wallet, honest test candles | Build PASS; browser CI PASS |

Не переписывать Dungeon, не сносить старые площадки, не менять реальные свечи.

## D-080: замечания владельца после 0.7.2

[Подробный план реализации всего объёма](DETAILED_OWNER_FIX_PLAN_0_7_2.md).

[Разбор и приёмка](OWNER_FEEDBACK_0_7_2_REVIEW.md). Статус: TODO, без runtime изменений.
Построчная сверка: 12 требований на последних скриншотах, 4 уточнения, 16 групп
прежних жалоб для контроля регрессий; осада/замок/монастырь остаются общим объёмом.

| Пункты | Существующая точка | Следующий шаг |
| --- | --- | --- |
| LAND-03 / TG-CIVIC-02 | BuildingOperations / ShelterRules / CivicPanel / CivicNotifications | Пригодные койки, точная причина и реальный статус заявки, сохранение жильцов |
| COMBAT-03 / COMBAT-04 | GamePage / BattleControls / VillageWorld / enemies.ts | Контроллер тапа без aside; общий способ интерполяции без гражданского пути бойца |
| COMBAT-07 | RaidFighter / GameService.Battles / RaidSimulation / личная панель | Настоящий отход без автоперевербовки/сброса HP, боевое лекарство с откатом и расходом; не добавлять новую защитную стойку или кнопки-заглушки |
| HUD-03 / UI-08 / UI-09 / COMBAT-05 | TopBar / GameMenu / нижний HUD / RaidBanner | Утверждены макеты HP/сил: телефон между погодой и кошельком, ПК слева от них, точные числа; убрать постоянные дубли/действия; Guard в подробностях |
| URL-01 | React Router / appBase / start/login links | Короткий вход единственного мира, совместимые старые ссылки |
| COMBAT-06 / CASTLE-02 / MAP-02 | RaidSimulation / ConstructionSite / Village / MONASTERY | Проектирование руин/недостроев, центрального замка/поражения, роли монастыря; пока не внедрять удаление объектов |
