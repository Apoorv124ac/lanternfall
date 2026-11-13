# Lanternfall

Couch co-op action roguelite for 1–4 players. The Eclipse Maw swallowed the sun, and the Lanternbearers carry the last ember down through the dark. 2.5D in Unity, with pixel art effects from [patch-gg/pixelfx](https://github.com/patch-gg/pixelfx) (MIT) played as sprite-sheet billboards.

## Open it

1. Install **Unity 6** (6000.0 LTS or newer) from Unity Hub.
2. Clone this repo, then in Unity Hub choose **Add → Add project from disk** and pick the repo folder.
3. Unity installs the Input System package. If it asks to enable the new input backends, click **Yes** (the Editor restarts).
4. On first open the project builds `Assets/Lanternfall/Lanternfall.unity` and opens it. If you don't see it, use the **Lanternfall → Open Slice Scene** menu.
5. Press **Play**.

## Play the slice (Floor 1)

| | Gamepad | Keyboard P1 | Keyboard P2 |
|---|---|---|---|
| Join | A / ✕ | J | , |
| Move | Stick or d-pad | WASD | Arrows |
| Attack (hold) | X / □ or RT | J | , |
| Special | Y / △ or RB | K | . |
| Lantern: pick up / throw · revive (hold near a downed ally) | A / ✕ | L | / |
| Begin / restart | Start | Enter | Enter |

In the lobby the Special button cycles your hero. Up to 4 players can join, and joining mid-floor works too.

**Heroes**
- **Cinder** (fire, burst): Fireball applies *Burning*. Fire Ring is an AoE burst around you.
- **Rime** (ice, tank): Ice Lance chills, and 3 chills *Freeze* a target. Frost Nova freezes everything nearby.
- **Volt** (lightning, ranged): Zap is an auto-target hit that *Stuns* briefly. Chain Lightning hops across up to 5 enemies.
- **Sol** (holy, healer): Smite applies *Exposed* (+25% damage taken). Rose Mend heals and revives allies around you. **Dawnlight:** the Lantern's light reaches 50% further while Sol carries it.

**Kindred Reactions** (the combo hook)
- **Shatter:** fire or lightning on a *Frozen* enemy deals 2.5× damage and sprays shards at its neighbours.
- **Overload:** lightning on a *Burning* enemy blows up, damaging and stunning everything around it.

**The Lantern** lights the arena. Enemies standing outside its light hit 50% harder, so stay together. Carrying it slows you and blocks your abilities, and Skullwings dive at whoever holds it. Throw it to reposition the light.

Floor 1 has three waves: Shades, then Skullwings, then an Elite Shade. Enemy count and HP scale with the number of players.

## How it's built

Everything is built from code. `Game` creates the camera, lights, arena, Lantern and HUD, so the scene only holds one GameObject.

```
Assets/Lanternfall/
  Scripts/
    Game.cs        state, joining, waves, camera, hit-stop/shake, HUD (IMGUI placeholder)
    Hero.cs        movement, Lantern carry/throw, revive, the 4 heroes' abilities
    Enemy.cs       Shade / Skullwing / Elite AI, statuses, Kindred Reactions
    Lantern.cs     the shared light, carry/throw, Dawnlight
    Fx.cs          loads pixelfx sheets, plays them as billboards (FxPlayer)
    FxData.cs      generated table of sheet metadata (fps, pivot, act contact time)
    PixelArt.cs    placeholder hero/enemy sprites drawn in code at pixelfx scale (24x38)
    Arena.cs, Actor.cs, Projectile.cs, Controls.cs, HeroDef.cs
  Resources/FX/    pixelfx sprite sheets (PNG)
  Editor/          import settings for the sheets (point filter, no compression) + scene setup
Tools/
  render-fx.sh     re-render the sheets from a pixelfx checkout and regenerate FxData.cs
  fx-meta/         the Aseprite-style JSON each sheet came with
```

Notes:
- 24 pixels = 1 world unit, so pixelfx effects and characters share one pixel scale.
- Special damage lands on the effect's own contact frame (`meta.pixelfx.act`), and reactions add hit-stop and screen shake.
- Built-in render pipeline by default. URP also works, because the sprites use `Sprites/Default` and the arena uses primitive default materials.
- The heroes and enemies are placeholder art drawn in code. Replace `PixelArt` with real sheets when the art exists.

## Next up

Real character sheets and animation, Steam Veil / Firestorm / Dawnbreak / Conduit, floors 2–4 and the Eclipse Maw boss, Bramble as the first unlock, a UI Toolkit HUD, audio, and online co-op later.

Effects © Patch, [pixelfx](https://github.com/patch-gg/pixelfx), MIT License.
