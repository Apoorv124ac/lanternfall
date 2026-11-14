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
        public float MaxHp, Speed, SpecialCooldown, PreferredRange;
        public WeaponDef Weapon => WeaponDef.For(Kind);
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
                new HeroDef { Kind = HeroKind.Cinder, Name = "Cinder", Role = "Fire · assault", Blurb = "Assault striker. Her SMG sets targets Burning; Fire Ring erupts around her. Burning foes Overload when Volt hits them.",
                    AttackName = "Emberline SMG", SpecialName = "Fire Ring", UltName = "Meteor Rain",
                    Accent = C("#ff8a2a"), MaxHp = 150, Speed = 6.2f, SpecialCooldown = 6f, PreferredRange = 8f, Element = Element.Fire,
                    Look = new Look { Cloth = C("#e9e6e1"), Cloth2 = C("#3a2a26"), Trim = C("#c8452a"), Glow = C("#ffb347"), Hair = C("#ff6a1a"),
                        Robe = false, SpikyHair = true, Vest = true, Backpack = true, Gun = Lanternfall.Weapon.Rifle, Skin = C("#c98a6a") } },
                new HeroDef { Kind = HeroKind.Rime, Name = "Rime", Role = "Ice · marksman tank", Blurb = "Armoured marksman. Piercing DMR rounds chill and freeze; Frost Nova locks a crowd for the team to Shatter.",
                    AttackName = "Glacier DMR", SpecialName = "Frost Nova", UltName = "Blizzard",
                    Accent = C("#7fd6ff"), MaxHp = 210, Speed = 5.6f, SpecialCooldown = 7f, PreferredRange = 11f, Element = Element.Ice,
                    Look = new Look { Cloth = C("#f2f4f7"), Cloth2 = C("#24344a"), Trim = C("#3d7fc2"), Glow = C("#9ee8ff"), Hair = C("#e9f4ff"),
                        Robe = false, Pauldrons = true, Helmet = true, Vest = true, Bulk = 1.18f, Gun = Lanternfall.Weapon.Rifle, Skin = C("#e7c0a8") } },
                new HeroDef { Kind = HeroKind.Volt, Name = "Volt", Role = "Lightning · skirmisher", Blurb = "Fast skirmisher. Arc Carbine shocks every few rounds; Chain Lightning jumps through a whole pack.",
                    AttackName = "Arc Carbine", SpecialName = "Chain Lightning", UltName = "Thunder God",
                    Accent = C("#fff05a"), MaxHp = 140, Speed = 7f, SpecialCooldown = 6f, PreferredRange = 9f, Element = Element.Lightning,
                    Look = new Look { Cloth = C("#2a2638"), Cloth2 = C("#16131f"), Trim = C("#ffe45a"), Glow = C("#fff6a0"), Hair = C("#f2f2f2"),
                        Robe = false, Goggles = true, SpikyHair = true, Vest = true, Height = 0.95f, Gun = Lanternfall.Weapon.Rifle, Skin = C("#a8735a") } },
                new HeroDef { Kind = HeroKind.Sol, Name = "Sol", Role = "Holy · medic", Blurb = "Close-range medic. Radiant scattergun Exposes enemies; Rose Mend heals and lifts fallen allies.",
                    AttackName = "Dawn Scattergun", SpecialName = "Rose Mend", UltName = "Dawnbreak",
                    Accent = C("#ff9cc0"), MaxHp = 150, Speed = 6.2f, SpecialCooldown = 9f, PreferredRange = 5.5f, Element = Element.Holy,
                    Look = new Look { Cloth = C("#efe4cf"), Cloth2 = C("#6b4a5a"), Trim = C("#e9b85a"), Glow = C("#ffd1e2"), Hair = C("#3b2416"),
                        Robe = false, Halo = true, Ponytail = true, Vest = true, Backpack = true, Gun = Lanternfall.Weapon.Shotgun, Skin = C("#b9806a") } },
            };
        }
    }
}
