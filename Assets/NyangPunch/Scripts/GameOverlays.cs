using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace NyangPunch
{
    public partial class Game
    {
        RectTransform ovPanel, ovContent;
        float ovY;
        Action reopen;

        const float PanelX = 36, PanelW = ColW - 72, Pad = 28, InnerW = PanelW - Pad * 2;

        RectTransform BeginOverlay(string id, Action rebuild)
        {
            CloseOverlay();
            overlay = id; reopen = rebuild;
            ovRoot = Art.Stretch(canvasRT, "Overlay " + id);
            var dim = ovRoot.gameObject.AddComponent<Image>(); dim.color = Art.Shade; dim.raycastTarget = true;
            var ovCol = (RectTransform)new GameObject("Col", typeof(RectTransform)).transform;
            ovCol.SetParent(ovRoot, false);
            ovCol.anchorMin = ovCol.anchorMax = new Vector2(0.5f, 1); ovCol.pivot = new Vector2(0.5f, 1);
            ovCol.sizeDelta = new Vector2(ColW, colH); ovCol.localScale = Vector3.one * colScale;
            ovPanel = Art.Rect(ovCol, "Panel", PanelX, 0, PanelW, 100);
            Art.Card(ovPanel, "Card", 0, 0, PanelW, 100, Art.Paper, 10);
            ovContent = Art.Rect(ovPanel, "Content", Pad, Pad, InnerW, 100);
            ovY = 0;
            return ovContent;
        }

        void EndOverlay()
        {
            float h = ovY + Pad * 2;
            ovPanel.sizeDelta = new Vector2(PanelW, h);
            ovPanel.anchoredPosition = new Vector2(PanelX, -Mathf.Max(24, (colH - h) / 2));
            var card = ovPanel.Find("Card");
            foreach (RectTransform c in card) c.sizeDelta = new Vector2(PanelW, h);
        }

        void CloseOverlay()
        {
            if (ovRoot != null) Destroy(ovRoot.gameObject);
            ovRoot = null; overlay = null; reopen = null;
        }

        /// <summary>Rebuilds the open popup for the new screen size (every popup registers how to rebuild itself).</summary>
        void ReopenOverlay() { if (reopen != null) { var r = reopen; r(); } else CloseOverlay(); }

        TMP_Text OvText(string s, int size, Color c, float h, FontStyle style = FontStyle.Bold, TextAnchor a = TextAnchor.UpperLeft)
        {
            var t = Art.Txt(ovContent, s, size, c, a, 0, ovY, InnerW, h, false, style);
            ovY += h + 10;
            return t;
        }

        void OvEarn(string label, string value, Color fill)
        {
            var f = Art.Card(ovContent, "Earn", 0, ovY, InnerW, 76, fill, 0);
            Art.Txt(f.transform.parent, label, 26, Art.Ink, TextAnchor.MiddleLeft, 20, 0, InnerW * 0.55f, 76);
            Art.Txt(f.transform.parent, value, 38, Art.Ink, TextAnchor.MiddleRight, InnerW * 0.4f, 0, InnerW * 0.6f - 20, 76);
            ovY += 76 + 14;
        }

        void OvButtons(params (string label, string sub, Action act, bool primary)[] btns)
        {
            float gap = 14, w = (InnerW - gap * (btns.Length - 1)) / btns.Length;
            for (int i = 0; i < btns.Length; i++)
            {
                var b = btns[i];
                Art.Button(ovContent, b.label, b.sub, i * (w + gap), ovY, w, 96, b.primary ? Art.Butter : Color.white, 30, b.act);
            }
            ovY += 96 + 16;
        }

        // ---------------------------------------------------------------- concrete overlays

        float shopScroll = 1f;
        ScrollRect shopRect;

        void ShowShop()
        {
            BeginOverlay("shop", ShowShop);
            OvText("강화 상점", 46, Art.Ink, 60);
            OvText("거실 방어에서 높은 층을 깰수록 더 많은 강화가 열려요.", 22, Art.InkSoft, 32, FontStyle.Normal);
            OvText($"보유 츄르 <color=#e8505b>{Defs.Fmt(S.churu)}</color>      최고 기록 <color=#e8505b>{S.bestFloor}층</color>", 26, Art.Ink, 38);

            // scrolling list: viewport height fills the screen minus header/footer
            const float rowH = 122, gap = 12, headH = 46;
            float viewH = Mathf.Max(300, colH - 48 - (ovY + Pad * 2) - (96 + 16) - 10);
            var viewport = Art.Rect(ovContent, "Viewport", 0, ovY, InnerW, viewH);
            var vpImg = viewport.gameObject.AddComponent<Image>(); vpImg.color = new Color(0, 0, 0, 0.001f);
            viewport.gameObject.AddComponent<RectMask2D>();
            var list = Art.Rect(viewport, "List", 0, 0, InnerW, 100);
            float y = 0;
            for (int section = 0; section < 2; section++)
            {
                bool puzzle = section == 1;
                Art.Txt(list, puzzle ? "거실 방어" : "은신처", 28, Art.Tomato, TextAnchor.MiddleLeft, 4, y, InnerW, headH);
                y += headH;
                foreach (var up in Defs.Ups)
                {
                    if (up.Puzzle != puzzle) continue;
                    int l = S.Up(up.Id); bool maxed = l >= up.Max;
                    double cost = maxed ? 0 : up.Cost(l);
                    int need = maxed || up.NeedFloor == null ? 0 : up.NeedFloor(l);
                    bool locked = need > S.bestFloor;
                    var row = Art.Card(list, "Row " + up.Name, 0, y, InnerW, rowH, locked ? new Color(0.95f, 0.94f, 0.93f) : Color.white, 0);
                    var rt = row.transform.parent;
                    Art.Txt(rt, up.Name, 28, Art.Ink, TextAnchor.UpperLeft, 18, 12, InnerW - 200, 36);
                    Art.Txt(rt, up.Txt(l) + (maxed ? "" : $"  >  <color=#2b2233>{up.Txt(l + 1)}</color>"), 20, Art.InkSoft, TextAnchor.UpperLeft, 18, 52, InnerW - 210, 30, false, FontStyle.Normal);
                    for (int p = 0; p < up.Max; p++) Art.Card(rt, "Pip", 18 + p * 22, 92, 16, 16, p < l ? Art.Cheese : Color.white, 0);
                    var id = up.Id;
                    string label = maxed ? "최대" : locked ? $"{need}층 돌파 필요" : $"츄르 {Defs.Fmt(cost)}";
                    var b = Art.Button(rt, label, null, InnerW - 178, 32, 162, 58, locked ? new Color(0.88f, 0.86f, 0.84f) : Art.Butter, locked ? 20 : 22, () => Buy(id));
                    b.SetInteractable(!maxed && !locked && S.churu >= cost);
                    if (locked) { b.Root.GetComponent<CanvasGroup>().alpha = 1f; b.Fill.color = new Color(0.88f, 0.86f, 0.84f); b.Label.color = Art.Tomato; }
                    y += rowH + gap;
                }
            }
            list.sizeDelta = new Vector2(InnerW, y);
            shopRect = viewport.gameObject.AddComponent<ScrollRect>();
            shopRect.content = list; shopRect.viewport = viewport;
            shopRect.horizontal = false; shopRect.vertical = true;
            shopRect.movementType = ScrollRect.MovementType.Clamped;
            shopRect.scrollSensitivity = 40;
            shopRect.verticalNormalizedPosition = shopScroll;
            shopRect.onValueChanged.AddListener(v => shopScroll = v.y);
            ovY += viewH + 16;
            OvButtons(("돌아가기", null, () => { sfx.Play("click"); shopScroll = 1f; CloseOverlay(); RefreshUI(); }, false));
            EndOverlay();
        }

        void Buy(Defs.UpId id)
        {
            var up = System.Array.Find(Defs.Ups, x => x.Id == id);
            int l = S.Up(id);
            if (l >= up.Max) return;
            if (up.NeedFloor != null && up.NeedFloor(l) > S.bestFloor) return;   // the button is disabled in these cases
            double cost = up.Cost(l);
            if (S.churu < cost) return;
            S.churu -= cost; S.up[(int)id] = l + 1; sfx.Play("churu");
            if (id == Defs.UpId.Hp && S.hasRun) S.run.hearts++;
            Persist();
            if (id == Defs.UpId.Room) hideoutBuilt = false;
            RefreshUI(); ShowShop();
        }

        void ShowOffline(double sec, double earned)
        {
            BeginOverlay("offline", () => ShowOffline(sec, earned));
            OvText("어서 와요", 22, Art.Tomato, 30);
            OvText("자는 동안 고양이들이 일했어요", 40, Art.Ink, 100);
            OvEarn(Defs.FmtDur(sec) + " 동안", "+" + Defs.Fmt(earned), Art.Butter);
            OvButtons(("츄르 받기", null, () => { sfx.Play("churu"); CloseOverlay(); RefreshUI(); }, true));
            EndOverlay();
        }

        /// <summary>Web: the game was opened in another tab, which now owns the save. This tab stops saving and waits.</summary>
        void ShowOtherTab()
        {
            BeginOverlay("othertab", ShowOtherTab);
            OvText("잠깐만요", 22, Art.Tomato, 30);
            OvText("다른 창에서 게임이 열렸어요", 40, Art.Ink, 56);
            OvText("진행이 서로 덮어써지지 않도록 이 창은 저장을 멈췄어요. 이 창에서 계속하려면 다시 불러오세요.", 24, Art.Ink, 70, FontStyle.Normal);
            OvButtons(("이 창에서 이어하기", null, () => { sfx.Play("click"); SaveStore.ReloadPage(); }, true));
            EndOverlay();
        }

        void ShowRunIntro()
        {
            BeginOverlay("run", ShowRunIntro);
            OvText("턴제 퍼즐", 22, Art.Tomato, 30);
            OvText("거실 방어", 46, Art.Ink, 60);
            OvText("- 내가 한 칸 움직이면 적도 한 칸 움직여요. <b>언제든 멈췄다가 이어서 할 수 있어요.</b>", 23, Art.Ink, 64, FontStyle.Normal);
            OvText("- 적 쪽으로 움직이면 <b>냥펀치</b>로 때리고 한 칸 밀어내요. 벽, 가구, 다른 적에 부딪히면 1 더 아파요.", 23, Art.Ink, 64, FontStyle.Normal);
            OvText("- 머리 위에 <color=#e8505b><b>!</b></color> 가 뜬 적은 다음 턴에 공격해요. 피하거나 밀어내세요.", 23, Art.Ink, 64, FontStyle.Normal);
            OvText("- 층을 깨면 츄르와 <b>선물 상자</b>가 은신처로 배달돼요.", 23, Art.Ink, 36, FontStyle.Normal);
            OvEarn("최고 기록", S.bestFloor > 0 ? S.bestFloor + "층" : "-", Color.white);
            OvButtons(("1층부터 시작", null, () => { sfx.Play("click"); StartRun("거실을 지켜라!"); }, true));
            EndOverlay();
        }

        void ShowClear(FloorReward rw)
        {
            BeginOverlay("clear", () => ShowClear(rw));
            OvText(rw.WasBoss ? "보스층 클리어! 보너스 츄르 +50%" : rw.NewBest ? "새 최고 기록!" : "층 클리어", 22, Art.Tomato, 30);
            OvText($"{rw.Floor}층을 지켰어요!", 46, Art.Ink, 60);
            OvEarn("츄르", "+" + Defs.Fmt(rw.Churu), Art.Butter);
            OvEarn(rw.Lucky ? "행운의 선물! 은신처로 배달" : "은신처로 선물 배달", Defs.BoxName[rw.Gift], Color.white);
            OvButtons(
                ("다음 층", null, () =>
                {
                    sfx.Play("click"); CloseOverlay(); RefreshUI();
                    int f = S.run.floor;
                    ShowBanner($"{f}층", f % 10 == 5 ? "로봇청소기는 일직선이면 돌진! 가구에 박게 하세요" : f % 10 == 0 ? "목욕통은 한 턴씩 쉬어요. 쉴 때 때리세요" : "");
                    if (f % 5 == 0) sfx.Play("boss");
                }, true),
                ("은신처 가기", "이어서 할 수 있어요", () => { sfx.Play("click"); CloseOverlay(); SetView("hide"); }, false));
            EndOverlay();
        }

        void ShowDead(int floor)
        {
            BeginOverlay("dead", () => ShowDead(floor));
            OvText("거실 방어 실패", 22, Art.Tomato, 30);
            OvText("냥펀치가 낮잠에 빠졌어요", 42, Art.Ink, 56);
            OvText($"<b>{floor}층</b>에서 쓰러졌어요. 지금까지 깬 층의 츄르와 선물 상자는 모두 받았어요.\n최고 기록 {S.bestFloor}층, 은신처 생산 +{S.bestFloor * 5}%", 24, Art.Ink, 100, FontStyle.Normal);
            OvButtons(
                ("다시 1층부터", null, () => { sfx.Play("click"); StartRun("다시 한 번!"); }, true),
                ("은신처 가기", null, () => { sfx.Play("click"); CloseOverlay(); SetView("hide"); }, false));
            EndOverlay();
        }
    }
}
