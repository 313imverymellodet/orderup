using System.Collections.Generic;
using UnityEngine;

public enum StType { Counter, Crate, Board, Stove, Plates, Window, Trash }

public class Station
{
    public int Index, X, Z;
    public StType Type;
    public Ing Crate;
    public int Item;               // what sits on it (holders only)
    public float Progress;         // chop 0..1, cook 0..1 (cooked) .. 2 (burnt)
    public int PlateCount;         // plate stack only
    public Transform Root, Anchor;
    public GameObject View; public int ViewCode = -1;
    public Bar Bar;
    public GameObject Warn;
    public bool Holds => Type == StType.Counter || Type == StType.Board || Type == StType.Stove;
    public Vector3 Pos => Root.position;
}

// A tiny world-space progress bar that always faces the camera.
public class Bar
{
    public Transform Root; Transform fill; SpriteRenderer fillSr;
    static Sprite px;
    public static Bar Make(Transform parent, Vector3 local, float width)
    {
        if (!px) { var t = new Texture2D(4, 4); var c = new Color[16]; for (int i = 0; i < 16; i++) c[i] = Color.white; t.SetPixels(c); t.Apply(); px = Sprite.Create(t, new Rect(0, 0, 4, 4), new Vector2(0, .5f), 4); }
        var b = new Bar { Root = new GameObject("bar").transform };
        b.Root.SetParent(parent, false); b.Root.localPosition = local;
        var bg = new GameObject("bg").AddComponent<SpriteRenderer>(); bg.sprite = px; bg.color = new Color(0, 0, 0, 0.7f);
        bg.transform.SetParent(b.Root, false); bg.transform.localPosition = new Vector3(-width * 0.5f - 0.03f, 0, 0.001f); bg.transform.localScale = new Vector3(width + 0.06f, 0.16f, 1);
        b.fillSr = new GameObject("fill").AddComponent<SpriteRenderer>(); b.fillSr.sprite = px; b.fillSr.sortingOrder = 1;
        b.fill = b.fillSr.transform; b.fill.SetParent(b.Root, false); b.fill.localPosition = new Vector3(-width * 0.5f, 0, 0); b.fill.localScale = new Vector3(0, 0.1f, 1);
        b.width = width;
        b.Root.gameObject.SetActive(false);
        return b;
    }
    float width;
    public void Set(float k, Color c)
    {
        bool on = k > 0.001f;
        if (Root.gameObject.activeSelf != on) Root.gameObject.SetActive(on);
        if (!on) return;
        fill.localScale = new Vector3(width * Mathf.Clamp01(k), 0.1f, 1);
        fillSr.color = c;
        var cam = Camera.main; if (cam) Root.rotation = cam.transform.rotation;
    }
}

public class Kitchen : MonoBehaviour
{
    public const float Cell = 1.25f, CounterH = 0.92f;
    public KitchenDef Def;
    public int W, H;
    public readonly List<Station> Stations = new List<Station>();
    Station[,] grid;
    bool[,] solid;
    public Station PlateStack, Window;

    public static Kitchen Build(KitchenDef def, Transform parent)
    {
        var k = new GameObject("Kitchen").AddComponent<Kitchen>();
        k.transform.SetParent(parent, false);
        k.Def = def;
        k.H = def.rows.Length; k.W = def.rows[0].Length;
        k.grid = new Station[k.W, k.H]; k.solid = new bool[k.W, k.H];
        k.BuildFloor();
        for (int r = 0; r < k.H; r++)
            for (int x = 0; x < k.W; x++)
            {
                int z = k.H - 1 - r;
                char ch = def.rows[r][x];
                if (ch == '.') continue;
                k.solid[x, z] = true;
                if (ch == ' ') continue;
                k.MakeStation(ch, x, z);
            }
        k.Decorate();
        return k;
    }

    public Vector3 CellPos(int x, int z) => transform.position + new Vector3((x - (W - 1) * 0.5f) * Cell, 0, (z - (H - 1) * 0.5f) * Cell);
    public Vector2Int CellOf(Vector3 p) { var l = p - transform.position; return new Vector2Int(Mathf.RoundToInt(l.x / Cell + (W - 1) * 0.5f), Mathf.RoundToInt(l.z / Cell + (H - 1) * 0.5f)); }
    public Station At(int x, int z) => x >= 0 && z >= 0 && x < W && z < H ? grid[x, z] : null;
    public bool Solid(int x, int z) => x < 0 || z < 0 || x >= W || z >= H || solid[x, z];
    public Vector3 Center => transform.position;
    public Vector2 Size => new Vector2(W * Cell, H * Cell);

    // ---------------------------------------------------------------- building
    static GameObject Fit(string path, float footprint, Transform parent, Vector3 pos, float yaw, float maxH = 99f)
    {
        var go = Kit.Spawn(path, 1f, parent, pos, 0);
        var b = Kit.WorldBounds(go);
        float s = footprint / Mathf.Max(0.01f, Mathf.Max(b.size.x, b.size.z));
        if (b.size.y * s > maxH) s = maxH / b.size.y;
        go.transform.localScale = Vector3.one * s;
        go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
        return go;
    }

    // which way the station faces: toward the neighbouring floor tile
    float FaceYaw(int x, int z)
    {
        if (!Solid(x, z - 1)) return 180f;
        if (!Solid(x, z + 1)) return 0f;
        if (!Solid(x - 1, z)) return -90f;
        if (!Solid(x + 1, z)) return 90f;
        return 180f;
    }

    void BuildFloor()
    {
        var fl = Kit.MeshObject("Floor", Kit.BuildQuad(1f, 1f));
        fl.transform.SetParent(transform, false);
        fl.transform.localRotation = Quaternion.Euler(90, 0, 0);
        fl.transform.localPosition = new Vector3(0, 0.001f, 0);
        fl.transform.localScale = new Vector3(W * Cell, H * Cell, 1);
        var tex = Kit.Tiles(64, Def.floorA, Def.floorB, Color.Lerp(Def.floorB, Color.black, 0.15f));
        tex.wrapMode = TextureWrapMode.Repeat;
        var m = new Material(Shader.Find("Standard")) { mainTexture = tex };
        m.mainTextureScale = new Vector2(W / 2f, H / 2f);
        m.SetFloat("_Glossiness", 0.08f);
        fl.GetComponent<MeshRenderer>().sharedMaterial = m;

        // big backdrop floor so the kitchen floats in a warm room, not a void
        var bd = Kit.MeshObject("Backdrop", Kit.BuildQuad(1f, 1f));
        bd.transform.SetParent(transform, false);
        bd.transform.localRotation = Quaternion.Euler(90, 0, 0);
        bd.transform.localPosition = new Vector3(0, -0.02f, 0);
        bd.transform.localScale = new Vector3(80, 80, 1);
        var bm = new Material(Shader.Find("Standard")) { color = Color.Lerp(Def.wall, Color.black, 0.55f) };
        bm.SetFloat("_Glossiness", 0f);
        bd.GetComponent<MeshRenderer>().sharedMaterial = bm;
    }

    Station MakeStation(char ch, int x, int z)
    {
        var st = new Station { Index = Stations.Count, X = x, Z = z };
        st.Type = ch switch
        {
            'K' => StType.Board, 'S' => StType.Stove, 'P' => StType.Plates, 'W' => StType.Window, 'X' => StType.Trash,
            'B' or 'M' or 'L' or 'T' or 'C' => StType.Crate, _ => StType.Counter,
        };
        st.Crate = ch switch { 'B' => Ing.Bread, 'M' => Ing.Patty, 'L' => Ing.Lettuce, 'T' => Ing.Tomato, 'C' => Ing.Cheese, _ => Ing.None };
        var root = new GameObject("st_" + ch + "_" + x + "_" + z).transform;
        root.SetParent(transform, false);
        root.position = CellPos(x, z);
        st.Root = root;
        float yaw = FaceYaw(x, z);
        float top = CounterH;
        switch (st.Type)
        {
            case StType.Stove:
                Fit("Furniture/kitchenStove", Cell * 0.98f, root, Vector3.zero, yaw);
                top = TopOf(root);
                var pan = Fit("Food/frying-pan", Cell * 0.72f, root, new Vector3(0, top, 0), yaw + 90f);
                top += 0.06f;
                st.Warn = MakeWarn(root, top + 1.1f);
                break;
            case StType.Trash:
                Fit("Furniture/trashcan", Cell * 0.7f, root, Vector3.zero, yaw, 1.0f);
                top = TopOf(root);
                break;
            default:
                Fit(ch == '|' ? "Furniture/kitchenBar" : (x + z) % 3 == 0 ? "Furniture/kitchenCabinetDrawer" : "Furniture/kitchenCabinet", Cell * 0.99f, root, Vector3.zero, yaw, CounterH);
                top = TopOf(root);
                break;
        }
        var anchor = new GameObject("anchor").transform;
        anchor.SetParent(root, false); anchor.localPosition = new Vector3(0, top, 0);
        st.Anchor = anchor;

        switch (st.Type)
        {
            case StType.Crate:
                var box = Fit("Furniture/cardboardBoxOpen", Cell * 0.8f, root, new Vector3(0, top, 0), yaw, 0.45f);
                var shown = ItemViews.Make(It.Ingr(st.Crate), anchor);
                shown.transform.localPosition = new Vector3(0, 0.3f, 0);
                shown.transform.localScale *= 1.25f;
                break;
            case StType.Board:
                Fit("Food/cutting-board", Cell * 0.78f, root, new Vector3(0, top, 0), yaw + 90f);
                anchor.localPosition += Vector3.up * 0.05f;
                var knife = Fit("Food/cooking-knife", Cell * 0.5f, root, new Vector3(Cell * 0.3f, top + 0.05f, 0), yaw + 60f);
                break;
            case StType.Plates:
                PlateStack = st;
                break;
            case StType.Window:
                Window = st;
                // serving hatch: glowing lip + bell
                var lip = Kit.FloorQuad("serve", Kit.Rounded, Kit.A(Def.accent, 0.95f), Cell * 0.9f, root, new Vector3(0, top + 0.012f, 0), 0);
                lip.transform.localScale = new Vector3(Cell * 0.9f, Cell * 0.9f, 1);
                var arrow = Kit.FloorQuad("arrow", Kit.Arrow, new Color(1, 1, 1, 0.9f), Cell * 0.55f, root, new Vector3(0, top + 0.02f, 0), 0);
                arrow.transform.localRotation = Quaternion.Euler(90, yaw, 0);
                st.Warn = MakeWarn(root, top + 1.3f);
                break;
        }
        if (st.Type == StType.Board || st.Type == StType.Stove) st.Bar = Bar.Make(root, new Vector3(0, top + 0.95f, 0), 0.8f);
        Stations.Add(st);
        grid[x, z] = st;
        return st;
    }

    static float TopOf(Transform root)
    {
        var b = Kit.WorldBounds(root.gameObject);
        return b.max.y - root.position.y;
    }

    static GameObject MakeWarn(Transform root, float y)
    {
        var w = new GameObject("warn").AddComponent<SpriteRenderer>();
        w.sprite = Sprite.Create(Kit.Glow, new Rect(0, 0, 64, 64), new Vector2(.5f, .5f), 64);
        w.color = new Color(1f, 0.25f, 0.15f, 0.9f);
        w.transform.SetParent(root, false); w.transform.localPosition = new Vector3(0, y, 0);
        w.transform.localScale = Vector3.one * 1.3f;
        w.gameObject.SetActive(false);
        return w.gameObject;
    }

    void Decorate()
    {
        // a low back wall behind the top row and hoods over the stoves
        var wallMat = new Material(Shader.Find("Standard")) { color = Def.wall };
        wallMat.SetFloat("_Glossiness", 0.1f);
        var wall = Kit.MeshObject("wall", Kit.BuildQuad(1f, 1f));
        wall.transform.SetParent(transform, false);
        wall.transform.localPosition = new Vector3(0, 1.4f, (H * 0.5f) * Cell + 0.02f);
        wall.transform.localScale = new Vector3(W * Cell + 0.2f, 2.8f, 1);
        wall.GetComponent<MeshRenderer>().sharedMaterial = wallMat;
        foreach (var st in Stations)
            if (st.Type == StType.Stove && st.Z == H - 1)
                Fit("Furniture/hoodModern", Cell * 0.9f, st.Root, new Vector3(0, 1.95f, 0.1f), 180f);
        // plants on the outside corners
        Kit.Spawn("Furniture/pottedPlant", 0.3f, transform, CellPos(-1, 0) + new Vector3(0.3f, 0, 0), 20);
        Kit.Spawn("Furniture/pottedPlant", 0.3f, transform, CellPos(W, H - 1) + new Vector3(-0.3f, 0, 0), 70);
    }

    // ---------------------------------------------------------------- queries
    // Push a circle (chef) out of solid cells.
    public Vector3 Collide(Vector3 p, float r)
    {
        var c = CellOf(p);
        for (int dx = -1; dx <= 1; dx++)
            for (int dz = -1; dz <= 1; dz++)
            {
                int x = c.x + dx, z = c.y + dz;
                if (!Solid(x, z)) continue;
                var cp = CellPos(x, z);
                float h = Cell * 0.5f;
                float nx = Mathf.Clamp(p.x, cp.x - h, cp.x + h), nz = Mathf.Clamp(p.z, cp.z - h, cp.z + h);
                float ddx = p.x - nx, ddz = p.z - nz;
                float d2 = ddx * ddx + ddz * ddz;
                if (d2 < r * r)
                {
                    if (d2 < 1e-6f)
                    {
                        // centre inside the box: push out along the shallowest axis
                        float ox = (p.x - cp.x), oz = (p.z - cp.z);
                        if (Mathf.Abs(ox) > Mathf.Abs(oz)) p.x = cp.x + Mathf.Sign(ox) * (h + r); else p.z = cp.z + Mathf.Sign(oz) * (h + r);
                    }
                    else
                    {
                        float d = Mathf.Sqrt(d2);
                        p.x = nx + ddx / d * r; p.z = nz + ddz / d * r;
                    }
                }
            }
        return p;
    }

    // The station a chef at pos, facing yaw, would use: the cell in front, else the nearest adjacent one.
    public Station Facing(Vector3 pos, float yawDeg)
    {
        var fwd = new Vector3(Mathf.Sin(yawDeg * Mathf.Deg2Rad), 0, Mathf.Cos(yawDeg * Mathf.Deg2Rad));
        var c = CellOf(pos + fwd * Cell * 0.75f);
        var st = At(c.x, c.y);
        if (st != null) return st;
        Station best = null; float bd = float.MaxValue;
        var me = CellOf(pos);
        for (int dx = -1; dx <= 1; dx++)
            for (int dz = -1; dz <= 1; dz++)
            {
                if (dx != 0 && dz != 0) continue;
                var s = At(me.x + dx, me.y + dz);
                if (s == null) continue;
                var to = s.Pos - pos; to.y = 0;
                float score = to.magnitude - Vector3.Dot(to.normalized, fwd) * 0.8f;
                if (to.magnitude < Cell * 1.1f && score < bd) { bd = score; best = s; }
            }
        return best;
    }

    // Keep station visuals in sync with their item codes (runs on host and clients alike).
    public void Refresh(float time)
    {
        foreach (var st in Stations)
        {
            int code = st.Type == StType.Plates ? -100 - st.PlateCount : st.Item;
            if (code != st.ViewCode)
            {
                if (st.View) Destroy(st.View);
                st.View = null;
                if (st.Type == StType.Plates)
                {
                    st.View = new GameObject("plates");
                    st.View.transform.SetParent(st.Anchor, false);
                    for (int i = 0; i < st.PlateCount; i++)
                    {
                        var p = ItemViews.Make(It.Plate, st.View.transform);
                        p.transform.localPosition = new Vector3(0, i * 0.07f, 0);
                    }
                }
                else if (st.Item != 0) st.View = ItemViews.Make(st.Item, st.Anchor);
                st.ViewCode = code;
            }
            if (st.Bar != null)
            {
                if (st.Type == StType.Board) st.Bar.Set(st.Item != 0 && It.StateOf(st.Item) == IState.Raw ? st.Progress : 0, Kit.Hex("#7cf56a"));
                else if (st.Type == StType.Stove)
                {
                    bool raw = st.Item != 0 && It.StateOf(st.Item) == IState.Raw;
                    bool cooked = st.Item != 0 && It.StateOf(st.Item) == IState.Prepped;
                    st.Bar.Set(raw ? st.Progress : cooked ? (st.Progress - 1f) : 0, raw ? Kit.Hex("#ffb13b") : Kit.Hex("#ff4b3b"));
                }
            }
            if (st.Type == StType.Stove && st.Warn)
            {
                bool danger = st.Item != 0 && It.StateOf(st.Item) == IState.Prepped && st.Progress > 1.35f;
                bool burnt = st.Item != 0 && It.StateOf(st.Item) == IState.Burnt;
                st.Warn.SetActive((danger && Mathf.PingPong(time * 5f, 1f) > 0.4f) || burnt);
                if (st.View && burnt) st.View.transform.localPosition = new Vector3(0, Mathf.Abs(Mathf.Sin(time * 20f)) * 0.03f, 0);
            }
        }
    }
}

// Builds the look of any item code.
public static class ItemViews
{
    static float Foot(Ing i, IState s) => i switch
    {
        Ing.Bread => 0.5f, Ing.Patty => 0.46f, Ing.Lettuce => 0.44f, Ing.Tomato => s == IState.Raw ? 0.36f : 0.4f, Ing.Cheese => 0.44f, _ => 0.4f,
    };

    static string Model(Ing i, IState s) => i switch
    {
        Ing.Bread => "bread",
        Ing.Patty => s == IState.Raw ? "meat-raw" : "meat-patty",
        Ing.Lettuce => "cabbage",
        Ing.Tomato => s == IState.Raw ? "tomato" : "tomato-slice",
        Ing.Cheese => s == IState.Raw ? "cheese" : "cheese-cut",
        _ => "plate",
    };

    public static GameObject Make(int code, Transform parent)
    {
        var root = new GameObject("item");
        root.transform.SetParent(parent, false);
        if (It.IsPlate(code))
        {
            var plate = Fit("Food/plate", 0.62f, root.transform, Vector3.zero);
            float y = TopOf(plate.transform) + 0.005f;
            int mask = It.Mask(code);
            var r = Recipes.ByMask(mask);
            if (r != null)
            {
                Fit("Food/" + r.model, r == Recipes.Salad ? 0.5f : 0.46f, root.transform, new Vector3(0, y, 0));
            }
            else
            {
                // loose layers, in burger order
                Ing[] order = { Ing.Bread, Ing.Patty, Ing.Cheese, Ing.Lettuce, Ing.Tomato };
                int n = 0;
                foreach (var i in order)
                {
                    if ((mask & It.Bit(i)) == 0) continue;
                    var part = MakeIng(i, IState.Prepped, root.transform, 0.78f);
                    part.transform.localPosition = new Vector3((n % 2 == 0 ? -1 : 1) * 0.06f * (n > 0 ? 1 : 0), y, 0);
                    y += TopOf(part.transform) - y + 0.004f;
                    n++;
                }
            }
        }
        else if (code != 0)
        {
            MakeIng(It.IngOf(code), It.StateOf(code), root.transform, 1f);
        }
        foreach (var rd in root.GetComponentsInChildren<Renderer>()) rd.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return root;
    }

    static GameObject MakeIng(Ing i, IState s, Transform parent, float scale)
    {
        var go = Fit("Food/" + Model(i, s), Foot(i, s) * scale, parent, Vector3.zero);
        if (i == Ing.Lettuce && s != IState.Raw)
        {
            // chopped lettuce: a flattened pile of leaves
            var ls = go.transform.localScale; go.transform.localScale = new Vector3(ls.x * 1.1f, ls.y * 0.32f, ls.z * 1.1f);
        }
        if (s == IState.Burnt) Kit.Tint(go, new Color(0.22f, 0.17f, 0.16f));
        return go;
    }

    static GameObject Fit(string path, float footprint, Transform parent, Vector3 pos)
    {
        var go = Kit.Spawn(path, 1f, parent, pos, 0);
        var b = Kit.WorldBounds(go);
        float s = footprint / Mathf.Max(0.01f, Mathf.Max(b.size.x, b.size.z));   // bounds are already in world units
        go.transform.localScale = Vector3.one * s;
        return go;
    }

    static float TopOf(Transform t)
    {
        var b = Kit.WorldBounds(t.gameObject);
        return t.parent ? t.parent.InverseTransformPoint(b.max).y : b.max.y;
    }
}
