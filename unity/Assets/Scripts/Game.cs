using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ORDER UP! — a co-op kitchen. Solo, or up to 4 chefs online (host-authoritative, relayed by the Railway service).
public class Game : MonoBehaviour
{
    public static Game I;
    public enum St { Menu, Countdown, Playing, Over }
    public enum Net { Solo, Host, Client }
    public St State = St.Menu;
    public Net Mode = Net.Solo;
    public SaveData Save;
    public Camera Cam;
    public Kitchen K;
    public KitchenDef Def;
    public readonly List<Chef> Chefs = new List<Chef>();
    public Chef Me;
    public bool AutoPlay, AutoDrive, Dev;

    public class Order { public int recipe; public float left, total; public int id; }
    public readonly List<Order> Orders = new List<Order>();
    public int Score, Served, Failed, Combo;
    public float TimeLeft, Countdown;
    public float Shift = 180f;          // dev: ?dev=1&short=1 for 25 s shifts
    float orderT, snapT, inT, camShake;
    int orderId;
    readonly List<float> plateBack = new List<float>();
    readonly List<string> events = new List<string>();     // host -> clients: sounds / popups
    readonly Dictionary<string, int> lastAct = new Dictionary<string, int>();
    int myAct;
    Transform world;
    Light sun;

    // ======================================================================
    void Awake()
    {
        I = this;
        Application.targetFrameRate = -1;
        QualitySettings.shadowDistance = 40f; QualitySettings.shadowCascades = 1;
        QualitySettings.shadowResolution = ShadowResolution.Medium; QualitySettings.antiAliasing = 2;
#if UNITY_WEBGL && !UNITY_EDITOR
        WebGLInput.captureAllKeyboardInput = false;
#endif
        var url = Application.absoluteURL;
        Dev = url.Contains("dev=1"); AutoPlay = url.Contains("bot=1"); AutoDrive = AutoPlay || (Dev && url.Contains("autodrive=1"));
        DevCam.Install(Dev);
        if (Dev && url.Contains("short=1")) Shift = 25f;
        var json = PlayerPrefs.GetString("ou_save", "");
        try { Save = string.IsNullOrEmpty(json) ? new SaveData() : JsonUtility.FromJson<SaveData>(json); } catch { Save = null; }
        if (Save == null || (Dev && url.Contains("fresh=1"))) Save = new SaveData();

        gameObject.AddComponent<Sfx>();
        Sfx.I.SetMuted(Save.muted);
        new GameObject("WebBridge").AddComponent<WebBridge>();
        Cam = Camera.main;
        Cam.clearFlags = CameraClearFlags.SolidColor;
        Cam.nearClipPlane = 0.3f; Cam.farClipPlane = 120f;
        sun = new GameObject("Sun").AddComponent<Light>();
        sun.type = LightType.Directional; sun.shadows = LightShadows.Soft; sun.shadowStrength = 0.55f;
        sun.shadowBias = 0.04f; sun.shadowNormalBias = 0.3f;
        sun.transform.rotation = Quaternion.Euler(62, -25, 0);
        sun.color = Kit.Hex("#fff1dc"); sun.intensity = 0.82f;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = Kit.Hex("#8f96a8");
        RenderSettings.ambientEquatorColor = Kit.Hex("#6e645c");
        RenderSettings.ambientGroundColor = Kit.Hex("#3a302a");
        world = new GameObject("World").transform;
        new GameObject("UI").AddComponent<UI>().Init();

        LoadKitchen(Save.kitchen);
        GoMenu();
        if (!Save.howto) UI.I.ShowHowTo();
        WebBridge.Ready();
        if (AutoPlay) StartCoroutine(AutoStart());
    }

    IEnumerator AutoStart() { yield return new WaitForSecondsRealtime(1.2f); StartSolo(); }

    public void Persist() { PlayerPrefs.SetString("ou_save", JsonUtility.ToJson(Save)); PlayerPrefs.Save(); }

    // ======================================================================
    public void LoadKitchen(string id)
    {
        Def = Kitchens.Get(id);
        Save.kitchen = Def.id;
        if (K) Destroy(K.gameObject);
        K = Kitchen.Build(Def, world);
        Cam.backgroundColor = Color.Lerp(Def.wall, Color.black, 0.62f);
    }

    public void SelectKitchen(string id) { LoadKitchen(id); Persist(); GoMenu(); }

    void ClearChefs() { foreach (var c in Chefs) if (c) Destroy(c.gameObject); Chefs.Clear(); Me = null; }

    public void GoMenu()
    {
        State = St.Menu; Mode = Net.Solo; Time.timeScale = 1;
        ClearChefs();
        ResetKitchen();
        // two idle chefs posing in the kitchen behind the menu
        var a = Chef.Create(world, Save.look, "YOU", 0); a.transform.position = FloorSpot(0); a.Yaw = 180; a.transform.rotation = Quaternion.Euler(0, 180, 0); Chefs.Add(a);
        var b = Chef.Create(world, (Save.look + 3) % Looks.All.Length, "PAL", 1); b.transform.position = FloorSpot(1); b.Yaw = 160; b.transform.rotation = Quaternion.Euler(0, 160, 0); b.Held = It.PlateWith(Recipes.Cheeseburger.mask); Chefs.Add(b);
        UI.I.ShowMenu();
        Sfx.I.Music(false);
        WebBridge.Gameplay(false);
    }

    // spawn points: open floor near the middle
    Vector3 FloorSpot(int i)
    {
        var spots = new List<Vector3>();
        for (int z = 1; z < K.H - 1; z++) for (int x = 1; x < K.W - 1; x++) if (!K.Solid(x, z)) spots.Add(K.CellPos(x, z));
        spots.Sort((p, q) => (p - K.Center).sqrMagnitude.CompareTo((q - K.Center).sqrMagnitude));
        // spread chefs over the central tiles, alternating sides
        var s = spots[Mathf.Min(spots.Count - 1, i * 3 + 1)];
        return s;
    }

    void ResetKitchen()
    {
        foreach (var st in K.Stations) { st.Item = 0; st.Progress = 0; }
        if (K.PlateStack != null) K.PlateStack.PlateCount = 4;
        Orders.Clear(); plateBack.Clear(); events.Clear(); lastAct.Clear();
        Score = Served = Failed = Combo = 0; TimeLeft = Shift; orderT = 0; myAct = 0;
    }

    // ---------------------------------------------------------------- solo
    public void StartSolo()
    {
        Mode = Net.Solo;
        UI.I.CloseScreens();
        ClearChefs(); ResetKitchen();
        Me = Chef.Create(world, Save.look, "YOU", 0);
        Me.Local = true; Me.Id = "me";
        Me.transform.position = FloorSpot(0);
        Chefs.Add(Me);
        BeginCountdown();
        WebBridge.Event("shift_solo_" + Def.id);
        WebBridge.RunStart(Def.id);
    }

    void BeginCountdown()
    {
        State = St.Countdown; Countdown = 3.4f;
        UI.I.ShowHud(true);
        Sfx.I.Music(true);
        WebBridge.Gameplay(true);
    }

    // ---------------------------------------------------------------- per frame
    void Update()
    {
        float dt = Mathf.Min(Time.deltaTime, 0.05f);
        if (Input.anyKeyDown || Input.touchCount > 0) Sfx.I.Unlock();

        if (State == St.Countdown)
        {
            float before = Countdown;
            Countdown -= dt;
            int a = Mathf.CeilToInt(before - 0.4f), b = Mathf.CeilToInt(Countdown - 0.4f);
            if (a != b && b >= 0) { UI.I.Big(b == 0 ? "GO!" : b.ToString(), b == 0); Sfx.I.Beep(b == 0); }
            if (Countdown <= 0.4f) { State = St.Playing; if (Mode != Net.Client) { SpawnOrder(); orderT = 7f; } }
        }

        // ---- local chef
        if (Me && (State == St.Playing || State == St.Countdown))
        {
            Vector2 input = UI.I.Joy; bool act = UI.I.ActPressed, hold = UI.I.ActHeld, dash = UI.I.DashPressed;
            float kx = (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1 : 0) - (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? 1 : 0);
            float ky = (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) ? 1 : 0) - (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ? 1 : 0);
            if (kx != 0 || ky != 0) input = new Vector2(kx, ky).normalized;
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.J)) act = true;
            if (Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.E) || Input.GetKey(KeyCode.J)) hold = true;
            if (Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.K)) dash = true;
            if (AutoDrive) { input = Bot.Input(this, Me, out act, out hold); dash = false; }
            if (State == St.Countdown) { input = Vector2.zero; act = false; hold = false; }
            Me.Drive(input, dash, dt, K, Chefs);
            if (act) myAct++;
            if (Mode == Net.Client)
            {
                if (act || (inT -= Time.unscaledDeltaTime) <= 0)
                {
                    inT = 1f / 15f;
                    WebBridge.NetSend("{\"t\":\"in\",\"x\":" + F(Me.transform.position.x) + ",\"z\":" + F(Me.transform.position.z) + ",\"y\":" + F(Me.Yaw) + ",\"a\":" + myAct + ",\"c\":" + (hold ? 1 : 0) + "}");
                }
            }
            else
            {
                if (act) Interact(Me);
                Me.Chopping = hold && CanChop(Me);
            }
        }
        foreach (var c in Chefs) if (!c.Local) c.NetSmooth(dt);

        if (State == St.Playing && Mode != Net.Client) Simulate(dt);
        K.Refresh(Time.time);
        if (State == St.Playing || State == St.Countdown) UI.I.UpdateHud(this);
        if (Mode == Net.Host && (State == St.Playing || State == St.Countdown || State == St.Over) && (snapT -= Time.unscaledDeltaTime) <= 0)
        {
            snapT = 1f / 12f;
            WebBridge.NetSend(Snapshot());
        }
        if (Me) UI.I.SetActionLabel(ActionLabel(Me));
    }

    static string F(float v) => v.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);

    // ======================================================================
    // Simulation (solo + host)
    void Simulate(float dt)
    {
        TimeLeft -= dt;
        // chopping
        foreach (var c in Chefs)
        {
            if (!c.Chopping) continue;
            var st = K.Facing(c.transform.position, c.Yaw);
            if (st == null || st.Type != StType.Board || !CanChop(c)) { c.Chopping = false; continue; }
            float before = st.Progress;
            st.Progress += dt / 2.3f;
            if (Mathf.FloorToInt(before * 8) != Mathf.FloorToInt(st.Progress * 8)) Emit("chop", st.Pos);
            if (st.Progress >= 1f) { st.Item = It.Ingr(It.IngOf(st.Item), IState.Prepped); st.Progress = 0; Emit("chopped", st.Pos); }
        }
        // stoves
        foreach (var st in K.Stations)
        {
            if (st.Type != StType.Stove || st.Item == 0 || It.IngOf(st.Item) != Ing.Patty) continue;
            var s = It.StateOf(st.Item);
            if (s == IState.Raw) { st.Progress += dt / 6f; if (st.Progress >= 1f) { st.Item = It.Ingr(Ing.Patty, IState.Prepped); Emit("cooked", st.Pos); } }
            else if (s == IState.Prepped)
            {
                float before = st.Progress;
                st.Progress += dt / 8f;
                if (before < 1.35f && st.Progress >= 1.35f) Emit("sizzle", st.Pos);
                if (st.Progress >= 2f) { st.Item = It.Ingr(Ing.Patty, IState.Burnt); Emit("burnt", st.Pos); }
            }
        }
        // plates come back from the dining room
        for (int i = plateBack.Count - 1; i >= 0; i--)
        {
            plateBack[i] -= dt;
            if (plateBack[i] <= 0) { plateBack.RemoveAt(i); K.PlateStack.PlateCount++; Emit("plate", K.PlateStack.Pos); }
        }
        // orders
        for (int i = Orders.Count - 1; i >= 0; i--)
        {
            var o = Orders[i];
            o.left -= dt;
            if (o.left <= 0)
            {
                Orders.RemoveAt(i);
                Failed++; Combo = 0;
                Score = Mathf.Max(0, Score - 10);
                Emit("fail:" + o.recipe, K.Window.Pos);
            }
        }
        orderT -= dt;
        int maxOrders = Mathf.Min(5, 3 + Chefs.Count / 2);
        if (orderT <= 0 && Orders.Count < maxOrders && TimeLeft > 12f) { SpawnOrder(); orderT = OrderGap(); }
        if (Orders.Count == 0 && orderT > 2.5f) orderT = 2.5f;   // never leave the pass empty for long
        if (TimeLeft <= 0) EndShift();
    }

    float OrderGap() => 21f / (1f + 0.5f * (Chefs.Count - 1)) * UnityEngine.Random.Range(0.85f, 1.15f) * (Served < 2 ? 1.2f : 1f);
    float Patience => Chefs.Count <= 1 ? 80f : 68f;

    void SpawnOrder()
    {
        var menu = Def.menu;
        // early orders are the simplest dish on the menu
        Recipe r = Served < 1 && Orders.Count == 0 ? SimplestDish() : menu[UnityEngine.Random.Range(0, menu.Length)];
        float total = Patience + r.parts.Length * 6f;
        Orders.Add(new Order { recipe = Recipes.Index(r), left = total, total = total, id = ++orderId });
        Emit("order", K.Window.Pos);
    }

    Recipe SimplestDish() { Recipe best = null; foreach (var r in Def.menu) if (best == null || r.parts.Length < best.parts.Length) best = r; return best; }

    void Emit(string ev, Vector3 at)
    {
        HandleEvent(ev, at);
        if (Mode == Net.Host) events.Add(ev + "@" + F(at.x) + "," + F(at.z));
    }

    // effects for an event, on every device
    void HandleEvent(string ev, Vector3 at)
    {
        var parts = ev.Split(':');
        switch (parts[0])
        {
            case "chop": Sfx.I.Chop(); break;
            case "chopped": Sfx.I.Ding(); UI.I.Float(at + Vector3.up * 1.4f, "CHOPPED!", Kit.Hex("#7cf56a")); break;
            case "cooked": Sfx.I.Ding(); UI.I.Float(at + Vector3.up * 1.4f, "COOKED!", Kit.Hex("#ffb13b")); break;
            case "sizzle": Sfx.I.Sizzle(); UI.I.Float(at + Vector3.up * 1.6f, "BURNING!", Kit.Hex("#ff4b3b")); break;
            case "burnt": Sfx.I.Burn(); UI.I.Float(at + Vector3.up * 1.6f, "BURNT!", Kit.Hex("#ff4b3b")); break;
            case "plate": Sfx.I.Clink(); break;
            case "pick": Sfx.I.Pick(); break;
            case "drop": Sfx.I.Drop(); break;
            case "trash": Sfx.I.Trash(); break;
            case "order": Sfx.I.Bell(); break;
            case "nope": Sfx.I.Nope(); if (parts.Length > 1) UI.I.Float(at + Vector3.up * 1.5f, parts[1], Color.white); break;
            case "serve":
                int pts = parts.Length > 2 ? int.Parse(parts[2]) : 0;
                Sfx.I.Serve(); camShake = 0.25f;
                UI.I.Float(at + Vector3.up * 1.6f, "+" + pts, Kit.Hex("#ffd23f"), 1.5f);
                WebBridge.Vibrate(30);
                break;
            case "fail":
                Sfx.I.Fail(); camShake = 0.35f;
                UI.I.Float(at + Vector3.up * 1.6f, "ORDER LOST  -10", Kit.Hex("#ff4b3b"));
                break;
        }
    }

    // ---------------------------------------------------------------- interactions
    bool CanChop(Chef c)
    {
        if (c.Held != 0) return false;
        var st = K.Facing(c.transform.position, c.Yaw);
        return st != null && st.Type == StType.Board && st.Item != 0 && !It.IsPlate(st.Item) && It.NeedsChop(It.IngOf(st.Item)) && It.StateOf(st.Item) == IState.Raw;
    }

    bool OnMenu(int mask) { foreach (var r in Def.menu) if ((r.mask & mask) == mask) return true; return false; }

    bool CanAdd(int plate, int ing)
    {
        if (!It.IsPlate(plate) || !It.Ready(ing)) return false;
        int bit = It.Bit(It.IngOf(ing)), m = It.Mask(plate);
        return (m & bit) == 0 && OnMenu(m | bit);
    }

    public void Interact(Chef c)
    {
        if (State != St.Playing) return;
        var st = K.Facing(c.transform.position, c.Yaw);
        if (st == null) return;
        int h = c.Held;
        var at = st.Pos;
        switch (st.Type)
        {
            case StType.Crate:
                if (h == 0) { c.Held = It.Ingr(st.Crate); Emit("pick", at); }
                else if (It.IsPlate(h) && CanAdd(h, It.Ingr(st.Crate))) { c.Held = It.PlateWith(It.Mask(h) | It.Bit(st.Crate)); Emit("drop", at); }
                else Emit("nope", at);
                break;
            case StType.Plates:
                if (h == 0 && st.PlateCount > 0) { c.Held = It.Plate; st.PlateCount--; Emit("plate", at); }
                else if (It.Ready(h) && st.PlateCount > 0 && OnMenu(It.Bit(It.IngOf(h)))) { c.Held = It.PlateWith(It.Bit(It.IngOf(h))); st.PlateCount--; Emit("plate", at); }
                else if (h == It.Plate) { c.Held = 0; st.PlateCount++; Emit("plate", at); }
                else if (st.PlateCount == 0) Emit("nope:NO PLATES YET", at);
                else Emit("nope", at);
                break;
            case StType.Trash:
                if (h == 0) break;
                c.Held = It.IsPlate(h) ? It.Plate : 0;
                Emit("trash", at);
                break;
            case StType.Window:
                if (It.IsPlate(h) && It.Mask(h) != 0) Serve(c, h, at);
                else if (h != 0) Emit("nope:PLATE IT FIRST!", at);
                break;
            default:
                int s = st.Item;
                if (s == 0 && h != 0)
                {
                    if (st.Type == StType.Stove && It.IngOf(h) != Ing.Patty) { Emit("nope:ONLY PATTIES COOK", at); break; }
                    if (st.Type == StType.Board && It.IsPlate(h)) { Emit("nope", at); break; }
                    st.Item = h; c.Held = 0;
                    st.Progress = st.Type == StType.Stove ? (It.StateOf(h) == IState.Raw ? 0 : It.StateOf(h) == IState.Prepped ? 1f : 2f) : 0;
                    Emit("drop", at);
                }
                else if (s != 0 && h == 0) { c.Held = s; st.Item = 0; st.Progress = 0; Emit("pick", at); }
                else if (s != 0 && h != 0)
                {
                    if (CanAdd(s, h)) { st.Item = It.PlateWith(It.Mask(s) | It.Bit(It.IngOf(h))); c.Held = 0; Emit("drop", at); }
                    else if (CanAdd(h, s)) { c.Held = It.PlateWith(It.Mask(h) | It.Bit(It.IngOf(s))); st.Item = 0; st.Progress = 0; Emit("pick", at); }
                    else if (It.IsPlate(h) && It.IsPlate(s)) Emit("nope", at);
                    else if (!It.IsPlate(h) && !It.IsPlate(s)) Emit("nope:HANDS FULL", at);
                    else Emit("nope:" + NotReadyMsg(It.IsPlate(h) ? s : h), at);
                }
                break;
        }
    }

    static string NotReadyMsg(int ing)
    {
        var i = It.IngOf(ing);
        if (It.StateOf(ing) == IState.Burnt) return "BURNT! TRASH IT";
        if (It.Cooks(i) && It.StateOf(ing) == IState.Raw) return "COOK IT FIRST";
        if (It.NeedsChop(i) && It.StateOf(ing) == IState.Raw) return "CHOP IT FIRST";
        return "NOT ON THE MENU";
    }

    void Serve(Chef c, int plate, Vector3 at)
    {
        int mask = It.Mask(plate);
        Order match = null;
        foreach (var o in Orders) if (Recipes.All[o.recipe].mask == mask && (match == null || o.left < match.left)) match = o;
        if (match == null) { Emit("nope:" + (Recipes.ByMask(mask) == null ? "NOT FINISHED" : "NOBODY ORDERED THAT"), at); return; }
        var r = Recipes.All[match.recipe];
        Combo++;
        int tip = Mathf.RoundToInt(r.value * 0.6f * Mathf.Clamp01(match.left / match.total));
        int comboBonus = Mathf.Min(Combo - 1, 4) * 3;
        int pts = r.value + tip + comboBonus;
        Score += pts; Served++;
        Orders.Remove(match);
        c.Held = 0;
        plateBack.Add(6f);
        Emit("serve:" + match.recipe + ":" + pts, at);
        if (Orders.Count == 0) orderT = Mathf.Min(orderT, 2f);
    }

    public string ActionLabel(Chef c)
    {
        if (State != St.Playing) return "";
        var st = K.Facing(c.transform.position, c.Yaw);
        if (st == null) return c.Held != 0 ? "" : "";
        int h = c.Held;
        switch (st.Type)
        {
            case StType.Crate: return h == 0 ? "GRAB " + It.Name(st.Crate) : It.IsPlate(h) ? "ADD" : "";
            case StType.Plates: return h == 0 ? "PLATE" : It.Ready(h) ? "PLATE IT" : h == It.Plate ? "RETURN" : "";
            case StType.Trash: return h != 0 ? "TRASH" : "";
            case StType.Window: return It.IsPlate(h) && It.Mask(h) != 0 ? "SERVE!" : "";
            case StType.Board:
                if (h == 0 && st.Item != 0 && It.NeedsChop(It.IngOf(st.Item)) && It.StateOf(st.Item) == IState.Raw) return "HOLD: CHOP";
                break;
        }
        if (st.Item == 0) return h != 0 ? "PUT DOWN" : "";
        if (h == 0) return "PICK UP";
        if (CanAdd(st.Item, h) || CanAdd(h, st.Item)) return "COMBINE";
        return "";
    }

    // ---------------------------------------------------------------- end of shift
    public int StarsFor(int score)
    {
        float k = 1f + 0.55f * Mathf.Max(0, Chefs.Count - 1);
        int s = 0; for (int i = 0; i < 3; i++) if (score >= Def.stars[i] * k) s = i + 1;
        return s;
    }

    void EndShift()
    {
        if (State == St.Over) return;
        State = St.Over;
        TimeLeft = 0;
        foreach (var c in Chefs) c.Chopping = false;
        int stars = StarsFor(Score);
        Sfx.I.Music(false);
        Sfx.I.Finish(stars);
        Save.shifts++;
        bool best = Score > Save.Best(Def.id);
        Save.Record(Def.id, Score, stars);
        Persist();
        if (!AutoDrive) WebBridge.RunSubmit(Def.id, Score, stars, Chefs.Count);
        WebBridge.Event("shift_end_" + Def.id, Score);
        WebBridge.Gameplay(false);
        if (Mode == Net.Host) WebBridge.NetSend(Snapshot());
        StartCoroutine(ShowResultsSoon(stars, best));
    }

    IEnumerator ShowResultsSoon(int stars, bool best)
    {
        UI.I.Big("TIME'S UP!", false);
        yield return new WaitForSecondsRealtime(1.6f);
        UI.I.ShowResults(this, stars, best);
        if (AutoPlay) { yield return new WaitForSecondsRealtime(Dev ? 12f : 6f); SelectKitchen(Def.id == "burgerbar" ? "splitshift" : "burgerbar"); StartSolo(); }
    }

    // ---------------------------------------------------------------- menu actions
    public void OpenOnline() { if (State != St.Menu) GoMenu(); WebBridge.NetOpen(Def.id, Save.look); }
    public void ToggleMute() { Save.muted = !Save.muted; Sfx.I.SetMuted(Save.muted); Persist(); }
    public void Quit() { Time.timeScale = 1; if (Mode != Net.Solo) WebBridge.NetLeave(); GoMenu(); }
    public void Pause() { if (State == St.Menu) return; if (Mode == Net.Solo) Time.timeScale = 0; UI.I.ShowPause(Mode != Net.Solo); }
    public void Resume() { Time.timeScale = 1; UI.I.CloseScreens(); }

    public string ShareText()
    {
        int stars = StarsFor(Score);
        return "ORDER UP!  " + Def.name + "  " + new string('*', stars) + "  " + Score + " pts, " + Served + " dishes served" + (Chefs.Count > 1 ? " with a crew of " + Chefs.Count : "") + ". Can your kitchen beat it?";
    }

    // ======================================================================
    // Networking. The page (kitchen.js) relays JSON between this client and the room.
    [Serializable] public class NetPlayer { public string id, name; public int look; }
    [Serializable] class Head { public string t; }
    [Serializable] class StartMsg { public string t, map, you, host; public NetPlayer[] players; }
    [Serializable] class InMsg { public string t, from; public float x, z, y; public int a, c; }
    [Serializable] public class NetChef { public string id; public float x, z, y; public int h, c; }
    [Serializable] public class NetOrder { public int r, i; public float l, t; }
    [Serializable] class SnapMsg
    {
        public string t; public int s, sc, sv, fl, pl; public float tl, cd;
        public int[] it, pr; public NetChef[] ch; public NetOrder[] od; public string[] ev;
    }
    [Serializable] class IdMsg { public string t, id, msg; }

    public void OnNet(string json)
    {
        Head h;
        try { h = JsonUtility.FromJson<Head>(json); } catch { return; }
        if (h == null) return;
        switch (h.t)
        {
            case "solo": UI.I.Toast("NO CREW ONLINE - COOKING SOLO"); StartSolo(); break;
            case "start": OnStart(JsonUtility.FromJson<StartMsg>(json)); break;
            case "in": if (Mode == Net.Host) OnInput(JsonUtility.FromJson<InMsg>(json)); break;
            case "snap": if (Mode == Net.Client) ApplySnap(JsonUtility.FromJson<SnapMsg>(json)); break;
            case "left":
            {
                var m = JsonUtility.FromJson<IdMsg>(json);
                for (int i = Chefs.Count - 1; i >= 0; i--)
                    if (Chefs[i].Id == m.id && !Chefs[i].Local) { UI.I.Toast(Chefs[i].Name + " LEFT THE KITCHEN"); Destroy(Chefs[i].gameObject); Chefs.RemoveAt(i); }
                break;
            }
            case "end":
                if (Mode == Net.Client && State != St.Over) { UI.I.Toast("THE HOST LEFT - SHIFT OVER"); GoMenu(); }
                break;
        }
    }

    void OnStart(StartMsg m)
    {
        if (m.map != Def.id) LoadKitchen(m.map);
        UI.I.CloseScreens();
        ClearChefs(); ResetKitchen();
        Mode = m.host == m.you ? Net.Host : Net.Client;
        for (int i = 0; i < m.players.Length; i++)
        {
            var p = m.players[i];
            bool me = p.id == m.you;
            var c = Chef.Create(world, p.look, me ? "YOU" : p.name, i);
            c.Id = p.id; c.Local = me;
            c.transform.position = FloorSpot(i);
            Chefs.Add(c);
            if (me) Me = c; else UI.I.NameTag(c.transform, p.name, Looks.Colors[i % Looks.Colors.Length]);
        }
        BeginCountdown();
        WebBridge.RunStart(Def.id);   // every chef gets their own server-timed run for the leaderboard
        WebBridge.Event("shift_online_" + Def.id, m.players.Length);
    }

    void OnInput(InMsg m)
    {
        var c = Chefs.Find(x => x.Id == m.from);
        if (!c) return;
        c.NetUpdate(new Vector3(m.x, 0, m.z), m.y);
        if (!lastAct.TryGetValue(m.from, out var last)) last = 0;
        int presses = Mathf.Clamp(m.a - last, 0, 3);
        lastAct[m.from] = Mathf.Max(last, m.a);
        // act on where the chef reports being right now
        c.transform.position = new Vector3(m.x, 0, m.z); c.Yaw = m.y;
        for (int i = 0; i < presses; i++) Interact(c);
        c.Chopping = m.c == 1 && CanChop(c);
    }

    string Snapshot()
    {
        var s = new SnapMsg
        {
            t = "snap", s = (int)State, sc = Score, sv = Served, fl = Failed, tl = TimeLeft, cd = Countdown,
            pl = K.PlateStack != null ? K.PlateStack.PlateCount : 0,
            it = new int[K.Stations.Count], pr = new int[K.Stations.Count],
            ch = new NetChef[Chefs.Count], od = new NetOrder[Orders.Count], ev = events.ToArray(),
        };
        for (int i = 0; i < K.Stations.Count; i++) { s.it[i] = K.Stations[i].Item; s.pr[i] = Mathf.RoundToInt(K.Stations[i].Progress * 100); }
        for (int i = 0; i < Chefs.Count; i++)
        {
            var c = Chefs[i];
            s.ch[i] = new NetChef { id = c.Id, x = Round(c.transform.position.x), z = Round(c.transform.position.z), y = Mathf.Round(c.Yaw), h = c.Held, c = c.Chopping ? 1 : 0 };
        }
        for (int i = 0; i < Orders.Count; i++) s.od[i] = new NetOrder { r = Orders[i].recipe, i = Orders[i].id, l = Round(Orders[i].left), t = Orders[i].total };
        events.Clear();
        return JsonUtility.ToJson(s);
    }
    static float Round(float v) => Mathf.Round(v * 100f) / 100f;

    void ApplySnap(SnapMsg s)
    {
        if (s.it != null && s.it.Length == K.Stations.Count)
            for (int i = 0; i < K.Stations.Count; i++) { K.Stations[i].Item = s.it[i]; K.Stations[i].Progress = s.pr[i] / 100f; }
        if (K.PlateStack != null) K.PlateStack.PlateCount = s.pl;
        Score = s.sc; Served = s.sv; Failed = s.fl; TimeLeft = s.tl;
        if ((St)s.s == St.Playing && State == St.Countdown) State = St.Playing;
        if (s.ch != null)
            foreach (var nc in s.ch)
            {
                var c = Chefs.Find(x => x.Id == nc.id);
                if (!c) continue;
                c.Held = nc.h; c.Chopping = nc.c == 1;
                if (!c.Local) c.NetUpdate(new Vector3(nc.x, 0, nc.z), nc.y);
            }
        Orders.Clear();
        if (s.od != null) foreach (var o in s.od) Orders.Add(new Order { recipe = o.r, left = o.l, total = o.t, id = o.i });
        if (s.ev != null)
            foreach (var e in s.ev)
            {
                int at = e.LastIndexOf('@');
                if (at < 0) { HandleEvent(e, K.Center); continue; }
                var xy = e.Substring(at + 1).Split(',');
                float.TryParse(xy[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var x);
                float.TryParse(xy[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var z);
                HandleEvent(e.Substring(0, at), new Vector3(x, 0, z));
            }
        if ((St)s.s == St.Over && State != St.Over) ClientOver();
    }

    void ClientOver()
    {
        State = St.Over;
        int stars = StarsFor(Score);
        Sfx.I.Music(false); Sfx.I.Finish(stars);
        Save.shifts++;
        bool best = Score > Save.Best(Def.id);
        Save.Record(Def.id, Score, stars); Persist();
        if (!AutoDrive) WebBridge.RunSubmit(Def.id, Score, stars, Chefs.Count);
        WebBridge.Gameplay(false);
        StartCoroutine(ShowResultsSoon(stars, best));
    }

    // ---------------------------------------------------------------- leaderboard reply
    [Serializable] public class RankMsg { public int rank, total, best; public bool newBest; public string map, error; }
    public void OnRank(string json)
    {
        var m = JsonUtility.FromJson<RankMsg>(json);
        if (m != null && m.map == Def.id) UI.I.SetRank(m);
    }

    // ======================================================================
    // Camera: frame the whole kitchen (landscape), or follow your chef (portrait phones).
    void LateUpdate()
    {
        float aspect = (float)Screen.width / Mathf.Max(1, Screen.height);
        const float pitch = 56f;
        Cam.fieldOfView = 38f;
        var size = K.Size;
        float vt = Mathf.Tan(Cam.fieldOfView * 0.5f * Mathf.Deg2Rad), ht = vt * aspect;
        float dW = (size.x * 0.5f + 0.9f) / ht;
        float dH = (size.y * 0.5f * Mathf.Sin(pitch * Mathf.Deg2Rad) + 1.6f) / vt;
        var focus = K.Center + new Vector3(0, 0, 0.35f);
        float dist = Mathf.Max(dW, dH);
        bool follow = aspect < 0.95f && Me && State != St.Menu;
        if (follow)
        {
            // show ~7.5 m across; slide with the chef, clamped to the kitchen
            dist = Mathf.Max(dH * 0.62f, (7.5f * 0.5f) / ht);
            var p = Me.transform.position;
            float spanX = Mathf.Max(0, size.x * 0.5f - (dist * ht) + 0.6f);
            float spanZ = Mathf.Max(0, size.y * 0.5f - (dist * vt) * 0.7f);
            focus.x = Mathf.Clamp(p.x, K.Center.x - spanX, K.Center.x + spanX);
            focus.z = Mathf.Clamp(p.z, K.Center.z - spanZ, K.Center.z + spanZ) + 0.35f;
        }
        var dir = Quaternion.Euler(pitch, 0, 0) * Vector3.forward;
        var want = focus - dir * dist;
        Cam.transform.position = follow ? Vector3.Lerp(Cam.transform.position, want, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 6f)) : want;
        Cam.transform.rotation = Quaternion.Euler(pitch, 0, 0);
        camShake = Mathf.Max(0, camShake - Time.unscaledDeltaTime * 2f);
        if (camShake > 0) Cam.transform.position += new Vector3(Mathf.PerlinNoise(Time.time * 40f, 0) - .5f, Mathf.PerlinNoise(0, Time.time * 40f) - .5f, 0) * camShake * 0.5f;
    }
}
