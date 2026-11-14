using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lanternfall
{
    /// <summary>Party-wide power that grows through the run. Every hero (human or AI) reads from it.</summary>
    public sealed class PartyStats
    {
        public int Level = 1;
        public float Xp, XpToNext = 12;
        public float Damage = 1f, AttackSpeed = 1f, Cooldown = 1f, MaxHpBonus, Area = 1f, Reaction = 1f, Lifesteal, MoveSpeed = 1f, UltRate = 1f, LightRadius = 1f, LightRegen = 2f, DodgeCd = 1f;
        public int ExtraShots;
        public readonly Dictionary<string, int> Taken = new Dictionary<string, int>();

        /// <summary>Returns how many levels were gained.</summary>
        public int AddXp(float amount)
        {
            Xp += amount;
            int gained = 0;
            while (Xp >= XpToNext)
            {
                Xp -= XpToNext;
                Level++;
                gained++;
                XpToNext = Mathf.Round(12 + (Level - 1) * 9 + Mathf.Pow(Level - 1, 1.5f) * 2);
            }
            return gained;
        }
    }

    public sealed class Upgrade
    {
        public string Id, Name, Desc;
        public int MaxStacks = 5;
        public Color Color;
        public Action<PartyStats> Apply;

        public static readonly List<Upgrade> Pool = new List<Upgrade>
        {
            new Upgrade { Id = "might", Name = "Ember Might", Desc = "+20% damage for the whole party", Color = Util.Hex("#ff8a3a"), Apply = p => p.Damage += 0.2f },
            new Upgrade { Id = "haste", Name = "Quickcast", Desc = "+18% attack speed", Color = Util.Hex("#ffe05a"), Apply = p => p.AttackSpeed += 0.18f },
            new Upgrade { Id = "focus", Name = "Deep Focus", Desc = "Specials recharge 18% faster", Color = Util.Hex("#7fd6ff"), Apply = p => p.Cooldown *= 0.82f },
            new Upgrade { Id = "heart", Name = "Lantern Heart", Desc = "+30 max HP and a full heal", Color = Util.Hex("#ff6a7a"), MaxStacks = 6, Apply = p => p.MaxHpBonus += 30 },
            new Upgrade { Id = "kindred", Name = "Kindred Bond", Desc = "Shatter and Overload hit 50% harder", Color = Util.Hex("#c08aff"), MaxStacks = 3, Apply = p => p.Reaction += 0.5f },
            new Upgrade { Id = "area", Name = "Wider Arcs", Desc = "+25% area for rings, novas and heals", Color = Util.Hex("#9affc8"), MaxStacks = 3, Apply = p => p.Area += 0.25f },
            new Upgrade { Id = "twin", Name = "Twin Cast", Desc = "+1 projectile, zap target and smite", Color = Util.Hex("#ffd1e2"), MaxStacks = 3, Apply = p => p.ExtraShots++ },
            new Upgrade { Id = "leech", Name = "Ember Leech", Desc = "Heal for 4% of damage dealt", Color = Util.Hex("#ff4f6a"), MaxStacks = 3, Apply = p => p.Lifesteal += 0.04f },
            new Upgrade { Id = "swift", Name = "Swift Steps", Desc = "+10% move speed, dodge 25% sooner", Color = Util.Hex("#bfffe0"), MaxStacks = 3, Apply = p => { p.MoveSpeed += 0.1f; p.DodgeCd *= 0.75f; } },
            new Upgrade { Id = "surge", Name = "Ultimate Surge", Desc = "Ultimates charge 40% faster", Color = Util.Hex("#fff6a0"), MaxStacks = 3, Apply = p => p.UltRate += 0.4f },
            new Upgrade { Id = "glow", Name = "Brighter Ember", Desc = "+20% Lantern light, +2 HP/s regen inside it", Color = Util.Hex("#ffae35"), MaxStacks = 3, Apply = p => { p.LightRadius += 0.2f; p.LightRegen += 2f; } },
        };

        public static List<Upgrade> Roll(PartyStats p, int n)
        {
            var open = Pool.FindAll(u => !p.Taken.TryGetValue(u.Id, out int k) || k < u.MaxStacks);
            var pick = new List<Upgrade>();
            while (pick.Count < n && open.Count > 0)
            {
                int i = UnityEngine.Random.Range(0, open.Count);
                pick.Add(open[i]);
                open.RemoveAt(i);
            }
            return pick;
        }

        public void Take(PartyStats p)
        {
            p.Taken.TryGetValue(Id, out int k);
            p.Taken[Id] = k + 1;
            Apply(p);
        }
    }
}
