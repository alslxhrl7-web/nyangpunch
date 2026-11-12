using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace NyangPunch
{
    /// <summary>
    /// Entry point. Builds the whole uGUI at runtime, runs the idle economy and switches between
    /// the box hideout (merge + idle) and the living-room defense (turn-based puzzle).
    /// Layout works in a 720-unit-wide portrait column scaled to fit the screen.
    /// </summary>
    public partial class Game : MonoBehaviour, IPuzzleFx
    {
        public const float ColW = 720;

        SaveData S;
        Puzzle P;
        Sfx sfx;

        RectTransform canvasRT, col, contentLayer, fxLayer, ovRoot;
        RawImage floorBg;
        float colH = 1280, colScale = 1, contentTop, contentBottom;
        Vector2 builtFor;

        float T, saveT, uiT;
        bool balanceReset, balanceKept, saveBroken;
        // Balance tests must start from scratch, so the editor (and development builds) drop a save made with another
        // BalanceVersion. A released build keeps it: wiping every player's progress on each update is not acceptable.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        const bool ResetOnBalanceChange = true;
#else
        const bool ResetOnBalanceChange = false;
#endif
        string view = "hide";
        string overlay;

        TMP_Text churuT, rateT, hintT, puzInfoT, giftDot;
        Art.Btn muteBtn, deliverBtn, yarnBtn, hissBtn, waitBtn, tabHide, tabPuz, tabShop;
        RectTransform hideBar, puzBar;

        // ---------------------------------------------------------------- lifecycle

        void Awake()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            Application.targetFrameRate = -1;   // web: let the browser pace frames (requestAnimationFrame); any other value switches Unity to a setTimeout loop
#else
            Application.targetFrameRate = 60;
#endif
            Load();
            SaveStore.ClaimSession(Defs.SaveKey);                  // web: this tab now owns the save (an older tab stops writing)
            SaveStore.HookPageHide(gameObject.name, nameof(SaveNow));   // web: save when the tab is hidden or closed
            sfx = new Sfx(gameObject) { Muted = S.muted };
            P = new Puzzle(S, this);
            if (S.hasRun && P.FloorDone) P.ClaimFloor();   // closed right after the last enemy fell: hand out the reward now
            EnsureEventSystem();
            BuildCanvas();
        }

        void Start()
        {
            RebuildUI();
            CatchUp(true, 0);
            SetView("hide");
            if (balanceReset) ShowBanner("새 밸런스로 시작해요", "밸런스가 바뀌어서 저장을 새로 만들었어요");
            else if (balanceKept) ShowBanner("게임이 업데이트됐어요", "밸런스가 조금 바뀌었어요. 진행은 그대로예요");
            else if (saveBroken) ShowBanner("저장을 읽지 못했어요", "새로 시작해요. 이전 저장은 따로 보관했어요");
        }

        void Update()
        {
            float dt = Mathf.Min(0.05f, Time.unscaledDeltaTime); T += dt;
            var size = canvasRT.rect.size;
            if ((size - builtFor).sqrMagnitude > 1) { RebuildUI(); SetView(view); }

            double gap = (SaveData.NowMs() - S.last) / 1000.0;
            CatchUp(gap > 5, Time.unscaledDeltaTime);
            saveT += dt; if (saveT > 2) { saveT = 0; Persist(false); }
            if (otherTab && overlay != "othertab") ShowOtherTab();
            uiT += dt; if (uiT > 0.25f) { uiT = 0; RefreshUI(); if (view == "hide") TryGifts(); }

            if (view == "hide") UpdateHideout(dt); else UpdatePuzzle(dt);
            UpdateFx(dt);
            UpdateBanner(dt);
            if (overlay == null && view == "puz") PuzzleKeys();
        }

        void OnApplicationPause(bool paused) { if (paused) SaveNow(); }
        void OnApplicationQuit() => SaveNow();

        /// <summary>Saves the latest state right away. Also called from the web page (NyangSave.jslib) when the tab is hidden or closed.</summary>
        public void SaveNow()
        {
            if (S == null) return;
            S.last = Math.Max(S.last, SaveData.NowMs());   // never back (see CatchUp)
            Persist();
        }

        // ---------------------------------------------------------------- save / economy

        void Load()
        {
            S = null;
            var json = SaveStore.Read(Defs.SaveKey);
            if (!string.IsNullOrEmpty(json)) { try { S = JsonUtility.FromJson<SaveData>(json); } catch (Exception e) { Debug.LogWarning("NyangPunch: save reset " + e.Message); } }
            bool unreadable = !string.IsNullOrEmpty(json) && (S == null || S.board == null || S.board.Length != Defs.Cols * Defs.MaxRows);
            if (unreadable) { SaveStore.Write(Defs.SaveKey + ".broken", json); saveBroken = true; S = null; }   // keep the original around
            if (S != null && S.balance != Defs.BalanceVersion)
            {
                if (ResetOnBalanceChange) { Debug.Log($"NyangPunch: 밸런스 {S.balance} → {Defs.BalanceVersion}, 새로 시작"); S = null; balanceReset = true; }
                else
                {
                    // released builds never wipe a player's progress: keep a copy of the old save and carry on with it
                    // (the clamps below bring anything the new balance no longer allows back into range)
                    Debug.Log($"NyangPunch: 밸런스 {S.balance} → {Defs.BalanceVersion}, 저장은 그대로 이어감");
                    SaveStore.Write(Defs.SaveKey + ".bak" + S.balance, json);
                    S.balance = Defs.BalanceVersion; balanceKept = true;
                }
            }
            if (S == null || S.board == null || S.board.Length != Defs.Cols * Defs.MaxRows) S = SaveData.Fresh();
            if (S.up == null) S.up = new int[Defs.UpCount];
            if (S.up.Length < Defs.UpCount) Array.Resize(ref S.up, Defs.UpCount);
            if (S.gifts == null) S.gifts = new List<int>();
            if (S.run == null) { S.run = new RunData(); S.hasRun = false; }
            if (S.hasRun && S.run.hearts <= 0) { S.run = new RunData(); S.hasRun = false; }   // closed on the knocked-out screen
            // values outside what the rules can produce (hand-edited or damaged saves) are clamped or dropped
            for (int i = 0; i < S.board.Length; i++) S.board[i] = Mathf.Clamp(S.board[i], 0, Defs.MaxTier);
            S.gifts.RemoveAll(g => g < 1 || g > Defs.MaxTier);   // a gift that is no box at all is dropped, not turned into a free box
            for (int i = 0; i < S.up.Length; i++) S.up[i] = Mathf.Max(0, S.up[i]);
            foreach (var u in Defs.Ups) S.up[(int)u.Id] = Mathf.Min(S.up[(int)u.Id], u.Max);   // e.g. Room above max would index past the board
            S.maxTier = Mathf.Clamp(S.maxTier, 0, Defs.MaxTier);
            if (!Finite(S.churu) || S.churu < 0) S.churu = 0;
            if (!Finite(S.total) || S.total < 0) S.total = 0;
            if (!Finite(S.stockT) || S.stockT < 0) S.stockT = 0;   // NaN would stop deliveries from ever refilling
            S.stock = Mathf.Max(0, S.stock); S.bestFloor = Mathf.Max(0, S.bestFloor);
            if (S.hasRun && !RunLooksValid(S.run)) { S.run = new RunData(); S.hasRun = false; }
            // a save from an older balance may hold more than today's limits allow: bring it back inside them
            S.stock = Mathf.Min(S.stock, S.StockCap);
            if (S.hasRun)
            {
                S.run.hearts = Mathf.Min(S.run.hearts, S.MaxHearts);
                S.run.cdYarn = Mathf.Clamp(S.run.cdYarn, 0, S.YarnCd);
                S.run.cdHiss = Mathf.Clamp(S.run.cdHiss, 0, S.HissCd);
            }
            if (S.hasRun) S.run.toSpawn = Mathf.Max(0, S.run.toSpawn);
        }

        static bool Finite(double d) => !double.IsNaN(d) && !double.IsInfinity(d);

        static bool RunLooksValid(RunData r)
        {
            if (r.obs == null || r.obs.Length != Puzzle.N * Puzzle.N || r.enemies == null || r.marks == null) return false;
            if (!Puzzle.InB(r.px, r.py) || r.obs[r.py * Puzzle.N + r.px] != 0 || r.floor < 1) return false;
            foreach (var o in r.obs) if (o < 0 || o > 2) return false;
            foreach (var e in r.enemies) if (e == null || !Puzzle.InB(e.x, e.y) || e.type < 0 || e.type >= EDef.All.Length || e.hp <= 0 || e.max <= 0) return false;
            foreach (var m in r.marks) if (m == null || !Puzzle.InB(m.x, m.y) || m.type < 0 || m.type >= EDef.All.Length) return false;
            return true;
        }

        // Every action saves right away (force). The 2 s autosave (force = false) skips the write when only the
        // clock-driven numbers moved (churu, stock timer, last), and then still writes at least every 10 s.
        string lastBody; float lastWriteT = -999;
        bool otherTab;   // web: another tab loaded the save after this one, so this tab no longer writes it

        void Persist(bool force = true)
        {
            ResolveEnemyPhase();   // never save a half-finished turn (it would hand out a free move after a reload)
            if (otherTab) return;
            if (!SaveStore.OwnsSession(Defs.SaveKey)) { otherTab = true; Debug.Log("NyangPunch: 다른 창이 저장을 가져가서 이 창은 저장을 멈춥니다"); return; }
            string body = SaveBody();
            float now = Time.realtimeSinceStartup;
            if (!force && body == lastBody && now - lastWriteT < 10f) return;
            lastBody = body; lastWriteT = now;
            SaveStore.Write(Defs.SaveKey, JsonUtility.ToJson(S));
        }

        /// <summary>The save without the fields that change every frame on their own.</summary>
        string SaveBody()
        {
            double c = S.churu, t = S.total, st = S.stockT; long l = S.last;
            S.churu = 0; S.total = 0; S.stockT = 0; S.last = 0;
            string b = JsonUtility.ToJson(S);
            S.churu = c; S.total = t; S.stockT = st; S.last = l;
            return b;
        }

        double AdvanceTime(double sec)
        {
            if (sec <= 0) return 0;
            double earned = S.Rate() * sec;
            S.churu += earned; S.total += earned;
            if (S.stock < S.StockCap)
            {
                S.stockT += sec;
                while (S.stockT >= S.StockDT && S.stock < S.StockCap) { S.stockT -= S.StockDT; S.stock++; }
            }
            if (S.stock >= S.StockCap) S.stockT = 0;
            return earned;
        }

        void CatchUp(bool showPopup, double frameSec)
        {
            long now = SaveData.NowMs();
            double sec;
            // An anchor more than a day ahead of the clock is a clock that was wrong once (not a quick back-and-forth
            // trick): start over from now, so an honest player is not left without offline rewards for days.
            if (S.last - now > 24L * 3600 * 1000) S.last = now;
            if (now >= S.last) { sec = Math.Min((now - S.last) / 1000.0, Defs.OfflineCapSeconds); S.last = now; }
            else
            {
                // The device clock went back. Never move the anchor back with it: setting the clock forward, collecting
                // the offline time and setting it back would otherwise pay out again and again. Keep producing in real
                // time and walk the anchor forward until the clock passes it again.
                sec = Math.Max(0, Math.Min(frameSec, Defs.OfflineCapSeconds));
                S.last += (long)(sec * 1000);
            }
            if (sec <= 0) return;
            double earned = AdvanceTime(sec);
            if (showPopup && sec >= 60 && earned >= 1 && overlay == null) ShowOffline(sec, earned);
        }

        // ---------------------------------------------------------------- UI shell

        void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            es.transform.SetParent(transform, false);
        }

        void BuildCanvas()
        {
            var go = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(transform, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(720, 1280);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;
            canvasRT = (RectTransform)go.transform;

            var bgRT = Art.Stretch(canvasRT, "Floor");
            floorBg = bgRT.gameObject.AddComponent<RawImage>();
            var tex = new Texture2D(2, 2) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
            tex.SetPixels(new[] { Art.FloorB, Art.FloorA, Art.FloorA, Art.FloorB }); tex.Apply();
            floorBg.texture = tex; floorBg.raycastTarget = false;
        }

        void RebuildUI()
        {
            var size = canvasRT.rect.size;
            builtFor = size;
            floorBg.uvRect = new Rect(0, 0, size.x / 160f, size.y / 160f);
            if (col != null) Destroy(col.gameObject);
            hideoutBuilt = false; puzzleBuilt = false;
            // these point at objects that lived under col: forget them now, not only when the puzzle is rebuilt
            // (a resize while on the hideout tab left them stale, and a drag in progress never got its pointer-up)
            eviews.Clear(); marks.Clear(); catInit = false; dragFrom = -1;

            colScale = Mathf.Min(1f, size.x / ColW);
            colH = size.y / colScale;
            col = (RectTransform)new GameObject("Column", typeof(RectTransform)).transform;
            col.SetParent(canvasRT, false);
            col.anchorMin = col.anchorMax = new Vector2(0.5f, 1); col.pivot = new Vector2(0.5f, 1);
            col.sizeDelta = new Vector2(ColW, colH); col.localScale = Vector3.one * colScale; col.anchoredPosition = Vector2.zero;

            contentLayer = Art.Rect(col, "Content", 0, 0, ColW, colH);
            BuildTopBar();
            BuildBottomBars();
            fxLayer = Art.Rect(col, "FX", 0, 0, ColW, colH);
            fx.Clear(); banner = null;
            if (overlay != null) ReopenOverlay();
        }

        void BuildTopBar()
        {
            var chip = Art.Card(col, "ChuruChip", 16, 16, 210, 58, Art.Butter, 0, "ui_pill", "ui_pill_ring");
            var icon = Art.Actor(chip.transform.parent, "Icon", "icon_churu"); Art.PlaceActor(icon, 30, 29, 1.15f);
            churuT = Art.Txt(chip.transform.parent, "0", 30, Art.Ink, TextAnchor.MiddleLeft, 52, 0, 150, 58);
            var rate = Art.Card(col, "RateChip", 236, 16, 210, 58, Color.white, 0, "ui_pill", "ui_pill_ring");
            Art.Txt(rate.transform.parent, "초당", 24, Art.Ink, TextAnchor.MiddleLeft, 20, 0, 70, 58);
            rateT = Art.Txt(rate.transform.parent, "0", 28, Art.Tomato, TextAnchor.MiddleLeft, 82, 0, 120, 58);
            muteBtn = Art.Button(col, S.muted ? "소리 끔" : "소리 켬", null, ColW - 16 - 128, 14, 128, 60, Art.Paper, 22, ToggleMute);
            contentTop = 92;
        }

        void BuildBottomBars()
        {
            float tabsY = colH - 16 - 76, w = (ColW - 32 - 24) / 3f;
            tabHide = Art.Button(col, "박스 은신처", null, 16, tabsY, w, 76, Art.Paper, 26, () => { sfx.Play("click"); CloseOverlay(); SetView("hide"); });
            tabPuz = Art.Button(col, "거실 방어", null, 16 + w + 12, tabsY, w, 76, Art.Paper, 26, () => { sfx.Play("click"); CloseOverlay(); SetView("puz"); });
            tabShop = Art.Button(col, "강화 상점", null, 16 + 2 * (w + 12), tabsY, w, 76, Art.Paper, 26, () => { sfx.Play("click"); ShowShop(); });
            giftDot = Art.Txt(tabHide.Root, "", 22, Color.white, TextAnchor.MiddleCenter, w - 34, -14, 44, 36, true);

            float barY = tabsY - 20 - 104;
            hideBar = Art.Rect(col, "HideBar", 0, barY - 52, ColW, 156);
            hintT = Art.Txt(hideBar, "", 22, Art.InkSoft, TextAnchor.MiddleCenter, 24, 0, ColW - 48, 44, false, FontStyle.Normal);
            deliverBtn = Art.Button(hideBar, "택배 받기", " ", 40, 52, ColW - 80, 104, Art.Butter, 34, () => { sfx.Play("click"); Deliver(); });

            puzBar = Art.Rect(col, "PuzBar", 0, barY - 52, ColW, 156);
            puzInfoT = Art.Txt(puzBar, "", 28, Art.Ink, TextAnchor.MiddleCenter, 24, 0, ColW - 48, 44);
            float bw = (ColW - 32 - 24) / 3f;
            yarnBtn = Art.Button(puzBar, "털뭉치", " ", 16, 52, bw, 104, Color.white, 28, () => { if (!S.hasRun || busy) return; mode = mode == "yarn" ? null : "yarn"; sfx.Play("click"); RefreshUI(); });
            hissBtn = Art.Button(puzBar, "하악질", " ", 16 + bw + 12, 52, bw, 104, Color.white, 28, () => { if (!S.hasRun || busy) return; mode = null; DoAct(Puzzle.Kind.Hiss); });
            waitBtn = Art.Button(puzBar, "쉬기", "한 턴 넘기기", 16 + 2 * (bw + 12), 52, bw, 104, Color.white, 28, () => DoAct(Puzzle.Kind.Wait));
            contentBottom = barY - 52 - 8;
        }

        void ToggleMute()
        {
            S.muted = !S.muted; sfx.Muted = S.muted; Persist();
            muteBtn.Label.text = S.muted ? "소리 끔" : "소리 켬";
            sfx.Play("click");
        }

        void SetView(string v)
        {
            view = v; mode = null;
            hideBar.gameObject.SetActive(v == "hide");
            puzBar.gameObject.SetActive(v == "puz");
            tabHide.SetColor(v == "hide" ? Art.Cheese : Art.Paper);
            tabPuz.SetColor(v == "puz" ? Art.Cheese : Art.Paper);
            if (v == "hide") { EnsureHideout(); TryGifts(); } else EnsurePuzzle();
            SetHideoutVisible(v == "hide");
            SetPuzzleVisible(v == "puz");
            RefreshUI();
            if (v == "puz" && !S.hasRun && overlay == null) ShowRunIntro();
        }

        void RefreshUI()
        {
            churuT.text = Defs.Fmt(S.churu);
            rateT.text = Defs.FmtRate(S.Rate());
            bool full = FreeSlots().Count == 0;
            deliverBtn.SetInteractable(S.stock > 0 && !full);
            deliverBtn.Sub.text = full ? "칸이 꽉 찼어요! 같은 상자끼리 합치세요"
                : $"남은 택배 {S.stock}/{S.StockCap}" + (S.stock < S.StockCap ? $", 다음 {Mathf.CeilToInt((float)(S.StockDT - S.stockT))}초" : ", 가득");
            hintT.text = S.merged < 1 ? "같은 상자 두 개를 끌어다 겹치면 한 단계 큰 상자가 돼요."
                : S.bestFloor < 1 ? "거실 방어에서 층을 깨면 선물 상자가 와요. 깬 층마다 생산 +5%."
                : $"선물 대기 {S.gifts.Count}개, 최고 {S.bestFloor}층, 생산 +{S.bestFloor * 5}%";
            giftDot.text = S.gifts.Count > 0 && view != "hide" ? $"<color=#e8505b>+{S.gifts.Count}</color>" : "";
            RefreshPuzzleBar();
        }

        // ---------------------------------------------------------------- floating FX

        class FxItem { public RectTransform rt; public Graphic g; public float life, max; public Vector2 vel; public bool puff; public Vector3 baseScale; }
        readonly List<FxItem> fx = new List<FxItem>();

        public void AddText(Vector2 colPos, string text, Color color, int size)
        {
            var t = Art.Txt(fxLayer, text, size, color, TextAnchor.MiddleCenter, colPos.x - 150, colPos.y - 30, 300, 60, true);
            fx.Add(new FxItem { rt = t.rectTransform, g = t, life = 0.9f, max = 0.9f, vel = new Vector2(0, 60) });
            if (fx.Count > 90) { Destroy(fx[0].rt.gameObject); fx.RemoveAt(0); }
        }

        void AddChuruText(Vector2 colPos, string text)
        {
            var root = Art.Rect(fxLayer, "Churu", colPos.x - 60, colPos.y - 20, 120, 40);
            var icon = Art.Actor(root, "Icon", "icon_churu"); Art.PlaceActor(icon, 24, 20, 0.65f);
            var t = Art.Txt(root, text, 20, new Color(1f, 0.84f, 0.89f), TextAnchor.MiddleLeft, 38, 0, 90, 40, true);
            fx.Add(new FxItem { rt = root, g = t, life = 1.1f, max = 1.1f, vel = new Vector2(0, 30) });
        }

        public void AddPuffs(Vector2 colPos, int n, float scale)
        {
            for (int i = 0; i < n; i++)
            {
                float a = UnityEngine.Random.Range(0, Mathf.PI * 2), sp = UnityEngine.Random.Range(60, 200);
                var im = Art.Actor(fxLayer, "Puff", "fx_puff");
                float s = UnityEngine.Random.Range(0.8f, 1.3f) * scale;
                Art.PlaceActor(im, colPos.x, colPos.y, s);
                fx.Add(new FxItem { rt = im.rectTransform, g = im, life = UnityEngine.Random.Range(0.25f, 0.45f), max = 0.45f, vel = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * sp, puff = true, baseScale = im.rectTransform.localScale });
            }
        }

        void UpdateFx(float dt)
        {
            for (int i = fx.Count - 1; i >= 0; i--)
            {
                var f = fx[i]; f.life -= dt;
                if (f.life <= 0 || f.rt == null) { if (f.rt != null) Destroy(f.rt.gameObject); fx.RemoveAt(i); continue; }
                f.rt.anchoredPosition += f.vel * dt;
                if (f.puff) { f.vel *= 0.9f; f.rt.localScale = f.baseScale * (1.4f - f.life / f.max * 0.4f); }
                var c = f.g.color; c.a = Mathf.Min(1, f.life / f.max * 2); f.g.color = c;
            }
        }

        // ---------------------------------------------------------------- banner

        class BannerUI { public RectTransform root; public CanvasGroup cg; public float t; }
        BannerUI banner;

        public void ShowBanner(string title, string sub)
        {
            if (banner != null) Destroy(banner.root.gameObject);
            float h = string.IsNullOrEmpty(sub) ? 64 : 104;
            var root = Art.Rect(fxLayer, "Banner", 40, contentTop + 8, ColW - 80, h);
            var bg = root.gameObject.AddComponent<Image>(); bg.sprite = Art.Spr("ui_pill"); bg.type = Image.Type.Sliced; bg.pixelsPerUnitMultiplier = 2; bg.color = new Color(Art.Ink.r, Art.Ink.g, Art.Ink.b, 0.9f); bg.raycastTarget = false;
            Art.Txt(root, title, 36, Art.Butter, TextAnchor.MiddleCenter, 10, 6, ColW - 100, 52, true);
            if (!string.IsNullOrEmpty(sub)) Art.Txt(root, sub, 22, Color.white, TextAnchor.MiddleCenter, 10, 58, ColW - 100, 40, false, FontStyle.Normal);
            banner = new BannerUI { root = root, cg = root.gameObject.AddComponent<CanvasGroup>(), t = 2.6f };
        }

        void UpdateBanner(float dt)
        {
            if (banner == null) return;
            banner.t -= dt;
            if (banner.t <= 0) { Destroy(banner.root.gameObject); banner = null; return; }
            banner.cg.alpha = Mathf.Min(1, banner.t * 2);
        }

        // ---------------------------------------------------------------- IPuzzleFx

        // Board effects are only drawn while the living room is on screen (a turn can finish after switching to the hideout).
        public void CellText(float cx, float cy, string text, Color color, int size) { if (view == "puz") AddText(CellToCol(cx, cy), text, color, size); }
        public void CellPuff(float cx, float cy, int count, float scale) { if (view == "puz") AddPuffs(CellToCol(cx, cy), count, scale); }
        public void Sound(string key) => sfx.Play(key);
        public void Shake(float amount) => shake = Mathf.Max(shake, amount);
        public void Banner(string title, string sub) => ShowBanner(title, sub);
        public void ChuruGained(double amount) { }
    }

    /// <summary>Routes pointer events on a board to callbacks in top-left local coordinates (y down).</summary>
    public class BoardInput : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public Action<Vector2> Down, Drag, Up;
        Vector2 Local(PointerEventData e)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform, e.position, e.pressEventCamera, out var lp);
            return new Vector2(lp.x, -lp.y);
        }
        public void OnPointerDown(PointerEventData e) => Down?.Invoke(Local(e));
        public void OnDrag(PointerEventData e) => Drag?.Invoke(Local(e));
        public void OnPointerUp(PointerEventData e) => Up?.Invoke(Local(e));
    }
}
