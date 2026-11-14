#!/usr/bin/env bash
# Re-render the pixelfx sprite sheets used by the slice and regenerate Assets/Lanternfall/Scripts/FxData.cs.
# Needs Node 18+ and Python 3. Usage: Tools/render-fx.sh [path-to-pixelfx-checkout]
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
PFX="${1:-$ROOT/../pixelfx}"
[ -d "$PFX" ] || git clone --depth 1 https://github.com/patch-gg/pixelfx "$PFX"
OUT="$(mktemp -d)"
FX="$ROOT/Assets/Lanternfall/Resources/FX"; META="$ROOT/Tools/fx-meta"
mkdir -p "$FX" "$META"
IDS="fire/fireball fire/fire-ring fire/spirit-flame ice/ice-lance ice/frost-nova ice/ice-shatter
lightning/zap-hit lightning/chain-lightning holy/heal-aoe holy/smite impacts/hit-spark
shadow/dark-explosion spawn/poof spawn/spawn-portal explosions/plasma-explosion
status/burning status/frozen status/stunned
fire/meteor ice/blizzard-vortex lightning/lightning-strike holy/sunburst impacts/ground-slam
shadow/void-orb shadow/eclipse shadow/shadow-flames ui/level-up pickups/xp-orb pickups/heart-pickup
arcane/magic-circle-cast spawn/summon-rise spawn/shatter-death arcane/blink-out"
for id in $IDS; do
  (cd "$PFX" && node tools/render.mjs "$id" --sheet --no-actor --bg none --out "$OUT" >/dev/null)
  n="${id#*/}"; cp "$OUT/$id/$n.png" "$FX/$n.png"; cp "$OUT/$id/$n.json" "$META/$n.json"
done
# Sol's heals: a rose recolor of holy/heal-aoe (one palette option)
(cd "$PFX" && node tools/render.mjs holy/heal-aoe --sheet --no-actor --bg none --out "$OUT/rose" \
  --opts '{"palette":["#3a0d24","#7a1f4a","#c8457a","#ff8fb4","#ffe3ee"]}' >/dev/null)
cp "$OUT/rose/holy/heal-aoe/heal-aoe.png" "$FX/heal-rose.png"; cp "$OUT/rose/holy/heal-aoe/heal-aoe.json" "$META/heal-rose.json"
# Archer bolts: purple recolor of fire/fireball
(cd "$PFX" && node tools/render.mjs fire/fireball --sheet --no-actor --bg none --out "$OUT/void" \
  --opts '{"palette":["#14061f","#3b0f5c","#7a2bb0","#c070ff","#f3dcff"]}' >/dev/null)
cp "$OUT/void/fire/fireball/fireball.png" "$FX/shadow-bolt.png"; cp "$OUT/void/fire/fireball/fireball.json" "$META/shadow-bolt.json"
python3 "$ROOT/Tools/gen_fxdata.py" "$ROOT"
echo "Done. Sheets in $FX, table in Assets/Lanternfall/Scripts/FxData.cs"
