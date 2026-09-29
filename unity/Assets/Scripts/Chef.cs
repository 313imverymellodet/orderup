using System.Collections.Generic;
using UnityEngine;

public class Chef : MonoBehaviour
{
    public const float Radius = 0.36f, Speed = 5.2f;
    public string Id, Name;
    public int Look, Slot;
    public bool Local;                // driven by this device's input
    public int Held;                  // item code in hand (host authoritative)
    public bool Chopping;
    public float Yaw;
    public Vector3 Vel;
    public float DashT, DashCd;

    // remote smoothing
    Vector3 netPos; float netYaw; bool hasNet;

    Animation anim;
    Transform model, hands;
    GameObject heldView; int heldViewCode = -1;
    float chopPhase;

    public static Chef Create(Transform parent, int look, string name, int slot)
    {
        var root = new GameObject("Chef_" + name);
        root.transform.SetParent(parent, false);
        var c = root.AddComponent<Chef>();
        c.Look = look; c.Name = name; c.Slot = slot;
        var m = Kit.Spawn("Characters/character-" + Looks.All[look % Looks.All.Length], 1.55f, root.transform);
        c.model = m.transform;
        c.anim = m.GetComponentInChildren<Animation>();
        if (c.anim)
        {
            c.anim.cullingType = AnimationCullingType.AlwaysAnimate;
            foreach (AnimationState s in c.anim) s.wrapMode = WrapMode.Loop;
            var hold = c.anim["holding-both"];
            if (hold != null)
            {
                hold.layer = 1; hold.weight = 0;
                foreach (var t in c.anim.GetComponentsInChildren<Transform>()) if (t.name.StartsWith("arm-")) hold.AddMixingTransform(t, true);
                hold.enabled = true;
            }
            var chop = c.anim["interact-right"];
            if (chop != null)
            {
                chop.layer = 2; chop.weight = 0; chop.speed = 2.2f;
                foreach (var t in c.anim.GetComponentsInChildren<Transform>()) if (t.name == "arm-right") chop.AddMixingTransform(t, true);
                chop.enabled = true;
            }
            c.anim.Play("idle");
        }
        foreach (var r in m.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        c.hands = new GameObject("hands").transform;
        c.hands.SetParent(root.transform, false);
        c.hands.localPosition = new Vector3(0, 0.78f, 0.5f);
        // player colour ring
        var ring = Kit.FloorQuad("ring", Kit.Ring, Looks.Colors[slot % Looks.Colors.Length], 1.05f, root.transform, Vector3.zero, 0.02f);
        Kit.FloorQuad("shadow", Kit.Disc, new Color(0, 0, 0, 0.3f), 0.9f, root.transform, Vector3.zero, 0.015f);
        return c;
    }

    // ---------------------------------------------------------------- local movement
    public void Drive(Vector2 input, bool dash, float dt, Kitchen k, List<Chef> all)
    {
        var want = new Vector3(input.x, 0, input.y) * Speed;
        DashCd -= dt;
        if (dash && DashCd <= 0 && input.sqrMagnitude > 0.1f) { DashT = 0.16f; DashCd = 0.7f; Sfx.I.Dash(); }
        if (DashT > 0) { DashT -= dt; want = new Vector3(input.x, 0, input.y).normalized * Speed * 2.6f; }
        Vel = Vector3.Lerp(Vel, want, 1f - Mathf.Exp(-dt * 14f));
        var p = transform.position + Vel * dt;
        // other chefs are soft obstacles
        foreach (var o in all)
        {
            if (o == this) continue;
            var d = p - o.transform.position; d.y = 0;
            float m = d.magnitude;
            if (m < Radius * 2f && m > 0.001f) p += d / m * (Radius * 2f - m);
        }
        p = k.Collide(p, Radius);
        p.y = 0;
        transform.position = p;
        if (input.sqrMagnitude > 0.02f) Yaw = Mathf.MoveTowardsAngle(Yaw, Mathf.Atan2(input.x, input.y) * Mathf.Rad2Deg, dt * 900f);
        transform.rotation = Quaternion.Euler(0, Yaw, 0);
    }

    // ---------------------------------------------------------------- network
    public void NetUpdate(Vector3 pos, float yaw)
    {
        if (!hasNet) { transform.position = pos; Yaw = yaw; }
        netPos = pos; netYaw = yaw; hasNet = true;
    }

    public void NetSmooth(float dt)
    {
        if (!hasNet) return;
        var before = transform.position;
        transform.position = Vector3.Lerp(transform.position, netPos, 1f - Mathf.Exp(-dt * 14f));
        Vel = (transform.position - before) / Mathf.Max(dt, 0.001f);
        Yaw = Mathf.LerpAngle(Yaw, netYaw, 1f - Mathf.Exp(-dt * 14f));
        transform.rotation = Quaternion.Euler(0, Yaw, 0);
    }

    // ---------------------------------------------------------------- visuals
    void LateUpdate()
    {
        float dt = Time.deltaTime;
        if (heldViewCode != Held)
        {
            if (heldView) Destroy(heldView);
            heldView = Held != 0 ? ItemViews.Make(Held, hands) : null;
            heldViewCode = Held;
            if (heldView) StartCoroutineSafe();
        }
        if (!anim) return;
        float sp = new Vector3(Vel.x, 0, Vel.z).magnitude / Speed;
        string clip = sp > 0.12f ? "walk" : "idle";
        if (!anim.IsPlaying(clip)) anim.CrossFade(clip, 0.12f);
        if (clip == "walk") anim["walk"].speed = Mathf.Clamp(sp, 0.6f, 1.6f) * 1.5f;
        var hold = anim["holding-both"];
        if (hold != null) { hold.enabled = true; hold.weight = Mathf.MoveTowards(hold.weight, Held != 0 ? 1f : 0f, dt * 8f); }
        var chop = anim["interact-right"];
        if (chop != null) { chop.enabled = true; chop.weight = Mathf.MoveTowards(chop.weight, Chopping ? 1f : 0f, dt * 10f); }
        // a little bob on the held item
        if (heldView) heldView.transform.localPosition = new Vector3(0, Mathf.Sin(Time.time * 12f) * 0.01f * Mathf.Clamp01(sp), 0);
    }

    void StartCoroutineSafe() { if (heldView) heldView.transform.localScale = Vector3.one * 0.6f; pop = 0f; }
    float pop = 1f;
    void Update()
    {
        if (pop < 1f && heldView)
        {
            pop = Mathf.Min(1f, pop + Time.deltaTime * 6f);
            heldView.transform.localScale = Vector3.one * Kit.EaseOutBack(pop);
        }
    }
}
