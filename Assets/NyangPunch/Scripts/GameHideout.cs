using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace NyangPunch
{
    public partial class Game
    {
        bool hideoutBuilt;
        RectTransform hideRoot, board;
        float ts, boardX, boardY;
        int builtRows;
        Image[] slotImgs, boxImgs;
        RectTransform[] badges;
        TMP_Text[] badgeTxts;
        float[] pops, emitT;
        Image dragImg;
        int dragFrom = -1;
        Vector2 dragPos, dragStart;
        bool dragMoved;

        // per-tier strings built once (UpdateHideout runs every frame; "box_" + t would allocate per box per frame)
        static readonly string[] BoxSpr = BuildTierStrings("box_"), TierTxt = BuildTierStrings("");
        static string[] BuildTierStrings(string prefix)
        {
            var a = new string[Defs.MaxTier + 1];
            for (int t = 0; t <= Defs.MaxTier; t++) a[t] = prefix + t;
            return a;
        }

        void EnsureHideout()
        {
            if (hideoutBuilt && builtRows == S.Rows) return;
            if (hideRoot != null) Destroy(hideRoot.gameObject);
            hideoutBuilt = true; builtRows = S.Rows;
            int rows = S.Rows, n = Defs.Cols * rows;
            float ch = contentBottom - contentTop;
            ts = Mathf.Floor(Mathf.Clamp(Mathf.Min((ColW - 48) / Defs.Cols, (ch - 28) / rows), 44, 168));
            float bw = ts * Defs.Cols, bh = ts * rows;
            boardX = (ColW - bw) / 2; boardY = contentTop + Mathf.Max(8, (ch - bh) / 2);

            hideRoot = Art.Rect(contentLayer, "Hideout", 0, 0, ColW, colH);
            Art.Card(hideRoot, "BoardCard", boardX - 10, boardY - 10, bw + 20, bh + 20, Art.Paper, 6);
            board = Art.Rect(hideRoot, "Board", boardX, boardY, bw, bh);
            var hit = board.gameObject.AddComponent<Image>(); hit.color = new Color(0, 0, 0, 0);
            var input = board.gameObject.AddComponent<BoardInput>();
            input.Down = BoardDown; input.Drag = BoardDrag; input.Up = BoardUp;

            slotImgs = new Image[n]; boxImgs = new Image[n]; badges = new RectTransform[n]; badgeTxts = new TMP_Text[n];
            pops = new float[n]; emitT = new float[n];
            for (int i = 0; i < n; i++)
            {
                float x = (i % Defs.Cols) * ts, y = (i / Defs.Cols) * ts;
                slotImgs[i] = Art.Img(board, "Slot" + i, "ui_cell", Art.Slot, x + 2, y + 2, ts - 4, ts - 4, true);
                boxImgs[i] = Art.Actor(board, "Box" + i, "box_1");
                var badge = Art.Card(board, "Badge" + i, x + 8, y + 8, 30, 30, Art.Butter, 0, "ui_pill", "ui_pill_ring");
                badges[i] = (RectTransform)badge.transform.parent;
                badgeTxts[i] = Art.Txt(badges[i], "1", 18, Art.Ink, TextAnchor.MiddleCenter, 0, 0, 30, 30);
                emitT[i] = Random.Range(0f, 4f);
            }
            dragImg = Art.Actor(hideRoot, "Drag", "box_1");
            dragImg.gameObject.SetActive(false);
        }

        void SetHideoutVisible(bool v) { if (hideRoot != null) hideRoot.gameObject.SetActive(v); }

        Vector2 SlotCenter(int i) => new Vector2(boardX + (i % Defs.Cols + 0.5f) * ts, boardY + (i / Defs.Cols + 0.5f) * ts);

        int SlotAt(Vector2 local)
        {
            int c = Mathf.FloorToInt(local.x / ts), r = Mathf.FloorToInt(local.y / ts);
            if (c < 0 || c >= Defs.Cols || r < 0 || r >= S.Rows) return -1;
            return r * Defs.Cols + c;
        }

        List<int> FreeSlots()
        {
            var l = new List<int>();
            for (int i = 0; i < S.Slots; i++) if (S.board[i] == 0) l.Add(i);
            return l;
        }

        void NoteTier(int t)
        {
            if (t <= S.maxTier) return;
            S.maxTier = t;
            string sub = $"초당 츄르 {Defs.FmtRate(Defs.BoxProd[t] * S.FloorBonus)}개를 만들어요";
            if (t == Defs.YarnNeed) sub = "거실 방어 스킬 '털뭉치' 해금!";
            if (t == Defs.HissNeed) sub = "거실 방어 스킬 '하악질' 해금!";
            ShowBanner($"새 상자! {Defs.BoxName[t]}", sub);
            sfx.Play("level");
        }

        int PlaceBox(int t, bool quiet)
        {
            var free = FreeSlots(); if (free.Count == 0) return -1;
            int i = free[Random.Range(0, free.Count)];
            S.board[i] = t; if (pops != null && i < pops.Length) pops[i] = 1;
            NoteTier(t);
            if (!quiet) sfx.Play("drop");
            return i;
        }

        void Deliver()
        {
            if (S.stock <= 0) return;   // the button is disabled when there is nothing to deliver
            int t = Random.value < S.Up(Defs.UpId.Quality) * 0.1f ? 2 : 1;
            if (PlaceBox(t, false) < 0) { ShowBanner("칸이 꽉 찼어요", "같은 상자끼리 합쳐서 자리를 만드세요"); return; }
            S.stock--; Persist(); RefreshUI();
        }

        void TryGifts()
        {
            if (!hideoutBuilt) return;
            while (S.gifts.Count > 0 && FreeSlots().Count > 0)
            {
                int t = S.gifts[0]; S.gifts.RemoveAt(0);
                int tierBefore = S.maxTier;
                int i = PlaceBox(t, true);
                if (view == "hide") AddPuffs(SlotCenter(i), 10, 1);
                // a gift that is a brand-new tier already raised the "new box" banner (maybe a skill unlock): keep that one
                if (S.maxTier == tierBefore) ShowBanner("선물 상자 도착!", $"{Defs.BoxName[t]}가 배달됐어요");
                sfx.Play("churu");
            }
        }

        void Merge(int from, int to)
        {
            int t = S.board[from] + 1;
            S.board[to] = t; S.board[from] = 0; pops[to] = 1.3f; S.merged++;
            var c = SlotCenter(to); AddPuffs(c, 12, 1.2f);
            double bonus = System.Math.Max(1, System.Math.Round(Defs.BoxProd[t] * 15));
            S.churu += bonus; AddText(c + new Vector2(0, -40), "+" + Defs.Fmt(bonus), Art.Butter, 32);
            sfx.Play("merge"); NoteTier(t);
        }

        void BoardDown(Vector2 p)
        {
            if (overlay != null) return;
            int i = SlotAt(p);
            if (i < 0 || S.board[i] == 0) return;
            dragFrom = i; dragPos = dragStart = p; dragMoved = false;
        }

        void BoardDrag(Vector2 p)
        {
            if (dragFrom < 0) return;
            dragPos = p;
            if ((p - dragStart).magnitude > 8) dragMoved = true;
        }

        void BoardUp(Vector2 p)
        {
            if (dragFrom < 0) return;
            int from = dragFrom; dragFrom = -1; dragImg.gameObject.SetActive(false);
            int to = SlotAt(p);
            var b = S.board;
            if (!dragMoved || to == from) { pops[from] = 0.6f; sfx.Play("click"); AddText(SlotCenter(from) + new Vector2(0, -30), Defs.BoxName[b[from]], Color.white, 22); return; }
            if (to < 0) return;
            if (b[to] == b[from] && b[from] < Defs.MaxTier) Merge(from, to);
            else if (b[to] == 0) { b[to] = b[from]; b[from] = 0; pops[to] = 0.5f; sfx.Play("drop"); }
            else { int t = b[to]; b[to] = b[from]; b[from] = t; pops[to] = 0.5f; pops[from] = 0.5f; sfx.Play("drop"); }
            Persist(); RefreshUI(); TryGifts();
        }

        void UpdateHideout(float dt)
        {
            if (!hideoutBuilt || builtRows != S.Rows) { EnsureHideout(); SetHideoutVisible(true); }
            int n = Defs.Cols * builtRows;
            float s = ts / 88f;
            int hover = dragFrom >= 0 && dragMoved ? SlotAt(dragPos) : -1;
            for (int i = 0; i < n; i++)
            {
                int t = S.board[i];
                // slot tint while dragging
                Color col = Art.Slot;
                if (dragFrom >= 0 && dragMoved && i != dragFrom)
                {
                    bool same = t == S.board[dragFrom] && t > 0 && t < Defs.MaxTier;
                    if (i == hover) col = same ? Art.SlotGood : Art.SlotSwap;
                    else if (same) col = Art.SlotHint;
                }
                slotImgs[i].color = col;

                bool show = t > 0 && !(dragFrom == i && dragMoved);
                boxImgs[i].gameObject.SetActive(show);
                badges[i].gameObject.SetActive(t > 0);
                if (t <= 0) continue;
                badgeTxts[i].text = TierTxt[t];
                if (!show) continue;
                Art.SetActorSprite(boxImgs[i], BoxSpr[t]);
                pops[i] = Mathf.Max(0, pops[i] - dt * 3);
                float pp = pops[i] > 0 ? Mathf.Sin(pops[i] * Mathf.PI) * 0.18f * pops[i] : 0;
                float br = Mathf.Sin(T * 2 + i) * 0.015f;
                float k = s * (t >= 6 ? 0.92f : 1f);
                var rt = boxImgs[i].rectTransform;
                rt.anchoredPosition = new Vector2((i % Defs.Cols + 0.5f) * ts, -((i / Defs.Cols + 0.5f) * ts + ts * 0.34f));
                rt.localScale = new Vector3(k * (1 + pp + br), k * (1 + pp - br), 1);

                emitT[i] -= dt;
                if (emitT[i] <= 0)
                {
                    emitT[i] = Random.Range(3.5f, 5.5f);
                    double amt = Defs.BoxProd[t] * S.FloorBonus * 4.5;
                    AddChuruText(SlotCenter(i) + new Vector2(Random.Range(-10, 10), -ts * 0.3f), "+" + (amt < 10 ? amt.ToString("0.0") : Defs.Fmt(amt)));
                }
            }
            if (dragFrom >= 0 && dragMoved)
            {
                dragImg.gameObject.SetActive(true);
                Art.SetActorSprite(dragImg, BoxSpr[S.board[dragFrom]]);
                Art.PlaceActor(dragImg, boardX + dragPos.x, boardY + dragPos.y + ts * 0.2f, s * 1.12f);
                dragImg.rectTransform.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(T * 12) * 3);
                dragImg.transform.SetAsLastSibling();
            }
        }
    }
}
