using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UI : MonoBehaviour
{
    public static UI I;
    Canvas canvas; CanvasScaler scaler;
    RectTransform root, hud, screens, tags, floats;
    Font F => Kit.Font;
    public static readonly Color Ink = Kit.Hex("#2b1b12"), Cream = Kit.Hex("#fff6e5"), Tomato = Kit.Hex("#ff5a3c"), Mustard = Kit.Hex("#ffc53d"), Mint = Kit.Hex("#5de0a5"), Sky = Kit.Hex("#46b8ff");
    static readonly Color Dim = new Color(0.22f, 0.11f, 0.05f, 0.72f);   // secondary buttons: readable on the light floor

    // ---- input
    public Vector2 Joy { get; private set; }
    // presses latch until the game reads them, so a tap is never lost to update order
    int actPresses, dashPresses;
    public bool ActPressed { get { bool b = actPresses > 0; actPresses = 0; return b; } }
    public bool ActHeld => actBtn && actBtn.Held;
    public bool DashPressed { get { bool b = dashPresses > 0; dashPresses = 0; return b; } }
    HoldButton actBtn, dashBtn;
    RectTransform joyBase, joyKnob; int joyId = -99; Vector2 joyOrigin; bool joyMouse;
    const float JoyRadius = 115f;
    Text actText;

    // ---- hud
    Text scoreText, timeText, bigText, toastText;
    float bigT, toastT;
    class Card { public RectTransform rt; public Image bg, dish, fill; public Image[] parts; public Text name; public int id = -1; public float pop; }
    readonly List<Card> cards = new List<Card>();
    RectTransform tickets;
    readonly List<(Transform t, Text x)> nameTags = new List<(Transform, Text)>();

    static readonly Dictionary<string, Sprite> icons = new Dictionary<string, Sprite>();
    public static Sprite Icon(string n) { if (!icons.TryGetValue(n, out var s)) icons[n] = s = Resources.Load<Sprite>("Icons/" + n); return s; }
    static Sprite disc, ring, star;

    public void Init()
    {
        I = this;
        var es = new GameObject("EventSystem"); es.AddComponent<EventSystem>().pixelDragThreshold = 2; es.AddComponent<StandaloneInputModule>();
        canvas = gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1080, 1920);
        gameObject.AddComponent<GraphicRaycaster>();
        root = (RectTransform)transform;
        disc = Spr(Kit.Disc); ring = Spr(Kit.Ring); star = Spr(StarTex());

        tags = Fill("tags", root);
        floats = Fill("floats", root);
        BuildHud();
        screens = Fill("screens", root);
        bigText = Txt(root, "", 230, new Vector2(.5f, .56f), Vector2.zero, Cream, TextAnchor.MiddleCenter, 1400);
        bigText.fontStyle = FontStyle.Italic; Outline(bigText, 7); bigText.gameObject.SetActive(false);
        toastText = Txt(root, "", 40, new Vector2(.5f, 1), new Vector2(0, -420), Cream, TextAnchor.MiddleCenter, 1300);
        Outline(toastText, 3); toastText.gameObject.SetActive(false);
    }

    static Sprite Spr(Texture2D t) => Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(.5f, .5f));

    static Texture2D StarTex()
    {
        int n = 128; var t = new Texture2D(n, n, TextureFormat.RGBA32, false); var px = new Color[n * n];
        var pts = new Vector2[10];
        for (int i = 0; i < 10; i++) { float a = Mathf.PI / 2 + i * Mathf.PI / 5; float r = i % 2 == 0 ? 0.48f : 0.2f; pts[i] = new Vector2(0.5f + Mathf.Cos(a) * r, 0.5f + Mathf.Sin(a) * r); }
        for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
        {
            var p = new Vector2((x + .5f) / n, (y + .5f) / n);
            bool inside = false;
            for (int i = 0, j = 9; i < 10; j = i++)
                if ((pts[i].y > p.y) != (pts[j].y > p.y) && p.x < (pts[j].x - pts[i].x) * (p.y - pts[i].y) / (pts[j].y - pts[i].y) + pts[i].x) inside = !inside;
            px[y * n + x] = new Color(1, 1, 1, inside ? 1 : 0);
        }
        t.SetPixels(px); t.Apply(); return t;
    }

    // ---------------------------------------------------------------- building blocks
    RectTransform Rect(string n, Transform p, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(n, typeof(RectTransform)); go.transform.SetParent(p, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = anchor; rt.pivot = new Vector2(.5f, .5f); rt.anchoredPosition = pos; rt.sizeDelta = size;
        return rt;
    }
    RectTransform Fill(string n, Transform p)
    {
        var rt = Rect(n, p, Vector2.zero, Vector2.zero, Vector2.zero);
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero; return rt;
    }
    RectTransform Box(Transform p, Vector2 anchor, Vector2 pos, Vector2 size, Color c, bool ray = false)
    {
        var rt = Rect("box", p, anchor, pos, size);
        var img = rt.gameObject.AddComponent<Image>(); img.sprite = Kit.RoundedSprite; img.type = Image.Type.Sliced; img.color = c; img.raycastTarget = ray;
        return rt;
    }
    Image Img(Transform p, Sprite s, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        var rt = Rect("img", p, anchor, pos, size);
        var img = rt.gameObject.AddComponent<Image>(); img.sprite = s; img.preserveAspect = true; img.raycastTarget = false; return img;
    }
    Text Txt(Transform p, string s, int size, Vector2 anchor, Vector2 pos, Color c, TextAnchor align = TextAnchor.MiddleCenter, float w = 700)
    {
        size = Mathf.Max(size, 30);   // readable floor: Lilita below this turns to mush on phones and short desktop windows
        var rt = Rect("txt", p, anchor, pos, new Vector2(w, size * 1.4f));
        var t = rt.gameObject.AddComponent<Text>();
        t.font = F; t.fontSize = size; t.fontStyle = FontStyle.Normal; t.alignment = align; t.color = c; t.text = s;
        t.raycastTarget = false; t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }
    static void Outline(Text t, float d) { var o = t.gameObject.AddComponent<Outline>(); o.effectColor = new Color(0.1f, 0.05f, 0.02f, 0.85f); o.effectDistance = new Vector2(d, -d); }
    Button Btn(Transform p, string label, Vector2 anchor, Vector2 pos, Vector2 size, Color bg, Color fg, Action onClick, int fs = 48)
    {
        var shadow = Box(p, anchor, pos + new Vector2(0, -10), size, Color.Lerp(bg, Color.black, 0.45f));
        var rt = Box(p, anchor, pos, size, bg, true);
        var b = rt.gameObject.AddComponent<Button>(); b.targetGraphic = rt.GetComponent<Image>();
        b.onClick.AddListener(() => { Sfx.I.Click(); onClick(); });
        rt.gameObject.AddComponent<Press>().Sink = 8;   // the face drops onto its shadow
        var t = Txt(rt, label, fs, new Vector2(.5f, .5f), Vector2.zero, fg, TextAnchor.MiddleCenter, size.x);
        t.fontStyle = FontStyle.Normal;
        return b;
    }

    // ---------------------------------------------------------------- HUD
    void BuildHud()
    {
        hud = Fill("hud", root);

        // left half: floating joystick
        var zone = Rect("joyzone", hud, Vector2.zero, Vector2.zero, Vector2.zero);
        zone.anchorMin = Vector2.zero; zone.anchorMax = new Vector2(0.55f, 0.7f); zone.offsetMin = zone.offsetMax = Vector2.zero;
        joyBase = (RectTransform)Img(hud, ring, Vector2.zero, Vector2.zero, new Vector2(JoyRadius * 2.2f, JoyRadius * 2.2f)).transform;
        joyBase.GetComponent<Image>().color = new Color(1, 1, 1, 0.5f);
        joyKnob = (RectTransform)Img(joyBase, disc, new Vector2(.5f, .5f), Vector2.zero, new Vector2(110, 110)).transform;
        joyKnob.GetComponent<Image>().color = new Color(1, 1, 1, 0.85f);
        joyBase.gameObject.SetActive(false);

        // right: action + dash
        var a = Rect("act", hud, new Vector2(1, 0), new Vector2(-210, 250), new Vector2(300, 300));
        var ai = a.gameObject.AddComponent<Image>(); ai.sprite = disc; ai.color = Kit.A(Tomato, 0.92f);
        actBtn = a.gameObject.AddComponent<HoldButton>();
        actBtn.OnDown = () => actPresses++;
        Img(a, ring, new Vector2(.5f, .5f), Vector2.zero, new Vector2(300, 300)).color = new Color(1, 1, 1, 0.8f);
        // beat ring: shrinks onto the button on every beat of the music
        beatRing = Img(a, ring, new Vector2(.5f, .5f), Vector2.zero, new Vector2(300, 300)); beatRing.raycastTarget = false;
        actText = Txt(a, "", 40, new Vector2(.5f, .5f), Vector2.zero, Color.white, TextAnchor.MiddleCenter, 280);
        actText.horizontalOverflow = HorizontalWrapMode.Wrap; actText.rectTransform.sizeDelta = new Vector2(250, 120); Outline(actText, 2);
        var d = Rect("dash", hud, new Vector2(1, 0), new Vector2(-440, 150), new Vector2(170, 170));
        var di = d.gameObject.AddComponent<Image>(); di.sprite = disc; di.color = Kit.A(Sky, 0.85f);
        dashBtn = d.gameObject.AddComponent<HoldButton>();
        dashBtn.OnDown = () => dashPresses++;
        var dt = Txt(d, "DASH", 36, new Vector2(.5f, .5f), Vector2.zero, Color.white); Outline(dt, 2);

        // top bar
        var bar = Box(hud, new Vector2(.5f, 1), new Vector2(0, -60), new Vector2(0, 100), new Color(0.1f, 0.06f, 0.03f, 0.55f));
        bar.anchorMin = new Vector2(0, 1); bar.anchorMax = new Vector2(1, 1); bar.sizeDelta = new Vector2(-40, 100);
        scoreText = Txt(bar, "0", 60, new Vector2(0, .5f), new Vector2(200, 0), Mustard, TextAnchor.MiddleLeft, 400);
        Outline(scoreText, 3);
        timeText = Txt(bar, "3:00", 60, new Vector2(.5f, .5f), Vector2.zero, Cream, TextAnchor.MiddleCenter, 300);
        Outline(timeText, 3);
        Btn(bar, "II", new Vector2(1, .5f), new Vector2(-70, 0), new Vector2(90, 80), new Color(1, 1, 1, 0.2f), Color.white, () => Game.I.Pause(), 40);

        // crew beat streak meter
        streakText = Txt(hud, "", 40, new Vector2(1, 0), new Vector2(-210, 450), Mint, TextAnchor.MiddleCenter, 420);
        streakText.fontStyle = FontStyle.Italic; Outline(streakText, 3);

        // order tickets (scaled up on landscape screens, where the canvas matches height)
        tickets = Rect("tickets", hud, new Vector2(0, 1), Vector2.zero, Vector2.zero);
        tickets.pivot = new Vector2(0, 1);
        for (int i = 0; i < 5; i++)
        {
            var c = new Card();
            c.rt = Box(tickets, new Vector2(0, 1), new Vector2(125 + i * 208, -225), new Vector2(196, 200), Cream);
            c.bg = c.rt.GetComponent<Image>();
            c.dish = Img(c.rt, null, new Vector2(.5f, 1), new Vector2(0, -62), new Vector2(104, 104));
            c.parts = new Image[5];
            for (int k = 0; k < 5; k++) c.parts[k] = Img(c.rt, null, new Vector2(.5f, 0), new Vector2(0, 58), new Vector2(40, 40));
            var fb = Box(c.rt, new Vector2(.5f, 0), new Vector2(0, 20), new Vector2(170, 18), new Color(0, 0, 0, 0.2f));
            c.fill = Box(fb, new Vector2(0, .5f), Vector2.zero, new Vector2(170, 18), Mint).GetComponent<Image>();
            c.fill.rectTransform.pivot = new Vector2(0, .5f);
            c.rt.gameObject.SetActive(false);
            cards.Add(c);
        }
        hud.gameObject.SetActive(false);
    }

    public void ShowHud(bool on) { hud.gameObject.SetActive(on); if (!on) ClearTags(); }

    public void SetActionLabel(string s)
    {
        if (actText.text != s) actText.text = s;
        var img = actBtn.GetComponent<Image>();
        img.color = Kit.A(string.IsNullOrEmpty(s) ? Color.Lerp(Tomato, Color.gray, 0.55f) : s == "SERVE!" ? Mint : Tomato, 0.92f);
    }

    Image beatRing; Text streakText; float beatFlash;
    public void BeatHit() => beatFlash = 1f;

    public void UpdateHud(Game g)
    {
        // beat ring + streak meter + the kitchen lights pulse with the music
        float ph = Sfx.I.Phase01;
        beatFlash = Mathf.Max(0, beatFlash - Time.unscaledDeltaTime * 3f);
        beatRing.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.0f, 1.55f, 1f - ph);
        beatRing.color = Color.Lerp(Kit.A(Color.white, 0.15f + 0.6f * ph * ph), Mint, beatFlash);
        int bs = g.BeatStreak;
        streakText.text = bs >= 2 ? "BEAT x" + bs + "   TIPS x" + g.TipMult.ToString("0.00") : "";
        streakText.color = bs >= 10 ? Mustard : Mint;
        streakText.rectTransform.localScale = Vector3.one * (1f + beatFlash * 0.15f);
        g.Pulse(Mathf.Exp(-ph * 7f));
        scoreText.text = g.Score.ToString();
        int t = Mathf.CeilToInt(Mathf.Max(0, g.TimeLeft));
        timeText.text = (t / 60) + ":" + (t % 60).ToString("00");
        timeText.color = t <= 20 && Mathf.PingPong(Time.unscaledTime * 3f, 1f) > 0.5f ? Tomato : Cream;
        // tickets: keep existing cards in place, new ones pop in
        for (int i = 0; i < cards.Count; i++)
        {
            var c = cards[i];
            bool on = i < g.Orders.Count;
            c.rt.gameObject.SetActive(on);
            if (!on) { c.id = -1; continue; }
            var o = g.Orders[i];
            var r = Recipes.All[o.recipe];
            if (c.id != o.id)
            {
                c.id = o.id; c.pop = 0;
                c.dish.sprite = Icon(r.model);
                for (int k = 0; k < c.parts.Length; k++)
                {
                    bool show = k < r.parts.Length;
                    c.parts[k].gameObject.SetActive(show);
                    if (!show) continue;
                    c.parts[k].sprite = Icon(It.Icon(r.parts[k]));
                    c.parts[k].rectTransform.anchoredPosition = new Vector2((k - (r.parts.Length - 1) * 0.5f) * 36f, 58);
                }
            }
            float k01 = Mathf.Clamp01(o.left / o.total);
            c.fill.rectTransform.sizeDelta = new Vector2(170 * k01, 18);
            c.fill.color = k01 > 0.5f ? Mint : k01 > 0.25f ? Mustard : Tomato;
            c.pop = Mathf.Min(1f, c.pop + Time.unscaledDeltaTime * 4f);
            float shake = k01 < 0.2f ? Mathf.Sin(Time.unscaledTime * 40f) * 4f : 0;
            c.rt.anchoredPosition = new Vector2(125 + i * 208 + shake, -225 + (1 - Kit.EaseOutBack(c.pop)) * 200);
            c.bg.color = k01 < 0.2f ? Color.Lerp(Cream, Tomato, Mathf.PingPong(Time.unscaledTime * 3, 0.5f)) : Cream;
        }
    }

    public void Big(string s, bool go)
    {
        bigText.text = s; bigText.color = go ? Mint : Cream; bigT = go ? 0.9f : 0.9f;
        bigText.gameObject.SetActive(true);
    }

    public void Toast(string s) { toastText.text = s; toastT = 2.8f; toastText.gameObject.SetActive(true); }

    public void Float(Vector3 world, string s, Color c, float scale = 1f) => StartCoroutine(FloatCo(world, s, c, scale));

    IEnumerator FloatCo(Vector3 world, string s, Color c, float scale)
    {
        var t = Txt(floats, s, Mathf.RoundToInt(44 * scale), Vector2.zero, Vector2.zero, c, TextAnchor.MiddleCenter, 700);
        Outline(t, 3);
        float k = 0;
        while (k < 1f)
        {
            k += Time.unscaledDeltaTime / 1.2f;
            var cam = Game.I.Cam;
            t.rectTransform.position = cam.WorldToScreenPoint(world) + Vector3.up * k * 110f * canvas.scaleFactor;
            t.rectTransform.localScale = Vector3.one * (k < 0.15f ? Mathf.Lerp(0.4f, 1.15f, k / 0.15f) : 1f);
            t.color = Kit.A(c, Mathf.Clamp01((1 - k) * 3));
            yield return null;
        }
        Destroy(t.gameObject);
    }

    public void NameTag(Transform t, string name, Color c)
    {
        var x = Txt(tags, name, 30, Vector2.zero, Vector2.zero, c, TextAnchor.MiddleCenter, 400);
        Outline(x, 2);
        nameTags.Add((t, x));
    }
    void ClearTags() { foreach (var n in nameTags) if (n.x) Destroy(n.x.gameObject); nameTags.Clear(); }

    // ---------------------------------------------------------------- screens
    RectTransform Screen(bool dim = true)
    {
        foreach (Transform c in screens) Destroy(c.gameObject);
        var s = Fill("screen", screens);
        if (dim) { var img = s.gameObject.AddComponent<Image>(); img.color = new Color(0.1f, 0.05f, 0.02f, 0.78f); }
        return s;
    }
    public void CloseScreens() { foreach (Transform c in screens) Destroy(c.gameObject); }

    IEnumerator Pop(RectTransform r, float delay = 0)
    {
        r.localScale = Vector3.zero;
        float k = -delay / 0.28f;
        while (k < 1f) { k += Time.unscaledDeltaTime / 0.28f; r.localScale = Vector3.one * Kit.EaseOutBack(Mathf.Clamp01(k)); yield return null; }
        r.localScale = Vector3.one;
    }
    IEnumerator Pulse(Transform t) { while (t) { t.localScale = Vector3.one * (1f + Mathf.Sin(Time.unscaledTime * 4f) * 0.035f); yield return null; } }

    Text Title(Transform p, string s, float y, int size, Color c)
    {
        var sh = Txt(p, s, size, new Vector2(.5f, 1), new Vector2(0, y - 12), Kit.Hex("#7a2412"), TextAnchor.MiddleCenter, 1400);
        sh.fontStyle = FontStyle.Italic;
        var t = Txt(p, s, size, new Vector2(.5f, 1), new Vector2(0, y), c, TextAnchor.MiddleCenter, 1400);
        t.fontStyle = FontStyle.Italic;
        return t;
    }

    public void ShowMenu()
    {
        ShowHud(false);
        var s = Screen(false);
        var g = Game.I;
        var shade = Box(s, new Vector2(.5f, .5f), Vector2.zero, Vector2.zero, new Color(0.12f, 0.06f, 0.02f, 0.35f));
        shade.anchorMin = Vector2.zero; shade.anchorMax = Vector2.one; shade.sizeDelta = Vector2.zero;
        Kit.Scrim(s, true, 760, new Color(0.14f, 0.07f, 0.03f, 0.75f));
        Kit.Scrim(s, false, 820, new Color(0.14f, 0.07f, 0.03f, 0.8f));
        var t = Title(s, "ORDER UP!", -230, 190, Mustard);
        StartCoroutine(Wobble(t.transform));
        var tag = Txt(s, "COOK TO THE BEAT  -  SOLO OR ONLINE CO-OP", 34, new Vector2(.5f, 1), new Vector2(0, -360), Cream, TextAnchor.MiddleCenter, 1000);
        Outline(tag, 2);

        // kitchens
        for (int i = 0; i < Kitchens.All.Length; i++)
        {
            var kd = Kitchens.All[i];
            bool sel = kd.id == g.Def.id;
            var card = Box(s, new Vector2(.5f, 1), new Vector2(i == 0 ? -240 : 240, -560), new Vector2(450, 270), sel ? kd.wall : new Color(1, 1, 1, 0.18f), true);
            Txt(card, kd.name, 50, new Vector2(.5f, .5f), new Vector2(0, 80), Color.white, TextAnchor.MiddleCenter, 440).fontStyle = FontStyle.Italic;
            var tl = Txt(card, kd.tagline, 25, new Vector2(.5f, .5f), new Vector2(0, 18), Kit.A(Color.white, 0.85f), TextAnchor.MiddleCenter, 400);
            tl.horizontalOverflow = HorizontalWrapMode.Wrap; tl.rectTransform.sizeDelta = new Vector2(400, 70); tl.fontStyle = FontStyle.Normal;
            int stars = g.Save.Stars(kd.id);
            for (int k = 0; k < 3; k++) Img(card, star, new Vector2(.5f, .5f), new Vector2(-70 + k * 70, -50), new Vector2(58, 58)).color = k < stars ? Mustard : new Color(0, 0, 0, 0.3f);
            int best = g.Save.Best(kd.id);
            Txt(card, best > 0 ? "BEST " + best : "NEW!", 30, new Vector2(.5f, .5f), new Vector2(0, -105), Color.white, TextAnchor.MiddleCenter, 400);
            var b = card.gameObject.AddComponent<Button>(); b.targetGraphic = card.GetComponent<Image>();
            var id = kd.id;
            b.onClick.AddListener(() => { if (id == Game.I.Def.id) return; Sfx.I.Click(); Game.I.SelectKitchen(id); });
        }

        // chef picker
        var pick = Box(s, new Vector2(.5f, 1), new Vector2(0, -900), new Vector2(560, 300), new Color(0, 0, 0, 0.3f));
        Img(pick, Icon(Looks.All[g.Save.look]), new Vector2(.5f, .5f), new Vector2(0, 16), new Vector2(250, 250));
        Txt(pick, "YOUR CHEF", 30, new Vector2(.5f, 0), new Vector2(0, 26), Cream);
        Btn(pick, "<", new Vector2(0, .5f), new Vector2(-40, 0), new Vector2(110, 150), new Color(1, 1, 1, 0.22f), Color.white, () => CycleLook(-1), 70);
        Btn(pick, ">", new Vector2(1, .5f), new Vector2(40, 0), new Vector2(110, 150), new Color(1, 1, 1, 0.22f), Color.white, () => CycleLook(1), 70);

        var solo = Btn(s, "COOK SOLO", new Vector2(.5f, 0), new Vector2(-240, 560), new Vector2(450, 170), Tomato, Color.white, () => g.StartSolo(), 58);
        StartCoroutine(Pulse(solo.transform));
        Btn(s, "CO-OP ONLINE", new Vector2(.5f, 0), new Vector2(240, 560), new Vector2(450, 170), Mint, Ink, () => g.OpenOnline(), 52);
        Btn(s, "LEADERBOARD", new Vector2(.5f, 0), new Vector2(0, 375), new Vector2(930, 120), Dim, Mustard, () => WebBridge.ShowBoard(Game.I.Def.id), 46);
        Btn(s, "HOW TO PLAY", new Vector2(.5f, 0), new Vector2(-240, 220), new Vector2(450, 110), Dim, Cream, ShowHowTo, 38);
        Btn(s, g.Save.muted ? "SOUND OFF" : "SOUND ON", new Vector2(.5f, 0), new Vector2(240, 220), new Vector2(450, 110), Dim, Cream, () => { g.ToggleMute(); ShowMenu(); }, 38);
    }

    IEnumerator Wobble(Transform t) { while (t) { t.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(Time.unscaledTime * 2.2f) * 2.5f); yield return null; } }

    void CycleLook(int d)
    {
        var g = Game.I;
        g.Save.look = (g.Save.look + d + Looks.All.Length) % Looks.All.Length;
        g.Persist();
        g.GoMenu();
    }

    public void ShowHowTo()
    {
        var s = Screen();
        Title(s, "HOW TO COOK", -220, 110, Mustard);
        (string icon, string text)[] rows =
        {
            ("burger-cheese", "Tickets at the top show what to cook.\nServe them before the timer runs out!"),
            ("tomato", "Grab ingredients from the crates.\nLettuce, tomato and cheese need CHOPPING."),
            ("cabbage", "KITCHEN BEATS: on a board, TAP to chop\nON THE BEAT. Twice as fast, bigger tips!"),
            ("meat-raw", "Patties go on the stove. Grab them when\ncooked - leave them and they BURN!"),
            ("plate", "Put ingredients on a plate, then take\nthe finished dish to the SERVING HATCH."),
            ("salad", "Move: drag left side / WASD.  Action:\nbutton / SPACE.  Dash: DASH / SHIFT."),
        };
        for (int i = 0; i < rows.Length; i++)
        {
            var row = Box(s, new Vector2(.5f, 1), new Vector2(0, -400 - i * 190), new Vector2(960, 170), new Color(1, 1, 1, 0.1f));
            Img(row, Icon(rows[i].icon), new Vector2(0, .5f), new Vector2(95, 0), new Vector2(130, 130));
            var t = Txt(row, rows[i].text, 34, new Vector2(0, .5f), new Vector2(560, 0), Cream, TextAnchor.MiddleLeft, 770);
            t.lineSpacing = 1.1f;
            StartCoroutine(Pop(row, 0.05f * i));
        }
        Btn(s, "LET'S COOK!", new Vector2(.5f, 0), new Vector2(0, 170), new Vector2(600, 150), Tomato, Color.white, () =>
        {
            Game.I.Save.howto = true; Game.I.Persist();
            if (Game.I.State == Game.St.Menu) ShowMenu(); else CloseScreens();
        }, 56);
    }

    public void ShowPause(bool online)
    {
        var s = Screen();
        Title(s, online ? "MENU" : "PAUSED", -520, 120, Cream);
        if (online) Txt(s, "Your crew is still cooking!", 36, new Vector2(.5f, 1), new Vector2(0, -640), Cream);
        Btn(s, "RESUME", new Vector2(.5f, .5f), new Vector2(0, 140), new Vector2(600, 160), Mint, Ink, () => Game.I.Resume(), 60);
        if (!online) Btn(s, "RESTART", new Vector2(.5f, .5f), new Vector2(0, -60), new Vector2(600, 130), Dim, Cream, () => { Game.I.Resume(); Game.I.StartSolo(); }, 46);
        Btn(s, "QUIT SHIFT", new Vector2(.5f, .5f), new Vector2(0, -230), new Vector2(600, 130), Dim, Tomato, () => Game.I.Quit(), 46);
    }

    Text rankText; Game.RankMsg lastRank;
    public void ShowResults(Game g, int stars, bool best)
    {
        ShowHud(false);
        var s = Screen();
        Title(s, stars >= 3 ? "MASTER CHEF!" : stars == 2 ? "GREAT SHIFT!" : stars == 1 ? "ORDER UP!" : "KITCHEN NIGHTMARE", -240, stars == 0 ? 90 : 120, stars > 0 ? Mustard : Tomato);
        for (int k = 0; k < 3; k++)
        {
            var st = Img(s, star, new Vector2(.5f, 1), new Vector2(-190 + k * 190, -450 + (k == 1 ? 30 : 0)), new Vector2(170, 170));
            st.color = k < stars ? Mustard : new Color(1, 1, 1, 0.15f);
            StartCoroutine(Pop(st.rectTransform, 0.25f + k * 0.25f));
            if (k < stars) StartCoroutine(StarSound(0.3f + k * 0.25f));
        }
        var sc = Txt(s, g.Score + " POINTS", 80, new Vector2(.5f, 1), new Vector2(0, -640), Color.white); Outline(sc, 4);
        Txt(s, g.Served + " SERVED   -   " + g.Failed + " MISSED" + (g.Chefs.Count > 1 ? "   -   CREW OF " + g.Chefs.Count : ""), 36, new Vector2(.5f, 1), new Vector2(0, -730), Cream, TextAnchor.MiddleCenter, 1000);
        Txt(s, g.OnBeatHits + " ON THE BEAT   -   BEST STREAK x" + g.BestStreak, 34, new Vector2(.5f, 1), new Vector2(0, -778), Mint, TextAnchor.MiddleCenter, 1000);
        rankText = Txt(s, best ? "NEW PERSONAL BEST!" : "BEST " + g.Save.Best(g.Def.id), 36, new Vector2(.5f, 1), new Vector2(0, -832), best ? Mint : Cream, TextAnchor.MiddleCenter, 1000);
        if (lastRank != null) ApplyRank();
        // next star goal
        float k1 = 1f + 0.55f * Mathf.Max(0, g.Chefs.Count - 1);
        if (stars < 3) Txt(s, "NEXT STAR AT " + Mathf.CeilToInt(g.Def.stars[stars] * k1), 30, new Vector2(.5f, 1), new Vector2(0, -884), Kit.A(Cream, 0.7f), TextAnchor.MiddleCenter, 1000);

        bool online = g.Mode != Game.Net.Solo;
        float y = 560;
        var again = Btn(s, online ? "COOK ONLINE AGAIN" : "COOK AGAIN", new Vector2(.5f, 0), new Vector2(0, y), new Vector2(680, 160), Tomato, Color.white, () => { if (online) g.OpenOnline(); else g.StartSolo(); }, 54);
        StartCoroutine(Pulse(again.transform));
        var share = Btn(s, "SHARE", new Vector2(.5f, 0), new Vector2(-320, y - 190), new Vector2(290, 120), Dim, Cream, () => { }, 42);
        share.gameObject.AddComponent<ShareOnPress>().Text = () => g.ShareText();
        var other = Kitchens.All[(Array.IndexOf(Kitchens.All, g.Def) + 1) % Kitchens.All.Length];
        Btn(s, other.name, new Vector2(.5f, 0), new Vector2(0, y - 190), new Vector2(290, 120), Dim, other.accent, () => g.SelectKitchen(other.id), 34);
        Btn(s, "MENU", new Vector2(.5f, 0), new Vector2(320, y - 190), new Vector2(290, 120), Dim, Cream, () => g.Quit(), 42);
        Btn(s, "LEADERBOARD", new Vector2(.5f, 0), new Vector2(0, y - 345), new Vector2(930, 110), Dim, Mustard, () => WebBridge.ShowBoard(g.Def.id), 40);
    }

    IEnumerator StarSound(float d) { yield return new WaitForSecondsRealtime(d); Sfx.I.Star(); }

    public void SetRank(Game.RankMsg m) { lastRank = m; ApplyRank(); }
    void ApplyRank()
    {
        if (!rankText || lastRank == null || lastRank.rank <= 0) return;
        rankText.text = "WORLD RANK #" + lastRank.rank + " OF " + lastRank.total + (lastRank.newBest ? "  -  NEW RECORD!" : "  -  BEST " + lastRank.best);
        rankText.color = lastRank.rank <= 10 ? Mustard : Mint;
    }

    // ---------------------------------------------------------------- per frame
    void Update()
    {
        float aspect = (float)UnityEngine.Screen.width / Mathf.Max(1, UnityEngine.Screen.height);
        scaler.matchWidthOrHeight = aspect > 0.75f ? 1f : 0f;
        if (tickets) tickets.localScale = Vector3.one * (aspect > 1.2f ? 1.35f : 1f);
        UpdateJoystick();
        float udt = Time.unscaledDeltaTime;
        if (bigT > 0)
        {
            bigT -= udt;
            float k = 1f - bigT / 0.9f;
            bigText.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.7f, 1f, Mathf.Clamp01(k * 4f));
            bigText.color = Kit.A(bigText.color, Mathf.Clamp01(bigT * 3f));
            if (bigT <= 0) bigText.gameObject.SetActive(false);
        }
        if (toastT > 0) { toastT -= udt; toastText.color = Kit.A(toastText.color, Mathf.Clamp01(toastT * 2f)); if (toastT <= 0) toastText.gameObject.SetActive(false); }
        var cam = Game.I ? Game.I.Cam : null;
        for (int i = nameTags.Count - 1; i >= 0; i--)
        {
            var (t, x) = nameTags[i];
            if (!t || !x) { if (x) Destroy(x.gameObject); nameTags.RemoveAt(i); continue; }
            x.rectTransform.position = cam.WorldToScreenPoint(t.position + Vector3.up * 2.1f);
        }
    }

    void UpdateJoystick()
    {
        Vector2? pos = null;
        bool live = hud.gameObject.activeSelf;
        if (Input.touchCount > 0)
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                var t = Input.GetTouch(i);
                bool left = t.position.x < UnityEngine.Screen.width * 0.55f && t.position.y < UnityEngine.Screen.height * 0.72f;
                if (joyId == -99 && t.phase == TouchPhase.Began && left && !OverUI(t.fingerId)) { joyId = t.fingerId; joyOrigin = t.position; joyMouse = false; }
                if (t.fingerId == joyId) { if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled) joyId = -99; else pos = t.position; }
            }
        }
        else
        {
            var mp = (Vector2)Input.mousePosition;
            bool left = mp.x < UnityEngine.Screen.width * 0.55f && mp.y < UnityEngine.Screen.height * 0.72f;
            if (Input.GetMouseButtonDown(0) && left && !OverUI(-1)) { joyId = -1; joyOrigin = mp; joyMouse = true; }
            if (joyMouse && joyId == -1) { if (Input.GetMouseButton(0)) pos = mp; else joyId = -99; }
        }
        if (pos.HasValue && live)
        {
            float r = JoyRadius * canvas.scaleFactor;
            var d = pos.Value - joyOrigin;
            if (d.magnitude > r) { joyOrigin += d.normalized * (d.magnitude - r); d = pos.Value - joyOrigin; }
            Joy = d / r;
            joyBase.gameObject.SetActive(true); joyBase.position = joyOrigin; joyKnob.anchoredPosition = d / canvas.scaleFactor;
        }
        else { Joy = Vector2.zero; joyBase.gameObject.SetActive(false); if (!pos.HasValue) joyId = -99; }
    }

    static bool OverUI(int id)
    {
        if (!EventSystem.current) return false;
        return id < 0 ? EventSystem.current.IsPointerOverGameObject() : EventSystem.current.IsPointerOverGameObject(id);
    }
}

public class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    readonly HashSet<int> ids = new HashSet<int>();
    public bool Held => ids.Count > 0;
    public Action OnDown;
    public void OnPointerDown(PointerEventData e) { ids.Add(e.pointerId); transform.localScale = Vector3.one * 0.92f; OnDown?.Invoke(); }
    public void OnPointerUp(PointerEventData e) { ids.Remove(e.pointerId); if (ids.Count == 0) transform.localScale = Vector3.one; }
    void OnDisable() { ids.Clear(); transform.localScale = Vector3.one; }
}
