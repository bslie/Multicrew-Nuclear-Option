# Multicrew Nuclear Option

[English](#english) · [Русский](#русский)

Gunners and Weapon System Officers for [Nuclear Option](https://store.steampowered.com/app/2168680/Nuclear_Option/).  
Мод для Nuclear Option: стрелок и офицер вооружения (WSO) в одном экипаже.

Based on a fork of [ItsBongus/SimpleWSO](https://github.com/ItsBongus/SimpleWSO).  
Форк оригинального мода [ItsBongus/SimpleWSO](https://github.com/ItsBongus/SimpleWSO).

---

## English

Select a friendly aircraft in spectate, press **H**, become a WSO.

The terms *Gunner* and *WSO* are used interchangeably in this document.

### Features

- One-button enter/exit
- Gunner turret reticle (when a turret is selected) shown on the pilot screen for coordination
- Gunner sees vanilla cockpit MFDs / sensors (TacScreen, TargetCam) while in the gunner seat
- Pilot sees the gunner's live camera view on the cockpit target MFD while a gunner station is occupied
- Hit feedback (impact sound + crosshair marker) for the gunner, including remote gunner clients
- Kill rewards (XP / funds) split 50/50 between pilot and gunner for gunner-station kills; kill bonus sound plays for both
- Kill feed shows both crew names (e.g. `Pilot + Gunner destroyed …`) for shared gunner kills
- Separate target lists for pilot and gunner; can be pushed bi-directionally (Gunner ↔ Pilot)
- While turrets (Chicane gun, Ibis turrets) are selected by the gunner, the pilot cannot take control — stops the pilot from ripping control while cycling weapons
- When neither pilot nor gunner is controlling a turret, AI takes over like vanilla
- Works with AI or player-controlled aircraft
- Works with all modded aircraft (KAR, BOTE, etc.)
- Auto-creates camera entry positions for modded aircraft so you can place external cameras. Configurable in F1 or the config file (see below)
- Pilot mod presence check: joining a player-piloted aircraft as gunner requires the pilot to have this mod loaded and advertising presence (AI pilots do not need it)

### How to use

1. Install [BepInEx](https://github.com/BepInEx/BepInEx)
2. Download the latest release from [GitHub Releases](https://github.com/bslie/Multicrew-Nuclear-Option/releases) and place the `MulticrewNuclearOption` folder (containing `MulticrewNuclearOption.dll`) in your Nuclear Option BepInEx plugins folder: `steamapps\common\Nuclear Option\BepInEx\plugins`
3. Start the game through Nuclear Option Mod Manager (recommended) or Steam
4. Start or join a multiplayer or singleplayer lobby
5. Join a faction
6. Click on a player or AI friendly aircraft
7. Press **H** (default) to enter gunner view
8. Press **U** (default) to share targets Pilot → Gunner or Gunner → Pilot
9. Press **K** to cycle between gunner camera positions
10. Press **H** again to leave the gunner seat and return to spectate

### Limitations

- **Both the gunner and the pilot** (unless the pilot is AI) need this mod loaded
- WSO does not have flight control
- WSO does not have countermeasures
- More than one gunner in the same aircraft is untested
- The Medusa rattles in the gunner seat for unknown reasons

### Configuration

Camera positions (cycle with **K**) are set up for most vanilla two+ seater aircraft — often where the targeting pod sits (Cricket, Compass, Chicane). For the Ibis and Tarantula, extra views are on the gun positions (door guns / side gun).

You can change camera offsets, keybinds, and whether shared target lists **replace** or **append**, via BepInEx Configuration Manager ([GitHub](https://github.com/BepInEx/BepInEx.ConfigurationManager) / NOMM) — press **F1** in game — or edit:

`\Nuclear Option\BepInEx\config\com.bongus.multicrewnuclearoption.cfg`

Camera offsets are under **F1 → Multicrew Nuclear Option → CameraOffsets**. Each aircraft is its own collapsible group (e.g. **UH-90 Ibis**) with **Position 1–4**. New airframes get the same treatment after you gunner them once (reopen F1 if the group is not visible yet). Values are aircraft-local meters (`X` right, `Y` up, `Z` forward). Empty positions are ignored in-game.

#### Default controls

| Key | Config entry | Action |
| --- | --- | --- |
| H | `ToggleGunnerKey` | Possess gunner seat on followed aircraft / leave |
| U | `ShareTargetsKey` | Share targets with the other seat |
| K | `CycleCameraPositionKey` | Cycle configured gunner camera positions |

Overview video:

[![Multicrew Nuclear Option overview](https://img.youtube.com/vi/KmdFBY28gDw/0.jpg)](https://www.youtube.com/watch?v=KmdFBY28gDw)

License: **CC0** — do what you want with it.

### Changelog

#### 1.0.3

**Crew displays, combat feedback, and shared rewards**

- **Gunner MFDs / sensors** — `TacScreen` and `TargetCam` now initialize in the gunner seat (fixes black cockpit displays for remote gunners)
- **Pilot gunner feed** — while a remote gunner station is occupied, the pilot's target MFD shows a live reconstruction of the gunner's camera view (pose streamed over the network; no video upload)
- **Hit feedback** — gunners get vanilla impact sound and crosshair hit markers; remote gunner clients receive hit feedback via a dedicated network message
- **Shared kill rewards** — XP and funds from gunner-station kills are split **50/50** between pilot and gunner; both hear the kill bonus sound
- **Kill feed** — shared gunner kills show both names, e.g. `Pilot + Gunner destroyed …`
- **Networking** — `GunnerJoinMsg` now carries the gunner's `PlayerNetId` for reward attribution and kill-feed labels

**Install:** extract `MulticrewNuclearOption.zip` into `BepInEx\plugins\`. Both pilot and gunner need this build in multiplayer (AI pilot exempt).

#### 1.0.2

- Renamed the mod from **SimpleWSO** to **Multicrew Nuclear Option** (`MulticrewNuclearOption.dll`, BepInEx GUID / config `com.bongus.multicrewnuclearoption`)
- Local builds: solution + auto-detected Nuclear Option `Managed` / BepInEx paths so IDE/`dotnet build` works without manual `-p` flags; build outputs ignored via `.gitignore`

> **Migrating from SimpleWSO:** remove the old `SimpleWSO` plugin folder and install `MulticrewNuclearOption`. Old config lived at `com.bongus.simplewso.cfg` — re-apply keybinds/camera offsets in the new config or via F1 if needed.

#### 1.0.1

- Fixed unreliable pilot presence advertising: prefer `GameManager.GetLocalAircraft` and periodically rebroadcast so late joiners can seat as gunner
- Require the pilot to advertise mod presence before a gunner can join a player-piloted aircraft (AI aircraft unchanged)
- Fixed gunner aircraft not despawning after a landed eject (stale `StatusDisplay` `onDisableUnit` callback aborted `ReturnToInventory`)
- Gunner camera cycle (**K**), HUD refresh, safer aircraft disable handling, and Configuration Manager (F1) camera offset editing
- Release cleanup: remove dead helpers and reduce routine log noise

### Final words

This was a fun project. The Nuclear Option developers have said they will not invest time into WSO/gunnery like this mod or Meteez's more feature-rich [NOMulticrew](https://github.com/Meteez/NOMulticrew/releases). After playing with friends, that seems like the right call for Shockfront.

Second seating makes sense where each crew role has real workload (Jump Ship, Star Citizen, WWII bombers). In Nuclear Option the pilot already juggles evasion, flying, and weapons — that pressure is a big part of the fun. Handing targeting to a WSO can unbalance fighters.

Rotary-wing airframes benefit most (Ibis guns, Tarantula side gun, Chicane turret): turret work and piloting split naturally so both players stay engaged.

---

## Русский

Выберите дружественный самолёт в режиме наблюдения и нажмите **H**, чтобы занять место WSO.

В этом документе термины *Gunner* (стрелок) и *WSO* (офицер вооружения) используются как синонимы.

### Возможности

- Вход и выход одной кнопкой
- Прицел турели стрелка (при выбранной турели) отображается на экране пилота для координации
- У стрелка работают ванильные MFD/сенсоры кабины (TacScreen, TargetCam)
- Пилот видит на целевом MFD живой ракурс стрелка, пока занята станция стрелка
- Обратная связь по попаданиям (звук + маркер прицела) у стрелка, в том числе в сетевой игре
- Награды за уничтожение (опыт / деньги) делятся 50/50 между пилотом и стрелком за киллы со станции стрелка; звук бонуса слышат оба
- В ленте убийств отображаются оба члена экипажа (например, `Пилот + Стрелок уничтожил …`)
- Отдельные списки целей у пилота и стрелка; передача в обе стороны (стрелок ↔ пилот)
- Пока стрелок управляет турелями (пушка Chicane, турели Ibis), пилот не может перехватить управление — это защищает от случайного срыва контроля при переключении оружия
- Если ни пилот, ни стрелок не управляют турелью, ИИ берёт управление на себя, как в оригинальной игре
- Работает с самолётами под управлением ИИ и игроков
- Совместим со всеми модовыми самолётами (KAR, BOTE и др.)
- Автоматически создаёт позиции камеры для модовых самолётов, чтобы можно было настроить внешние ракурсы. Настраивается через F1 или конфиг (см. ниже)
- Проверка наличия мода у пилота: занять место стрелка на самолёте игрока можно только если у пилота установлен этот мод (для самолётов с ИИ-пилотом проверка не требуется)

### Как пользоваться

1. Установите [BepInEx](https://github.com/BepInEx/BepInEx)
2. Скачайте последний релиз на [GitHub Releases](https://github.com/bslie/Multicrew-Nuclear-Option/releases) и поместите папку `MulticrewNuclearOption` (с файлом `MulticrewNuclearOption.dll`) в каталог плагинов: `steamapps\common\Nuclear Option\BepInEx\plugins`
3. Запустите игру через Nuclear Option Mod Manager (рекомендуется) или Steam
4. Создайте или зайдите в одиночное или сетевое лобби
5. Выберите фракцию
6. Кликните по дружественному самолёту игрока или ИИ
7. Нажмите **H** (по умолчанию), чтобы занять место стрелка
8. Нажмите **U** (по умолчанию), чтобы передать цели: пилот → стрелок или стрелок → пилот
9. Нажмите **K**, чтобы переключать позиции камеры стрелка
10. Снова нажмите **H**, чтобы выйти и вернуться в режим наблюдения

### Ограничения

- **Мод должен быть установлен и у стрелка, и у пилота** (если пилот не ИИ)
- WSO не управляет полётом
- У WSO нет средств противодействия (тепловые ловушки и т. п.)
- Несколько стрелков в одном самолёте не тестировалось
- На Medusa в кресле стрелка самолёт заметно трясётся — причина неизвестна

### Настройка

Позиции камеры (переключение клавишей **K**) настроены для большинства ванильных двух- и многоместных самолётов — обычно там, где расположен целевой подвес (Cricket, Compass, Chicane). Для Ibis и Tarantula дополнительные ракурсы привязаны к оружейным позициям (дверные пулемёты / боковая пушка).

Смещения камеры, привязки клавиш и режим передачи целей (**замена** или **добавление**) настраиваются через BepInEx Configuration Manager ([GitHub](https://github.com/BepInEx/BepInEx.ConfigurationManager) / NOMM) — **F1** в игре — или в файле:

`\Nuclear Option\BepInEx\config\com.bongus.multicrewnuclearoption.cfg`

Смещения камеры: **F1 → Multicrew Nuclear Option → CameraOffsets**. Для каждого самолёта — отдельная группа (например, **UH-90 Ibis**) с позициями **Position 1–4**. Новые модели появляются в списке после первого входа в качестве стрелка (если группы нет — закройте и снова откройте F1). Координаты задаются в метрах в системе координат самолёта (`X` — вправо, `Y` — вверх, `Z` — вперёд). Пустые позиции в игре игнорируются.

#### Управление по умолчанию

| Клавиша | Параметр конфига | Действие |
| --- | --- | --- |
| H | `ToggleGunnerKey` | Занять место стрелка на выбранном самолёте / выйти |
| U | `ShareTargetsKey` | Передать цели другому члену экипажа |
| K | `CycleCameraPositionKey` | Переключить настроенные позиции камеры стрелка |

Обзорный ролик:

[![Обзор Multicrew Nuclear Option](https://img.youtube.com/vi/KmdFBY28gDw/0.jpg)](https://www.youtube.com/watch?v=KmdFBY28gDw)

Лицензия: **CC0** — используйте как угодно.

### Список изменений

#### 1.0.3

**Экраны экипажа, обратная связь по бою и общие награды**

- **MFD/сенсоры у стрелка** — `TacScreen` и `TargetCam` инициализируются в кресле стрелка (исправлены чёрные мониторы у удалённого стрелка)
- **Экран стрелка у пилота** — пока занята удалённая станция стрелка, на целевом MFD пилота показывается живой ракурс стрелка (поза камеры по сети, без передачи видеопотока)
- **Обратная связь по попаданиям** — у стрелка ванильный звук попадания и маркер на прицеле; удалённые клиенты стрелка получают feedback отдельным сетевым сообщением
- **Общие награды за килл** — опыт и деньги за уничтожение со станции стрелка делятся **50/50** между пилотом и стрелком; звук бонуса слышат оба
- **Лента убийств** — при совместном килле отображаются оба имени, например `Пилот + Стрелок уничтожил …`
- **Сеть** — `GunnerJoinMsg` передаёт `PlayerNetId` стрелка для атрибуции наград и подписи в kill feed

**Установка:** распакуйте `MulticrewNuclearOption.zip` в `BepInEx\plugins\`. В мультиплеере мод нужен и пилоту, и стрелку (самолёт с ИИ-пилотом — исключение).

#### 1.0.2

- Мод переименован с **SimpleWSO** в **Multicrew Nuclear Option** (`MulticrewNuclearOption.dll`, GUID и конфиг BepInEx: `com.bongus.multicrewnuclearoption`)
- Упрощена локальная сборка: solution и автоопределение путей `Managed` / BepInEx для Nuclear Option — проект собирается в IDE и через `dotnet build` без ручных флагов `-p`; артефакты сборки добавлены в `.gitignore`

> **Переход с SimpleWSO:** удалите старую папку плагина `SimpleWSO` и установите `MulticrewNuclearOption`. Старый конфиг находился в `com.bongus.simplewso.cfg` — при необходимости заново настройте клавиши и камеры в новом конфиге или через F1.

#### 1.0.1

- Исправлена ненадёжная передача сигнала о наличии мода у пилота: приоритет `GameManager.GetLocalAircraft` и периодическая повторная отправка, чтобы игроки, подключившиеся позже, могли занять место стрелка
- Перед посадкой стрелка на самолёт игрока пилот должен сообщать о наличии мода (для самолётов с ИИ без изменений)
- Исправлено: самолёт не исчезал после катапультирования на земле (устаревший обработчик `StatusDisplay` на событии `onDisableUnit` прерывал `ReturnToInventory`)
- Переключение камеры стрелка (**K**), обновление HUD, более безопасная обработка отключения юнита и редактирование смещений камеры в Configuration Manager (F1)
- Подготовка к релизу: удалён неиспользуемый код, сокращён объём служебных логов

### Напоследок

Это был интересный проект. Разработчики Nuclear Option заявляли, что не планируют развивать WSO и стрелковые места — ни в духе этого мода, ни в более функциональном [NOMulticrew](https://github.com/Meteez/NOMulticrew/releases) от Meteez. После игры с друзьями кажется, что для Shockfront это разумное решение.

Второе место в экипаже оправдано там, где у каждой роли есть реальная нагрузка (Jump Ship, Star Citizen, бомбардировщики времён Второй мировой). В Nuclear Option пилот и так одновременно уклоняется, ведёт самолёт и работает с оружием — это важная часть игрового процесса. Передать прицеливание WSO может нарушить баланс в истребителях.

Больше всего от мода выигрывают вертолёты (пулемёты Ibis, боковая пушка Tarantula, турель Chicane): управление турелью и пилотирование естественно распределяют нагрузку, и оба игрока остаются вовлечёнными.
