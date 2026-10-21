# Правило владельца21.10.2026: ПК не должен спать во время работы

Для активной длительной работы tools/work-session.ps1 -Mode Start/Renew
удерживает временный SYSTEM_REQUIRED с15минутной арендой; экран может гаснуть.
Продлевать примерно каждые10минут вместе с get_usage_limits. При завершении/паузе
выполнять -Mode Stop; помощник освобождает запрос в течение10секунд. Без продления
аренда истекает сама. Постоянные powercfg-настройки и посторонние процессы не менять.

# Правило владельца21.10.2026: остановка при остатке9% и меньше

В начале долгой работы, перед новым длительным этапом и примерно каждые10минут
активной работы проверять get_usage_limits. При остатке9%или меньше в любом
применимом доступном окне немедленно прекратить новые исследования/изменения/тесты,
сохранить исходники/проверки/журналы/точную передачу и проверить сохранённые файлы.
Без повторной выдачи наград безопасно сохранить допустимое игровое состояние;
затем остановить только свои тестовые игры/фоновые процессы. Сообщить остаток,
путь передачи и следующий шаг. Владелец заранее разрешил PAUSED для активной
долгой цели при этом пороге; возобновлять только по явной просьбе. Не объявлять
цель выполненной. Если данные недоступны, процент не выдумывать; непрерывное
наблюдение между ходами не обещать. Это правило выше исторических указаний ниже.

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

# D-111 · Балансировку экономики поручить Всевышнему —21.10.2026

Владелец: «Думаю что экономику более точную надо оставить на всевышнего.
Заложить в него анализ экономики и налогов всех».
Это поручение расширяет экономическую аналитику существующего директора:
считать всех жителей, зарплату отдельно от достоверной частной прибыли,
неизвестные затраты/убытки/дань, распределения нагрузки, казну/резервы/еду,
историю и последствия. Точную рабочую шкалу подбирает Всевышний по данным;
предложение0/10/20/30% не является утверждённым балансом. Проверены реальные
заработки16–20.10 READ ONLY; LIVE_INCOME_ANALYSIS_2026_10_21.md/JSON.
Аналитика сама по себе не завершает автономию T054 и не даёт произвольные
списания. Продолжать T024/T025/T026/T054 с версиями, будущим effectiveAt,
принятыми условиями сделок, свежей проверкой, журналом и выключателем.
Госзарплата и неизвестная старая себестоимость освобождены; Dcoin отдельно.
Срез:696Domain/16API-boundary-migration/4browser и frontend PASS.
Изолированная миграция54→55 сохранила20сравниваемых таблиц; политики пусты,
списания/ИИ/Telegram выключены. Полная автономия ещё не реализована.
См. DIRECTOR_ECONOMY_1_0.md и verification JSON; новый полный CI ещё нужен.
Публичной выкладки/реальных платежей нет; D109/D110 сохраняются.

---

# D-110 · Подтверждена граница графики и механик —21.10.2026

Текущее продолжение T053: feat/desertovia-1-0-director-resilience.
Подписанный прежний bridge ограничивает ответ16KiB; стратегический JSON
проверяется строго; весь вызов адаптера ограничен50сек. Отказ/поздний ответ
не создаёт событие; принятая оплачиваемая работа продолжается независимо.
25 финальных API/boundary +1 общий login-regression и13mockedBot PASS;
раньше37API прошли до расширения дедлайна на синхронный запуск.
Domain/frontend не менялись; их687Domain и frontend PASS принадлежат
родительскому597c74a. PR93/597c74a прошёл весь CI39562557428:
687Domain/413API/414browser+2skip,frontend/Docker PASS.
Локально54→54: сравниваемые строки и2 квитанции сохранены, HTTP старого
повтора проверен после перезапуска. Реальные ИИ/Telegram выключены.
См. DIRECTOR_RESILIENCE_1_0.md и verification JSON. Полный CI нового
кандидата ещё нужен; весь функциональный план остаётся активным.

Текущее продолжение T051: серверные выключатели категорий Всевышнего,
отдельная версия политики и атомарные квитанции/журнал. Новые предложения
фильтруются, принятые события сохраняются; автономная экономика не включена.
См. docs/dezertovia/DIRECTOR_POLICY_1_0.md и verification JSON.687Domain,
22API+1 финальная гонка,13mockedBot и frontend PASS. Локальная миграция54
сохранила сравниваемые старые строки; HTTP/restart сохранил2 квитанции.
Точный597c74a прошёл весь CI39562557428 (см. выше). PR92/283b17f уже прошёл весь
CI39558809271:686Domain/403API/414browser+2skip,frontend/Docker PASS.
Не применять отложенный графический редизайн; публичного выпуска нет.

Прямой ответ владельца на D-108: «Сохранить графику мира и персонажей;
механики 1.0 продолжать».

Продолжать согласованный функциональный план1.0. Существующие изображения
мира, персонажей, зданий и игровые спрайты сохранять. Порядок D-109 остаётся
в силе: редизайн интерфейса после готовности всей функциональной части.
Уточнение D-108 разрешено; повторно ожидать ответа по этому вопросу не нужно.
Публичная выкладка и реальные платежи этим ответом не разрешены.

Текущее продолжение: feat/desertovia-1-0-director-followup. Срез T050 поверх
существующего Всевышнего сохранён547af63 в PR92; см. DIRECTOR_CONTRACT_1_0.md. Никакой новой автономии
или экономических полномочий этим срезом не включать. PR91/fad14b7 прошёл весь CI39555352195:
682Domain/392API/414browser+2skip. PR92/283b17f прошёл CI39558809271;
его новый код не приписывать проверкам PR91. 686Domain/26API/13mockedBot PASS; текущий код загружен локально без миграции.
Сохранённый WIP stash476670c остаётся страховкой;
отложенный UI stash add7fe3d не применять до готовности всей функциональности.

---

# D-109 · Сначала вся функциональность, интерфейс в самом конце —21.10.2026

Прямое уточнение владельца: «Нет, интерфейс надо делать в самом конце когда
вся функциональная часть будет готова».

Продолжать функциональный план1.0; визуальный редизайн отложить до готовности
всей функциональной части. Это уточнение снимает ожидание D-108 о возможности
продолжать механики. Графику персонажей, мира, зданий и спрайты сохранять.
Начатое оформление отделено от действующей игры, не объявляется проверенным.
Рейдовый срез сохранён локальным коммитом7784be8; публичного выпуска нет.
Рейдовый PR90/8a6871d: CI39549109973 зелёный, все5 задач:672 Domain,386 API,410 browser+2skip. Прежний CI
d8df65d/39548135422 выявил нарушение изоляции старых downgrade-тестов;
они исправлены,5 локальных API прошли, игровые guard не ослаблялись.
Рабочая ветка продолжения: feat/desertovia-1-0-dungeon-bodies. Новую графику не вести.
В ней Group reconnect читает принятые тела/карту из checkpoint:10 API PASS,
код72f051a и merge исправления PR90. Теперь общий профиль царя в новых данжах
реализован; см. ROYAL_DUNGEON_BODY_1_0.md и отдельную verification-запись.
Миграция52→53 сохранила все сравниваемые строки. Реальный320px сценарий
выбор/перезапуск приGathering/бой/отступление/один возврат копья прошёл
с DevTools=false. Полный CI нового кандидата ещё предстоит.
Существенные прежние OPEN группы/трофеев останавливают только зависимую работу.

---

# D-108 · Визуальная граница и уточнение объёма —21.10.2026

Владелец дополнил активную цель: «Графика персонажей и сама игра должна
остаться прежней. А вот интерфейс требует быть перерисованы под согласованный
стиль и формат».

Графику персонажей, карты, зданий и существующие игровые спрайты не заменять.
Перерисовывать интерфейс в выбранном «Живом царстве», сохраняя живые действия.
Смысл «сама игра» уточняется прямым асинхронным вопросом: только внешний вид
мира или также запрет изменения правил. До ответа дальнейшие зависимые
изменения механик не выполнять; независимую работу над UI продолжать.
Это ожидание уточнения части объёма, а не остановка всей цели.

Ранее проверенный локальный рейдовый срез сохранён отдельно в
feat/desertovia-1-0-mortal-heroes; он не объявлен выпущенным или отменённым.
Публичной выкладки и реальных платежей по-прежнему нет.

---

# D-107 · Вся Дезертовия1.0: кодирование возобновлено —20.10.2026

Владелец: «Не тормози! Продолжай кодировать. Цель не достигнута».
Активная цель docs/dezertovia/GOAL_1_0.md: весь единый план с проверками.
Ограничение текущего шага D-106 документами снято новым прямым поручением.
Продолжать код, тесты, локальный запуск и GitHub автономно; публичную выкладку
и реальные платежи без отдельного поручения не выполнять. Сохранять прогресс
и исходную основу. Существенные OPEN останавливают только зависимые изменения.
Текущая ветка feat/desertovia-1-0-mortal-heroes. Порядок: docs/dezertovia/EXECUTION_SEQUENCE_1_0.md.
Цель активна, весь план ещё не выполнен.

---

# D-106 · Концепция фермы: текущее поручение только о документации — 20.10.2026

Последнее описание владельца: «Фермерское хозяйство Дезертовии».
Читать docs/dezertovia/FARM_GAMEPLAY_CONCEPT_1_0.md и DECISIONS.md.
В текущем уточнении меняются только локальные документы; не запускать новый
игровой код, серверные действия, публикацию GitHub или изменение исходного архива.
Ранее начатые код/проверки сохранены отдельно, весь план1.0 ещё не завершён.
Подготовка может входить в посадку без отдельного ожидания; окно показывает
затраты/прогноз/историю, закрытие не отменяет работу.90/10, немедленные10%
и пропорциональные складские потери уже приняты D-105 и не переоткрываются.
Текущая локальная ветка feat/desertovia-1-0-farm, графика1 «Живое царство» выбрана.

---

# D-105 · Продолжать весь основной план — 20.10.2026

Владелец: «Почему ты остановился? Основной план делай полностью».
Продолжать DAG после каждого проверенного среза. Выпуск на публичный сервер
не объявлять из завершённого кода. Следить за точными SHA/проверками/миграциями.
Подтверждены: завершение принятой работы перед отдыхом; перенос себестоимости
с распределением по базовым ценам и отдельным legacy unknown; немедленная
защита10% урожая участников; фактические потери общего склада и невыданного
фермерского резерва пропорциональны, без денежного долга. Полные решения: docs/dezertovia/DECISIONS.md.
Основной checkout сейчас feat/desertovia-1-0-economy; M1 в PR75 проверяется
отдельно. Новые существенные вопросы останавливают только зависимое поведение.

---

# D-104 · Реализация 1.0 разрешена — 20.10.2026

Новое поручение владельца: «Но пока это все тестовый сервер. Так что можно.
Начинай работу если готов». Оно перекрывает ограничение «только документы» D-103.
Начать с воспроизводимой среды M0 и доступных задач единого DAG. Сохранять
прогресс/предметы/счета и соседние приложения даже в тестовом окружении.
Код, проверки, GitHub и подготовка следующих этапов выполняются автономно;
существенные OPEN решения уточнять до зависимого изменения. Пока ответ ожидается,
продолжать независимую работу. Не объявлять старое release-поручение новым.

Текущая ветка feat/desertovia-1-0-foundation. Читать
 docs/dezertovia/DEVELOPMENT_1_0.md и IMPLEMENTATION_PROGRESS_1_0.json.
Исходный пакет/документальная сверка D-103 сохранены как завершённый этап.
Текущие SOURCE/implemented/verified/released разделяются. Не запускать
старый install request, не сбрасывать данные и не выдавать новые правила за
готовые без проверки. VS Code остаётся общим рабочим пространством.

---

# D-103 · Дезертовия 1.0: только анализ и документация — 20.10.2026

Последнее поручение владельца: «Сейчас выполняй анализ и уточнение документации.
Написание игрового кода и выкладку этим поручением не запускай».
Это текущая граница работы; исторические разрешения на 0.7.x ниже не разрешают
новую реализацию/выкладку. Игровая версия не меняется.

Точка входа: docs/dezertovia/UNIFIED_PLAN_1_0.md; доказательства и source SHA:
CODE_AUDIT_1_0.md; ферма/питание/труд: FARM_BALANCE_1_0.md;
состояние: PLANNING_PROGRESS_1_0.md и progress.json.planning_1_0.
Исходный пакет получен из «ИГРЫ ДЕЗЕРТОВИИ 1»: все 23 файла прочитаны,
SHA-256/CRC/manifest проверены. SOURCE_PACKAGE_RECEIPT.json фиксирует источник;
TASK_ORDER_1_0.md / INTEGRATED_BACKLOG_1_0.json — 47 исходных + 6 farm карточек.
PKG-D100–118 не смешивать с репозиторными D-102/D-103. C07: entertainment
Dcoin заменяет свечи, хозяйственные деньги отдельно. C06 требует реальной
автономии в будущем; safe rest и фонд госзаказа ещё не реализованы.
Документация сверена; OPEN решения не закрыты молча. Реализация не разрешена.
Работать в отдельной docs-ветке, через открытый VS Code, сохранять progress.
Оба репозитория и новая ветка инструментов Вадима прочитаны без интеграции.
Ни игровые entrypoint, ни старые rollout requests, ни production пробы не запускать.

Исторические записи прошлых этапов ниже сохраняются.

---

# 0.7.16 УСТАНОВЛЕНА И НЕЗАВИСИМО ПРОВЕРЕНА — 19 октября 2026

Verified 2026-10-19T10:40:06.689723+00:00. Gameexact4bb125f2dc05711ebaf4dac2723f3ee19eae3ea9, CI39236625836 SUCCESS537Domain/288API/238browser+2normalSkip (120+120 cases, both shards required),4unit/lint/build/Docker. Botexact8dbbc3a000367a84c12869f6aed0a2b12318c148, CI39236650441 SUCCESS1104/serverSKIPPED.45operatorguards and16additional Chromium/WebKit checks. SourcePR71 merged72521742; botPR95/96/98 finalmerge1366a0b8, exact tested trees.
Actualpreflight39238163222,install39238318586,independentcheck39238986121 SUCCESS.143→143profiles,0lostprogress,145accounts/145loginbindings,38migrations,buildingnumbers/skills/stats/renown preserved.121otherbotmodules/657assets andconfig preserved, including separately installed newsroom90f55e3. Propose/budgets/quarantine unchanged. PGdump desertod-2026-10-19_1036.dump restorecatalogPASS; SQLiteonlinebackup/integrity/code/config/profile snapshots retained.
Game report captures map buildings in WebKit; ! top-left/hidden in form, validation correction and idempotent retry, untouched ruined blueprint cancellation, gear kept during live raid, popup opening-click guard, dark resource tiles. Hut14=internal40/10,-5,ruin100/no work; no real site was removed. Reports persist to memory and existingdaily digest, no per-reportDM/ZIP. New group puzzle threads cleaned with capped miss replies; private game conversations preserved. Timers in memory, no historical purge.
Report144 specific original failure remains unconfirmed; improved known400/413 correction/retry, not a claim all unknown failures fixed. All121–150 triaged, globalmap/newgear/recoverybalance remain plans. No synthetic live reports/messages/modelprobes/waves/payments/resets. Publichealth200/exact4bb/Telegramtrue/devfalse. post0716request=check; do not repeat install. Receipt RELEASE_0_7_16_RESULT.json / private POST0716_RELEASE_RESULT.json. Historical notes below.

# 0.7.16 — owner authorized 19 October 2026

Owner: «Составь план из этих ошибок и правь. Разрешаю сразу же загружать на сервер». This supersedes the report follow-up deployment HOLD. No repeated approval required. Implement, test exact pins, fresh live baseline, actual backups, narrow bot payload, deploy and independent check. Preserve player progress, external news work, configurations and director Propose/budgets/quarantine. No synthetic production reports/messages/battles/model probes.

# HOLD: report follow-up SOURCE ONLY — owner forbids deployment now

Owner18Oct2026: «Сейчас эти правки на сервер не грузи». This supersedes all
previous installation permission for these follow-up changes. DO NOT deploy,
change live configs/code, issue a rollout request or send synthetic reports/DMs.
Live0.7.15/52f2f6c and bot7963416a remain installed. No version bump for preparation.

User reportfe3ea7d4 inspected read-only: statusSent, one attempt, iOS0.7.15,
440x766 viewport,77,859-byteJPEG; mainmap entirely absent, HUD/minimap present.
Image inspected locally only; no real screenshot/account payload committed.
Reproduced in WebKit: visible map becomes absent in JPEG. html2canvas reads
img.naturalWidth/naturalHeight before the cloned data-URI image loads (zero size).
Await image.decode() in async onclone before parsing. Existing Pixi capture stays
unchanged; no extra game renders or preserveDrawingBuffer. Populated-map comparisons
PASS by day/night on desktopChrome and WebKit; no physical iPhone hardware check.

Owner also requests no individual report DMs: durablefeedback+identity/context
memory only, existing daily digest. No new timer/timezone/schedule. Preserve
historical receipts; legacy bridge Sent ack remains compatible with0.7.15 and
now means accepted into memory. New receipts Recorded, no Telegram call/model
call on intake. Screenshots stay in game PG storage with existing bounds/retention.
The game receipt explains the shared daily report. Old individual reports must
not be resent; new/original text enters daily summary without identity metadata
being forwarded to its model, full identity stays in owner-only report.
Preparation/tests/GitHub source only. Install only after a NEW owner instruction.
Historical release records follow.

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

#0.7.15 installation authorized — FINAL CHECKS PENDING, NOT INSTALLED

Owner18Oct2026: «Сразу грузи на сервер в гитхаб оба РЕПО наших».
Both game and batyushka source/GitHub installation authorized; no reconfirmation.
Small ! above menus and Dungeon, bug/complaint/suggestion, pre-form JPEG,
authenticated identity/version/context, durable outbox and owner-only ZIP.
Cancel/X/Escape erase memory draft without server/bot/browser/file storage.
Stamp0.7.15; final exact game/bot CI, fresh actual protected baseline and new
post0715 report→backups/deploy→separatecheck required. Preserve actual unrelated
bot news/vision, configs, Propose/budgets/quarantine and all player progress.
Only almighty.py,feedback.py,feedback_context.py,new game_feedback.py botpayload.
No synthetic production reports/messages/waves/model probes/payments/resets.
Prior green CIs do not replace final pins. Historical status follows.

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

#0.7.13 установка разрешена18Oct2026 — подготовка, НЕ УСТАНОВЛЕНО

Владелец: «ставь на сервер» — обе правки Dungeon и Android icons. Следующий
малый номер0.7.13. Exact final source/CI обязательны после version stamp.
Новый post0713flow, baseline0.7.12/56a6238; не переигрывать post0712 requests.
До перезапуска дождаться естественного конца всех Dungeon. Только registry
feedback_context.py в боте; свежий actual inventory защищает параллельные news
изменения. Propose/бюджеты/карантин/прогресс/конфиги сохранить. PG/SQLite/code/
profile backups, независимый check. Подробно RELEASE_0_7_13.md. ПокаNOTREADY.

# Android HUD icons — SOURCE ONLY,18Oct2026

Владелец прислал смещённые значки ресурсов поверх чисел. После заточки
ResourceIcon стал всегда возвращать дополнительный .resource-icon; мобильный
CSS .hud-resource-icon>.tile-icon перестал совпадать,24px рисовались в16pxслоте.
В fix/android-resource-icons обычные ресурсы снова возвращают прямой TileIcon;
wrapper и +N остаются для заточенных предметов. Старое оформление/ПК/баланс
сохраняются. Проверка фактических границ/перекрытий на320/390/430/960px в
post-071.spec.ts,desktop иPixel7. Локально2browserPASS/4ширины,TS/build/lintPASS; screenshots390/960 просмотрены.
ПолныйCIpending; журнал ANDROID_HUD_ICONS.md.
Dungeon fix уже exactCI39129933021 SUCCESS и mergedPR63; сервер всё ещё
0.7.12/56a6238. Эта ветка не установлена, версия не меняется ради CSS/документов.

# Подземелья: исправление проверено и слито, НЕ УСТАНОВЛЕНО18Oct2026

Запрос владельца: задержки команд и движения в подземельях. PR63 merged
fb500b8626bc0ef03f93b8f87c984d7edd6e5170; дерево идентично exact testedsource
d9ad3c4f5dba2bcf0218c33cd604b4e7f3b95f1e.
CI39129933021 attempt1 SUCCESS:533Domain/278API/212browser+2штатныхskip,
4Node clienttests,lint/frontend/DockerPASS. Локально278API и focused17PASS;
старый код отклонён6/6regressions, после восстановления focused17сноваPASS.
Dungeon medical checks actorOnly при force=true; очередь one-inflight+latest,
disconnect/unmount без replay; phase/settlement ожидания вне100ms simulation.
Неудачный commit сохраняет live run/выходы для retry. Правила боя/добычи/раны/
авторизация/DBschema прежние. Журнал DUNGEON_INPUT_LATENCY.md и RESULT.json.
СЕРВЕР НЕ МЕНЯЛСЯ: проверенный0.7.12/56a62381eb33b35e401e1766c2f4525d2709b361,
подтверждён публичнымreleaseheader. Естественныйпоход послеfix не измерен.
Для выпуска новыйmanifest/operator/exactCI/botversionmapping/backups/check;
старыеrequests не повторять. АктивныйDungeon не прерывать ради обновления.

# Подземелья: задержки команд и тиков, 18Oct2026 — SOURCE ONLY

Владелец сообщает плохой отклик персонажей в Dungeon, похожий на прежние набеги.
Ветка fix/dungeon-input-latency, журнал docs/dezertovia/DUNGEON_INPUT_LATENCY.md.
Dungeon clicks ошибочно force-синхронизировали всю деревню/отношения под village
lock. Теперь только actorOnly, но force=true и новые ранения/ошибки моста блокируют.
Клиент держит одну выполняемую и одну последнюю команду, без повтора после reconnect.
Phase/settlement DB ожидания отделены от 100ms simulation; по одному settlement
на run, неудачная транзакция сохраняет run/выходы для retry. Здоровье/урон/лут/
авторизация/баланс/БД-схема неизменны. Никаких live проб/сообщений/волн/сбросов.
Локально17API (включая4medical и2worker regression),4client unit PASS;
TypeScript/frontend build PASS. Полный CI ещё не подтверждён. Сервер остаётся
проверенным0.7.12/56a6238, исправление ещё не установлено. Не обещать измеренный
ping/FPS без естественного подземелья. Номер версии за диагностику не повышать.

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

# D-101: проверка и выпуск изменений второго разработчика, 0.7.12

18.10.2026 владелец разрешил полный анализ, исправления, тесты и установку после
успешных проверок, без повторного согласования. Основа: buhaiov-vs/Desertovia-1,
feat/weapon-sharpening e30fc988c957b339ba1e7a172d5d70b0d3f59025 поверх5394d0c.
Весь пакет сохраняется; найденные ошибки исправляются до выпуска. Версия0.7.12,
не0.8. Подробности/границы/проверки: docs/dezertovia/RELEASE_0_7_12.md.
Пока кандидат: успешные локальные проверки не означают установленную версию.
Только новый приватный post0712 rollout: точные CI, актуальный baseline55094d5,
резервные копии, миграции с проверкой сохранности, отдельный независимый check.
Бот — только feedback_context.py с mapping; все остальные фактические файлы,
включая новые Игрища, persona и YouTube-reader, защищены. Не повторять старые
requests, не менять бюджет/Propose/карантин, не создавать пробные волны/сообщения.

# V-01–V-06 в исходниках, не установлены

17Oct2026 пакет отзывов записан в fix/viewer-todo-0712. Тёмный склад: чернила
на пергаменте. Опыт режется голодом на старте цикла, не пропавшей в конце едой.
Неизменный мир не пересобирает декорации. Камыш бесконечен, таймера нет.
Счётчики костра и золота читают вклады. Путь не срезает воду. LIVE 0.7.11.
Версия, сервер и Propose не менялись.

# Дополнение TODO: атмосферные набеги и игровая ночь — PLAN

17Oct2026 владелец попросил сохранить ожидание игровойночи для тумана/зловещих
сценариев; дневные нападения остаются. ATMOSPHERIC_RAIDS_TODO.md фиксирует
7предложенных сценариев, полный RaidSettings для Всевышнего, разведданные/
скрытие таймера и сторон, окружение с общим бюджетом,1–2редких намёка.
Время — цикл деревни, не реальные часы/тема UI. Планирование/отмена/restart/
продление согласия без двойных волн. Никаких дозоров игроков; NPC-разведчики.
Только документы; код/сервер/версия/Propose не изменены. Связи:TODO.md,
VIEWER_TODO V-20/V-21. НовыеfeedbackID не выдумывать.

# TODO зрителей обновлён17Oct2026 — только документы

По поручению владельца опубликован docs/dezertovia/VIEWER_TODO.md, вход TODO.md.
Прочитаны все120 сохранённых feedback (#1–120), private read39052058163 SUCCESS,
older_omitted0. Новые#118 XP/еда,#119 гостевое заселение,#120 тёмные склады.
23группы с приоритетами/приёмкой; старые исправления отделены от регрессий,
резерв/неполные/другие проекты отмечены. Только пересказ без имён/ID/сырыхЛС.
Новый игровой код, версия, сервер, статусы отзывов не менялись. LIVE по последней
квитанции0.7.11; следующийобычныйномер0.7.12, затем до0.7.99.

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

# D-099 clarification: all numeric raid controls are sliders

Owner explicitly wants sliders instead of numeric entry and explanations of ×.
Current source replaces number inputs and species shares with native ranges,
readouts with ×/percent/minutes/seconds and per-field plain hints. Existing
10browserPASS was before this change; final slider checks now10/10PASS on
phone/desktop (1.0min), including keyboard/range limits/units/percent/save/launch.
Finalfrontend TypeScript/build/lintPASS; both phone screenshots inspected.
No balance/authority/runtime/version/installation change. Same PR56 branch.

# D-099: simplify raid editor and explain rejected settings — SOURCE ONLY

Owner cannot use the 0.7.11 editor; screenshot shows generic raid.settings.
Read docs/dezertovia/RAID_EDITOR_USABILITY.md. Fix per-species100%checkbox trap,
clear quick presets/difficulty, advanced disclosure and exact field errors before
preview/save/launch. Keep owner multipliers (player damage4 is valid), authority,
idempotency and safe preview. No balance change/new live wave/model/message.
Work fix/raid-editor-clarity: frontend TypeScript/build/lintPASS; final10/10browser
phone/desktopPASS31.2s, no retries. No new live installation.
LIVE remains exact0.7.11/ba7050f/check39036486712; no new release/deploy request.

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

# 0.7.11 full-browser follow-up — validate newest source, NOT INSTALLED

Fullbrowser exposed a legacy partial-state CivicPanel regression (missing storage
array). Shared upgrade predicate now handles absent collections safely; all10
existing cooperative desktop/phone tests and TS PASS without weakening assertions.
CI39030967136 on9b4ece1 is superseded/cancelled, NOT ready. Read latest PATCH journal
and PR54 checks for new exact source. BotPR52/CI39031013157 SUCCESS903 onbd2c9f2;
its version map must also include this newer game source and rerun exactCI.
No deployment date/request for0.7.11; preserve live0.7.9 and all previous bounds.

# 0.7.11 implemented on feature branch; final validation pending, NOT INSTALLED

Public draftPR54 and bot canonical draftPR52 retain all D-093–097 scope. Read
PATCH_0_7_11_IMPLEMENTATION.md and RELEASE_0_7_11.md (docs/dezertovia).
Local fullDomain516 + final17 PASS, newWALL/P2browser12 PASS phone/desktop,
TS/lint/frontendPASS. CI39030589990 on118d6c6 backend516/270 + frontend/Docker
PASS; completebrowser still pending. A final null-ID research guard is now added
and needs final exact CI. Do not mistake preliminary run for final readiness.
Bot futurepayload includes app/db/helper/feedback/civic_notifications AND
feedback_context source→version mapping; preserve actual installed budget/
quarantine/llm/town_proxy/Propose. No0.7.11install date/request; LIVE0.7.9 below.

# D-097: implement 0.7.11 now; preserve fence style/day/night

Owner authorized implementation of the full retained plan, not just documentation.
Read `docs/dezertovia/PATCH_0_7_11_IMPLEMENTATION.md` for the resumable checklist,
worktrees, entry points, tests and art contract; keep it current after each packet.
Feature branches feat/patch-0-7-11 in game079-record and bot079-final. Keep all
D-093–096 scope, original outlines/palette/pixel scale and matched night variants.
New fence art is explicitly authorized, superseding the historical art pause.
No 0.7.11 installation date selected. Do not replay older deployment requests.
State IN PROGRESS, not READY/installed; previous PLAN-only limits superseded.

# D-096: 0.7.11 adds social rescue and owner raid editor, still PLAN

Read `docs/dezertovia/RESCUE_AND_RAID_CONTROLS_0_7_11.md` and the full
`NEXT_PATCH_PLAN.md`. Owner requests rescue at any time before recovery, without
an eligibility timeout. Remove automatic 5-minute admission; only physical arrival
in a working hospital grants a one-time reduction of remaining wound time.
Initial proposal: 25% remaining time and +2 renown for a useful completed rescue;
no permanent dependence on other players, no repeat reward/healing or paid-currency
addition. Handle the tsar and external recovery without inventing authority.
Owner gets editable BOSS / mixed / swarm presets, auto and manual controls,
preview/saved configurations, explicit owner-only above-recommended settings;
director retains bounded Propose. Max 48 simultaneous units; freeze announced
settings; cancel all waves of an event. This supersedes older rescue auto-admission
and restricted start controls. Other D-093–095 scope remains. Docs only, no deploy.

# D-095: next patch target 0.7.11; retain 0.7 series through 0.7.99

Owner explicitly selected 0.7.11 for the next patch. Then count 0.7.12, 0.7.13,
etc. through 0.7.99; do not jump to 0.8 or 1.0 for small changes. Compare integer
version components; no policy beyond 0.7.99 selected yet. Full retained scope:
`docs/dezertovia/NEXT_PATCH_PLAN.md`, including D-093 difficulty and D-094 feedback.
This is planning/version policy only; no runtime/version-file/deploy changes,
no installation date. Historical release receipts must not be rewritten.

# D-094: next patch includes owner and viewer feedback, still PLAN

Read `docs/dezertovia/NEXT_PATCH_PLAN.md` and `FEEDBACK_REVIEW_2026_10_17.md`.
All 117 accepted feedback records and five substantive owner messages reviewed.
Owner suggestions bypass feedback capture through Helper.answer; model promises
of saving are false. FB-01 is a priority fix. Only the last 12 helper turns exist;
older owner messages are not recovered. Owner numbering by type, medical map
regression, food consumption, wall upgrades and space/capacity added; retain D-093.
Do not treat old 0.7.7 reports as fresh confirmed bugs. This turn changed docs only;
no new deployment date, runtime remains independently verified 0.7.9.

# D-093: next patch scope recorded, not implemented

Read `docs/dezertovia/RAID_DIFFICULTY_NEXT_PATCH.md` before the next balance work.
Owner requested adding the discussed difficulty table to the nearest patch.
Outcome/cooldown/reporting first; adaptive readiness and budget next; then property
losses, consumables and group tactics. Numbers are initial simulation targets.
Backpack death losses remain deferred. No runtime/configuration/version changes,
no new deployment date. LIVE remains independently verified 0.7.9 below.

# 0.7.9 installed and independently verified — 17 October 2026

Owner D-092 authorized deployment and testing by players. Verified at
2026-10-17 08:59:55 UTC (10:59:55 Europe/Oslo).

- Installed game: `7c738bc30cfb7cfa21dc4c6e6dc7de9496ad77e8`.
- Bot payload source: `b72fa1711b58062caf13cb80b315a5b01f2a5506`; only
  `feedback_context.py` replaced; 15 protected modules preserved.
- Exact game CI 39017965139: SUCCESS, 460 domain, 255 API, 169 browser tests,
  one normal skip; lint, frontend build and Docker passed.
- Exact bot CI 39018066817: SUCCESS, 891 tests; server job intentionally skipped.
  All 27 rollout operator guards passed.
- Public PR 53 merged as `49ecb4de`; bot canonical PR 51 as `6ac9b8dd`.
  Both merged trees equal their tested source trees.
- Fresh preflight 39019126424, installation 39019193847 and separate independent
  check 39019429338: SUCCESS.
- All 119 profiles, progress, 121 accounts and 121 login bindings preserved.
  All four building-number checks report zero errors; 27 migrations applied,
  including MonsterBehavior; 24 frontend chunks and feedback version checked.
- Fresh PostgreSQL dump restore catalog verified; SQLite, code, configuration,
  profile, identity and combat backups retained privately. AI budget, quarantine,
  configuration and director Propose mode preserved.
- Property raids, crowd behavior and bosses are live. Backpack death losses
  remain design only. No forced waves, messages, model probes, payments or resets.
- There was no active battle at upgrade and no natural combat timing sample at
  final check. Actual phone performance still requires player observation.

Do not reinstall or replay requests. The current post079 request is `action=check`.
Earlier READY, NOT INSTALLED and preparation entries below are historical.

# D-092: owner authorized0.7.9 deployment now

Owner17Oct: «Давай грузить и тестировать в игре игроками». Ship D-091 through a
new guarded0.7.9 rollout, no further permission required. Read RELEASE_0_7_9.md.
Previous7e50c66/CI39016736130 FAILED one API assertion contaminated by a background
clinical poll for another village; not ready. Scope the assertion to all actors
of the tested village and still require two forced checks, fresh injury/failure
rejection and untouched other residents. No runtime clinical restriction weakened.
Exact final CI, newpost079 operator/candidate, actual live preflight, PG/SQLite/
code/config/profile backups and separate check mandatory. LIVE0.7.8 until success.
No old rollout replay, forced test waves, synthetic player messages/model probes,
payments or progress resets. Death backpack losses stay design only.

# D-091: monster behavior implementation, NOT INSTALLED

Owner17Oct authorized detailed plan and integration; property/supplies are the
primary raid objective. Read docs/dezertovia/MONSTER_BEHAVIOR_INTEGRATION.md.
PR53 draft; local460Domain/255API+32regression/22browser/lint/build PASS.
Exact full CI pending on final head; no READY claim or installation.
Branch feat/monster-behavior: isolated Nez UtilityAI + per-battle RVO2-CS,
property selection/crowd costs, existing swarm/elite/mixed plus bounded1–3boss,
telegraphed property strike/phase, director options/fresh budget, JSON migration.
Dungeon shares the behavior/crowd adapter without enabling DungeonOnly in raids.
Personal storage can be looted by its actual escaping carrier; existing Steals
catalog and total24goods cap. Backpacks/death losses remain DESIGN ONLY.
LIVE remains0.7.8/aab588f. No new rollout request, no version bump for source.
Preserve player control/progress/medical rules/budget/Propose. No live probes,
forced waves/messages/payments/resets. Separate exact CI/preflight/backups/check
required for a future new release; do not replay post078 or old deployments.

# 0.7.8 INSTALLED and independently verified —17Oct2026

Owner D-089 «Загрузи. Посмотрим что даст» authorized immediate rollout.
LIVE gameaab588f93dd71c074f50d056ea7d3bcba814e842, bot two-file payload source
3903ee140342605992e9d29260b0c9bab2c8e4a3. Exact CI39011042846 SUCCESS441Domain/254API/
167browser+1normal skip/frontend/Docker; botCI39011057542 SUCCESS891tests/serverSKIPPED.
24operator guardsPASS. Freshpreflight39012284286, install39012337683 and independent
check39012556053 SUCCESS at2026-10-17T07:09:16.5232290Z.
119→119profiles,0progresslosses,121accounts/loginbindings preserved;26migrations,
24frontendchunks/feedback0.7.8/building numbers all4checks0. ActualPGdump restore
catalog verified; SQLite/code/config/profile/identity backups retained privately.
Only almighty.py and feedback_context.py replaced.14other modules/configs/
AI budget/quarantine/Propose retained. Threat proposals after1quiet hour when
at least3capable online,2hcategory pause after any proposal; owner approval and
fresh checks retained. No synthetic messages/raids/payments/model probes/resets.
Natural director behavior is to be observed on scheduled reviews, not forced.
No reinstallation/old request replay. Earlier READY/source-only notes historical.

# D-089: owner authorized0.7.8 installation now

Latest owner: «Загрузи. Посмотрим что даст». This authorizes guarded installation
of D-088 director threat pacing as0.7.8. No new permission required.
Fresh exact game/bot CI, newpost078 candidate/operator/payload, actual preflight,
PG/SQLite/config/profile backups and independentcheck required. Do not replay
post077. Only bot almighty.py and feedback_context.py; all unrelated modules,
AI budget, quarantine, owner approval/Propose and player progress preserved.
LIVE remains0.7.7 until actual installation/check succeed. No synthetic raids,
messages, payments or model probes. Earlier source-only restriction superseded.

# D-088: director raid deadlock fix in review, NOT INSTALLED

Owner17Oct reports director repeatedly waiting for repairs instead of proposing
raids. Read docs/dezertovia/DIRECTOR_RAID_PRESSURE.md (public game repo).
Read-only live audit39010114851 confirmed15online/14capable/2patients,
23sites/11damaged and repeatednone/rest/work_notice; no model error.
Source changes: patients/sites/damage no longer grant blanket immunity;
minimum3capable online, real peace/rest/pending raid/recent loss/defeat guards.
After1hwithout encounter, propose a bounded raid; any raid proposal pauses the
category2h including rejected/expired, no enemy-switch bypass. Explicit pacing
fallback only after a valid model answer, creates owner proposal, never a raid.
AI budget/auth/owner approval/battle budget unchanged. No version bump for this
source-only work. LIVE remains0.7.7/44b4e426. Do not replay post077 requests or
install this with old payload hashes. New reviewed rollout/CI needed for release.

# 0.7.7 INSTALLED and independently verified — 17 October 2026

Owner D-087 authorized installation no earlier05:30UTC/07:30Europe/Oslo.
Fresh preflight39006496030 SUCCESS; install39006543221 SUCCESS;
independent check39006763680 SUCCESS at2026-10-17T05:34:46.7429731Z.
LIVE game44b4e426cdfdd9bb72621a9febeb624130a8d6e2, bot payload source
d462e634df7ac49efd45b1ab3bc09e49fd11796b. Exact CI38979407726 and38980131645
rechecked authenticated: completed/success/attempt1; tested/merged trees match.
119→119profiles,0lost profiles/progress;121accounts and121login bindings retained.
26migrations includingPlayerFeedback077; all4building-number checks0;24public
frontend chunks and feedback live version0.7.7 verified. PostgreSQL dump restore
catalog verified; SQLite/config/code/profile backups retained privately.
14protected bot modules/configurations/quarantine/AI budget and directorPropose
preserved. Only civic_notifications.py/feedback_context.py replaced in bot.
No synthetic messages, model probes, forced raids, payments or progress resets.
No natural battle timing sample at finalcheck: physical-phone FPS still unverified.
Do not reinstall or replay older requests. Earlier READY/NOTINSTALLED entries
below describe preparation, not the current runtime.

# D-087:0.7.7 READY, scheduled17Oct2026 07:30Oslo; NOT INSTALLED

Source44b4e426cdfdd9bb72621a9febeb624130a8d6e2, CI38979407726 SUCCESS439Domain+
251API+167browser/1normal skip,frontend/DockerPASS; PR51 merged6540b98 identicaltree.
Botd462e634/CI38980131645 SUCCESS890tests/serverSKIPPED; canonicalPR49 merged422cb81
identicaltree.24operator guards and local numbering SQLPASS. Read
 docs/dezertovia/RELEASE_0_7_7.md and PLAYER_FEEDBACK_0_7_7.md. Private ready
candidate/operator in bolig-agent ops/almighty-release. Earliest installation
17Oct2026 05:30UTC=07:30Europe/Oslo, one-time task scheduled. LIVE remains0.7.6/d0f88e2.
No post077 request yet. Fresh exact CI/baseline/services/hash preflight, actual
backups and separate independent check still required. Preserve progress/keys/
Propose/budget/quarantine; no old request replay, broad deploy, forced raids,
messages, payments, model probes or resets. Earlier INPROGRESS/HOLD are history.

# D-087: feedback fixes 0.7.7 IN PROGRESS, scheduled no earlier 17Oct2026 05:30UTC

Owner authorized implementation/testing and deployment at07:30Oslo17Oct.
Read docs/dezertovia/PLAYER_FEEDBACK_0_7_7.md. Stable building numbers, food,
targeted repair10%, selected storage, own housing, destination/stat labels,
free donation. Preserve coordinates/ownership/progress/resources/medical rules.
LIVE remains0.7.6/d0f88e2. No old rollout replay; exact CI/fresh preflight/backups
and independent check required. No installation before05:30UTC.

#0.7.6 INSTALLED and independently verified16Oct2026

LIVEgame d0f88e2e92d83e8c30a7566fb21c916a51023b1d, CI38969100973 SUCCESS424Domain+
244API+155browser/1normal skip,frontend/DockerPASS. PublicPR50 mergee62f516,
tree identical to tested source. Botmetadata891d1a8 CI38969173093 SUCCESS887tests/
serverSKIPPED, canonicalPR48 merge14e6dd5.20operator guards and4readonly parser
checksPASS. Freshpreflight38970195603, install38970339155 and independentcheck
38970711893 SUCCESS2026-10-16T21:38:28.7746706Z.119→119profiles,0progresslosses,
121accounts/loginbindings,25migrations,24frontendchunks,feedback0.7.6PASS.
PG/SQLite/code/config/profile backups saved;14protected botmodules/configs/
quarantine/Propose/budget preserved. No forced raids/messages/payments/model probes
or resets. Combat clicks now force medical refresh only for their own actor;
new injury/unavailability restrictions retained. Settlement yields after each
individually committed resident. Bounded internal-operation wait/hold metrics.
Old behavior4expected regression failures; fixed244API PASS locally and in CI.
Owner0.7.5 late stutter report showed lockmax3015ms/simulation median2ms. New
natural real-phone battle capture still required; not a claim all jitter gone.
See docs/dezertovia/RELEASE_0_7_6.md. Do not replay/reinstall older requests.
Earlier READY/HOLD/NOTLIVE records are history.

# D-086: late-fight contention after0.7.5, implementation in progress

Owner actual0.7.5 report: early smooth then late stutter. Simulation median2ms,
lock max3015ms; not server CPU proof. Attack click force-refreshes all residents
under global lock; narrow to actor only while preserving fresh medical authority.
Yield work settlement lock after each individually committed resident. Add bounded
operation wait/hold aggregates so future captures attribute holders/waiters.
Branchfix/combat-lock-contention, planned0.7.6, LIVE0.7.5/374323f unchanged.
Read docs/dezertovia/COMBAT_LOCK_CONTENTION_0_7_6.md; testing/CI/rollout pending.
No forced raids/messages/payments/model probes/resets or older request replay.

#0.7.5 INSTALLED and independently verified16Oct2026

LIVEgame374323f8c3ef519694497371716e611d3396ee43, CI38963553875 SUCCESS424Domain+
239API+155browser/1normal skip,frontend/DockerPASS. PublicPR49 mergedbbb5f29,
tree identical to tested source. Botmetadata380d8ea CI38963592945 SUCCESS887tests/
serverSKIPPED, canonicalPR47 mergede0a4cfa.20operator guardsPASS. Freshpreflight
38965661962, install38965752003 and independentcheck38966145677 SUCCESS
2026-10-16T20:59:15.5558416Z.119→119profiles,0progress losses,121accounts/bindings,
25migrations,24frontendchunks,feedbackversion0.7.5PASS. PG/SQLite/code/config/
profile backups saved;14protected modules/quarantine/Propose/budget preserved.
Subpixel combat motion, shared bounded jitter-aware pacing and wall damage-only
static-world fast path are LIVE. Reproduced9/48positions before vs48/48after,
late30/30,healthbar drift0 in actual map browser regression; not physical-phone
FPS proof. Natural next battle capture still needed; initial client frame spikes
and lock512ms were observed, do not claim every source of jitter gone. No forced
raids/messages/payments/model probes or resets. No old request replay/reinstall.
See docs/dezertovia/RELEASE_0_7_5.md. Earlier READY/HOLD/NOTLIVE entries are history.

# D-085: movement-only stutter after0.7.4; client pacing fix in progress

Owner reports map/buttons smooth, fighting avatars stutter. Two owner-provided
0.7.4 diagnostics: packet gaps1502/1609.5ms, frame updatep95~4–6ms; second capture
has early frame spikes117ms and world updatep95~41ms. Server full report peak
simulation23.7ms, lock512.8ms; read-only real-battle audit38960868199, no mutations.
Reproduced in actual renderer: slow fighter changes drawn coordinate only9of48
frames because world pixels are rounded; fixed1000ms tween ends before late
snapshots. Workfix/combat-motion-cadence, candidate0.7.5: keep subpixel actor
positions, common bounded jitter-aware pacing for avatars/enemies/healthbars;
ignore wall damage-only packets until geometry changes. No extrapolation, no
server/combat/medical/economic changes. Local26browser tests, TypeScript/lint/build PASS; exact CI and fresh rollout pending.
LIVE remains0.7.4/74adbe8 until exactCI/fresh rollout/independentcheck. No old
request replay/forced raids/messages/payments/model probes or profile resets.

#0.7.4 INSTALLED16Oct2026; independent verificationPASS

Owner requested no guard/post/camp bindings and combat diagnostics; prior server
fix installation authorization continued. LIVE game74adbe8c1d778f0751a755a4b36dbefe6f16a405,
CI38956420909 SUCCESS424Domain+239API+151browser/1skip,frontend/DockerPASS.
PR48 mergeddfdc0ac with identical tested tree. Bot metadata67f9149,
CI38957570632 SUCCESS887tests/serverSKIPPED; canonicalPR46 merged6ae84ce.
20operator guardsPASS. Preflight38958939581, install38959075262 and separatecheck
38959583714 SUCCESS2026-10-16T20:04:47.4389681Z.119→119profiles,0progress losses,
121accounts/bindings,25migrations,24publicJS chunks,feedback live versionPASS.
Old unfinished duties0.14protected botmodules/configs/quarantine/Propose/budget
preserved; PG/SQLite/config/code/profile backups saved. No forced raids/messages/
payments/model probes or resets. No guard task, sector, counter or camp leash.
Manual ground/target orders, local range-only self-defense, HP/ammo/heal receipt
preservation; slow navigation and combat renderer/snapshot fixes. Bounded server/
client diagnostics and Settings download enabled. No natural postdeploy raid
sample at finalcheck; physical phone FPS and any remaining freeze need actual
measurements. See docs/dezertovia/RELEASE_0_7_4.md and RELEASE_0_7_4_RESULT.json.
Do not reinstall/replay oldrequests. Earlier pending/NOTLIVE entries are history.

# New request: remove guard/post binding and diagnose combat freezes

Owner16Oct2026: players choose where to go/what to do; no guard duty. Implement
fix/free-combat-and-diagnostics, plan docs/dezertovia/FREE_COMBAT_AND_DIAGNOSTICS.md.
Manual movement/targeting, local self-defense, no camp leash or guard prerequisite.
Preserve paid work/HP/ammo/injuries; settle legacy paid duties once. Bounded
combat diagnostics and remove repeated citizen work from battle snapshot handling.
Implementation0.7.4 includes bounded server/client logs and renderer/navigation fixes.
Local424Domain+22browser PASS; final239API and exact CI pending. Not READY.
All code first, then focused checks and exact CI. Current LIVE0.7.3 below remains
until fresh rollout. No synthetic production actions or old request replay.

#0.7.3 INSTALLED16Oct2026, independently verified

Owner authorized installation. LIVE game672d72e167dd477f925df6394ecc5b188d3924ff;
CI38941941103 SUCCESS419Domain+235API+149browser/1skip, frontend/DockerPASS.
PublicPR47 merged9ac0007; its tree matches the tested source. Bot original civic
patch e4269ef7 plus version-registry fix f4dd269, CI38948020819 SUCCESS887tests,
serverSKIPPED. Narrow two-file bot payload preserves quarantine/budget/Propose.
Preflight38947072346 PASS. Initial install38947223449 switched runtime butFAILED
at feedback version lookup; separatecheck38947768084FAILED. Missing registry
mapping fixed in botPR45; repair38948536494PASS without game reinstall.
Final independentcheck38948699510SUCCESS2026-10-16T18:37:12.2283924Z:
119→119profiles,0skill/stat/renownlosses,121accounts/loginbindings retained,
25migrations,24publicJS chunks and feedback live-version lookupPASS.
PG/SQLite/code/config/profile backups saved; quarantine and Propose preserved.
No synthetic messages, model/payment/raid probes or progress resets.18operator
checks PASS, including version-registry coverage before switching future games.
See docs/dezertovia/RELEASE_0_7_3.md / RELEASE_0_7_3_RESULT.json. Do not reinstall
or replay older requests. Existing castle not moved; phone FPS not measured;
new director tax/economic executors remain separate work. Earlier PLAN below
is history. Current HUD is icons/bars, numbers on tap; Chat/Retreat/Heal44px row.

# D-081: реализация полного плана разрешена, без установки

16.10 владелец сказал «Делай» после DETAILED_OWNER_FIX_PLAN_0_7_2.md.
Реализовать весь сохранённый объём в feat/owner-combat-and-siege; код сначала,
затем общая проверка. Сохранять профили/прогресс/вложения, текущее Propose,
карантин/бюджет бота. Никаких production-волн, тестовых сообщений или установки.
Исторические PLAN ниже не блокируют разрешённую реализацию. Кандидат0.7.3, LIVE0.7.2.
См. docs/dezertovia/OWNER_CORRECTIONS_IMPLEMENTATION.md: код в проверке, не READY.
Первый стиль HUD отклонён владельцем; использовать существующие resource-card
рамки/фон/размеры, проверять на настоящей карте. Нельзя выдавать пустой стенд за игру.
D-083: только сердце/молния и полосы, числа по нажатию. «Здоров» убрать.
Уточнение владельца: боевые Отступить/Лечение в одном ряду с Чатом, высота44px;
без растягивания на экран. Тёмное недоступное лечение сохраняет видимую причину.
Постоянного HP вне боя нет, не подменять его100%; детали в DECISIONS/implementation.

# D-080: разбор новых замечаний после 0.7.2, только TODO

16.10 владелец прислал скриншоты: два выхода, окно врага, рывки игроков,
счётчик охраны, снос жилья, монастырь/замок/разрушение, повтор slug в URL.
Добавил компактные HP/силы и перенос Строить/Голосование в меню.
COMBAT-07: только рабочие Отступить/Лечение при личном участии в бою;
атака тапом, новую защитную стойку после уточнения о пользе исключить из пакета;
отдельная компактная строка без карточки врага. Отступление/лечение HP требуют
серверных состояний, сохранения HP при возврате и идемпотентного расхода.
HUD-03: владелец утвердил макеты телефона и ПК. Две тонкие полосы сверху:
красная HP, жёлто-зелёная силы; телефон между погодой и кошельком, ПК слева
от них, на ПК точные числа постоянно. Без новой строки/полноширинной панели;
стрелки макета не часть игры. COMBAT-07 — только личный бой, нет лекарства →
тёмная disabled-кнопка с причиной. Это утверждённый план, ещё не реализация.
Сверка скриншотов: в OWNER_FEEDBACK_0_7_2_REVIEW.md теперь отдельные12 строк
из трёх последних скриншотов +4 последующих уточнения и16 групп прежних жалоб.
Недострои/разрушение всех зданий/центральный замок/поражение/монастырь — часть
общего плана, не исчезнувшие пожелания. Старый релиз не равен свежей проверке;
перед завершением пакета проверить перечисленные регрессии, не обещать весь
план выполненным после одного HUD. Код/сервер этим поручением не меняются.
Подробная имплементация всего объёма: docs/dezertovia/DETAILED_OWNER_FIX_PLAN_0_7_2.md.
Сейчас PLAN; сервер/код не меняются. Рекомендации новых механик не выдавать за LIVE.
Читать docs/dezertovia/OWNER_FEEDBACK_0_7_2_REVIEW.md и NEXT_TODO.md.
Live04c3950/0.7.2 не менялся. Read-only audit38917722704: хижина4/4,
других свободных коек0, старая заявка сейчасrejected. Запрет сноса обоснован;
ошибка — скрытая причина/неясная заявка. Данные без имён в private ops.
Клик уже отправляет attack, но aside остаётся; сглаживание бойцов отдельно.
Сначала исправляющий пакет UI/снос/бой; полноценные руины, стартовый центральный
замок, проигрыш и роль монастыря пока проектирование, не выполненная механика.
Сохранить профили/прогресс/жильё/вложения; не выселять и не передвигать центр.
Документы не повышают версию и не разрешают новую установку. BotD079 отдельно,
не переигрывать старые rollout requests, не трогать модерацию/бюджет/Propose.

# D-077 /0.7.2 INSTALLED16.10.2026, независимо проверен

Owner «выкладывай» разрешил пакетD-074/D-075/D-076. LIVEgame
04c395052a106562b8e00b6c73a8a5b119b1aea5, bot five-file payloadac2ca8e4c8b53921cc34a47efa77ab046e376a0b.
ExactCI38891077915 SUCCESS401Domain+230API+133browser/1штатныйskip,
frontend/DockerPASS; bot38891110155 SUCCESS840tests/client/runner,
serverSKIPPED. PublicPR46 mergedd31c554, botPR40 merged8ef414a; деревья
совпадают с tested source. Дополнительный public mergeCI38892389532SUCCESS.
Freshpreflight38892398267PASS. Install38892470520 фактически заменил runtime,
APIhealthy11:18:41UTC, но workflowFAIL из-за ошибочного asset-probe: обрезал
точку в basename webworkerAll.es5-*. Диагностика38893024313FAIL, checker исправлен.
Independentcheck38893238465PASS. Повтор38893366537 поймал единичный404
BitmapFont, следующий полный публичный24chunk прогонPASS; bounded3attempt
retry404/502/503/504 добавлен, persistentfailure остаётся запрещённым.
Finalindependentcheck38893644226PASS, 2026-10-16T11:25:20.7425857Z.
Не переименовывать failed workflow в SUCCESS: факты и история в release record.

117→117профилей,0потерь навыков/характеристик/славы;
119аккаунтов и119Telegrambindings сохранены,24миграции.
PG/SQLite/обаconfig/snapshot backups сохранены. FlagsHospital/Almighty true,
Propose сохранён. Protectedidentity/Almighty/civic/budget/proxy hashes unchanged.
Публичные24JSchunk/0.7.2 и livefeedbackversion подтверждены; operator22testsPASS.
Нет синтетических сообщений, AI-проб, платежей/волн или сбросов.

Пакет: очередь/ошибки улучшений, компактныйHUD, пациенты в карточке больницы,
клик для атаки, reuse графики/плавное движение, контекст отзывов батюшки.
Обычным жителям ещё нужна охрана; самооборона всех TODO. FPS реального телефона
не измерен; не обещать устранение каждого возможного лага. Экономические/tax
инструменты Всевышнего в этот пакет не входят. Исторические hold/READY ниже
не текущее состояние. Не повторять install; новый exactsource только check
со снимками post-0.7.2. Нельзя запускать старыеpost071/live-combat requests.

# D-077: выпуск0.7.2 разрешён, проверяется точный пакет

Владелец16.10 сказал «выкладывай» для пакетаD-074/D-075/D-076.
Это снимает repository-only hold этого пакета. Следующий малый номер0.7.2;
package/lock/patch notes согласованы. Исходныйd732713 прошёл полный CI,
после изменения номера требуется новый exact CI. Не объявлять установленным
до actual preflight/backups/install/отдельного check. Узкий bot payload включает
новый feedback_context.py; защищённые budget/proxy/Almighty модули сохраняются.
Без синтетических сообщений, AI-проб, платежей/волн и сбросов прогресса.

# D-076: компактный HUD, пациенты и отрисовка боя — repository only

Владелец16.10: валюты съедают экран; пациент лежит снаружи больницы; вместо
четырёх боевых кнопок нужен клик по врагу; при врагах игра фризит, после них
нормальна. Читать docs/dezertovia/COMBAT_RENDERING_HOTFIX.md. В PR46 вместе
с D-074. Live0.7.1/e5de51e не менялся. Номер версии не повышать за эту ветку.
Исправлены подтверждённые лишние пересоздания объектов и рывки интерполяции;
не выдавать стенд за измерение FPS реального телефона или доказательство
отсутствия серверных задержек. Обычные жители пока атакуют на охране, царь
вступает Воеводой без неё. Всеобщая самооборона отдельно в TODO, не готова.

# D-074: queue capacity fix and read-only director diagnosis

Read docs/dezertovia/CIVIC_REQUEST_CAPACITY.md. Implemented in fix/civic-request-capacity.
Requests use active sites+pending applications, public3/private1; no legacy deletion.
Civic error text localized. No new deployment yet; LIVE0.7.1/e5de51e unchanged.
Director audit38875853709: enabled/Propose, recent none decisions,2budget skips,
no service failure. Hourly owner digest is an idea, not new active messages.

# D-073 /0.7.1 INSTALLED16.10.2026 и независимо проверен

Владелец явно разрешил исправление Telegram заявок и выпуск0.7.1 сейчас;
D-071/D-072 repository-only hold для данного пакета снят этим поручением.
LIVEgamee5de51ed04ff8f3b5cff1c5befb2032fa42635da, bot payload source7458b6bed439c19ce8be56db799911e1281f80d1.
Preflight38873012466 PASS; install38873092159 PASS; separate check38873661537 PASS.
Install verification2026-10-16T08:25:08.3536159Z, independent check2026-10-16T08:26:14.6780507Z.
116→116профилей,0потерь навыков/характеристик/славы;
118аккаунтов и118ExternalLogins сохранены,24миграции.
PG/SQLite/обаconfig/snapshot backups созданы. Hospital/Almighty true, Propose сохранён.
Game CI38871373656 SUCCESS387Domain+229API+125browser/1skip, frontend/DockerPASS;
botCI38871375837 SUCCESS828tests/client/runner, serverSKIPPED; operator22PASS.
PR45 mergedacfd0c3, botPR39 mergedcd653bd; runtime деревья равны tested source.

Настоящие заявки build/upgrade/demolish царю в личку, worker15с, до решения
не создаёт площадку. Bot OWNER_IDS + точная Telegram-привязка владельца,
повторное одобрение идемпотентно, обычные требования стройки проверяются снова.
Старая очередь27заявок принята; separate check подтвердил4реальные Telegram
квитанции Sent. Остальная очередь рассылалась постепенно; housing не входит.
Отчёты батюшке теперь сохраняют автора и код аккаунта только в личных карточках.
Установлены вместе7bot-файлов: app/db/feedback/feedback_identity/feedback_report/
almighty/civic_notifications. Установленный AI-budgetfix и town_proxy/llm/ai_budget
проверены по hashes и сохранены. Свечи/ключи/вещи/стройки/лечение не сбрасывать.
Нет синтетических сообщений, модельных проб, платежей или тестовых волн.

Новый операторpost071_release.py / post071-candidate.json, не старый rollout0.7.
Существующий exact source повторно НЕ устанавливать, только проверка со снимками.
Фактический факт — INSTALLED; старые ready/hold/candidate ниже являются историей.

# D-073:0.7.1 deployment authorized16Oct

Owner requested missing construction notices fixed AND0.7.1 installed now.
This supersedes D-071/D-072 repository-only holds for the combined release.
Read docs/dezertovia/CIVIC_TELEGRAM_0_7_1.md. New exact CI/payload/operator/backups/independent preservation
check required; never replay old0.7 trigger. No progress resets or synthetic
player messages. Actual pending civic notices to owner are authorized.

# D-072: идентификация автора отзыва, repository only

Владелец16.10 просит добавлять к багу/репорту батюшке автора и игровой код.
В feat/feedback-game-identity добавлен read-only /api/feedback/identity:
Telegram ID → существующий AccountId/CitizenId, прежняя bridge-авторизация.
См. docs/dezertovia/FEEDBACK_GAME_IDENTITY.md и парную bot-ветку. Без привязки
по нику, без изменения профилей/прогресса/свечей. Новая установка не разрешена.
Проверенный0.7.1/573af93 ниже — отдельный кандидат; новые изменения пока в ветке.
LIVE0.7/0540a2a не менялся. Номер за эту правку не повышаем.

# D-071:0.7.1 проверен в репозитории, НЕ установлен

Exact source573af93ccb2892b7e07c48529f8c387b63237cab, CI38864253995 SUCCESS:
387 Domain + 224 API + 125 browser PASS / 1 штатный skip; lint/build/Docker PASS. Public PR44.
Пакет: остановка активной/очередной нежити, Воевода/царь и команды, береговой
стык забора, управление старыми площадками/25% salvage первоначальному владельцу,
отступы/крестик, Dcoin/золотая иконка/личный wallet HUD. Питание автоматическое;
по жалобе об отсутствии кнопки уточнены тексты, алгоритм еды не менялся.
См. docs/dezertovia/RELEASE_0_7_1.md и POST_0_7_IMPLEMENTATION.md.
LIVE остаётся0.7/0540a2a. Новая установка НЕ разрешена; старые rollout/pins
не запускать. Все прежние pending/PLAN ниже — история.

# D-071: реализация пакета0.7.1 разрешена, проверка кандидата

16.10.2026 владелец после полного планаD-070 сказал «Делай». Реализация
в feat/post-0.7-fixes, по docs/dezertovia/POST_0_7_FIX_PLAN.md. Сверяться с
POST_0_7_IMPLEMENTATION.md. Сначала весь пакет, затем общая проверка.
Пакетный номер0.7.1; установленный LIVE0.7/0540a2a этим не меняется.
Работают API/Domain/React/Pixi основы; Dungeon служит образцом Воеводы/команд.
Новый запуск старого rollout0.7 запрещён. Установку этим поручением не начинать.
Сохранить частные материалы/владение, прогресс и лечение; без автоматического
сноса старых площадок. Самооборона всех жителей остаётся отдельной идеей.

# D-070: единый план0.7.1 и небольшие шаги версий

16.10.2026 владелец попросил полный план последних замечаний и запретил
ненужные скачки версий. Читать `docs/dezertovia/POST_0_7_FIX_PLAN.md`:
COMBAT-02A–D (берег/атака царя/крестик/усыпление), LAND-02 (старые личные
площадки и разборка), UI-06 (отступы), UI-07 (Dcoin/иконка/балансы на карте).
Dungeon — источник готового управления, царского Воеводы и проверки владения;
сохранять геометрию и устойчивое состояние деревенского RaidBattle.
Сейчас PLAN/docs only: реализация, изображения и новая установка не выполнены.
LIVE остаётся0.7/0540a2a; предыдущий выпуск D-069 уже завершён, не повторять.

Правило версий: следующий исправляющий пакет0.7.1, затем0.7.2 и далее;
после0.7.9 можно0.7.10. Документы/коммиты/тесты сами номер не повышают.
0.8 — осмысленный отдельный этап,1.0 — отдельное решение о готовности.
Текущий package0.7.0 пока не менять. Перед настоящим выпуском согласовать
один номер в package/lockfile/настройках/release metadata/патчноутах.
Самооборона всех жителей пока предложение, не утверждённое расширение.
Не сносить старые площадки автоматически; сохранять владельцев/вложения.
Тестовые и реальные свечи различать; Dcoin — переименование UI без конвертации.

# D-069 / 0.7 INSTALLED16.10.2026 и независимо проверен

LIVEgame0540a2a006d2c72b580b37f74387f1e53e68f9eb, version0.7/engine2.
Preflight38853486600 PASS; install38853561064 PASS; independentcheck38853958496 PASS.
APIhealthy05:04:48UTC, check05:05:31UTC, не раньше разрешённых05:00UTC=07:00Oslo.
113→113профилей,0потерь привязок/славы/навыков/характеристик;22миграции.
PG/SQLite/config/snapshot backups созданы. Telegram/health/routes PASS,
Hospital/Almighty true, Propose сохранён. Новый клиентский GamePage загружается.
Bot ONLYalmighty.py из3f7038c, SHA256748acaf68f5e1a739b2e0cb7e284e6206b0e5fcd69227deab7e81f1e23d1867f;
установленный AI-budgetfix сохранён, старые app/town_proxy/llm не копировались.
Synthetic messages/model probes/payments/forcedraids0. Не запускать старые deploy.
Оба game CI38817618664/38851717640 SUCCESS; bot38811556338 attempt2 SUCCESS816tests,
operator15PASS. READY/HOLD ниже — история, не текущее состояние.
Public release record: docs/dezertovia/RELEASE_0_7.md; private result:
deploy/LIVE_COMBAT_RELEASE_0_7_RESULT.json. Если exact source уже на сервере,
только check с существующим snapshot, повторная установка не нужна.

# D-069 — повторные проверки16.10 PASS, выпуск ещё не выполнен

По запросу «Перед выкладкой проверь не раз всё» повторный full CI38851717640
наТОМ ЖЕ0540a2a SUCCESS380Domain+217API+121browser/1skip, buildsPASS.
BotCI38811556338 attempt2 на3f7038c SUCCESS816tests, serverjobSKIPPED.
Оператор15PASS и требует predeploy_ci38851717640 вместе с первичным38817618664.
Подробности в docs/dezertovia/RELEASE_0_7.md и приватном PREDEPLOY_RECHECK_0_7.md.
Candidate.ready=true после второго успеха; LIVE61d726c не менялся. Выпуск только
16.10.2026 07:00Europe/Oslo=05:00UTC; actual backups/report/deploy/independent
preservation check обязательны. Не выдавать проверки за фактическую установку.

# D-069: 0.7 READY, scheduled16.10.2026 07:00 Europe/Oslo

Full code/checks completed. Public PR37 MERGED3e757dae87a4f3984b26f71104c7994abb0b06a9,
tree identical to tested runtime0540a2a006d2c72b580b37f74387f1e53e68f9eb.
CI38817618664 SUCCESS:380Domain+217API+121browser/1expectedskip; frontend/DockerPASS.
Bot source3f7038c886e8ea37c98191a32ba0c3f558b05e69, CI38811556338 SUCCESS816tests;
PR38 merged canonical0054f4c. Payload ONLYalmighty.py over installed budgetfix3fd0c1c.
See docs/dezertovia/RELEASE_0_7.md for final scope/pins/readiness, and implementation
log for history. Includes AI-SPEECH-01: no director service JSON in Father chat.

INSTALLATION NOT YET DONE. LIVE0.6.1/61d726c verified externally HTTP200, Telegram
login true/dev false. Scheduled one-time task confirmed16.10.2026 07:00Europe/Oslo
=05:00UTC; no earlier. Private bolig-agent ops/almighty-release contains reviewed
live-combat operator/candidate/payload. Trigger live-combat-request.json must NOT
exist before deadline; report→deploy→independent check, exact successful CI, backup,
profile/skill/stat preservation. Do not run old request/pins or private main deploy.
No synthetic messages/payments/raids, no progress reset. Append actual deployment
receipt only AFTER release. Earlier checkpoints/PLAN/CI failures below are history.

# D-069 / 0.7 candidate: implemented, final CI then scheduled release

Full D-068 code batch and AI-SPEECH-01 are implemented. Public PR37 contains
server individual battles, real terrain/gates/breaches, serial waves and combat
UI, energy/cycle/boundary/storage fixes. See docs/dezertovia/COMBAT_AND_FIXES_IMPLEMENTATION.md
for exact code/checkpoints. Bot PR38 merged, tested source3f7038c / CI38811556338
SUCCESS816 tests; only almighty.py runtime payload, preserving installed budget
repair3fd0c1c. Previous game CI passed but final game source needs exact CI.
LIVE game remains0.6.1/61d726c. Automatic release scheduled16.10.2026 07:00
Europe/Oslo =05:00UTC, NEVER earlier. Private ops candidate may be prepared now;
do not create deploy/live-combat-request.json until the deadline. Release only
ready exact pins with successful CI, backups, independent preservation check.
No artificial player messages/raids/payments, no progress or stat resets.

# D-069: реализация разрешена; установка не раньше07:00 Осло

Владелец15.10.2026 разрешил весь код и тесты пакета D-068. Реализовать в
feat/live-village-combat: COMBAT-01/UI, ENERGY-02, UI-05, CYCLE-02, ENERGY-03,
LAND-01, STORAGE-UX. Сначала закончить код, затем проверки. Установка разрешена
на16.10.2026 07:00 Europe/Oslo (05:00UTC), НЕ раньше. До времени LIVE0.6.1
не менять. Разрешение на реализацию снимает прежний PLAN-only, но не временной
барьер. Не устанавливать непрошедший точный CI код, не сбрасывать прогресс/
реальные свечи, не запускать тестовые волны/сообщения на production. См.
docs/dezertovia/COMBAT_AND_FIXES_IMPLEMENTATION.md. Никакая установка ещё не сделана.

# D-068: единый план патча — новая оборона и связанные исправления

Владелец: «Пиши сейчас весь этот план, будем все править». Состав/порядок/
приёмка: docs/dezertovia/COMBAT_AND_FIXES_PATCH_PLAN.md. Вместе D-066 новая
боёвка и боевой UI; ENERGY-02, UI-05, CYCLE-02, ENERGY-03, LAND-01; компактная
STORAGE-UX без смены ёмкостей. Директор один, костёр ориентир, состав по готовности,
отдельные серверные враги, вход/пролом, автоатака/тап. Полная экипировка/рюкзаки/
эмбиент/рулетка — следующая очередь. Сначала согласованный пакет кода, потом
общие проверки. Сейчас создан план, код/сервер не менялись. LIVE0.6.1 остаётся.
Для начала реализации читать этот план и последние сообщения; не повторять
старые установки и не выдавать PLAN за deployed. Номер нового выпуска не назначен.

# D-067: сегодняшняя предложка сверена с0.6.1, только TODO

Прочитаны все88 записей приватного feedback-report38803828120,34 за15октября,
#55–88;11 новых после прежней сверки (#78–88). См.
`docs/dezertovia/FEEDBACK_REVIEW_2026_10_15_EVENING.md`.
Первый приоритет: ENERGY-02 (#87: усталость до reload после регенерации) и
UI-05 (#88: глаз перекрывает сообщение), оба после0.6.1. CYCLE-02/#82,
ENERGY-03/#78 и LAND-01/#86 — проверка/воспроизведение, не объявленные готовые
исправления. Упоминания/передача/камыш/12ч regen/ники/основной запрет личной
стройки внутри уже есть. Новые идеи не автоматически одобрены. Сейчас docs/TODO,
никакого deploy/сброса/сообщений/пометки отзывов в боте. D-066 остаётся PLAN.

# D-066: план живой обороны и адаптивных нападений

Владелец просит сохранить обсуждение, сейчас только PLAN. Читать
`docs/dezertovia/RAID_REWORK_PLAN.md`: одна выбранная Всевышним сторона,
отдельные враги/HP/удары, проход/пролом, автоатака и выбор цели, состав по
готовности деревни. Костёр только ориентир центра, НЕ цель уничтожения.
Один ИИ, тактические скрипты, существующий Propose/одобрение сохранены.
Параметры и политика ущерба отмечены как предложения. Старый первый этап
«одна пачка + общий расчёт» не удовлетворяет новому запросу. LIVE0.6.1 неизменён;
разрешение D-064 на его выпуск не разрешает новую боёвку. Реализацию не начинали.

# D-064: 0.6.1 installed and independently verified

15.10.2026 владелец: «Все выкладывать». D-063 repository-only hold снят для
полного проверенного патча. LIVE game61d726c184315d54c3d2c86b4984954bf818e31b,
bot6e87f6054ab6700f552f6d8fe2c56a3b41ee33d6. CI38795831107 PASS:
349 Domain+207 API+107 browser/1 штатный skip; frontend/Docker PASS.
Bot CI38792441921:801 PASS. Установка38797652832 и независимая проверка
38798016055 PASS:111→111 профилей,0 потерь прогресса,21 миграция, PostgreSQL/
SQLite backup. Всевышний Propose сохранён, Telegram auth работает. Реальные
свечи/ключи не менялись, синтетических сообщений/платежей не было.
Посадник подаёт заявки на любые доступные здания; одобряет царь. Четыре bot
файла, включая llm.py, установлены. См. docs/dezertovia/RELEASE_0_6_1.md.
Старые candidate/hold ниже — история. Не повторять установку по старым заданиям.

# D-063: candidate0.6.1 — menus, energy, construction and AI diagnostics

Текущее поручение15.10: docs/dezertovia/MOBILE_NEEDS_0_6_1.md. Только кандидат
в репозитории, без новой установки. LIVE0.6 изD-062 остаётся. Энергетик300ДЗК/
3тестовыхсвечи,12чобщийоткат; взвар4–16/120–480мин только с одобрением царя.
Критичный дефект EF JSON/повторного Save и rollback чужой стройки исправляется
с проверкой сохранности. Для bot нужны almighty.py И llm.py, старый payload
не подходит. Прогресс/реальные свечи не сбрасывать и не менять.

Дополнение D-063: посадник отправляет заявки на любое доступное здание
(включая больницу/замок), царь утверждает; до решения нет площадки/резерва
деревьев. Повторная полная проверка при одобрении. См. раздел дополнения в
MOBILE_NEEDS_0_6_1.md. Всё ещё repository-only, LIVE не менять.

# D-062: 0.6 released and independently verified

15.10.2026 D-061 выполнено. LIVE0.6, game143bf9ad569d2bc01d43356c8fc21467c59e544a,
bot9a9c8c27233244a9e0e9a709e4a2ff1f12ad0374. CI38784399850 PASS:
346 Domain+196 API+91 browser/1 штатный skip; frontend/Docker PASS.
Install38785792763 и independent check38786456457 PASS:110→110 профилей,
0 потерь прогресса,19 миграций, PostgreSQL/SQLite backup. Telegram/больница работают.
Всевышний включён Propose; первый естественный анализ модели успешен, эффекты
требуют согласия царя. Энергия12 реальных часов при100% условиях; предупреждение
об обмороке и понятный таймер. Нет сброса/настоящих списаний/синтетических сообщений.
Game PR34 включает PR33 и слит в main; bot PR35 слит в свою каноническую ветку.
См. docs/dezertovia/RELEASE_0_6.md. D-058 отменён D-061. Старые PLAN/LIVE ниже — история.
Не повторять старые установки. Новое runtime требует нового задания, docs не деплоить.

# D-061: approved energy adjustment and 0.6 rollout

15.10.2026 владелец: «Добавь эти правки, если наш патч готов для выгрузки то запускай».
Применить восстановление полного запаса за12 реальных часов при100% условиях,
предупреждение перед третьим уставшим действием; без бонуса25% после обморока.
Срок обморока12 игровых часов, расход/медленная добыча/стройка сохранены.
Разрешена выкладка готового проверенного0.6 с backup, совместимым bridge бота,
сохранением прогресса и включением Всевышнего в Propose по D-056.
Это новое поручение снимает D-058 о запрете установки. Не сбрасывать данные,
не менять настоящие свечи/налоги, не делать синтетических Telegram сообщений.
Точный SHA/CI и итог установки записать в RELEASE_0_6.md и приватный release record.

# D-060: вся очередь реализована в ветке

15.10.2026 «Делай все правки»: восемь пунктов TODO_EXECUTION_GUIDE.md реализованы
в feat/kingdom-economy поверх e7fbe91 / PR33. Читать
docs/dezertovia/TODO_IMPLEMENTATION_0_6.md: код, тестовые числа и проверки.
Полный итоговый CI — на публикуемом SHA; старые PLAN ниже исторические.
Налог preview, свечи тестовые, прогресс сохранён. Бот не изменялся.
D-058 действует: НЕТ server install/enable/настоящих Telegram сообщений.

# D-059: инструкция выполнения TODO

15.10.2026 по просьбе владельца подготовлен docs/dezertovia/TODO_EXECUTION_GUIDE.md:
восемь пунктов, файлы, порядок, приёмка; ВСЁ под 6.1 SOL Высокое. Это только инструкция,
не поручение начать все новые функции. Новое runtime не менялось.
D-058 ниже продолжает запрещать server deploy/enable/настоящие сообщения.

# D-058: СТОП ВЫКЛАДКЕ

15.10.2026 владелец сказал «Пока не выкладывать». Продолжать код/проверки/
GitHub, НЕ устанавливать game/bot, НЕ включать Всевышнего на сервере и НЕ
отправлять настоящие owner proposals. D-056 разрешает реализацию, но его
часть о выпуске/включении отложена этим новым решением. LIVE0.5.3 не меняется.
Game PR33, bot PR35; только CI/репозитории до нового поручения.

# D-056: реализовать и включить Всевышнего

Историческое поручение D-056: реализовать весь проект и режим Propose.
Включение на сервере ОТЛОЖЕНО решением D-058 выше.
Один ИИ анализирует, присылает владельцу через батюшку предложения с кнопками
согласия/отказа. Это явно разрешённые личные уведомления, не групповой флуд.
Минута на дешёвую проверку,15мин на модель, важные события не чаще5мин.
Сначала весь код, затем итоговые проверки/выпуск с сохранением прогресса.
Реальные налоги/кошельки не менять. Старые PLAN/Off не отменяют новое поручение.

# D-055: подробный план имплементации Всевышнего

15.10.2026: владелец просит план. Основная техническая точка:
`docs/dezertovia/ALMIGHTY_IMPLEMENTATION_PLAN.md`. Один ИИ, общая память,
инструменты/режимы/lease/ledger/scheduler/чат/API/приёмка/включение.
Директор остаётся PLAN; код и сервер этим поручением не меняются.

# D-054: Всевышний — один ИИ

15.10.2026: владелец уточнил: бухгалтерия, вражеское командование, сложность,
ивенты и прочее — обязанности ОДНОГО директора, не отдельные ИИ/агенты.
Единые личность/память/замысел/журнал, серверные модули исполняют решения.
ALMIGHTY_DIRECTOR_PLAN.md обновлён; существующий runtime не менялся.

# D-053: таймер нападения и срок лечения

15.10.2026: исправить преждевременную ходьбу зомби/скелетов и огромные числа
минут лечения. До strikesAt — только предупреждение, затем показ/атака;
серверные сроки волн/боевой расчёт не менять. Сроки лечения в реальных днях,
часах, минутах с явной подписью; не пересчитывать без реального cycle config.
Изменения в feat/almighty-patchnotes / PR33, без deployment. Проверки и детали:
docs/dezertovia/RAID_MEDICAL_FIXES.md. LIVE остаётся прежней0.5.3.

# D-051/D-052: Всевышний и история обновлений

15.10.2026: директор жизни/сложности/ивентов — PLAN в
`docs/dezertovia/ALMIGHTY_DIRECTOR_PLAN.md`. Объединить командира, бухгалтера и
батюшку; сервер валидирует ограниченный каталог. Бухгалтер только советует,
чат не даёт прав; числа предложены, автомат не включён.
D-052: история по нажатию версии в настройках, без deployment.
Ветка feat/almighty-patchnotes; существующая0.5.3 остаётся LIVE.

# D-050: 0.5.3 выпущена и проверена

15.10.2026 выполнено D-049. LIVE0.5.3, runtime sourcea980d8daeb2d3ff10609df7894444d57c8992c74.
PR32 слит в main (merge724484d). CI38736161629:329 Domain +180 API,
71 browser PASS/1 штатный skip; frontend/Docker PASS. После установки100→100
профилей, без потери личности/славы/навыков/опыта;17 миграций, больница100
подтверждений/0 недоступных, Telegram работает. Никакого сброса/тестовых оплат
или сообщений. Закрыты UI-01–04, BUILD-01 и ENERGY-01 (уточнение правил/цены).
Документ: docs/dezertovia/RELEASE_0_5_3.md. Исторические PLAN/LIVE ниже устарели.
Не повторять старую0.5.2 выкладку/активацию. Новые идеи остаются backlog.

# D-049: выполнить исправления и выложить проверенный выпуск

15.10 владелец сказал «Правь и выкладываем». Разрешены реализация D-048,
итоговые проверки, GitHub и затем deployment с резервными копиями/сохранением
прогресса. Ветка fix/post-release-regressions. Старые «только план» D-046–048
перекрыты. Не менять сложность/тарифы/очередь, не сбрасывать данные.
Сначала весь код, затем проверки. Версия исправлений0.5.3.

# D-048: подробный план исправлений, пока без реализации

Владелец попросил составить план для следующего захода.
Главная точка: docs/dezertovia/REGRESSION_FIX_PLAN_0_5_2.md.
Порядок: весь UI-пакет → серверные резервы стройки → энергия/тексты → итоговые
проверки. UI-01–04, BUILD-01, ENERGY-01. Сложность/очередь/прогресс сохранять.
При госпитализации резерв нельзя освободить без безопасного правила возврата
оплаченного цикла; исходно держать вместе с паузой. Старые циклы не терять.
Сейчас только docs, без кода/тестов runtime/выкладки. 0.5.3 лишь предложение.

# D-047: проверить UI-регрессии и отзывы, без нового выпуска

15.10 владелец сообщил о чате поверх здания, характеристиках супруга, отступе
подписей и попросил прочитать отзывы бота. Расследование/TODO:
docs/dezertovia/FEEDBACK_TRIAGE_2026_10_15.md. 77 записей прочитаны, новые53–77
разобраны; UI-01–04, BUILD-01, ENERGY-01. Код игры/сервер не менялись.
В приватном ops-репо добавлен вывод уже отредактированных текстов без имён/ID
пользователя: хранилище артефактов заполнено. Это чтение, не deploy/Telegram.
Не считать PLAN исправлениями и не принимать ускорение/ослабление правил из отзывов.

# D-046: карточка персонажа — расследование и TODO, без реализации

15.10.2026 владелец попросил найти причину повторов «Супруг» среди характеристик
и записать в TODO. Подтверждено: CharacterSheet.tsx:119–124 повторяет блок
супруга/жилья/состояния внутри citizen.stats.map; числа берутся из своего citizen.
План P1 и приёмка: docs/dezertovia/NEXT_TODO.md, D-046. Пока не исправлено.
Это поручение не разрешает новый deploy или изменение игрового кода.
Последний выпуск0.5.2: tag v0.5.2, source29e4ba057ea903e6ef827af64892c599c16818b9;
исторические записи ниже о кандидате0.5.2/старом LIVE0.5.1 устарели.

# D-043: выпуск0.5.2 разрешён

15.10 владелец: «Загружаем обнову на сервер чтоб народ начал тестировать».
Разрешены согласованный bot/game выпуск, резервное копирование, additive
миграции и включение больницы после установки адаптера. Не сбрасывать прогресс,
не выполнять тестовые платежи или сообщения в Telegram. Перед изменениями
read-only проверка; после точные SHA, proxy/auth/health и базовая сохранность.

# D-042: пациенты лежат, раненых переносят после боя

В ходе реализации владелец уточнил: персонаж должен лежать в больнице с причиной
лечения; добавить переноску раненых после боя. Код в GameService.Rescue,
RescueRules, VillageWorld, WoundedTransport migration. Другой игрок переносит
без профессии, не лечит; fallback санитары через5 реальных минут, до постройки
больницы временный лазарет у костра. Native InjuredUntil и duel Recovery раздельны.
Offline пациенты видны, поза лёжа, причина+таймер. Ряд коек отвергнут: разнесённые
места на карте (до4, свой всегда) и отдельные полные карточки всех в больнице.
Кандидат0.5.2, проверки/состояние в IMPLEMENTATION_LOG.md. Пока только РЕПО.

# D-041: весь пакет разрешён к реализации, без deployment

15.10 владелец сказал «Выполняй наш полный план». CURRENT_MASTER_PLAN/BALANCE_PROPOSAL
теперь рабочий объём. Все18 блоков + крупные undead; ветки feat/cooperative-update
игры и бота. Сначала весь код, затем итоговая проверка/тесты. Новые числа baseline
применять с сохранением прогресса, без live/реальных списаний. Не останавливаться
между блоками и не трактовать старую запись «только план» как запрет текущей работы.

# D-039: начать с актуальной сохранённой памяти

Главный текущий план: docs/dezertovia/CURRENT_MASTER_PLAN.md. Продолжение после
сжатия: SESSION_HANDOFF.md; цифры: BALANCE_PROPOSAL.md; порядок: BATCH_UPDATE_PLAN.md.
18 блоков общего пакета ещё не реализованы. Сейчас владелец попросил сохранить
план, не начать код/deployment. Бот-часть tone PR33 уже LIVE, больница/обводки/
удалённые кнопки только готовы в отдельных PR. Старые TODO не переопределяют это.

# D-037: пакет правок, тесты после завершения кода

Следующее поручение на реализацию по BATCH_UPDATE_PLAN.md выполнять единым
согласованным пакетом: сначала закончить правки, затем общий поиск ошибок и
тесты. Не гонять полный набор после каждой кнопки/мелочи; исправления проверять
целево, полный повтор по риску. Не отключать обязательный финальный CI и не
проверять сохранность ПОСЛЕ изменения live. Сейчас только план, не реализация.

# D-027: восстановление маршрута игры после обновления бота

14.10.2026 владелец сказал «Чини». Бот из старой feature-ветки затёр proxy,
из-за чего адрес игры открыл legacy frontend, хотя .NET upstream был здоров.
Маршрут восстановлен, bot source получил preflight/health guard (PR31).
Deployment38603201121 и final check38603465467 успешны. Игра остаётся0.5.1,
release ad88be344908a7fb42bc434ce89aad94c92d1665. Без сброса/новой механики.
Старые bot-ветки перед deployment rebase: в них guard ещё отсутствует.
Больница отложена владельцем на время инцидента, не отменена.
Подробности: docs/dezertovia/INCIDENT_ROUTING_2026_10_14.md.

# D-026: небольшие бонусы восстановления (только план)

Уровень больницы + личный навык пациента сокращают срок. Рекомендации процентов
не утверждены. Снимок один раз при поступлении, общий authoritative срок у бота;
цены и часовые эффекты платного лечения сохраняются. См. HOSPITAL_PLAN.md.

# D-025: больница — сначала план и графика

Владелец выбрал больницу следующей задачей. Подготовить HOSPITAL_PLAN.md,
найти отдельный hospital asset; при отсутствии нарисовать день/ночь. Это ещё
не game integration/списание/выпуск. Графика art/hospital-proposal — предложение,
нормализовать/запечь обводку перед runtime. Сохранять native Injury/Remedy,
дуэльный срок отдельно; оплата у бота, с durable idempotency. Без deployment.

# D-024: обновить TODO, без изменения игры/сервера

Последние мысли владельца сравнить с кодом; актуальный порядок/статусы в
NEXT_TODO.md и VIEWER_BACKLOG.md. Энергия/энергетики уже есть частично,
больница у бота есть; не дублировать. Мост — жалоба для воспроизведения.
Замок поднят в ближайший план. Срок обморока игровые часы, дуэли реальные.
Тарифы/свечи дуэли из действующего сервиса бота. Это только план, без списаний.

# D-022: компактная кнопка чата (0.5.1)

Убрать полноширинную полосу и население из чата; жители/онлайн наверху «Город».
Чат/уведомления не перекрывают голосование и другие окна. Сохранять SignalR,
историю, черновик, режимы и счётчик непрочитанных. Без изменения данных игроков.

Версия текущего выпуска0.5.1 по решению владельца. Источник номера —
web/package.json (0.5.1); SettingsPanel отображает0.5.1. Обновлять номер и
release record вместе при следующих выпусках.

# D-021: режимы чата и уведомления

Владелец сказал «Приступай»: скрытый/четверть/full, удобные кнопки, toast как0.4.
Сохранять существующий SignalR, историю/отправку/события/NPC и оформление.
Сверяться с CHAT_MODES.md. Без сброса/свечей и manual сообщений игрокам.

# Поручение D-020: реализовать пилот стройплощадки хижины

Владелец сказал «Делай» после подробного плана CONSTRUCTION_VISUAL_PLAN.md.
Подключить стадии хижины, сохранить исходники, материалы сбоку и ночной фонарь.
Серверные стадии/время/стоимость и прогресс не менять. Проверить до выпуска.

# Поручение14.10: ночные здания, чат и мобильные исправления

Владелец явно разрешил подключить все26 подготовленных пар зданий день/ночь
с плавным переходом и вернуть Срамославу общий игровой чат. Исправить скриншоты
Telegram: описание поручения во всю ширину над кнопками; ввод количества не
сбрасывать при стирании/обновлении склада. Сверяться с NIGHT_BUILDINGS_CHAT.md
и D-019. Старый остановленный художественный блок не запрещает это поручение.
Не сбрасывать игру, не менять реальные свечи и не посылать сообщения в Telegram.

# Текущее уточнение: один мир и Срамослав

Владелец временно оставил только Дезертовию: скрыть выбор/создание городов,
ограничить другие миры конфигурацией, сохранить их код/данные для возврата.
Упростить Telegram вход, убрать сломанный widget с нашего экрана и полностью
выключить вход по почте в deployment (D-017). Телефонные кнопки по реальному числу.
Заменить Седого на Срамослава и подключить существующий ИИ бота для беседы
в игре. Узкий private bridge, без game commands/денег/сообщений в Telegram.
Эти новые явные решения имеют приоритет над историческим сохранением UI миров.

# Текущее поручение14.10: первый запуск

Владелец разрешил по очереди начать адаптацию с Telegram-авторизации и запуска
обновлённой игры на своём сервере, включая переключение нынешнего адреса.
Сохранять все функции автора. Проверять отдельно перед переключением;
старую базу не сбрасывать, реальные свечи/другие игры не менять.
Документальные ограничения старых этапов ниже не отменяют это поручение.

# Инструкции для работы над Дезертовией

Начинать с docs/dezertovia/README.md и DECISIONS.md; найти нужные DZ-ID в
RULES_AND_SOURCES.md и ADAPTATION_MATRIX.md, затем читать реальные функции.
Последнее явное решение владельца имеет приоритет над историческим backlog.

- Инженерная основа автора — .NET Domain/Infrastructure/Api, EF/PostgreSQL,
  React/PixiJS/Vite/SignalR. Адаптировать, не переписывать проект и не ставить
  старый Python-двигатель вторым владельцем игрового состояния.
- Сохранять сервисы, каталоги, lifecycle, auth, ORM, renderer и тесты основы.
  Исправлять параметры/правила и добавлять недостающие поля/модули точечно.
- Новый художественный блок остановлен; существующий UI/редактор автора остаётся.
  Не импортировать старые shell.js/Canvas renderer и не рисовать без нового поручения.
- Все функции автора сохраняем доступными, включая новые набеги/данжи/миры.
  Наша Дезертовия — основной мир; медленная работа, профессия2ч,
  queue1+1, общий запас внутри/свой снаружи, последнее решение царя.
- Основные свечи, донаты, модерация и другие игры бота отдельно. Экономика
  Дезертовии тестовая; налог preview, AI read-only. Права решает сервер, не модель.
- Документы различают LIVE старой версии, BASE, BRANCH, PLAN, LATER, PAUSED/EXCLUDED.
  Нельзя выдавать найденный/написанный код за проверенную и выпущенную функцию.
- Реальные ключи/.env/БД/бэкапы/сырые отзывы/рабочие cookie не включать в git,
  Actions artifacts, публичные планы и вывод команд. Не давать deployment ключи
  сторонней ветке. Все upstream изменения принимаются выборочно после проверки.
- Работать в отдельных ветках. Не force-push upstream, не зеркалировать поверх
  своих изменений. Выпуск/сброс/переезд live не следуют из поручения написать план.
- Для каждого этапа обновлять decisions, матрицу и progress.json вместе с кодом;
  запись реальной проверки обязательна перед verified/released. AGENTS.md и
  CLAUDE.md сохранять согласованными. Коммиты на английском, без сторонних подписей.

Историческое поручение до запуска: документация без изменения кода. Перекрыто текущим поручением Telegram/Oracle выше.

Обновление контекста14.10: перед прежней матрицей читать
docs/dezertovia/CURRENT_ADAPTATION_LIST.md. Отдельно просмотренe23bc94,
слит в текущую release-ветку. Chat/Presence/Governors/боевые системы
теперь существуют; не создавать их повторно. По D-013 сохранять их доступность,
адаптируя правила, вместо отключения. По D-014 чат0/¼/full — явная задача UI.
Первый этап Telegram/Oracle выпущен, сверяться с TELEGRAM_LAUNCH.md. Другие правила/UI ещё адаптируются по очереди.

Последние решения: D-013 — НЕ отключать новые функции автора; D-014 — чат
по нашему образцу (скрытый со всплывающими/четверть/fullscreen). Сверяться с
CURRENT_ADAPTATION_LIST.md. Сохранение инженерии включает сохранение возможностей.
