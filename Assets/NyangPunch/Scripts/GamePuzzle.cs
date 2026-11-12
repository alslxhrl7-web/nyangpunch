using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace NyangPunch
{
    public partial class Game
    {
        const int N = Puzzle.N;

        bool puzzleBuilt, busy;
        string mode;
        float shake, walkT, hurtT;
        RectTransform puzRoot, pboard, shakeRoot;
        float cs, pbX, pbY;
        Image[] hintImgs, obsImgs;
        TMP_Text[] arrowTxts;
        Image catImg;
        Vector2 catPos; bool catInit;
        readonly List<RectTransform> marks = new List<RectTransform>();

        class EView
        {
            public RectTransform root; public Image body, hpBg, hpFill, bubble; public TMP_Text bang, zz, stars;
            public Vector2 pos; public bool init; public float lunge, flash; public int type = -1;
        }
        readonly Dictionary<int, EView> eviews = new Dictionary<int, EView>();

        // reused every frame by UpdatePuzzle (no per-frame allocations)
        static readonly int[][] D4 = { new[] { 1, 0 }, new[] { -1, 0 }, new[] { 0, 1 }, new[] { 0, -1 } };
        readonly HashSet<int> aliveIds = new HashSet<int>();
        readonly List<int> deadIds = new List<int>();
        readonly Color[] hintWant = new Color[N * N];
        readonly string[] arrowWant = new string[N * N];

        void DestroyEnemyViews()
        {
            foreach (var v in eviews.Values) if (v.root != null) Destroy(v.root.gameObject);
            eviews.Clear();
        }

        void EnsurePuzzle()
        {
            if (puzzleBuilt) return;
            if (puzRoot != null) Destroy(puzRoot.gameObject);
            puzzleBuilt = true; eviews.Clear(); marks.Clear(); catInit = false;
            float ch = contentBottom - contentTop;
            cs = Mathf.Floor(Mathf.Clamp(Mathf.Min((ColW - 28) / N, (ch - 20) / N), 40, 100));
            pbX = (ColW - cs * N) / 2; pbY = contentTop + Mathf.Max(8, (ch - cs * N) / 2);

            puzRoot = Art.Rect(contentLayer, "Puzzle", 0, 0, ColW, colH);
            shakeRoot = Art.Rect(puzRoot, "Shake", 0, 0, ColW, colH);
            Art.Card(shakeRoot, "BoardCard", pbX - 10, pbY - 10, cs * N + 20, cs * N + 20, Art.Paper, 6);
            pboard = Art.Rect(shakeRoot, "Board", pbX, pbY, cs * N, cs * N);
            var hit = pboard.gameObject.AddComponent<Image>(); hit.color = new Color(0, 0, 0, 0);
            pboard.gameObject.AddComponent<BoardInput>().Down = PuzzleDown;

            hintImgs = new Image[N * N]; obsImgs = new Image[N * N]; arrowTxts = new TMP_Text[N * N];
            for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
                    Art.Img(pboard, "Cell", null, ((x + y) & 1) == 1 ? Art.CellB : Art.CellA, x * cs, y * cs, cs, cs);
            for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
                {
                    int i = y * N + x;
                    hintImgs[i] = Art.Img(pboard, "Hint", "ui_cell", Color.clear, x * cs + 2, y * cs + 2, cs - 4, cs - 4, true);
                    arrowTxts[i] = Art.Txt(pboard, "", Mathf.RoundToInt(cs * 0.4f), Art.Tomato, TextAnchor.MiddleCenter, x * cs, y * cs, cs, cs);
                    var art = arrowTxts[i].rectTransform; art.pivot = new Vector2(0.5f, 0.5f); art.anchoredPosition = new Vector2((x + 0.5f) * cs, -(y + 0.5f) * cs);
                }
            for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
                {
                    var im = Art.Actor(pboard, "Obs", "obs_cushion");
                    Art.PlaceActor(im, (x + 0.5f) * cs, (y + 0.5f) * cs, cs / 70f);
                    im.gameObject.SetActive(false);
                    obsImgs[y * N + x] = im;
                }
            catImg = Art.Actor(pboard, "Cat", "cat_idle");
        }

        void SetPuzzleVisible(bool v) { if (puzRoot != null) puzRoot.gameObject.SetActive(v); }

        Vector2 CellToCol(float cx, float cy) => new Vector2(pbX + cx * cs, pbY + cy * cs);

        // ---------------------------------------------------------------- input & turns

        void PuzzleDown(Vector2 p)
        {
            if (overlay != null || busy || !S.hasRun) return;
            var R = S.run;
            int cx = Mathf.FloorToInt(p.x / cs), cy = Mathf.FloorToInt(p.y / cs);
            if (!Puzzle.InB(cx, cy)) return;
            int dx = cx - R.px, dy = cy - R.py;
            if (dx == 0 && dy == 0) return;
            if (mode == "yarn")
            {
                // a diagonal tap throws along the closer axis instead of being refused
                if (Mathf.Abs(dx) >= Mathf.Abs(dy)) DoAct(Puzzle.Kind.Yarn, System.Math.Sign(dx), 0);
                else DoAct(Puzzle.Kind.Yarn, 0, System.Math.Sign(dy));
                return;
            }
            if (Mathf.Abs(dx) >= Mathf.Abs(dy)) DoAct(Puzzle.Kind.Dir, System.Math.Sign(dx), 0);
            else DoAct(Puzzle.Kind.Dir, 0, System.Math.Sign(dy));
        }

        void PuzzleKeys()
        {
            if (!S.hasRun || busy) return;
            int dx = 0, dy = 0;
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) dy = -1;
            else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) dy = 1;
            else if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) dx = -1;
            else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) dx = 1;
            if (dx != 0 || dy != 0) { DoAct(mode == "yarn" ? Puzzle.Kind.Yarn : Puzzle.Kind.Dir, dx, dy); return; }
            if (Input.GetKeyDown(KeyCode.Space)) DoAct(Puzzle.Kind.Wait);
            else if (Input.GetKeyDown(KeyCode.Alpha1) && yarnBtn.Button.interactable) yarnBtn.Button.onClick.Invoke();
            else if (Input.GetKeyDown(KeyCode.Alpha2) && hissBtn.Button.interactable) hissBtn.Button.onClick.Invoke();
            else if (Input.GetKeyDown(KeyCode.Escape)) { mode = null; RefreshUI(); }
        }

        void DoAct(Puzzle.Kind kind, int dx = 0, int dy = 0)
        {
            if (busy || overlay != null || view != "puz" || !S.hasRun) return;
            int ox = S.run.px, oy = S.run.py;
            if (!P.Act(kind, dx, dy))
            {
                // blocked move (wall or furniture): no error sound, the cat leans into it and springs back
                if (kind == Puzzle.Kind.Dir && catInit) { catPos += new Vector2(dx, dy) * cs * 0.24f; if (dx != 0) S.run.face = dx; }
                RefreshUI(); return;
            }
            if (kind == Puzzle.Kind.Dir && (S.run.px != ox || S.run.py != oy)) walkT = 0.18f;   // walked (a punch leaves the cat in place)
            if (kind == Puzzle.Kind.Yarn) mode = null;
            busy = true;
            pendingEnemy = !P.FloorDone;
            RefreshUI();
            StartCoroutine(P.FloorDone ? ClearRoutine() : EnemyRoutine());
        }

        // The enemies' reply to the player's move. Normally runs 0.17 s later from EnemyRoutine, but a save
        // (autosave, tab hidden, quit) that lands in between runs it first, so a saved game is always a whole turn.
        bool pendingEnemy; int pendingHurt;
        void ResolveEnemyPhase()
        {
            if (!pendingEnemy) return;
            pendingEnemy = false;
            pendingHurt = S != null && S.hasRun && P != null ? P.EnemyPhase() : 0;
        }

        IEnumerator EnemyRoutine()
        {
            yield return new WaitForSeconds(0.17f);
            if (!S.hasRun) { busy = false; pendingEnemy = false; yield break; }
            ResolveEnemyPhase();
            int hurt = pendingHurt; pendingHurt = 0;
            if (hurt > 0) hurtT = 0.45f;
            Persist(); RefreshUI();
            if (S.run.hearts <= 0) { yield return new WaitForSeconds(0.6f); RunOver(); yield break; }
            if (P.FloorDone) { StartCoroutine(ClearRoutine()); yield break; }
            yield return new WaitForSeconds(0.14f);
            busy = false;
        }

        // The cleared board stays on screen for a moment, then the reward is claimed and the next floor generated.
        // A save during the pause (autosave, tab hidden, quit) stores the finished floor; Awake claims it on reload,
        // and once claimed FloorDone is false, so the reward can only be handed out once either way.
        IEnumerator ClearRoutine()
        {
            busy = true;
            Persist();
            yield return new WaitForSeconds(0.42f);
            if (!S.hasRun || !P.FloorDone) { busy = false; RefreshUI(); yield break; }   // nothing left to claim
            var rw = P.ClaimFloor();
            Persist();
            catInit = false;
            DestroyEnemyViews();
            sfx.Play("clear");
            ShowClear(rw);
            if (view != "puz") SetView("puz");
            busy = false;
            RefreshUI();
        }

        void RunOver()
        {
            int floor = S.run.floor;
            P.EndRun(); Persist(); busy = false; mode = null;
            ShowDead(floor);
            if (view != "puz") SetView("puz");
            RefreshUI();
        }

        void StartRun(string bannerSub)
        {
            P.NewRun(); Persist(); CloseOverlay(); catInit = false;
            DestroyEnemyViews();
            if (view != "puz") SetView("puz");   // the run always starts on the living-room screen
            RefreshUI(); ShowBanner("1층", bannerSub);
        }

        void RefreshPuzzleBar()
        {
            if (puzInfoT == null) return;
            if (S.hasRun)
            {
                var R = S.run;
                string hearts = new string('♥', Mathf.Max(0, R.hearts)), lost = new string('♥', Mathf.Max(0, S.MaxHearts - R.hearts));
                puzInfoT.text = $"{R.floor}층{(R.boss ? " (보스)" : "")}   <color=#e8505b>{hearts}</color><color=#2b223340>{lost}</color>   남은 적 {P.EnemiesLeft}";
            }
            else puzInfoT.text = "";
            bool yarnTarget = P.YarnHasTarget, hissTarget = P.HissHasTarget;
            if (mode == "yarn" && !yarnTarget) mode = null;
            SkillButton(yarnBtn, P.YarnOpen, S.hasRun ? S.run.cdYarn : 0, Defs.YarnNeed, "yarn", "한 줄 끝까지 던지기", yarnTarget, "같은 줄에 적이 없어요");
            SkillButton(hissBtn, P.HissOpen, S.hasRun ? S.run.cdHiss : 0, Defs.HissNeed, "hiss", "주변 적 밀어내기", hissTarget, "주변에 적이 없어요");
            waitBtn.SetInteractable(S.hasRun);
        }

        // A skill that would do nothing is greyed out with the reason, rather than answering with an error sound.
        void SkillButton(Art.Btn b, bool open, int cd, int need, string id, string desc, bool hasTarget, string noTarget)
        {
            b.SetInteractable(open && S.hasRun && cd <= 0 && hasTarget);
            b.SetColor(mode == id ? Art.Mint : Color.white);
            b.Sub.text = !open ? $"{Defs.BoxName[need]} 만들면 해금" : cd > 0 ? $"{cd}턴 뒤 사용" : !hasTarget ? noTarget : mode == id ? "방향을 누르세요" : desc;
        }

        // ---------------------------------------------------------------- IPuzzleFx (puzzle-side)

        public void Swipe(int cx, int cy, int dx, int dy)
        {
            if (view != "puz") return;   // board effects only while the living room is on screen
            var im = Art.Actor(fxLayer, "Swipe", "icon_paw");
            var c = CellToCol(cx + 0.5f, cy + 0.5f);
            Art.PlaceActor(im, c.x, c.y, cs / 22f);
            im.rectTransform.localRotation = Quaternion.Euler(0, 0, -Mathf.Atan2(dy, dx) * Mathf.Rad2Deg - 90);
            fx.Add(new FxItem { rt = im.rectTransform, g = im, life = 0.22f, max = 0.22f, vel = Vector2.zero });
        }

        public void Ring(int cx, int cy)
        {
            if (view != "puz") return;
            var im = Art.Actor(fxLayer, "Ring", "fx_ring");
            var c = CellToCol(cx + 0.5f, cy + 0.5f);
            Art.PlaceActor(im, c.x, c.y, cs / 40f);
            fx.Add(new FxItem { rt = im.rectTransform, g = im, life = 0.35f, max = 0.45f, puff = true, baseScale = im.rectTransform.localScale });
        }

        public void Yarn(int x0, int y0, int x1, int y1)
        {
            if (view != "puz") return;
            var im = Art.Actor(fxLayer, "Yarn", "icon_yarn");
            var a = CellToCol(x0 + 0.5f, y0 + 0.3f); var b = CellToCol(x1 + 0.5f, y1 + 0.3f);
            Art.PlaceActor(im, a.x, a.y, cs / 70f);
            fx.Add(new FxItem { rt = im.rectTransform, g = im, life = 0.25f, max = 0.25f, vel = new Vector2(b.x - a.x, -(b.y - a.y)) / 0.25f });
        }

        public void Lunge(int id) { if (eviews.TryGetValue(id, out var v)) v.lunge = 0.25f; }
        public void Flash(int id) { if (eviews.TryGetValue(id, out var v)) v.flash = 0.18f; }

        // ---------------------------------------------------------------- per-frame view sync

        EView MakeEView(EnemyData e)
        {
            var v = new EView();
            v.root = Art.Rect(pboard, "Enemy" + e.id, 0, 0, 0, 0);
            v.body = Art.Actor(v.root, "Body", EDef.Of(e.type).Sprite);
            v.hpBg = Art.Img(v.root, "HpBg", "ui_pill", Art.Ink, 0, 0, 10, 10, true);
            v.hpFill = Art.Img(v.root, "HpFill", "ui_pill", Art.Tomato, 0, 0, 10, 10, true);
            var bub = Art.Card(v.root, "Bubble", cs * 0.16f, -cs * 0.5f, cs * 0.28f, cs * 0.28f, Art.Tomato, 0, "ui_pill", "ui_pill_ring");
            v.bubble = bub;
            v.bang = Art.Txt(bub.transform.parent, "!", Mathf.RoundToInt(cs * 0.24f), Color.white, TextAnchor.MiddleCenter, 0, 0, cs * 0.28f, cs * 0.28f, true);
            v.zz = Art.Txt(v.root, "Zz", Mathf.RoundToInt(cs * 0.22f), new Color(0.81f, 0.89f, 1f), TextAnchor.MiddleCenter, -cs * 0.5f, -cs * 0.5f, cs * 0.4f, cs * 0.3f, true);
            v.stars = Art.Txt(v.root, "★★", Mathf.RoundToInt(cs * 0.2f), Art.Butter, TextAnchor.MiddleCenter, -cs * 0.4f, -cs * 0.56f, cs * 0.8f, cs * 0.3f, true);
            v.type = e.type;
            return v;
        }

        void UpdatePuzzle(float dt)
        {
            if (!puzzleBuilt) { EnsurePuzzle(); SetPuzzleVisible(true); }
            // board shake
            if (shake > 0) { shakeRoot.anchoredPosition = new Vector2(Random.Range(-shake, shake), Random.Range(-shake, shake)) * 0.5f; shake = Mathf.Max(0, shake - dt * 30); }
            else shakeRoot.anchoredPosition = Vector2.zero;

            bool has = S.hasRun;
            var R = S.run;
            catImg.gameObject.SetActive(has);
            if (!has)
            {
                for (int i = 0; i < N * N; i++) { obsImgs[i].gameObject.SetActive(false); hintImgs[i].color = Color.clear; arrowTxts[i].text = ""; }
                if (eviews.Count > 0) DestroyEnemyViews();
                foreach (var m in marks) m.gameObject.SetActive(false);
                return;
            }

            // obstacles, hints, charge arrows. The wanted hint colour / arrow text of every cell is worked out first and
            // only assigned when it changes, so unchanged cells are not marked dirty (and re-meshed) every frame.
            for (int i = 0; i < N * N; i++)
            {
                int o = R.obs[i];
                obsImgs[i].gameObject.SetActive(o != 0);
                if (o != 0) Art.SetActorSprite(obsImgs[i], o == 1 ? "obs_cushion" : "obs_plant");
                hintWant[i] = Color.clear; arrowWant[i] = "";
            }
            if (!busy && overlay == null)
            {
                foreach (var d in D4)
                {
                    int x = R.px + d[0], y = R.py + d[1];
                    if (mode == "yarn")
                    {
                        while (Puzzle.InB(x, y) && !P.Obs(x, y))
                        {
                            bool en = P.At(x, y) != null;
                            hintWant[y * N + x] = en ? new Color(0.91f, 0.31f, 0.36f, 0.35f) : new Color(1f, 0.82f, 0.25f, 0.35f);
                            if (en) break; x += d[0]; y += d[1];
                        }
                    }
                    else if (Puzzle.InB(x, y) && !P.Obs(x, y))
                        hintWant[y * N + x] = P.At(x, y) != null ? new Color(0.91f, 0.31f, 0.36f, 0.3f) : new Color(1, 1, 1, 0.65f);
                }
            }
            foreach (var e in R.enemies)
            {
                if (!e.charging) continue;
                int x = e.x + e.cdx, y = e.y + e.cdy;
                // Jua has no arrow glyphs: rotate a single ">" to point along the charge
                float rot = e.cdx > 0 ? 0 : e.cdx < 0 ? 180 : e.cdy > 0 ? -90 : 90;
                while (Puzzle.InB(x, y) && !P.Obs(x, y) && !(x == R.px && y == R.py))   // the charge stops at the cat
                {
                    hintWant[y * N + x] = new Color(0.91f, 0.31f, 0.36f, 0.22f + Mathf.Sin(T * 10) * 0.1f);
                    arrowWant[y * N + x] = ">";
                    arrowTxts[y * N + x].rectTransform.localRotation = Quaternion.Euler(0, 0, rot);
                    if (P.At(x, y) != null) break; x += e.cdx; y += e.cdy;
                }
            }
            for (int i = 0; i < N * N; i++)
            {
                if (hintImgs[i].color != hintWant[i]) hintImgs[i].color = hintWant[i];
                if (arrowTxts[i].text != arrowWant[i]) arrowTxts[i].text = arrowWant[i];
            }

            // spawn marks
            while (marks.Count < R.marks.Count)
            {
                var m = Art.Rect(pboard, "Mark", 0, 0, cs, cs);
                var ring = Art.Actor(m, "Ring", "fx_mark"); Art.PlaceActor(ring, cs / 2, cs / 2, cs / 46f);
                Art.Txt(m, "?", Mathf.RoundToInt(cs * 0.34f), Art.Tomato, TextAnchor.MiddleCenter, 0, 0, cs, cs, true);
                marks.Add(m);
            }
            for (int i = 0; i < marks.Count; i++)
            {
                bool on = i < R.marks.Count;
                marks[i].gameObject.SetActive(on);
                if (on) { marks[i].anchoredPosition = new Vector2(R.marks[i].x * cs, -R.marks[i].y * cs); marks[i].localRotation = Quaternion.identity; marks[i].GetChild(0).localRotation = Quaternion.Euler(0, 0, -T * 60); }
            }

            // enemies
            var alive = aliveIds; alive.Clear();
            foreach (var e in R.enemies)
            {
                alive.Add(e.id);
                if (!eviews.TryGetValue(e.id, out var v) || v.type != e.type) { if (v != null && v.root != null) Destroy(v.root.gameObject); v = MakeEView(e); eviews[e.id] = v; }
                var d = EDef.Of(e.type);
                var target = new Vector2((e.x + 0.5f) * cs, (e.y + 0.5f) * cs);
                if (!v.init) { v.pos = target; v.init = true; }
                v.pos = Vector2.Lerp(v.pos, target, Mathf.Min(1, dt * 16));
                Vector2 lunge = Vector2.zero;
                if (v.lunge > 0) { v.lunge -= dt; float k = Mathf.Sin(v.lunge / 0.25f * Mathf.PI) * cs * 0.25f; lunge = new Vector2(Mathf.Sign(R.px - e.x) * (R.px != e.x ? 1 : 0), Mathf.Sign(R.py - e.y) * (R.py != e.y ? 1 : 0)) * k; }
                v.flash = Mathf.Max(0, v.flash - dt);
                float jitter = e.threat ? Mathf.Sin(T * 40) * 1.5f : 0;
                v.root.anchoredPosition = new Vector2(v.pos.x + lunge.x + jitter, -(v.pos.y + lunge.y));
                float k0 = d.Boss ? cs / 112f : cs / 58f;
                if (e.type == (int)EType.Rat) k0 *= 1.25f;
                if (e.type == (int)EType.Cucumber || e.type == (int)EType.Tub) k0 *= 0.85f;
                float pop = 1 + v.flash * 1.2f;
                Art.PlaceActor(v.body, 0, cs * 0.06f, k0 * pop, e.face < 0 ? -1 : 1);
                v.body.color = v.flash > 0 ? new Color(1f, 0.75f, 0.75f) : Color.white;
                bool hp = e.max > 1;
                v.hpBg.gameObject.SetActive(hp); v.hpFill.gameObject.SetActive(hp);
                if (hp)
                {
                    float w = Mathf.Min(cs * 0.8f, e.max * 10f);
                    v.hpBg.rectTransform.anchoredPosition = new Vector2(-w / 2 - 3, -cs * 0.38f + 3); v.hpBg.rectTransform.sizeDelta = new Vector2(w + 6, 12);
                    v.hpFill.rectTransform.anchoredPosition = new Vector2(-w / 2, -cs * 0.38f); v.hpFill.rectTransform.sizeDelta = new Vector2(Mathf.Max(0, w * e.hp / e.max), 6);
                }
                v.bubble.transform.parent.gameObject.SetActive(e.threat);
                if (e.threat) ((RectTransform)v.bubble.transform.parent).anchoredPosition = new Vector2(cs * 0.16f, cs * 0.5f + Mathf.Sin(T * 8) * 3);
                v.zz.gameObject.SetActive(d.Slow && e.rest);
                v.stars.gameObject.SetActive(e.stun > 0);
            }
            var dead = deadIds; dead.Clear();
            foreach (var kv in eviews) if (!alive.Contains(kv.Key)) dead.Add(kv.Key);
            foreach (var id in dead) { var root = eviews[id].root; if (root != null) Destroy(root.gameObject); eviews.Remove(id); }

            // cat
            var ct = new Vector2((R.px + 0.5f) * cs, (R.py + 0.5f) * cs);
            if (!catInit) { catPos = ct; catInit = true; }
            catPos = Vector2.Lerp(catPos, ct, Mathf.Min(1, dt * 18));
            walkT = Mathf.Max(0, walkT - dt); hurtT = Mathf.Max(0, hurtT - dt);
            string mood = R.hearts <= 0 ? "cat_ko" : hurtT > 0 ? "cat_hurt" : walkT > 0 ? ((int)(T * 12) % 2 == 0 ? "cat_walk1" : "cat_walk2") : (T % 3.4f) < 0.12f ? "cat_blink" : "cat_idle";
            Art.SetActorSprite(catImg, mood);
            float breathe = walkT > 0 ? 0 : Mathf.Sin(T * 2.4f) * 0.02f;
            Art.PlaceActor(catImg, catPos.x, catPos.y + cs * 0.02f, cs / 64f, R.face < 0 ? -1 : 1);
            catImg.rectTransform.localScale = new Vector3(catImg.rectTransform.localScale.x * (1 + breathe), catImg.rectTransform.localScale.y * (1 - breathe), 1);
            catImg.transform.SetAsLastSibling();
        }
    }
}
