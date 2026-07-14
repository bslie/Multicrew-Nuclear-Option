# Multicrew Nuclear Option

[English](#english) · [Русский](#русский)

Gunners and Weapon System Officers for [Nuclear Option](https://store.steampowered.com/app/2168680/Nuclear_Option/).  
Стрелки и офицеры вооружения для Nuclear Option.

Based on a fork of [ItsBongus/SimpleWSO](https://github.com/ItsBongus/SimpleWSO).  
Основан на форке [ItsBongus/SimpleWSO](https://github.com/ItsBongus/SimpleWSO).

---

## English

Select a friendly aircraft in spectate, press **H**, become a WSO.

The terms *Gunner* and *WSO* are used interchangeably in this document.

### Features

- One-button enter/exit
- Gunner turret reticle (when a turret is selected) shown on the pilot screen for coordination
- Separate target lists for pilot and gunner; can be pushed bi-directionally (Gunner ↔ Pilot)
- While turrets (Chicane gun, Ibis turrets) are selected by the gunner, the pilot cannot take control — stops the pilot from ripping control while cycling weapons
- When neither pilot nor gunner is controlling a turret, AI takes over like vanilla
- Works with AI or player-controlled aircraft
- Works with all modded aircraft (KAR, BOTE, etc.)
- Auto-creates camera entry positions for modded aircraft so you can place external cameras. Configurable in F1 or the config file (see below)
- Pilot mod presence check: joining a player-piloted aircraft as gunner requires the pilot to have this mod loaded and advertising presence (AI pilots do not need it)

### How to use

1. Install [BepInEx](https://github.com/BepInEx/BepInEx)
2. Download and place the `MulticrewNuclearOption` folder (containing `MulticrewNuclearOption.dll`) in your Nuclear Option BepInEx plugins folder: `steamapps\common\Nuclear Option\BepInEx\plugins`
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
- MFDs do not work for the gunner (they still work for the pilot)
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

Выберите дружественный самолёт в режиме наблюдения, нажмите **H** — и вы WSO.

В этом документе *Gunner* (стрелок) и *WSO* (офицер вооружения) означают одно и то же.

### Возможности

- Вход и выход одной кнопкой
- Прицел турели стрелка (когда выбрана турель) отображается у пилота для координации
- Отдельные списки целей у пилота и стрелка; можно передавать в обе стороны (стрелок ↔ пилот)
- Пока стрелок держит турели (пушка Chicane, турели Ibis), пилот не может перехватить управление — нельзя случайно сорвать контроль при переключении оружия
- Если ни пилот, ни стрелок не управляют турелью, AI работает как в ванили
- Работает с AI- и игровыми самолётами
- Работает со всеми модовыми самолётами (KAR, BOTE и т.д.)
- Автоматически создаёт слоты камер для модовых машин, чтобы можно было задать внешние ракурсы. Настраивается в F1 или в конфиге (см. ниже)
- Проверка присутствия мода у пилота: сесть стрелком на самолёт игрока можно только если у пилота загружен этот мод и он анонсирует presence (для AI-пилотов проверка не нужна)

### Как пользоваться

1. Установите [BepInEx](https://github.com/BepInEx/BepInEx)
2. Скачайте и положите папку `MulticrewNuclearOption` (с `MulticrewNuclearOption.dll`) в plugins Nuclear Option: `steamapps\common\Nuclear Option\BepInEx\plugins`
3. Запустите игру через Nuclear Option Mod Manager (рекомендуется) или Steam
4. Создайте или зайдите в мультиплеерный / одиночный лобби
5. Присоединитесь к фракции
6. Кликните по дружественному самолёту игрока или AI
7. Нажмите **H** (по умолчанию), чтобы войти в место стрелка
8. Нажмите **U** (по умолчанию), чтобы поделиться целями: пилот → стрелок или стрелок → пилот
9. Нажмите **K**, чтобы переключать позиции камеры стрелка
10. Снова нажмите **H**, чтобы выйти и вернуться в spectate

### Ограничения

- **И стрелку, и пилоту** (если пилот не AI) нужен загруженный мод
- MFD у стрелка не работают (у пилота работают)
- У WSO нет управления полётом
- У WSO нет средств РЭБ / ловушек (countermeasures)
- Несколько стрелков в одном самолёте не тестировалось
- Medusa трясётся на месте стрелка по неизвестной причине

### Настройка

Позиции камеры (цикл по **K**) заданы для большинства ванильных двухместных и более машин — часто там, где стоит targeting pod (Cricket, Compass, Chicane). У Ibis и Tarantula дополнительные ракурсы на оружейных позициях (бортовые / боковая пушка).

Смещения камер, бинды и режим передачи целей (**замена** или **добавление**) меняются через BepInEx Configuration Manager ([GitHub](https://github.com/BepInEx/BepInEx.ConfigurationManager) / NOMM) — **F1** в игре — или в файле:

`\Nuclear Option\BepInEx\config\com.bongus.multicrewnuclearoption.cfg`

Смещения камер: **F1 → Multicrew Nuclear Option → CameraOffsets**. Каждый самолёт — своя группа (например **UH-90 Ibis**) с **Position 1–4**. Новые машины появляются после первого входа стрелком (если группы нет — снова откройте F1). Координаты в метрах в СК самолёта (`X` вправо, `Y` вверх, `Z` вперёд). Пустые слоты в игре игнорируются.

#### Управление по умолчанию

| Клавиша | Параметр конфига | Действие |
| --- | --- | --- |
| H | `ToggleGunnerKey` | Занять место стрелка на выбранном самолёте / выйти |
| U | `ShareTargetsKey` | Поделиться целями с другим местом |
| K | `CycleCameraPositionKey` | Переключить настроенные позиции камеры стрелка |

Обзорный ролик:

[![Обзор Multicrew Nuclear Option](https://img.youtube.com/vi/KmdFBY28gDw/0.jpg)](https://www.youtube.com/watch?v=KmdFBY28gDw)

Лицензия: **CC0** — делайте что хотите.

### Список изменений

#### 1.0.2

- Мод переименован с **SimpleWSO** в **Multicrew Nuclear Option** (`MulticrewNuclearOption.dll`, GUID / конфиг BepInEx `com.bongus.multicrewnuclearoption`)
- Локальная сборка: solution и автоопределение путей `Managed` / BepInEx Nuclear Option — сборка в IDE/`dotnet build` без ручных `-p`; артефакты сборки в `.gitignore`

> **Миграция с SimpleWSO:** удалите старую папку плагина `SimpleWSO` и поставьте `MulticrewNuclearOption`. Старый конфиг был `com.bongus.simplewso.cfg` — при необходимости заново задайте бинды и камеры в новом конфиге или через F1.

#### 1.0.1

- Исправлена ненадёжная реклама presence пилота: приоритет `GameManager.GetLocalAircraft` и периодический rebroadcast, чтобы поздние joiners могли сесть стрелком
- Перед посадкой стрелка на самолёт игрока требуется анонс presence мода у пилота (для AI без изменений)
- Исправлено: самолёт стрелка не исчезал после landed eject (устаревший колбэк `StatusDisplay` на `onDisableUnit` прерывал `ReturnToInventory`)
- Цикл камеры стрелка (**K**), обновление HUD, более безопасная обработка отключения юнита и правка смещений камер в Configuration Manager (F1)
- Чистка релиза: удалены мёртвые хелперы, меньше шума в логах

### Напоследок

Это был интересный проект. Разработчики Nuclear Option говорили, что не будут вкладываться в WSO/стрелков вроде этого мода или более богатого [NOMulticrew](https://github.com/Meteez/NOMulticrew/releases) от Meteez. После игры с друзьями кажется, что для Shockfront это верное решение.

Второе место имеет смысл там, где у каждой роли своя нагрузка (Jump Ship, Star Citizen, бомбардировщики WWII). В Nuclear Option пилот и так жонглирует уклонением, пилотированием и оружием — это часть кайфа. Отдать прицеливание WSO может разбалансировать истребители.

Больше всего выигрывают вертолёты и винтокрылы (пушки Ibis, бортовая Tarantula, турель Chicane): турель и пилотирование естественно делят работу, и оба игрока остаются заняты.
