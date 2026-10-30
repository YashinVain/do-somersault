using UnityEngine;
using UnityEngine.UI;

namespace JumpGame.EditorTools
{
    // The screens that sit on top of the map: buying a location, the summary after a location and the character shop.
    public static partial class PrototypeSceneBuilder
    {
        static Button titleShop;

        struct MapExtras
        {
            public Button buy, back;
            public Text buyLabel, mapCoins;
            public UiPanel summary;
            public Text summaryTitle, summaryTotals, summaryCoins, summaryNextLabel;
            public Text[] rowLabels, rowScores;
            public Image[] rowDots;
            public Button summaryNext, summaryStay;
            public RectTransform[] mapStaggered;
        }

        static Image Panel(UiKit ui, string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, Color color)
        {
            var img = ui.Rect(name, parent, anchor, pos, size).gameObject.AddComponent<Image>();
            img.sprite = ui.pill;
            img.type = Image.Type.Sliced;
            img.color = color;
            return img;
        }

        // Label without the cartoon outline and shadow.
        static Text Flat(UiKit ui, string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, int fontSize, TextAnchor align, string color, FontStyle style = FontStyle.Normal)
        {
            var t = ui.Label(name, parent, anchor, pos, size, fontSize, align, style);
            Object.DestroyImmediate(t.GetComponent<Outline>());
            Object.DestroyImmediate(t.GetComponent<Shadow>());
            t.color = Hex(color);
            return t;
        }

        static void Flatten(Button b)
        {
            Object.DestroyImmediate(b.GetComponent<Shadow>());
            var label = b.GetComponentInChildren<Text>();
            Object.DestroyImmediate(label.GetComponent<Outline>());
            Object.DestroyImmediate(label.GetComponent<Shadow>());
        }

        static MapExtras BuildMapExtras(UiKit ui, RectTransform mapPanel)
        {
            var x = new MapExtras();
            var center = new Vector2(0.5f, 0.5f);
            var topLeft = new Vector2(0f, 1f);
            var topRight = new Vector2(1f, 1f);

            // Buying a paid location.
            x.buy = ui.Btn("Buy", mapPanel, center, new Vector2(0f, -120f), new Vector2(380f, 84f), "Купить", "#EE6A55", 32);
            x.buyLabel = x.buy.GetComponentInChildren<Text>();

            // Back to the title, and the coin count.
            x.back = ui.Btn("Back", mapPanel, topLeft, new Vector2(24f, -20f), new Vector2(150f, 54f), "Меню", "#2BA39C", 26);
            SoundButton(ui, mapPanel, topLeft, new Vector2(186f, -20f), new Vector2(190f, 54f), "#34495E", false);
            var coinDot = ui.Rect("CoinDot", mapPanel, topRight, new Vector2(-24f, -30f), new Vector2(26f, 26f)).gameObject.AddComponent<Image>();
            coinDot.sprite = ui.knob;
            coinDot.color = new Color(1f, 0.8f, 0.26f);
            coinDot.raycastTarget = false;
            x.mapCoins = ui.Label("MapCoins", mapPanel, topRight, new Vector2(-58f, -26f), new Vector2(150f, 34f), 26, TextAnchor.MiddleRight);

            // Location summary: a dim layer, a card with one row per dive and two buttons.
            var summaryRt = ui.Stretch("LocationSummary", ui.canvas);
            var dim = summaryRt.gameObject.AddComponent<Image>();
            dim.color = new Color(0.03f, 0.05f, 0.1f, 0.62f);
            var card = Panel(ui, "Card", summaryRt, center, new Vector2(0f, 6f), new Vector2(780f, 660f), new Color(0.09f, 0.13f, 0.22f, 0.97f)).rectTransform;

            x.summaryTitle = Flat(ui, "Title", card, center, new Vector2(0f, 284f), new Vector2(720f, 60f), 42, TextAnchor.MiddleCenter, "#F4F1EA", FontStyle.Bold);
            var bar = ui.Rect("Bar", card, center, new Vector2(0f, 244f), new Vector2(84f, 5f)).gameObject.AddComponent<Image>();
            bar.color = Hex("#EE6A55");
            bar.raycastTarget = false;

            const int rows = 8;
            x.rowLabels = new Text[rows];
            x.rowScores = new Text[rows];
            x.rowDots = new Image[rows * 3];
            var stagger = new System.Collections.Generic.List<RectTransform> { x.summaryTitle.rectTransform };
            for (int i = 0; i < rows; i++)
            {
                var row = ui.Rect("Row_" + i, card, center, new Vector2(0f, 196f - i * 42f), new Vector2(660f, 40f));
                x.rowLabels[i] = Flat(ui, "Label", row, center, new Vector2(-190f, 0f), new Vector2(280f, 40f), 28, TextAnchor.MiddleLeft, "#E6EBF0");
                for (int d = 0; d < 3; d++)
                {
                    var dot = ui.Rect("Medal_" + d, row, center, new Vector2(20f + d * 40f, 0f), new Vector2(28f, 28f)).gameObject.AddComponent<Image>();
                    dot.sprite = ui.knob;
                    dot.raycastTarget = false;
                    x.rowDots[i * 3 + d] = dot;
                }
                x.rowScores[i] = Flat(ui, "Score", row, center, new Vector2(240f, 0f), new Vector2(180f, 40f), 28, TextAnchor.MiddleRight, "#FFD24A", FontStyle.Bold);
                stagger.Add(row);
            }

            x.summaryTotals = Flat(ui, "Totals", card, center, new Vector2(0f, -152f), new Vector2(720f, 40f), 30, TextAnchor.MiddleCenter, "#F4F1EA", FontStyle.Bold);
            x.summaryCoins = Flat(ui, "Coins", card, center, new Vector2(0f, -194f), new Vector2(720f, 34f), 24, TextAnchor.MiddleCenter, "#FFD24A");
            stagger.Add(x.summaryTotals.rectTransform);
            stagger.Add(x.summaryCoins.rectTransform);

            x.summaryStay = ui.Btn("Stay", card, center, new Vector2(-190f, -266f), new Vector2(300f, 74f), "Остаться", "#2BA39C", 28);
            x.summaryNext = ui.Btn("Next", card, center, new Vector2(170f, -266f), new Vector2(340f, 74f), "Следующая локация", "#EE6A55", 28);
            x.summaryNextLabel = x.summaryNext.GetComponentInChildren<Text>();
            stagger.Add((RectTransform)x.summaryStay.transform);
            stagger.Add((RectTransform)x.summaryNext.transform);

            x.summary = summaryRt.gameObject.AddComponent<UiPanel>();
            LinkArray(x.summary, "staggered", stagger.ToArray());
            Set(x.summary, "slide", new Vector2(0f, -30f));
            summaryRt.gameObject.SetActive(false);

            x.mapStaggered = new RectTransform[0];
            return x;
        }

        // The character shop: a dark panel on the left, the diver stays on the hero shot on the right.
        static ShopScreen BuildShop(UiKit ui, GameFlow flow, UiPanel titlePanel)
        {
            var left = new Vector2(0f, 0.5f);
            var topRight = new Vector2(1f, 1f);
            var rt = ui.Stretch("ShopScreen", ui.canvas);

            var shade = ui.Stretch("Shade", rt).gameObject.AddComponent<RawImage>();
            shade.texture = AssetDatabaseLoadShade();
            shade.raycastTarget = true;

            RectTransform LeftText(string n, Vector2 pos, Vector2 size, int fontSize, string color, FontStyle style, out Text text)
            {
                text = Flat(ui, n, rt, left, pos, size, fontSize, TextAnchor.MiddleLeft, color, style);
                text.rectTransform.pivot = new Vector2(0f, 0.5f);
                text.rectTransform.anchoredPosition = pos;
                return text.rectTransform;
            }

            var heading = LeftText("Heading", new Vector2(96f, 250f), new Vector2(500f, 36f), 26, "#8E9CAB", FontStyle.Normal, out var headingText);
            headingText.text = "МАГАЗИН";
            var title = LeftText("Name", new Vector2(96f, 170f), new Vector2(620f, 90f), 66, "#F4F1EA", FontStyle.Bold, out var nameText);
            var blurb = LeftText("Blurb", new Vector2(98f, 80f), new Vector2(430f, 90f), 26, "#C8D2DB", FontStyle.Normal, out var blurbText);
            blurbText.horizontalOverflow = HorizontalWrapMode.Wrap;
            blurbText.alignment = TextAnchor.UpperLeft;
            var perk = LeftText("Perk", new Vector2(98f, 6f), new Vector2(430f, 100f), 22, "#7FD3B5", FontStyle.Normal, out var perkText);
            perkText.horizontalOverflow = HorizontalWrapMode.Wrap;
            perkText.alignment = TextAnchor.UpperLeft;
            perkText.rectTransform.pivot = new Vector2(0f, 1f);
            perkText.rectTransform.anchoredPosition = new Vector2(98f, 40f);
            var status = LeftText("Status", new Vector2(98f, -62f), new Vector2(500f, 44f), 34, "#FFD24A", FontStyle.Bold, out var statusText);

            var action = ui.Btn("Action", rt, left, new Vector2(98f, -128f), new Vector2(300f, 70f), "Купить", "#EE6A55", 32);
            var actionRt = (RectTransform)action.transform;
            actionRt.pivot = new Vector2(0f, 0.5f);
            actionRt.anchoredPosition = new Vector2(98f, -128f);
            Flatten(action);

            var prev = ui.Btn("Prev", rt, left, new Vector2(98f, -212f), new Vector2(70f, 60f), "<", "#34495E", 32);
            var next = ui.Btn("Next", rt, left, new Vector2(318f, -212f), new Vector2(70f, 60f), ">", "#34495E", 32);
            foreach (var b in new[] { prev, next })
            {
                var brt = (RectTransform)b.transform;
                brt.pivot = new Vector2(0f, 0.5f);
                Flatten(b);
            }
            ((RectTransform)prev.transform).anchoredPosition = new Vector2(98f, -212f);
            ((RectTransform)next.transform).anchoredPosition = new Vector2(318f, -212f);
            var counter = Flat(ui, "Counter", rt, left, new Vector2(243f, -212f), new Vector2(120f, 40f), 26, TextAnchor.MiddleCenter, "#C8D2DB");
            counter.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            counter.rectTransform.anchoredPosition = new Vector2(243f, -212f);

            var back = ui.Btn("Back", rt, left, new Vector2(98f, -282f), new Vector2(180f, 58f), "Назад", "#2BA39C", 26);
            ((RectTransform)back.transform).pivot = new Vector2(0f, 0.5f);
            ((RectTransform)back.transform).anchoredPosition = new Vector2(98f, -282f);
            Flatten(back);

            var coinDot = ui.Rect("CoinDot", rt, topRight, new Vector2(-24f, -30f), new Vector2(26f, 26f)).gameObject.AddComponent<Image>();
            coinDot.sprite = ui.knob;
            coinDot.color = new Color(1f, 0.8f, 0.26f);
            coinDot.raycastTarget = false;
            var coins = ui.Label("Coins", rt, topRight, new Vector2(-58f, -26f), new Vector2(150f, 34f), 26, TextAnchor.MiddleRight);

            var shop = ui.canvas.gameObject.AddComponent<ShopScreen>();
            var panel = rt.gameObject.AddComponent<UiPanel>();
            LinkArray(panel, "staggered", new Object[] { heading, title, blurb, perk, status, actionRt, prev.transform, next.transform, counter.rectTransform, back.transform });
            Link(shop, "flow", flow);
            Link(shop, "panel", panel);
            Link(shop, "titlePanel", titlePanel);
            Link(shop, "openButton", titleShop);
            Link(shop, "backButton", back);
            Link(shop, "prevButton", prev);
            Link(shop, "nextButton", next);
            Link(shop, "actionButton", action);
            Link(shop, "nameText", nameText);
            Link(shop, "blurbText", blurbText);
            Link(shop, "perkText", perkText);
            Link(shop, "statusText", statusText);
            Link(shop, "counterText", counter);
            Link(shop, "coinsText", coins);
            Link(shop, "actionLabel", action.GetComponentInChildren<Text>());
            rt.gameObject.SetActive(false);
            return shop;
        }

        static Texture2D AssetDatabaseLoadShade() => UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(TextureDir + "/TitleShade.png");
    }
}
