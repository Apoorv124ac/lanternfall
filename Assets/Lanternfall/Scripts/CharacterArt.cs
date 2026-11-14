using UnityEngine;

namespace Lanternfall
{
    public static class CharacterArt
    {
        static CharacterArtConfig config;
        static bool loaded;

        static CharacterArtConfig Config
        {
            get
            {
                if (!loaded) { config = Resources.Load<CharacterArtConfig>("CharacterArt"); loaded = true; }
                return config;
            }
        }

        public static Humanoid BuildHero(Transform parent, HeroDef def)
        {
            GameObject prefab = null;
            if (Config)
                switch (def.Kind)
                {
                    case HeroKind.Cinder: prefab = Config.Cinder; break;
                    case HeroKind.Rime: prefab = Config.Rime; break;
                    case HeroKind.Volt: prefab = Config.Volt; break;
                    case HeroKind.Sol: prefab = Config.Sol; break;
                }
            return prefab ? Humanoid.FromModel(parent, prefab, def.Look) : Humanoid.Build(parent, def.Look);
        }

        public static Humanoid BuildEnemy(Transform parent, EnemyKind kind)
        {
            var look = EnemyLook(kind);
            GameObject prefab = null;
            if (Config)
                switch (kind)
                {
                    case EnemyKind.Cultist: prefab = Config.Cultist; break;
                    case EnemyKind.Archer: prefab = Config.Archer; break;
                    case EnemyKind.Brute: prefab = Config.Brute; break;
                    case EnemyKind.Boss: prefab = Config.Boss; break;
                }
            return prefab ? Humanoid.FromModel(parent, prefab, look) : Humanoid.Build(parent, look);
        }

        static Look EnemyLook(EnemyKind kind)
        {
            Color skin = Util.Hex("#6e5f6a");
            switch (kind)
            {
                case EnemyKind.Archer:
                    return new Look { Skin = skin, Cloth = Util.Hex("#2a2440"), Cloth2 = Util.Hex("#151222"), Trim = Util.Hex("#3fa0a0"), Glow = Util.Hex("#7dfcff"),
                        Robe = false, Hood = true, Mask = true, Cape = true, Left = Weapon.Bow, Height = 0.97f };
                case EnemyKind.Brute:
                    return new Look { Skin = skin, Cloth = Util.Hex("#3a1450"), Cloth2 = Util.Hex("#1c0c26"), Trim = Util.Hex("#8a3a4a"), Glow = Util.Hex("#ff4f6a"),
                        Robe = false, Mask = true, Horns = true, Pauldrons = true, Right = Weapon.Club, Height = 1.45f, Bulk = 1.5f };
                case EnemyKind.Boss:
                    return new Look { Skin = Util.Hex("#3a2a44"), Cloth = Util.Hex("#1a0d24"), Cloth2 = Util.Hex("#0d0612"), Trim = Util.Hex("#c04aff"), Glow = Util.Hex("#ff5ab4"),
                        Robe = true, Mask = true, Crown = true, Cape = true, Pauldrons = true, Right = Weapon.Staff, Left = Weapon.Orb, Height = 3.0f, Bulk = 1.35f };
                default:
                    return new Look { Skin = skin, Cloth = Util.Hex("#2b1d3a"), Cloth2 = Util.Hex("#15101f"), Trim = Util.Hex("#6b2f8a"), Glow = Util.Hex("#ff9a3a"),
                        Robe = true, Hood = true, Mask = true, Right = Weapon.Sword };
            }
        }
    }
}
