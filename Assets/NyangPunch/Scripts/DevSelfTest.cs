#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace NyangPunch
{
    /// <summary>
    /// Editor-only pre-release check: a bot plays the living-room defense from floor 1 through the floor-10 boss,
    /// several times, on a fresh save each time, and looks for bugs (stuck floors, broken board state, exceptions,
    /// sound keys with nothing to play). The bot's hearts are topped up every turn, so this finds bugs, not difficulty.
    /// Use (play mode, so sounds can be resolved): NyangPunch.DevSelfTest.Run(10, 10)
    /// </summary>
    public static class DevSelfTest
    {
        class Fx : IPuzzleFx
        {
            public readonly HashSet<string> Sounds = new HashSet<string>();
            public void CellText(float cx, float cy, string text, Color color, int size) { }
            public void CellPuff(float cx, float cy, int count, float scale) { }
            public void Sound(string key) { Sounds.Add(key); }
            public void Shake(float amount) { }
            public void Banner(string title, string sub) { }
            public void Swipe(int cx, int cy, int dx, int dy) { }
            public void Ring(int cx, int cy) { }
            public void Yarn(int x0, int y0, int x1, int y1) { }
            public void Lunge(int enemyId) { }
            public void Flash(int enemyId) { }
            public void ChuruGained(double amount) { }
        }

        static readonly int[][] D = { new[] { 1, 0 }, new[] { -1, 0 }, new[] { 0, 1 }, new[] { 0, -1 } };
        const int N = Puzzle.N, TurnCap = 400;
        // keys the view plays itself (Game*.cs sfx.Play), plus the revive sound the bot never triggers with 99 hearts
        static readonly string[] ViewSounds = { "click", "clear", "level", "churu", "merge", "drop", "boss" };

        public static string Run(int runs = 10, int lastFloor = 10)
        {
            var problems = new List<string>(); var sounds = new HashSet<string>(); var sb = new StringBuilder();
            int cleared = 0, pots = 0; long turnsAll = 0, hurtAll = 0;
            for (int run = 1; run <= runs; run++)
            {
                var S = SaveData.Fresh(); S.maxTier = Defs.HissNeed;   // both skills available so they get exercised
                var fx = new Fx(); var P = new Puzzle(S, fx);
                int turns = 0, hurtTotal = 0, maxFloorTurns = 0; string fail = null;
                try
                {
                    P.NewRun();
                    while (S.run.floor <= lastFloor && fail == null)
                    {
                        int floor = S.run.floor, floorTurns = 0;
                        if (!P.AllFreeConnected()) fail = $"{floor}층: 가구가 빈칸을 막음";
                        // the game saves the run as JSON every couple of seconds: a floor must survive a save/load unchanged
                        string json = JsonUtility.ToJson(S);
                        if (fail == null && JsonUtility.ToJson(JsonUtility.FromJson<SaveData>(json)) != json) fail = $"{floor}층: 저장했다 불러오면 판이 달라짐";
                        while (fail == null)
                        {
                            var r = S.run; r.hearts = 99;
                            int potsBefore = CountObs(r, 2);
                            Step(P, r);
                            turns++; floorTurns++;
                            if (!P.FloorDone) hurtTotal += P.EnemyPhase();
                            pots += potsBefore - CountObs(r, 2);
                            fail = Check(r, floor);
                            if (fail == null && !P.AllFreeConnected()) fail = $"{floor}층: 판 도중에 빈칸이 갈라짐";
                            if (fail != null) break;
                            if (P.FloorDone)
                            {
                                var rw = P.ClaimFloor();
                                if (rw.Churu <= 0 || rw.Gift < 1) fail = $"{floor}층: 보상이 이상함(츄르 {rw.Churu}, 선물 {rw.Gift})";
                                break;
                            }
                            if (floorTurns >= TurnCap) fail = $"{floor}층: {TurnCap}턴 안에 끝나지 않음(막힘), 남은 적 {P.EnemiesLeft}, {Dump(r)}";
                        }
                        maxFloorTurns = Math.Max(maxFloorTurns, floorTurns);
                    }
                }
                catch (Exception e) { fail = $"{S.run.floor}층: 예외 {e.GetType().Name}: {e.Message}"; }
                foreach (var k in fx.Sounds) sounds.Add(k);
                turnsAll += turns; hurtAll += hurtTotal;
                if (fail == null) cleared++; else problems.Add($"{run}번째 판 - {fail}");
                sb.Append($"[{run}] {(fail == null ? "통과" : "실패")} {turns}턴, 가장 긴 층 {maxFloorTurns}턴, 받은 피해 {hurtTotal}\n");
            }
            // every sound the rules asked for must resolve to something playable
            var game = UnityEngine.Object.FindAnyObjectByType<Game>();
            if (game != null)
            {
                var sfx = (Sfx)typeof(Game).GetField("sfx", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(game);
                foreach (var k in sounds) if (!sfx.Has(k)) problems.Add($"소리 '{k}'에 재생할 것이 없음");
                foreach (var k in ViewSounds) if (!sfx.Has(k)) problems.Add($"화면 쪽 소리 '{k}'에 재생할 것이 없음");
            }
            else problems.Add("플레이 모드가 아니라 소리 확인을 건너뜀");
            var list = new List<string>(sounds); list.Sort();
            return $"결과: {cleared}/{runs}판 통과 (1층~{lastFloor}층 보스), 평균 {turnsAll / Math.Max(1, runs)}턴, 평균 받은 피해 {hurtAll / Math.Max(1, runs)}, 깨진 화분 {pots}개\n"
                + $"문제 {problems.Count}건" + (problems.Count > 0 ? ":\n - " + string.Join("\n - ", problems) : "") + "\n"
                + $"쓰인 소리: {string.Join(", ", list)}\n" + sb;
        }

        static int CountObs(RunData r, int kind) { int n = 0; foreach (var o in r.obs) if (o == kind) n++; return n; }
        static string Dump(RunData r)
        {
            var sb = new StringBuilder($"고양이({r.px},{r.py}) 적:");
            foreach (var e in r.enemies) sb.Append($" t{e.type}({e.x},{e.y})hp{e.hp}");
            return sb.ToString();
        }

        /// <summary>Board invariants after every turn.</summary>
        static string Check(RunData r, int floor)
        {
            var seen = new HashSet<int>(); var ids = new HashSet<int>();
            if (r.toSpawn < 0) return $"{floor}층: 남은 소환 수가 음수 ({r.toSpawn})";
            var markCells = new HashSet<int>();
            foreach (var m in r.marks)
            {
                if (!Puzzle.InB(m.x, m.y) || r.obs[m.y * N + m.x] != 0) return $"{floor}층: 소환 표시가 판 밖이나 가구 위에 있음 ({m.x},{m.y})";
                if (!markCells.Add(m.y * N + m.x)) return $"{floor}층: 소환 표시 둘이 한 칸에 겹침 ({m.x},{m.y})";
            }
            foreach (var e in r.enemies)
            {
                if (!ids.Add(e.id)) return $"{floor}층: 적 번호가 겹침 ({e.id})";
                if (e.hp > e.max) return $"{floor}층: 적 체력이 최대보다 큼 ({e.hp}/{e.max})";
                if (!Puzzle.InB(e.x, e.y)) return $"{floor}층: 적이 판 밖에 있음 ({e.x},{e.y})";
                if (r.obs[e.y * N + e.x] != 0) return $"{floor}층: 적이 가구 위에 있음 ({e.x},{e.y})";
                if (e.x == r.px && e.y == r.py) return $"{floor}층: 적이 고양이와 겹침 ({e.x},{e.y})";
                if (!seen.Add(e.y * N + e.x)) return $"{floor}층: 적 둘이 한 칸에 겹침 ({e.x},{e.y})";
                if (e.hp <= 0) return $"{floor}층: 체력 0인 적이 남아 있음";
            }
            if (!Puzzle.InB(r.px, r.py) || r.obs[r.py * N + r.px] != 0) return $"{floor}층: 고양이 위치가 이상함 ({r.px},{r.py})";
            return null;
        }

        /// <summary>One bot action: hiss when surrounded, yarn a distant enemy in line, punch a neighbour, else walk toward the nearest enemy.</summary>
        static void Step(Puzzle P, RunData r)
        {
            int adj = 0; EnemyData next = null;
            foreach (var e in r.enemies) if (Math.Abs(e.x - r.px) + Math.Abs(e.y - r.py) == 1) { adj++; next = e; }
            if (adj >= 2 && P.HissOpen && r.cdHiss <= 0 && P.Act(Puzzle.Kind.Hiss)) return;
            if (next != null && P.Act(Puzzle.Kind.Dir, next.x - r.px, next.y - r.py)) return;
            if (P.YarnOpen && r.cdYarn <= 0)
                foreach (var d in D)
                    for (int x = r.px + d[0], y = r.py + d[1]; Puzzle.InB(x, y) && !P.Obs(x, y); x += d[0], y += d[1])
                        if (P.At(x, y) != null) { if (P.Act(Puzzle.Kind.Yarn, d[0], d[1])) return; break; }
            if (r.enemies.Count > 0)
            {
                var dist = new int[N * N]; for (int i = 0; i < dist.Length; i++) dist[i] = 999; var q = new Queue<int>();
                foreach (var e in r.enemies) foreach (var d in D) { int x = e.x + d[0], y = e.y + d[1]; if (Puzzle.InB(x, y) && !P.Obs(x, y) && P.At(x, y) == null && dist[y * N + x] > 0) { dist[y * N + x] = 0; q.Enqueue(y * N + x); } }
                while (q.Count > 0) { int c = q.Dequeue(); foreach (var d in D) { int x = c % N + d[0], y = c / N + d[1]; if (!Puzzle.InB(x, y) || P.Obs(x, y) || P.At(x, y) != null || dist[y * N + x] <= dist[c] + 1) continue; dist[y * N + x] = dist[c] + 1; q.Enqueue(y * N + x); } }
                int best = 999, bx = 0, by = 0;
                foreach (var d in D) { int x = r.px + d[0], y = r.py + d[1]; if (Puzzle.InB(x, y) && !P.Obs(x, y) && P.At(x, y) == null && dist[y * N + x] < best) { best = dist[y * N + x]; bx = d[0]; by = d[1]; } }
                if (best < 999 && P.Act(Puzzle.Kind.Dir, bx, by)) return;
            }
            P.Act(Puzzle.Kind.Wait);
        }
    }
}
#endif
