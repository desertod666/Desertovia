# Разработка Дезертовии 1.0

20.10.2026, D-104. Владелец разрешил начать реализацию: «Но пока это все тестовый
сервер. Так что можно. Начинай работу если готов». Это перекрывает запрет кода
в D-103. Единый backlog сохраняется; открытые продуктовые решения уточняются
до изменения зависящих от них правил. Исторические release requests не запускаются.

## Первая цепочка

M0: свежая база, SDK/Node, синтетическая БД, baseline и воспроизводимая отладка.
PKG-T010: нынешний `Coins` отображается как хозяйственное золото, а тестовые
свечи остаются отдельным счётом. API добавляет `currencies` с идентификаторами
`economic-gold → DZK`, `test-candle → TEST_CANDLE`. Поля `coins`, `testCandles`,
ключи журналов, цены, баланс/правила операций остаются совместимыми. Старый
сервер без metadata поддерживается клиентом. Материальный `ResourceKind.Gold`
и основной свечной счёт батюшки не конвертируются. Перенос entertainment Dcoin
и судьба свечного лечения принадлежат будущей PKG-T060/решениям PKG-D105/116.

Новый денежный значок использует существующую графику золота. Сумма денег
остаётся кошельком, руда/материалы — inventory; одинаковый рисунок не создаёт
обмена ресурса на деньги. Старые patch notes сохраняют исторические названия.

## Рабочее окружение Windows и VS Code

- SDK из `global.json`: 10.0.300. Локально установлен в соседний
  `tools/dotnet` официальным dotnet-install.ps1, без изменения системного SDK.
- Node 24 берётся из bundled runtime Codex, если он доступен; иначе нужен
  установленный Node 24. `tools/dev.ps1` настраивает PATH только своего процесса.
- Расширение C# для VS Code установлено; `.vscode` содержит build/test задачи,
  запуск API и Chrome, compound `Desertovia: local game`, а также attach к API.
- Приложение: `http://127.0.0.1:5190`, API: `http://127.0.0.1:5180`.
  `tools/dev.ps1 -Mode Backend` и `-Mode Frontend` воспроизводят запуск из консоли.
  Для F5 остановить уже работающий экземпляр API либо выбрать attach к нему.

## База и внешние сервисы

Отдельный контейнер `desertovia-foundation-dev-postgres`, PostgreSQL17,
384MiB/0.5CPU. Его порт опубликован только на loopback Oracle:55432.
Локальный SSH-туннель:

```powershell
ssh -N -o BatchMode=yes -o ExitOnForwardFailure=yes -L 127.0.0.1:55432:127.0.0.1:55432 oracle-vps
```

Туннель использует уже настроенный SSH-профиль; ключи не находятся в репозитории.
Синтетические реквизиты из dev.ps1 относятся только к этому новому кластеру.
Локальный мир — `desertovia_foundation`, browser fixtures — отдельная
`desertovia_foundation_e2e`. Данные прежнего игрового контейнера не используются.
Development-вход остаётся только на локальном API. Telegram token/username/bridge
пусты, Hospital/Almighty выключены. Нет реальных сообщений, платежей или Gemini.

## API-тесты без Docker Desktop

По умолчанию factory сохраняет прежний Testcontainers-путь CI. Для Windows
можно явно задать `DESERTOD_TEST_POSTGRES` с доступом к выделенному loopback
кластеру. Factory создаёт БД `desertod_test_<guid>` и после завершения удаляет
только её. Имя БД из входной строки никогда не используется как тестовый мир.
Нелокальный адрес отвергается. Migration-тесты, самостоятельно создающие
PostgreSqlContainer, по-прежнему требуют Docker и проверяются в CI.

```powershell
$env:DESERTOD_TEST_POSTGRES='Host=127.0.0.1;Port=55432;Username=desertovia_dev;Password=desertovia_dev_only'
..\tools\dotnet\dotnet.exe test tests/Desertod.Api.Tests -c Release --filter FullyQualifiedName~KingdomEconomyApiTests
```

Эта переменная предназначена исключительно для синтетического кластера.
Существующий CI игры строит .NET/клиент/Docker и выполняет обе browser-части,
без установки на сервер. Bot main может запускать deployment, поэтому первый
этап не меняет бот и его main. Проверки и точные результаты —
`IMPLEMENTATION_PROGRESS_1_0.json`; отсутствие проверенного кейса не означает PASS.
