using UnityEngine;

namespace Lanternfall
{
    public enum HeroKind { Cinder, Rime, Volt, Sol }
    public enum Element { Fire, Ice, Lightning, Holy, None }

    /// <summary>Tuning for each Lanternbearer. Numbers are first-pass; tweak freely.</summary>
    public sealed class HeroDef
    {
        public HeroKind Kind;
        public string Name, Role, AttackName, SpecialName;
        public Color Cloak, Accent;
        public float MaxHp, Speed, AttackCooldown, SpecialCooldown;
        public Element Element;

        static HeroDef[] all;
        public static HeroDef Get(HeroKind k) { Init(); return all[(int)k]; }
        public static int Count => 4;

        static Color C(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }

        static void Init()
        {
            if (all != null) return;
            all = new[]
            {
                new HeroDef { Kind = HeroKind.Cinder, Name = "Cinder", Role = "Fire · burst", AttackName = "Fireball", SpecialName = "Fire Ring",
                    Cloak = C("#c8452a"), Accent = C("#ffb03a"), MaxHp = 100, Speed = 5.2f, AttackCooldown = 0.42f, SpecialCooldown = 6f, Element = Element.Fire },
                new HeroDef { Kind = HeroKind.Rime, Name = "Rime", Role = "Ice · tank", AttackName = "Ice Lance", SpecialName = "Frost Nova",
                    Cloak = C("#3d7fc2"), Accent = C("#bff1ff"), MaxHp = 140, Speed = 4.6f, AttackCooldown = 0.5f, SpecialCooldown = 8f, Element = Element.Ice },
                new HeroDef { Kind = HeroKind.Volt, Name = "Volt", Role = "Lightning · ranged", AttackName = "Zap", SpecialName = "Chain Lightning",
                    Cloak = C("#6a4bc9"), Accent = C("#fff35c"), MaxHp = 90, Speed = 5.8f, AttackCooldown = 0.32f, SpecialCooldown = 7f, Element = Element.Lightning },
                new HeroDef { Kind = HeroKind.Sol, Name = "Sol", Role = "Holy · healer", AttackName = "Smite", SpecialName = "Rose Mend",
                    Cloak = C("#e7d7b8"), Accent = C("#ff8fb4"), MaxHp = 100, Speed = 5f, AttackCooldown = 0.7f, SpecialCooldown = 10f, Element = Element.Holy },
            };
        }
    }
}
