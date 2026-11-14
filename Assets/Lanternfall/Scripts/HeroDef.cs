using UnityEngine;

namespace Lanternfall
{
    public enum HeroKind { Cinder, Rime, Volt, Sol }
    public enum Element { Fire, Ice, Lightning, Holy, None }

    /// <summary>Tuning and look for each Lanternbearer.</summary>
    public sealed class HeroDef
    {
        public HeroKind Kind;
        public string Name, Role, Blurb, AttackName, SpecialName, UltName;
        public Color Accent;
        public float MaxHp, Speed, AttackCooldown, SpecialCooldown, PreferredRange;
        public Element Element;
        public Look Look;

        static HeroDef[] all;
        public static HeroDef Get(HeroKind k) { Init(); return all[(int)k]; }
        public const int Count = 4;
        static Color C(string h) => Util.Hex(h);

        static void Init()
        {
            if (all != null) return;
            all = new[]
            {
                new HeroDef { Kind = HeroKind.Cinder, Name = "Cinder", Role = "Fire · burst damage", Blurb = "Hurls fireballs and erupts in rings of flame. Burning foes explode when Volt strikes them.",
                    AttackName = "Fireball", SpecialName = "Fire Ring", UltName = "Meteor Rain",
                    Accent = C("#ff8a2a"), MaxHp = 150, Speed = 6.2f, AttackCooldown = 0.38f, SpecialCooldown = 6f, PreferredRange = 7f, Element = Element.Fire,
                    Look = new Look { Cloth = C("#b23a22"), Cloth2 = C("#3a2220"), Trim = C("#f2a33a"), Glow = C("#ffb347"), Hair = C("#ff6a1a"),
                        SpikyHair = true, Cape = true, Right = Weapon.Gauntlet, Left = Weapon.Orb, Skin = C("#c98a6a") } },
                new HeroDef { Kind = HeroKind.Rime, Name = "Rime", Role = "Ice · tank & control", Blurb = "Armoured frost-knight. Ice lances chill, Frost Nova freezes a crowd solid for the team to Shatter.",
                    AttackName = "Ice Lance", SpecialName = "Frost Nova", UltName = "Blizzard",
                    Accent = C("#7fd6ff"), MaxHp = 210, Speed = 5.6f, AttackCooldown = 0.45f, SpecialCooldown = 7f, PreferredRange = 3.5f, Element = Element.Ice,
                    Look = new Look { Cloth = C("#2f5f9a"), Cloth2 = C("#1d2a40"), Trim = C("#cfe9ff"), Glow = C("#9ee8ff"), Hair = C("#e9f4ff"),
                        Robe = false, Pauldrons = true, Ponytail = true, Bulk = 1.18f, Right = Weapon.Staff, Left = Weapon.Shield, Skin = C("#e7c0a8") } },
                new HeroDef { Kind = HeroKind.Volt, Name = "Volt", Role = "Lightning · ranged & mobile", Blurb = "Storm-runner with twin rods. Zaps auto-target, Chain Lightning jumps through a whole pack.",
                    AttackName = "Zap", SpecialName = "Chain Lightning", UltName = "Thunder God",
                    Accent = C("#fff05a"), MaxHp = 140, Speed = 7f, AttackCooldown = 0.28f, SpecialCooldown = 6f, PreferredRange = 6.5f, Element = Element.Lightning,
                    Look = new Look { Cloth = C("#4b3596"), Cloth2 = C("#211a3a"), Trim = C("#ffe45a"), Glow = C("#fff6a0"), Hair = C("#f2f2f2"),
                        Robe = false, Goggles = true, SpikyHair = true, Height = 0.95f, Right = Weapon.Rods, Left = Weapon.Rods, Skin = C("#a8735a") } },
                new HeroDef { Kind = HeroKind.Sol, Name = "Sol", Role = "Holy · healer", Blurb = "Rose-gold priestess. Smite exposes enemies; Rose Mend heals and lifts fallen allies.",
                    AttackName = "Smite", SpecialName = "Rose Mend", UltName = "Dawnbreak",
                    Accent = C("#ff9cc0"), MaxHp = 150, Speed = 6.2f, AttackCooldown = 0.6f, SpecialCooldown = 9f, PreferredRange = 7.5f, Element = Element.Holy,
                    Look = new Look { Cloth = C("#efe4cf"), Cloth2 = C("#9b7a52"), Trim = C("#e9b85a"), Glow = C("#ffd1e2"), Hair = C("#3b2416"),
                        Halo = true, Hood = false, Ponytail = true, Right = Weapon.Staff, Skin = C("#b9806a") } },
            };
        }
    }
}
