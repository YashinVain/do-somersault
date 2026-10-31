using System;
using UnityEngine;

namespace JumpGame
{
    // What a character wears. Purely cosmetic: every character moves and weighs the same,
    // so medals and levels stay fair whoever you pick.
    [Flags]
    public enum Gear
    {
        None = 0,
        SwimCap = 1 << 0,
        Goggles = 1 << 1,
        Eyes = 1 << 2,
        Hair = 1 << 3,
        Beard = 1 << 4,
        Ponytail = 1 << 5,
        Shirt = 1 << 6,
        Backpack = 1 << 7,
        Tank = 1 << 8,
        SwimRing = 1 << 9,
        Antenna = 1 << 10,
        PirateHat = 1 << 11,
        Eyepatch = 1 << 12,
        Sunglasses = 1 << 13,
        Mask = 1 << 14,
        Blade = 1 << 15
    }

    // What a character does differently. Every value is a multiplier (1 = like the swimmer) or a flat bonus.
    // The medal thresholds are tuned for the swimmer, so the perks nudge the dive rather than rewrite it.
    public class Perk
    {
        public string title = "", text = "";
        public float gravity = 1f, spin = 1f, jump = 1f, edge = 1f, splash = 1f, coins = 1f, cannonball = 1f;
        [Tooltip("Flat points added (times the ring multiplier) for a clean head-first or feet-first entry and for the bullseye.")]
        public int headClean, feetClean, bullseye;
        [Tooltip("Points given for a flop, a slip or a knock on the tower instead of nothing.")]
        public int failScore;
        [Tooltip("Short-circuits in the water: sparks, then stands still.")]
        public bool breaks;

        public static readonly Perk None = new Perk();
    }

    public class CharacterSkin
    {
        public string title, blurb;
        public int price;
        public Color skin, face, outfit, cap, strap, lens, shirt, hair, accent;
        public Gear gear;
        [Tooltip("Torso width multiplier.")]
        public float width = 1f;
        public bool metallic;
        public Perk perk = Perk.None;
        [Tooltip("Name of the authored model in DiverModel; empty is the shared human model.")]
        public string model;
    }

    // Placeholders built from colours and a few props until the Blender models arrive.
    public static class Skins
    {
        static Color C(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }

        static CharacterSkin Make(string title, string blurb, int price, string skin, string outfit, Gear gear,
                                  string face = null, string cap = "#FFC743", string strap = "#1E2A4A", string lens = "#2A2A30",
                                  string shirt = "#F4F1EA", string hair = "#3A2A20", string accent = "#C8553D",
                                  float width = 1f, bool metallic = false, Perk perk = null, string model = null)
        {
            return new CharacterSkin
            {
                title = title, blurb = blurb, price = price,
                skin = C(skin), face = C(face ?? skin), outfit = C(outfit), cap = C(cap), strap = C(strap), lens = C(lens),
                shirt = C(shirt), hair = C(hair), accent = C(accent), gear = gear, width = width, metallic = metallic,
                perk = perk ?? Perk.None, model = model
            };
        }

        public static readonly CharacterSkin[] All =
        {
            Make("Пловец", "Шапочка, очки, плавки. С него всё началось.", 0, "#EFBC98", "#283B8F", Gear.SwimCap | Gear.Goggles,
                 lens: "#7FD3F0",
                 perk: new Perk { title = "Чистая работа", text = "+50 очков за чистый вход головой", headClean = 50 }),
            Make("Школьник", "Портфель не оставил даже на вышке.", 150, "#F2C6A5", "#2E3A56", Gear.Hair | Gear.Shirt | Gear.Backpack | Gear.Eyes,
                 hair: "#3A2A20", shirt: "#F4F1EA", accent: "#C8553D",
                 perk: new Perk { title = "Карманные деньги", text = "Портфель тянет вниз, зато монет на 25% больше", gravity = 1.1f, coins = 1.25f }),
            Make("Толстяк", "Весит много, плюхает громко. Круг на всякий случай.", 200, "#EDB28F", "#E4572E", Gear.SwimRing | Gear.Sunglasses,
                 accent: "#35B6A8", strap: "#22252B", width: 1.34f,
                 perk: new Perk { title = "Плюх", text = "Огромные брызги и волны, бомбочка ×1,5. Крутится медленнее", splash = 2.2f, cannonball = 1.5f, spin = 0.85f, jump = 0.93f }),
            Make("Дед", "Прыгает с вышки с 1962 года и не собирается останавливаться.", 250, "#E2AE92", "#2C8C7A", Gear.Hair | Gear.Beard | Gear.Shirt | Gear.Eyes,
                 hair: "#EDEDED", shirt: "#9CC3D8", width: 1.08f,
                 perk: new Perk { title = "Старая школа", text = "Долго стоит на краю, не срываясь. +60 за ровного солдатика", edge = 0.6f, feetClean = 60, spin = 0.92f }),
            Make("Спортсменка", "Тренируется с шести утра, хвост всегда собран.", 300, "#E8B08E", "#D93B4C", Gear.Hair | Gear.Ponytail | Gear.Shirt | Gear.Eyes,
                 hair: "#4A2C1F", shirt: "#D93B4C",
                 perk: new Perk { title = "Гибкость", text = "Вращается быстрее всех", spin = 1.15f }),
            Make("Зомби", "Сначала нырнул, потом всё остальное.", 400, "#8FB07A", "#4B4A52", Gear.Hair | Gear.Shirt | Gear.Eyes,
                 hair: "#3D4A33", shirt: "#5A6B4E", lens: "#D94141",
                 perk: new Perk { title = "Не больно", text = "Плашмя, срыв и удар о вышку дают 50 очков вместо нуля", failScore = 50 }),
            Make("Спортсмен с протезом", "Бегал на лезвии, теперь прыгает с вышки. Рекордсмен по входам.", 500, "#E8B796", "#1E4FA3", Gear.SwimCap | Gear.Goggles | Gear.Blade,
                 cap: "#1E4FA3", lens: "#7FD3F0",
                 perk: new Perk { title = "Пружина", text = "Лезвие толкает сильнее: прыжок выше и дальше", jump = 1.12f }),
            Make("Водолаз", "Баллоны, маска и ласты. Вода — его дом.", 600, "#20252C", "#1B6FB0", Gear.Mask | Gear.Tank,
                 face: "#E5B08F", strap: "#22252B", lens: "#7FD3F0", accent: "#E8B422",
                 perk: new Perk { title = "Тихий вход", text = "Почти без брызг. +80 за чистый вход головой, +40 за солдатика", splash = 0.4f, headClean = 80, feetClean = 40 }),
            Make("Пират", "Корабль потерял, но вышку нашёл.", 800, "#D9A27E", "#6B3E2A", Gear.PirateHat | Gear.Eyepatch | Gear.Beard | Gear.Shirt | Gear.Eyes,
                 hair: "#1E1A18", shirt: "#C94B4B", accent: "#2B2320", strap: "#15120F",
                 perk: new Perk { title = "Сокровища", text = "На 50% больше монет за каждый прыжок", coins = 1.5f }),
            Make("Робот", "Считает обороты точнее судей.", 1000, "#AEB6C0", "#5A6572", Gear.Antenna | Gear.Eyes,
                 face: "#C5CCD4", lens: "#66F0FF", accent: "#FF5E5E", metallic: true, model: "Robot",
                 perk: new Perk { title = "Тяжёлый и хрупкий", text = "Падает быстрее, центр круга даёт +200. После входа в воду замыкает и ломается", gravity = 1.3f, bullseye = 200, breaks = true })
        };
    }
}
