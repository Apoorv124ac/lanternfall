# Lanternfall

A third-person co-op action roguelite. The Eclipse Maw swallowed the sun, and four Lanternbearers carry the last ember down four floors to light it again. Built in Unity, with pixel art effects from [patch-gg/pixelfx](https://github.com/patch-gg/pixelfx) (MIT) played as billboards.

## Open it

1. Install **Unity 6.3 LTS (6000.3.28f1)** from Unity Hub. Newer Unity 6 versions also work.
2. Get this repo onto your PC with GitHub Desktop (**File → Clone**) or **Code → Download ZIP**.
3. In Unity Hub choose **Add → Add project from disk** and pick the folder.
4. If Unity asks to enable the new Input System backends, click **Yes**.
5. The game scene builds itself on first open. If you don't see it, use **Lanternfall → Open Game Scene**. Then press **Play**.

> Already had the old slice open? Delete `Assets/Lanternfall/Lanternfall.unity`, then run **Lanternfall → Create Game Scene**.

## How to play

On the title screen, pick your hero with **A / D** (or the left stick) and press **Enter / A**. The other three heroes fight beside you as AI companions. A friend can press **A** on a second gamepad at any time to take one of them over.

| | Keyboard + mouse | Gamepad |
|---|---|---|
| Move | WASD | Left stick |
| Camera | Mouse | Right stick |
| Attack (hold) | Left mouse | X / □ or RT |
| Special | Right mouse or E | Y / △ or RB |
| Ultimate (charges as you deal damage) | Q | LB or LT |
| Dodge roll (you can't be hit mid-roll) | Space | B / ○ |
| Revive a downed ally (hold) | F | A / ✕ |
| Pause / controls | Esc | Start |

**The run:** there are four floors: Ashen Gate, Frozen Cloister, Storm Spire and the Maw's Throne. Each floor has waves of the Eclipse cult. Clearing a floor fully heals the party and opens a portal to the next one. The **Eclipse Herald** boss waits at the bottom. Its attack patterns are slams, orb volleys, summoning, and in phase 2 the Eclipse Nova.

**Growing stronger:** enemies drop ember shards (XP). On every level-up the game pauses and you pick one of three blessings for the whole party. They include more damage, faster attacks, faster specials, more HP, extra projectiles, a bigger area, lifesteal, faster ultimates and a brighter Lantern. Bosses fall to a party that has levelled up.

**Staying alive:** every enemy attack shows a **red circle** on the floor first, so roll out of it. Standing in the Lantern's light heals you, and enemies outside the light hit 50% harder. Downed heroes get back up on their own after 10 seconds, or sooner if someone revives them. If the whole party goes down, you retry the floor and keep your levels.

**Heroes**
- **Cinder** (fire): Fireball, Fire Ring, and the ultimate **Meteor Rain**.
- **Rime** (ice tank, draws enemy attention): Ice Lance, Frost Nova (freezes), and the ultimate **Blizzard**, a vortex that pulls enemies in.
- **Volt** (lightning): auto-targeting Zap, Chain Lightning, and the ultimate **Thunder God**.
- **Sol** (holy healer): Smite (enemies hit take +25% damage), Rose Mend (heals and revives), and the ultimate **Dawnbreak**, which revives and fully heals everyone.

**Kindred Reactions**
- **Shatter:** fire or lightning on a frozen enemy deals 2.5× damage and sprays shards.
- **Overload:** lightning on a burning enemy causes an explosion that stuns everything around it.

## Characters and art

The heroes and the Eclipse cult (Cultist, Archer, Brute, Herald) are 3D humanoids built and animated in code (`Humanoid.cs`). That means the game runs with zero art assets. To use real rigged models instead:

1. Import a rigged humanoid. Free options are Unity's *Starter Assets – ThirdPerson* (Asset Store) or characters from mixamo.com.
2. Run **Lanternfall → Create Character Art Config**.
3. Drag a character prefab into any slot (Cinder … Boss).

Filled slots use your model, and empty slots keep the built-in one. Animator controllers with a `Speed` float (like Starter Assets) get walk/run driven automatically.

## Code map

```
Assets/Lanternfall/Scripts/
  Game.cs        run flow: title, floors, waves, boss, level-ups, drop-in co-op
  HUD.cs         all UI (IMGUI placeholder): title, hero cards, bars, level-up cards, pause
  CameraRig.cs   over-the-shoulder follow camera + aim assist
  Hero.cs        movement, dodge, abilities, ultimates, companion AI
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
