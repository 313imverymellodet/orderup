mergeInto(LibraryManager.library, {
  SD_Gameplay: function (on) { if (window.SD && window.SD.gameplay) window.SD.gameplay(!!on); },
  SD_Event: function (namePtr, value) { if (window.SD && window.SD.track) window.SD.track(UTF8ToString(namePtr), value); },
  SD_Ready: function () { if (window.SD && window.SD.ready) window.SD.ready(); },

  OU_Vibrate: function (ms) { try { if (navigator.vibrate && (!navigator.userActivation || navigator.userActivation.hasBeenActive)) navigator.vibrate(ms); } catch (e) {} },

  OU_ArmShare: function (textPtr) {
    var text = UTF8ToString(textPtr), w = window;
    var url = w.location.origin + w.location.pathname;
    var doShare = function () {
      if (!w.__ouPending) return;
      var t = w.__ouPending; w.__ouPending = null;
      if (navigator.share) navigator.share({ title: "ORDER UP!", text: t, url: url }).catch(function () {});
      else if (navigator.clipboard) navigator.clipboard.writeText(t + "\n" + url).then(function () { w.ouToast && w.ouToast("Copied! Paste it anywhere"); });
      if (w.SD && w.SD.track) w.SD.track("share", 0);
    };
    if (!w.__ouHooked) { w.__ouHooked = true; ["pointerup", "touchend", "click"].forEach(function (ev) { w.addEventListener(ev, doShare, true); }); }
    w.__ouPending = text;
    setTimeout(doShare, 450);
  },

  OU_RunStart: function (mapPtr) { if (window.kitchen) window.kitchen.start(UTF8ToString(mapPtr)); },
  OU_RunSubmit: function (mapPtr, score, stars, crew) { if (window.kitchen) window.kitchen.submit(UTF8ToString(mapPtr), score, stars, crew); },
  OU_ShowBoard: function (mapPtr) { if (window.kitchen) window.kitchen.board(UTF8ToString(mapPtr)); },
  OU_NetOpen: function (mapPtr, look) { if (window.kitchen) window.kitchen.lobby(UTF8ToString(mapPtr), look); },
  OU_NetSend: function (jsonPtr) { if (window.kitchen) window.kitchen.send(UTF8ToString(jsonPtr)); },
  OU_NetLeave: function () { if (window.kitchen) window.kitchen.leave(); }
});
