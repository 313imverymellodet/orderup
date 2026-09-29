using System.Collections.Generic;
using UnityEngine;

// ?bot=1 autoplay: a small planner that cooks the most urgent order. Used for the attract/demo mode and soak tests.
public static class Bot
{
    static Station target; static bool wantHold; static float actCd, think;
    static List<Vector2Int> path = new List<Vector2Int>();
    static Vector2Int goalCell = new Vector2Int(-1, -1);
    public static string Doing = "";

    public static Vector2 Input(Game g, Chef me, out bool act, out bool hold)
    {
        act = false; hold = false;
        if (g.State != Game.St.Playing) return Vector2.zero;
        actCd -= Time.deltaTime; think -= Time.deltaTime;
        if (think <= 0 || target == null) { think = 0.25f; Plan(g, me); }
        if (target == null) return Vector2.zero;

        var k = g.K;
        // already beside and facing the target?
        var facing = k.Facing(me.transform.position, me.Yaw);
        var tp = target.Pos; var to = tp - me.transform.position; to.y = 0;
        bool adjacent = Adjacent(k, me.transform.position, target);
        if (adjacent)
        {
            var dir = to.normalized;
            if (facing != target) return new Vector2(dir.x, dir.z) * 0.25f;   // turn toward it
            if (wantHold) { hold = true; return Vector2.zero; }
            if (actCd <= 0) { act = true; actCd = 0.3f; think = 0.12f; }
            return Vector2.zero;
        }
        // walk the grid path
        var cell = k.CellOf(me.transform.position);
        if (path.Count == 0 || goalCell != GoalFor(k, target, cell)) Repath(k, cell);
        while (path.Count > 0 && path[0] == cell) path.RemoveAt(0);
        if (path.Count == 0) { var d = to.normalized; return new Vector2(d.x, d.z); }
        var next = k.CellPos(path[0].x, path[0].y) - me.transform.position; next.y = 0;
        if (next.magnitude < 0.15f) { path.RemoveAt(0); }
        var n = next.normalized;
        return new Vector2(n.x, n.z);
    }

    static bool Adjacent(Kitchen k, Vector3 pos, Station st)
    {
        var c = k.CellOf(pos);
        int dx = Mathf.Abs(c.x - st.X), dz = Mathf.Abs(c.y - st.Z);
        if (dx + dz != 1) return false;
        var cp = k.CellPos(c.x, c.y); var d = pos - cp; d.y = 0;
        return d.magnitude < 0.45f;
    }

    // ---------------------------------------------------------------- planning
    static void Set(Station st, string why, bool hold = false)
    {
        if (st != target) path.Clear();
        target = st; wantHold = hold; Doing = why;
    }

    static void Plan(Game g, Chef me)
    {
        var k = g.K;
        int h = me.Held;
        Recipe goal = null; float least = float.MaxValue;
        foreach (var o in g.Orders) if (o.left < least) { least = o.left; goal = Recipes.All[o.recipe]; }
        var plateSt = FindPlateOnCounter(k, goal);
        int have = plateSt != null ? It.Mask(plateSt.Item) : 0;

        if (h != 0)
        {
            if (It.IsPlate(h))
            {
                int m = It.Mask(h);
                if (m != 0 && OrderFor(g, m)) { Set(k.Window, "serve"); return; }
                if (goal != null && (m & ~goal.mask) == 0 && plateSt == null) { Set(EmptyCounter(k, me), "park plate"); return; }
                if (goal != null && (m & ~goal.mask) == 0 && plateSt != null)
                {
                    // two plates: merge ours into the parked one if it fits, else park
                    Set(EmptyCounter(k, me), "park plate 2"); return;
                }
                Set(Trash(k, me), "wrong plate"); return;
            }
            var ing = It.IngOf(h); var s = It.StateOf(h);
            if (s == IState.Burnt) { Set(Trash(k, me), "burnt"); return; }
            if (s == IState.Raw && It.NeedsChop(ing)) { Set(Free(k, StType.Board, me) ?? EmptyCounter(k, me), "to board"); return; }
            if (s == IState.Raw && It.Cooks(ing)) { Set(Free(k, StType.Stove, me) ?? EmptyCounter(k, me), "to stove"); return; }
            // ready ingredient: onto the parked plate, or plate it
            if (plateSt != null && (have & It.Bit(ing)) == 0 && (goal == null || (goal.mask & It.Bit(ing)) != 0)) { Set(plateSt, "add " + ing); return; }
            if (plateSt == null && k.PlateStack.PlateCount > 0) { Set(k.PlateStack, "plate it"); return; }
            Set(Trash(k, me), "surplus"); return;
        }

        // hands empty
        foreach (var st in k.Stations)
            if (st.Type == StType.Board && st.Item != 0 && It.StateOf(st.Item) == IState.Raw && It.NeedsChop(It.IngOf(st.Item))) { Set(st, "chop", true); return; }
        foreach (var st in k.Stations)
            if (st.Type == StType.Stove && st.Item != 0 && It.StateOf(st.Item) != IState.Raw)
                if (It.StateOf(st.Item) == IState.Burnt || st.Progress > 1.2f || (goal != null && (goal.mask & It.Bit(Ing.Patty)) != 0 && (have & It.Bit(Ing.Patty)) == 0)) { Set(st, "take patty"); return; }
        if (plateSt != null && OrderFor(g, have)) { Set(plateSt, "take dish"); return; }
        if (goal == null) { target = null; Doing = "idle"; return; }

        foreach (var part in goal.parts)
        {
            if ((have & It.Bit(part)) != 0) continue;
            // a prepared one lying around?
            foreach (var st in k.Stations)
                if (st.Holds && st.Item != 0 && !It.IsPlate(st.Item) && It.IngOf(st.Item) == part && It.Ready(st.Item) && st != plateSt)
                    if (st.Type != StType.Stove || It.StateOf(st.Item) == IState.Prepped) { Set(st, "fetch ready " + part); return; }
            // already being prepared?
            bool busy = false;
            foreach (var st in k.Stations)
                if (st.Item != 0 && !It.IsPlate(st.Item) && It.IngOf(st.Item) == part && It.StateOf(st.Item) == IState.Raw) busy = true;
            if (busy) continue;
            if (It.Cooks(part) && Free(k, StType.Stove, me) == null) continue;
            if (It.NeedsChop(part) && Free(k, StType.Board, me) == null) continue;
            Set(Crate(k, part, me), "crate " + part); return;
        }
        if (plateSt == null && k.PlateStack.PlateCount > 0) { Set(k.PlateStack, "get plate"); return; }
        // waiting on the stove: stand next to it
        foreach (var st in k.Stations) if (st.Type == StType.Stove && st.Item != 0) { Set(st, "wait stove"); target = null; return; }
        target = null; Doing = "wait";
    }

    static bool OrderFor(Game g, int mask) { if (mask == 0) return false; foreach (var o in g.Orders) if (Recipes.All[o.recipe].mask == mask) return true; return false; }

    static Station FindPlateOnCounter(Kitchen k, Recipe goal)
    {
        Station best = null;
        foreach (var st in k.Stations)
        {
            if (st.Type != StType.Counter || !It.IsPlate(st.Item)) continue;
            int m = It.Mask(st.Item);
            if (goal != null && (m & ~goal.mask) != 0) continue;
            if (best == null || Bits(m) > Bits(It.Mask(best.Item))) best = st;
        }
        return best;
    }
    static int Bits(int m) { int n = 0; while (m != 0) { n += m & 1; m >>= 1; } return n; }

    static Station Nearest(Kitchen k, Chef me, System.Predicate<Station> ok)
    {
        Station best = null; float bd = float.MaxValue;
        var from = k.CellOf(me.transform.position);
        foreach (var st in k.Stations)
        {
            if (!ok(st)) continue;
            int d = PathLen(k, from, st);
            if (d < 0) continue;
            if (d < bd) { bd = d; best = st; }
        }
        return best;
    }
    static Station Free(Kitchen k, StType t, Chef me) => Nearest(k, me, s => s.Type == t && s.Item == 0);
    static Station Trash(Kitchen k, Chef me) => Nearest(k, me, s => s.Type == StType.Trash);
    static Station Crate(Kitchen k, Ing i, Chef me) => Nearest(k, me, s => s.Type == StType.Crate && s.Crate == i);
    // park plates near the pass
    static Station EmptyCounter(Kitchen k, Chef me)
    {
        Station best = null; float bd = float.MaxValue;
        foreach (var st in k.Stations)
        {
            if (st.Type != StType.Counter || st.Item != 0) continue;
            if (Goals(k, st).Count == 0) continue;
            float d = (st.Pos - k.PlateStack.Pos).magnitude + (st.Pos - k.Window.Pos).magnitude * 0.5f;
            if (d < bd) { bd = d; best = st; }
        }
        return best;
    }

    // ---------------------------------------------------------------- grid paths
    static List<Vector2Int> Goals(Kitchen k, Station st)
    {
        var l = new List<Vector2Int>();
        foreach (var d in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right })
        {
            var c = new Vector2Int(st.X, st.Z) + d;
            if (!k.Solid(c.x, c.y)) l.Add(c);
        }
        return l;
    }

    static Vector2Int GoalFor(Kitchen k, Station st, Vector2Int from)
    {
        var goals = Goals(k, st);
        Vector2Int best = new Vector2Int(-1, -1); int bd = int.MaxValue;
        foreach (var gl in goals) { int d = Mathf.Abs(gl.x - from.x) + Mathf.Abs(gl.y - from.y); if (d < bd) { bd = d; best = gl; } }
        return best;
    }

    static int PathLen(Kitchen k, Vector2Int from, Station st)
    {
        var p = Bfs(k, from, Goals(k, st));
        return p == null ? -1 : p.Count;
    }

    static void Repath(Kitchen k, Vector2Int from)
    {
        var goals = Goals(k, target);
        var p = Bfs(k, from, goals);
        path = p ?? new List<Vector2Int>();
        goalCell = GoalFor(k, target, from);
    }

    static List<Vector2Int> Bfs(Kitchen k, Vector2Int from, List<Vector2Int> goals)
    {
        if (goals.Count == 0) return null;
        var prev = new Dictionary<Vector2Int, Vector2Int>();
        var q = new Queue<Vector2Int>();
        q.Enqueue(from); prev[from] = from;
        Vector2Int? hit = null;
        while (q.Count > 0)
        {
            var c = q.Dequeue();
            if (goals.Contains(c)) { hit = c; break; }
            foreach (var d in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right })
            {
                var n = c + d;
                if (k.Solid(n.x, n.y) || prev.ContainsKey(n)) continue;
                prev[n] = c; q.Enqueue(n);
            }
        }
        if (!hit.HasValue) return null;
        var path = new List<Vector2Int>();
        var cur = hit.Value;
        while (cur != from) { path.Add(cur); cur = prev[cur]; }
        path.Reverse();
        return path;
    }
}
