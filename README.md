# SimpleWSO #

Gunners and Weapon System Officers for Nuclear Option. Simply select a friendly aircraft in spectate, Press H, become a WSO.
The term 'Gunner' and 'WSO' are used interchangeably during this document.

## Features ##
- One button enter/exit
- Gunner turret reticule (when turret selected) shown on pilot screen for coordination
- Seperate target lists for pilot and Gunner, can be pushed bi-directionally from Gunner <-> Pilot
- While turrets (Chicane gun, Ibis Turrets) are selected by gunner, Pilot cannot take control. Stops pilot ripping control from gunner while cycling weapons.
- When Pilot and Gunner aren't controlling a turret, AI takes over like vanilla.
- Works with AI or Player controlled aircraft
- Works with all modded aircraft, KAR and BOTE
- Auto creates camera entry positions for modded aircraft to allow custom placement of external cameras. Configurable in F1 or config file (see below)

## How to use ##
1. Install BepinEx https://github.com/BepInEx/BepInEx
2. Download and place the SimpleWSO folder (containing SimpleWSO.dll) in your Nuclear Option BepInEx plugin folder. (steamapps\common\Nuclear Option\BepInEx\plugins)
3. Start the game either through Nuclear Option Mod Manager (recommended) or via steam.
4. Start or join a multiplayer or singleplayer lobby.
5. Join a faction
6. Click on a player or AI friendly aircraft.
7. Press H by default to enter gunner view.
8. Press U by default to share targets from Pilot -> Gunner or Gunner -> Pilot
9. Press K to cycle between gunner camera positions."
10. Press H again to leave the gunner seat and be put back into spectate.

## Limitations and things to note ##
Every attempt has been made to make this mod as seamless as possible, but there are some unfortunate caveats:
- BOTH THE GUNNER AND PILOT (Unless the pilot is AI) REQUIRES THIS MOD LOADED TO BE ABLE TO USE, MAKE SURE ITS WORKING FOR BOTH PLAYERS BEFORE BEING UPSET PLEASE.
- MFDs do not work for the gunner, they still work for the pilot but its an area I did not want to touch for 1.0, potentially in the future I will attempt to get the vanilla ones working.
- WSO does not have flight control, a given but it means you can't take over sticks if they need to leave or AFK
- WSO does not have counter measures, no design reason why they do not, I just did not see it as a priority because the pilot needs SOMETHING to do.
- I do not know if more than one person can be in an aircraft as a gunner at once. theoretically yes but if not then uhhh don't do that.
- The Medusa rattles like a motherfucker when you're in the gunner seat and i have no idea why but its kind of funny.

## Configuration ## 
I've set up camera positions (Gunner camera offset cycleable with K) for most of the vanilla two+ seater aircraft. Often placed where the "targeting pod" is on most aircraft e.g. the Cricket, Compass, Chichane. For the Ibis and Tarantula the
additional camera views are on the "gun" positions, so door guns for Ibis and side gun for the Tarantula

You can change these camera offsets along with keybinds and an option so that when a target list is shared between Pilot and Gunner and vice versa it can either fully replace or append the target lists, based on how much you trust your other 
crewman.

BepInEx Configuration Manager (https://github.com/BepInEx/BepInEx.ConfigurationManager or downloadable via NOMM) is the easiest way to change these. You just press F1 in game. Otherwise you can change the settings in: 
`\Nuclear Option\BepInEx\config\com.bongus.simplewso.cfg`

Camera offsets are under **F1 → SimpleWSO → CameraOffsets**. Each aircraft appears as its own collapsible group (e.g. **UH-90 Ibis**) with **Position 1–4** inside. New airframes get the same treatment after you gunner them once (reopen F1 if the group is not visible yet). Values are aircraft-local meters (`X` right, `Y` up, `Z` forward). Empty positions are ignored in-game.

### Default Controls ###
| Key | Config entry | Action |
| --- | --- | --- |
| H | `ToggleGunnerKey` | Possess gunner seat on followed aircraft / leave |
| U | `ShareTargetsKey` | Share targets with the other seat |
| K | `CycleCameraPositionKey` | Cycle configured gunner camera positions |

Youtube Link to overview:

[![IMAGE ALT TEXT HERE](https://img.youtube.com/vi/KmdFBY28gDw/0.jpg)](https://www.youtube.com/watch?v=KmdFBY28gDw)

Whole things CC0 do what you want with it.

## Final Words ## 
This was a fun project, and I'm pretty happy with the results. The Nuclear Option devs have said they're not going to invest any time into WSO/gunnery like this mod or Meteez's Better and more feature-rich mod: https://github.com/Meteez/NOMulticrew/releases. After playing with my mod with friends, I think it's the right call by Shockfront to not add second seating.

Second seating or dedicated gunners make sense in games where the player has significantly more to do, like Jump Ship, Star Citizen, or WWII games where you're crewing a bomber. Modern aircraft and missile warfare is very much select target → fire. In NO the pilot has to do everything themselves, wearing their "evading fire", "fly the aircraft", and "target and shoot at thing" hats all at once, is what makes the game so engaging. Having a second person in your Compass, for example, removes one of those hats and sort of unbalances the whole thing, because now the pilot only has to focus on manoeuvring.

The rotary-wing aircraft benefit the most from multicrew, with the Ibis' guns, the Tarantula's AC-130-lite side gun, and the Chicane's turret. That's because they push more towards the aforementioned games, where operating the turret and the complexity of piloting naturally split the workload so both players have meaningful things to do instead of one detracting from the other's experience.

It's fun to fantasise about multiple people in a fighter coordinating attacks, independently launching missiles at different targets, and operating like a well-oiled machine. But Nuclear Option's gameplay doesn't really lend itself to that. There's a reason the majority of the airframes in this game, and most modern real-life fighters, are single-seaters. It's not the 40s anymore where a B-17 is engaging 3 different aircraft with its gunners while trying to line up on a bombing target while the flight engineer putting out a fire from a stray tracer.
