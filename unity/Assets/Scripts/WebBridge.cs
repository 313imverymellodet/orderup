using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.EventSystems;

// Page integrations: analytics, share sheet, haptics, leaderboard + online kitchens (kitchen.js).
public class WebBridge : MonoBehaviour
{
    public static WebBridge I;

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] static extern void SD_Gameplay(int on);
    [DllImport("__Internal")] static extern void SD_Event(string name, int value);
    [DllImport("__Internal")] static extern void SD_Ready();
    [DllImport("__Internal")] static extern void OU_ArmShare(string text);
    [DllImport("__Internal")] static extern void OU_Vibrate(int ms);
    [DllImport("__Internal")] static extern void OU_RunStart(string map);
    [DllImport("__Internal")] static extern void OU_RunSubmit(string map, int score, int stars, int crew);
    [DllImport("__Internal")] static extern void OU_ShowBoard(string map);
    [DllImport("__Internal")] static extern void OU_NetOpen(string map, int look);
    [DllImport("__Internal")] static extern void OU_NetSend(string json);
    [DllImport("__Internal")] static extern void OU_NetLeave();
#endif

    void Awake() { I = this; gameObject.name = "WebBridge"; }

    public static void Gameplay(bool on)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        SD_Gameplay(on ? 1 : 0);
#endif
    }
    public static void Event(string name, int value = 0)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        SD_Event(name, value);
#endif
    }
    public static void Ready()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        SD_Ready();
#endif
    }
    public static void Vibrate(int ms)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        OU_Vibrate(ms);
#endif
    }
    // Web Share needs a live gesture: arm on pointer-down, the page fires it on pointer-up.
    public static void ArmShare(string text)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        OU_ArmShare(text);
#else
        GUIUtility.systemCopyBuffer = text;
#endif
    }
    public static void RunStart(string map)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        OU_RunStart(map);
#endif
    }
    public static void RunSubmit(string map, int score, int stars, int crew)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        OU_RunSubmit(map, score, stars, crew);
#endif
    }
    public static void ShowBoard(string map)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        OU_ShowBoard(map);
#endif
    }
    public static void NetOpen(string map, int look)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        OU_NetOpen(map, look);
#endif
    }
    public static void NetSend(string json)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        OU_NetSend(json);
#endif
    }
    public static void NetLeave()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        OU_NetLeave();
#endif
    }
}

public class ShareOnPress : MonoBehaviour, IPointerDownHandler
{
    public Func<string> Text;
    public void OnPointerDown(PointerEventData e) { if (Text != null) WebBridge.ArmShare(Text()); }
}
