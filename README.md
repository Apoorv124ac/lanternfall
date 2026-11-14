# Lanternfall

A top-down co-op hero shooter roguelite, rebuilt around the feel of a twin-stick soldier prototype: a high follow camera, run / tactical sprint / slide, aim-down-sights, tracers and shell casings. The Eclipse Maw swallowed the sun, and four Lanternbearers carry the last ember down four floors to light it again. Built in Unity, with pixel art effects from [patch-gg/pixelfx](https://github.com/patch-gg/pixelfx) (MIT) played as billboards.

## Open it

1. Install **Unity 6.3 LTS (6000.3.28f1)** from Unity Hub. Newer Unity 6 versions also work.
2. Get this repo onto your PC with GitHub Desktop (**File → Clone**) or **Code → Download ZIP**.
3. In Unity Hub choose **Add → Add project from disk** and pick the folder.
4. If Unity asks to enable the new Input System backends, click **Yes**.
5. The game scene builds itself on first open. If you don't see it, use **Lanternfall → Open Game Scene**. Then press **Play**.

> Already had the old slice open? Delete `Assets/Lanternfall/Lanternfall.unity`, then run **Lanternfall → Create Game Scene**.

## How to play

On the title screen, pick your hero with **A / D** (or the left stick) and press **Enter / A**. The other three heroes fight beside you as AI squadmates. A friend can press **A** on a second gamepad at any time to take one of them over.

| | Keyboard + mouse | Gamepad |
|---|---|---|
| Move | WASD | Left stick |
| Aim | Mouse (crosshair follows the cursor) | Right stick (light aim assist) |
| Fire (hold) | Left mouse | RT |
| Aim down sights (tighter spread, slower walk) | Right mouse | LT |
| Run / tactical sprint | Hold Shift / double-tap Shift (can't shoot while sprinting) | L3 or LB |
| Crouch / slide (slide while running, you can shoot mid-slide and dodge hits) | Ctrl or C | B / ○ |
| Reload | R | X / □ |
| Special | E | Y / △ |
| Ultimate (charges as you deal damage) | Q | RB |
| Revive a downed ally (hold) | F | A / ✕ |
| Camera: action / tactical view | Tab | Select |
| Dev HUD (speed, posture, stamina, hits/shots) | F3 | |
| Pause / controls | Esc | Start |

Running, sprinting and sliding use **stamina** (green bar over your hero).

**The run:** there are four floors: Ashen Gate, Frozen Cloister, Storm Spire and the Maw's Throne. Each floor has waves of the Eclipse cult: blade-rushing Cultists, laser-sighted Gunners and slamming Brutes. Clearing a floor fully heals the party and opens a portal to the next one. The **Eclipse Herald** boss waits at the bottom. Its attack patterns are slams, orb volleys, summoning, and in phase 2 the Eclipse Nova.

**Growing stronger:** enemies drop ember shards (XP). On every level-up the game pauses and you pick one of three blessings for the whole party. They include more damage, fire rate, extended mags, faster reloads, armor-piercing rounds, crit chance, stamina, extra bullets, more HP, lifesteal, faster specials and ultimates, and a brighter Lantern. Bosses fall to a party that has levelled up.

**Staying alive:** every enemy attack is telegraphed. Melee hits show a **red circle** on the floor, and Gunners show a **red laser sight** that blinks just before they fire a 3-round burst, so slide out of the line. Standing in the Lantern's light heals you, and enemies outside the light hit 50% harder. Downed heroes get back up on their own after 10 seconds, or sooner if someone revives them. If the whole party goes down, you retry the floor and keep your levels.

**Heroes**
- **Cinder** (fire, assault): *Emberline SMG*, fast fire that sets enemies burning. Fire Ring, and the ultimate **Meteor Rain**.
- **Rime** (ice, marksman tank, draws enemy attention): *Glacier DMR*, heavy shots that pierce and freeze. Frost Nova, and the ultimate **Blizzard**.
- **Volt** (lightning, skirmisher): *Arc Carbine*, every 4th hit arcs lightning. Chain Lightning, and the ultimate **Thunder God**.
- **Sol** (holy medic): *Dawn Scattergun*, a close-range shotgun. Rose Mend (heals and revives), and the ultimate **Dawnbreak**, which revives and fully heals everyone.

**Kindred Reactions**
- **Shatter:** fire or lightning on a frozen enemy deals 2.5× damage and sprays shards.
- **Overload:** lightning on a burning enemy causes an explosion that stuns everything around it.

## Characters and art

The heroes and the Eclipse cult (Cultist, Gunner, Brute, Herald) are 3D humanoids built and animated in code (`Humanoid.cs`). That means the game runs with zero art assets. To use real rigged models instead:

1. Import a rigged humanoid. Free options are Unity's *Starter Assets – ThirdPerson* (Asset Store) or characters from mixamo.com.
2. Run **Lanternfall → Create Character Art Config**.
3. Drag a character prefab into any slot (Cinder … Boss). The `Archer` slot is used for the Gunner.

Filled slots use your model, and empty slots keep the built-in one. Animator controllers with a `Speed` float (like Starter Assets) get walk/run driven automatically.

## Code map

```
Assets/Lanternfall/Scripts/
  Game.cs        run flow: title, floors, waves, boss, level-ups, drop-in co-op
  HUD.cs         all UI (IMGUI): crosshair, hit marker, ammo, stamina, dev HUD, level-up cards, pause
  CameraRig.cs   top-down follow camera (action / tactical), mouse-to-ground aiming
  Hero.cs        walk/run/sprint/crouch/slide, stamina, shooting, abilities, squad AI
  Weapon.cs      the four guns: hitscan, spread, mags, reloads, tracers, muzzle flash, shell casings
  Controls.cs    keyboard+mouse, arrow-key and gamepad bindings
  Enemy.cs       cult AI, telegraphed attacks, boss patterns, statuses, Kindred Reactions
  Humanoid.cs    procedural character bodies + animation (or a rigged model)
  CharacterArt.cs, CharacterArtConfig.cs  looks per character, optional model slots
  World.cs       floor themes, waves, arena builder
  Combat.cs      hits, telegraphs, pickups, projectiles, lightning bolts, the Lantern
  Upgrades.cs    party XP, levels, blessings
  Fx.cs, FxData.cs  pixelfx sheet loading and billboard playback
Tools/render-fx.sh  re-render the pixelfx sheets and regenerate FxData.cs
```

Effects © Patch, [pixelfx](https://github.com/patch-gg/pixelfx), MIT License.
