using UnityEngine;

// All audio is synthesized at boot: zero audio files.
public class Sfx : MonoBehaviour
{
    public static Sfx I;
    const int SR = 22050;
    const float TAU = Mathf.PI * 2f;
    AudioSource[] voices; int next;
    AudioSource music;
    AudioClip chop, ding, sizzle, burn, clink, pick, drop, trash, bell, nope, serve, fail, click, star, dash, beep, go, win, lose;
    public bool Muted { get; private set; }
    System.Random rnd = new System.Random(3);
    float N() => (float)(rnd.NextDouble() * 2 - 1);
    float lastChop;

    void Awake()
    {
        I = this;
        voices = new AudioSource[10];
        for (int i = 0; i < voices.Length; i++) { voices[i] = gameObject.AddComponent<AudioSource>(); voices[i].playOnAwake = false; }
        music = gameObject.AddComponent<AudioSource>();
        music.loop = true; music.volume = 0.24f; music.playOnAwake = false;
        Build();
        music.clip = Music();
    }

    public void SetMuted(bool m) { Muted = m; AudioListener.volume = m ? 0 : 1; }
    bool unlocked;
    public void Unlock() { unlocked = true; }
    public void Music(bool on) { if (on) { if (!music.isPlaying) { music.Play(); clockStart = Time.unscaledTime; } } else music.Stop(); }

    // ---- KITCHEN BEATS: the music is 126 bpm and the clip is a whole number of bars, so the beat grid is the
    // playback position. Each device judges its own taps against the music it hears.
    public const float Bpm = 126f, BeatLen = 60f / Bpm;
    float clockStart;
    public float BeatTime => music.isPlaying && music.time > 0.01f ? music.time : Time.unscaledTime - clockStart;
    public float Phase01 => Mathf.Repeat(BeatTime / BeatLen, 1f);
    // a little more room after the beat than before it (touch + audio latency land late)
    public bool OnBeat()
    {
        float b = BeatTime / BeatLen;
        float err = (b - Mathf.Round(b)) * BeatLen;
        return err >= -0.10f && err <= 0.15f;
    }
    public void BeatHit(int streak) { Play(clink, 0.3f, 1.1f + Mathf.Min(streak, 12) * 0.05f); Chop(); }

    void Play(AudioClip c, float vol, float pitch = 1f)
    {
        var s = voices[next]; next = (next + 1) % voices.Length;
        s.pitch = pitch; s.PlayOneShot(c, vol);
    }

    public void Chop() { if (Time.unscaledTime - lastChop < 0.07f) return; lastChop = Time.unscaledTime; Play(chop, 0.4f, Random.Range(0.9f, 1.15f)); }
    public void Ding() => Play(ding, 0.45f);
    public void Sizzle() => Play(sizzle, 0.35f);
    public void Burn() => Play(burn, 0.5f);
    public void Clink() => Play(clink, 0.35f, Random.Range(0.95f, 1.1f));
    public void Pick() => Play(pick, 0.35f, Random.Range(0.95f, 1.1f));
    public void Drop() => Play(drop, 0.35f, Random.Range(0.9f, 1.05f));
    public void Trash() => Play(trash, 0.4f);
    public void Bell() => Play(bell, 0.5f);
    public void Nope() => Play(nope, 0.35f);
    public void Serve() => Play(serve, 0.6f);
    public void Fail() => Play(fail, 0.55f);
    public void Click() => Play(click, 0.4f);
    public void Star() => Play(star, 0.55f);
    public void Dash() => Play(dash, 0.3f, Random.Range(0.95f, 1.1f));
    public void Beep(bool isGo) => Play(isGo ? go : beep, 0.5f);
    public void Finish(int stars) => Play(stars > 0 ? win : lose, 0.7f);

    static AudioClip Clip(string n, float[] d) { var c = AudioClip.Create(n, d.Length, 1, SR, false); c.SetData(d, 0); return c; }
    delegate float Gen(float t, float dt);
    static float[] R(float dur, Gen g)
    {
        int n = (int)(SR * dur); var d = new float[n]; float dt = 1f / SR;
        for (int i = 0; i < n; i++) d[i] = Mathf.Clamp(g(i * dt, dt) * Mathf.Clamp01((n - i) / (SR * 0.008f)), -1, 1);
        return d;
    }
    float[] Arp(float[] notes, float step, float tail, float vol)
    {
        float ph = 0;
        return R(step * notes.Length + tail, (t, dt) =>
        {
            int k = Mathf.Min((int)(t / step), notes.Length - 1);
            ph += TAU * notes[k] * dt;
            float lt = t - k * step;
            return (Mathf.Sin(ph) + 0.35f * Mathf.Sin(ph * 2) + 0.15f * Mathf.Sin(ph * 3)) * Mathf.Exp(-lt * (k < notes.Length - 1 ? 12 : 3f)) * vol;
        });
    }

    void Build()
    {
        float ph = 0, lp = 0;
        chop = Clip("chop", R(0.09f, (t, dt) => { lp += (N() - lp) * 0.6f; ph += TAU * Mathf.Lerp(900, 300, t / 0.09f) * dt; return (lp * 0.7f + Mathf.Sin(ph) * 0.5f) * Mathf.Exp(-t * 60); }));
        ding = Clip("ding", Arp(new[] { 1318.5f, 1760f }, 0.07f, 0.5f, 0.35f));
        lp = 0; float hp = 0;
        sizzle = Clip("sizzle", R(0.9f, (t, dt) => { float n = N(); float o = n - hp; hp = n; lp += (o - lp) * 0.5f; return lp * 0.6f * Mathf.Sin(t / 0.9f * Mathf.PI) * (0.7f + 0.3f * Mathf.Sin(t * 60)); }));
        lp = 0; ph = 0;
        burn = Clip("burn", R(0.8f, (t, dt) => { lp += (N() - lp) * 0.15f; ph += TAU * Mathf.Lerp(220, 90, t / 0.8f) * dt; return (lp * 1.2f + (Mathf.Sin(ph) > 0 ? 0.25f : -0.25f)) * Mathf.Exp(-t * 3); }));
        ph = 0;
        clink = Clip("clink", R(0.25f, (t, dt) => (Mathf.Sin(TAU * 2600 * t) * 0.5f + Mathf.Sin(TAU * 3900 * t) * 0.3f) * Mathf.Exp(-t * 25)));
        ph = 0;
        pick = Clip("pick", R(0.1f, (t, dt) => { ph += TAU * Mathf.Lerp(420, 880, t / 0.1f) * dt; return Mathf.Sin(ph) * Mathf.Exp(-t * 28) * 0.8f; }));
        ph = 0;
        drop = Clip("drop", R(0.12f, (t, dt) => { ph += TAU * Mathf.Lerp(600, 260, t / 0.12f) * dt; return Mathf.Sin(ph) * Mathf.Exp(-t * 26) * 0.8f; }));
        lp = 0;
        trash = Clip("trash", R(0.35f, (t, dt) => { lp += (N() - lp) * Mathf.Lerp(0.6f, 0.1f, t / 0.35f); return lp * Mathf.Exp(-t * 9) * 1.1f + Mathf.Sin(TAU * 120 * t) * Mathf.Exp(-t * 14) * 0.5f; }));
        bell = Clip("bell", R(1.1f, (t, dt) => (Mathf.Sin(TAU * 1568 * t) * 0.5f + Mathf.Sin(TAU * 3136 * t * 1.003f) * 0.2f + Mathf.Sin(TAU * 2349 * t) * 0.15f) * Mathf.Exp(-t * 3.5f)));
        ph = 0;
        nope = Clip("nope", R(0.22f, (t, dt) => { ph += TAU * (t < 0.1f ? 330 : 247) * dt; return (Mathf.Sin(ph) > 0 ? 0.3f : -0.3f) * Mathf.Exp(-t * 8); }));
        serve = Clip("serve", Arp(new[] { 783.99f, 987.77f, 1174.66f, 1567.98f }, 0.07f, 0.7f, 0.4f));
        fail = Clip("fail", Arp(new[] { 392f, 349.23f, 293.66f }, 0.14f, 0.6f, 0.4f));
        ph = 0;
        click = Clip("click", R(0.04f, (t, dt) => { ph += TAU * 1300 * dt; return Mathf.Sin(ph) * Mathf.Exp(-t * 90) * 0.6f; }));
        star = Clip("star", Arp(new[] { 1046.5f, 1567.98f }, 0.06f, 0.5f, 0.4f));
        lp = 0;
        dash = Clip("dash", R(0.25f, (t, dt) => { lp += (N() - lp) * Mathf.Lerp(0.05f, 0.5f, Mathf.Sin(t / 0.25f * Mathf.PI)); return lp * Mathf.Sin(t / 0.25f * Mathf.PI) * 1.1f; }));
        beep = Clip("beep", R(0.2f, (t, dt) => Mathf.Sin(TAU * 740 * t) * 0.6f * Mathf.Min(1, (0.2f - t) * 20)));
        go = Clip("go", R(0.6f, (t, dt) => (Mathf.Sin(TAU * 1480 * t) * 0.5f + Mathf.Sin(TAU * 1109 * t) * 0.3f) * Mathf.Exp(-t * 3)));
        win = Clip("win", Arp(new[] { 523.25f, 659.25f, 783.99f, 1046.5f, 783.99f, 1046.5f, 1318.5f }, 0.11f, 1.2f, 0.4f));
        lose = Clip("lose", Arp(new[] { 392f, 369.99f, 349.23f, 311.13f }, 0.2f, 1f, 0.4f));
    }

    // 126 bpm bouncy kitchen swing in F: F - Dm - Bb - C, walking bass, marimba-ish melody, shaker.
    AudioClip Music()
    {
        float bpm = 126f, beat = 60f / bpm;
        int bars = 8; float dur = beat * 4 * bars;
        int n = (int)(SR * dur); var d = new float[n];
        float[][] chords = { new[] { 349.23f, 440f, 523.25f }, new[] { 293.66f, 349.23f, 440f }, new[] { 233.08f, 293.66f, 349.23f }, new[] { 261.63f, 329.63f, 392f } };
        float[][] bass = { new[] { 87.31f, 110f, 130.81f, 110f }, new[] { 73.42f, 87.31f, 110f, 87.31f }, new[] { 58.27f, 73.42f, 87.31f, 73.42f }, new[] { 65.41f, 82.41f, 98f, 82.41f } };
        float[] mel = { 698.46f, 880f, 1046.5f, 880f, 783.99f, 698.46f, 587.33f, 698.46f, 698.46f, 587.33f, 523.25f, 587.33f, 698.46f, 783.99f, 880f, 783.99f };
        float hp = 0, blp = 0;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / SR, bt = t / beat;
            int bi = (int)bt, barIdx = bi / 4, bar = barIdx % 4;
            float ib = (bt - bi) * beat;
            float nz = N();
            float kick = (bi % 2 == 0) ? Mathf.Sin(TAU * (55 + 80 * Mathf.Exp(-ib * 30)) * ib) * Mathf.Exp(-ib * 10) * 0.5f : 0;
            float snare = (bi % 2 == 1) ? (nz * 0.4f + Mathf.Sin(TAU * 200 * ib) * 0.3f) * Mathf.Exp(-ib * 18) * 0.25f : 0;
            float e8 = bt * 2; int e8i = (int)e8; float i8 = (e8 - e8i) * beat / 2;
            // swung shaker
            float shaker = (nz - hp) * Mathf.Exp(-i8 * 45) * (e8i % 2 == 1 ? 0.05f : 0.03f); hp = nz;
            // walking bass, one note per beat
            float bf = bass[bar][bi % 4];
            float tri = Mathf.Abs(2f * ((bf * t) % 1f) - 1f) * 2f - 1f;
            blp += (tri - blp) * 0.3f;
            float bs = blp * Mathf.Exp(-ib * 3) * 0.32f;
            // offbeat piano chords
            float pad = 0;
            if (bi % 2 == 1) foreach (var f in chords[bar]) pad += Mathf.Sin(TAU * f * t) + 0.3f * Mathf.Sin(TAU * f * 2 * t);
            pad *= 0.045f * Mathf.Exp(-ib * 6);
            // marimba melody in the second half
            float m = 0;
            if (barIdx >= 4)
            {
                float mf = mel[e8i % 16];
                m = (Mathf.Sin(TAU * mf * i8) + 0.4f * Mathf.Sin(TAU * mf * 4f * i8) * Mathf.Exp(-i8 * 30)) * Mathf.Exp(-i8 * 9) * 0.12f;
            }
            d[i] = Mathf.Clamp((kick + snare + shaker + bs + pad + m) * 0.85f, -1, 1);
        }
        return Clip("music", d);
    }
}
