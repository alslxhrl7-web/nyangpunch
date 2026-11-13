using System;
using System.Collections.Generic;

namespace NyangPunch
{
    /// <summary>Visual/audio side effects the turn logic asks the view to play. Coordinates are in grid cells.</summary>
    public interface IPuzzleFx
    {
        void CellText(float cx, float cy, string text, UnityEngine.Color color, int size);
        void CellPuff(float cx, float cy, int count, float scale);
        void Sound(string key);
        void Shake(float amount);
        void Banner(string title, string sub);
        void Swipe(int cx, int cy, int dx, int dy);
        void Ring(int cx, int cy);
        void Yarn(int x0, int y0, int x1, int y1);
        void Lunge(int enemyId);
        void Flash(int enemyId);
        void ChuruGained(double amount);
    }

    public struct FloorReward
    {
        public int Floor, Gift;
        public double Churu;
        public bool WasBoss, NewBest, Lucky;
    }

    /// <summary>Turn-based living-room defense. Pure rules; the view drives timing between player and enemy phases.</summary>
    public class Puzzle
    {
        public const int N = 7;
        static readonly int[][] Dirs4 = { new[] { 1, 0 }, new[] { -1, 0 }, new[] { 0, 1 }, new[] { 0, -1 } };
        static readonly int[][] Dirs8 = { new[] { 1, 0 }, new[] { -1, 0 }, new[] { 0, 1 }, new[] { 0, -1 }, new[] { 1, 1 }, new[] { 1, -1 }, new[] { -1, 1 }, new[] { -1, -1 } };

        readonly SaveData S;
        readonly IPuzzleFx fx;
        readonly Random rng = new Random();

        public Puzzle(SaveData save, IPuzzleFx effects) { S = save; fx = effects; }

        public RunData R => S.run;
        public bool HasRun => S.hasRun;

        int Ri(int n) => rng.Next(n);
        double Rf() => rng.NextDouble();
        static int Idx(int x, int y) => y * N + x;
        public static bool InB(int x, int y) => x >= 0 && y >= 0 && x < N && y < N;
        public EnemyData At(int x, int y)
        {
            var l = R.enemies;   // plain loop: the view calls this every frame, a lambda would allocate each time
            for (int i = 0; i < l.Count; i++) { var e = l[i]; if (e.x == x && e.y == y) return e; }
            return null;
        }
        public bool Obs(int x, int y) => R.obs[Idx(x, y)] != 0;
        static int Cheb(int ax, int ay, int bx, int by) => Math.Max(Math.Abs(ax - bx), Math.Abs(ay - by));
        static int Manh(int ax, int ay, int bx, int by) => Math.Abs(ax - bx) + Math.Abs(ay - by);
        static bool Adj(EnemyData e, int x, int y) => Manh(e.x, e.y, x, y) == 1;   // everyone attacks orthogonally
        public bool YarnOpen => S.maxTier >= Defs.YarnNeed;
        public bool HissOpen => S.maxTier >= Defs.HissNeed;
        public int EnemiesLeft => R.enemies.Count + R.toSpawn + R.marks.Count;
        public ThemeDef Theme => ThemeDef.ForFloor(R.floor);
        public int TileAt(int x, int y) => R.tiles != null && R.tiles.Length == N * N ? R.tiles[Idx(x, y)] : 0;
        /// <summary>Yard rule: the cat hiding in grass cannot be tracked by enemies that are not right next to it.</summary>
        public bool Hidden => S.hasRun && TileAt(R.px, R.py) == Tile.Grass;
        int HissRange => R.cat == (int)Breed.Mackerel ? 2 : 1;
        bool wet;   // the cat splashed into a puddle this turn: skill cooldowns do not tick down

        // Used by the view to disable a skill button when it would do nothing (instead of an error sound).
        public bool YarnHasTarget
        {
            get
            {
                if (!S.hasRun || R.enemies == null) return false;
                foreach (var d in Dirs4)
                    for (int x = R.px + d[0], y = R.py + d[1]; InB(x, y) && !Obs(x, y); x += d[0], y += d[1])
                        if (At(x, y) != null) return true;
                return false;
            }
        }
        // Bosses (Fixed) cannot be pushed, so a hiss with only a boss next to the cat would do nothing.
        public bool HissHasTarget => S.hasRun && R.enemies != null && R.enemies.Exists(HissTarget);
        bool HissTarget(EnemyData e) { int d = Cheb(e.x, e.y, R.px, R.py); return d >= 1 && d <= HissRange && !EDef.Of(e.type).Fixed; }

        int PickType(int f)
        {
            var pool = new List<int> { 0, 0 };
            if (f >= 2) pool.Add(1);
            if (f >= 4) pool.Add(2);
            if (f >= 7) pool.Add(3);
            if (f >= 11) { pool.Add(3); pool.Add(1); }
            return pool[Ri(pool.Count)];
        }

        EnemyData AddEnemy(int type, int x, int y, int hpBonus = 0)
        {
            var d = EDef.Of(type);
            var e = new EnemyData { id = R.nextId++, type = type, x = x, y = y, hp = d.Hp + hpBonus, max = d.Hp + hpBonus, face = 1 };
            R.enemies.Add(e);
            return e;
        }

        bool FreeCell(int minD, bool edge, out int fx_, out int fy_)
        {
            for (int k = 0; k < 300; k++)
            {
                int x = Ri(N), y = Ri(N);
                if (edge) { int s = Ri(4); if (s == 0) y = 0; else if (s == 1) y = N - 1; else if (s == 2) x = 0; else x = N - 1; }
                if (Obs(x, y) || At(x, y) != null || (x == R.px && y == R.py)) continue;
                if (R.marks.Exists(m => m.x == x && m.y == y)) continue;
                if (Cheb(x, y, R.px, R.py) < minD) continue;
                fx_ = x; fy_ = y; return true;
            }
            fx_ = fy_ = 0; return false;
        }

        public void NewRun()
        {
            int cat = S.HasCat(S.pick) ? S.pick : 0;
            S.run = new RunData { floor = 1, hearts = S.MaxHearts, nextId = 1, cat = cat };
            S.hasRun = true;
            GenFloor();
        }

        public void GenFloor()
        {
            int f = R.floor;
            R.turn = 0; R.obs = new int[N * N]; R.tiles = new int[N * N]; R.enemies = new List<EnemyData>(); R.marks = new List<MarkData>();
            R.px = 3; R.py = 3; R.cdYarn = 0; R.cdHiss = 0;   // nextId is not reset: ids stay unique for the whole run
            R.shield = R.cat == (int)Breed.Calico; wet = false;
            // Furniture must never wall off a cell: every free cell has to be reachable from the cat,
            // otherwise an enemy (or the boss) can end up somewhere it can never be hit and the floor never ends.
            int nObs = 3 + (f % 3);
            for (int attempt = 0; ; attempt++)
            {
                Array.Clear(R.obs, 0, R.obs.Length);
                if (attempt >= 60) break;   // give up on furniture rather than risk a stuck floor
                for (int k = 0, placed = 0; placed < nObs && k < 200; k++)
                {
                    int x = Ri(N), y = Ri(N);
                    if (Cheb(x, y, 3, 3) <= 1 || R.obs[Idx(x, y)] != 0) continue;
                    R.obs[Idx(x, y)] = 1 + Ri(2); placed++;
                }
                if (AllFreeConnected()) break;
            }
            // theme floor tiles (never under furniture or the cat's start cell)
            var th = ThemeDef.ForFloor(f);
            if (th.TileKind != Tile.None)
                for (int k = 0, placed = 0; placed < th.TileCount && k < 300; k++)
                {
                    int x = Ri(N), y = Ri(N);
                    if ((x == 3 && y == 3) || R.obs[Idx(x, y)] != 0 || R.tiles[Idx(x, y)] != 0) continue;
                    R.tiles[Idx(x, y)] = th.TileKind; placed++;
                }
            if (f % 5 == 0)
            {
                int type = f % 10 == 5 ? (int)EType.Vacuum : (int)EType.Tub;
                if (!FreeCell(3, false, out int bx, out int by)) { bx = 0; by = 0; }
                AddEnemy(type, bx, by, (f - 5) / 10 * 3);
                R.toSpawn = Defs.BossAdds(f);
                R.boss = true;
            }
            else
            {
                int total = Defs.FloorEnemies(f);
                int initial = Defs.FloorInitial(f, total);
                for (int i = 0; i < initial; i++) if (FreeCell(3, false, out int x, out int y)) AddEnemy(PickType(f), x, y);
                R.toSpawn = total - initial;
                R.boss = false;
            }
        }

        void Damage(EnemyData e, int n, string why = null)
        {
            e.hp -= n;
            fx.Flash(e.id);
            fx.CellText(e.x + 0.5f + (float)(Rf() - 0.5) * 0.2f, e.y + 0.05f, why != null ? $"{why} -{n}" : $"-{n}", why != null ? Art.Butter : UnityEngine.Color.white, why != null ? 26 : 32);
            if (e.hp <= 0)
            {
                R.enemies.Remove(e); R.kills++;
                bool boss = EDef.Of(e.type).Boss;
                fx.CellPuff(e.x + 0.5f, e.y + 0.5f, boss ? 22 : 7, boss ? 1.6f : 1f);
                double gain = (1 + S.Up(Defs.UpId.Hunter)) * S.ChuruMul;
                S.churu += gain; fx.ChuruGained(gain);
                fx.CellText(e.x + 0.5f, e.y + 0.4f, "+" + (gain == Math.Floor(gain) ? gain.ToString("0") : gain.ToString("0.0")), Art.Pink, 24);
                if (boss) { fx.Banner(EDef.Of(e.type).Name + " 처치!", ""); fx.Sound("kill_boss"); fx.Shake(10); }
                else fx.Sound(KillSound(e.type));
            }
        }

        /// <summary>A plant pot (obs 2) shatters and disappears when something slams into it. Cushions and walls stay.</summary>
        bool BreakPot(int x, int y)
        {
            if (!InB(x, y) || R.obs[Idx(x, y)] != 2) return false;
            R.obs[Idx(x, y)] = 0;
            fx.CellPuff(x + 0.5f, y + 0.5f, 10, 1.2f);
            fx.CellText(x + 0.5f, y + 0.3f, "와장창!", Art.Butter, 26);
            fx.Sound("bump_pot");
            return true;
        }

        /// <summary>Each enemy kind goes out with its own sound; kinds without a recording fall back to "pop" (see Sfx.Fallback).</summary>
        static string KillSound(int type)
        {
            switch ((EType)type)
            {
                case EType.Mouse: case EType.Rat: return "squeak";
                case EType.Fly: return "kill_fly";
                case EType.Cucumber: return "kill_cucumber";
                default: return "pop";
            }
        }

        void Push(EnemyData e, int dx, int dy, int depth = 0)
        {
            if (!R.enemies.Contains(e) || EDef.Of(e.type).Fixed) return;
            int nx = e.x + dx, ny = e.y + dy;
            if (!InB(nx, ny) || Obs(nx, ny)) { Damage(e, 1, "쿵"); if (!BreakPot(nx, ny)) fx.Sound("bump"); fx.Shake(4); return; }
            var o = At(nx, ny);
            if (o != null) { Damage(e, 1, "쿵"); Damage(o, 1, "쿵"); fx.Sound("bump"); fx.Shake(4); return; }
            if (nx == R.px && ny == R.py) return;
            e.x = nx; e.y = ny;
            int t = TileAt(nx, ny);
            // kitchen: a slick tile sends the pushed enemy one more cell (and into the wall if that is where it leads)
            if (t == Tile.Slick && depth < N) { fx.CellText(nx + 0.5f, ny + 0.2f, "주르륵", Art.Butter, 22); Push(e, dx, dy, depth + 1); return; }
            if (t == Tile.Puddle) Soak(e);
        }

        /// <summary>Bathroom: an enemy that steps or is pushed into a puddle loses its next turn.</summary>
        void Soak(EnemyData e)
        {
            if (EDef.Of(e.type).Boss || e.stun > 0) return;
            e.stun = 1; e.threat = false;
            fx.CellText(e.x + 0.5f, e.y + 0.2f, "첨벙", new UnityEngine.Color(0.55f, 0.8f, 1f), 22);
        }

        /// <summary>After the cat steps onto a cell: kitchen tiles slide it one more cell, bathroom puddles splash.</summary>
        void CatLanded(int dx, int dy)
        {
            int t = TileAt(R.px, R.py);
            if (t == Tile.Slick)
            {
                int nx = R.px + dx, ny = R.py + dy;
                if (InB(nx, ny) && !Obs(nx, ny) && At(nx, ny) == null)
                {
                    R.px = nx; R.py = ny;
                    fx.CellText(nx + 0.5f, ny + 0.1f, "주르륵", Art.Butter, 22);
                    if (TileAt(nx, ny) == Tile.Puddle) Splash();
                }
            }
            else if (t == Tile.Puddle) Splash();
        }

        void Splash()
        {
            R.tiles[Idx(R.px, R.py)] = Tile.None;
            wet = true;
            fx.CellPuff(R.px + 0.5f, R.py + 0.6f, 8, 0.8f);
            fx.CellText(R.px + 0.5f, R.py + 0.1f, "첨벙! 부르르", new UnityEngine.Color(0.55f, 0.8f, 1f), 24);
            fx.Sound("pop");
        }

        public enum Kind { Dir, Yarn, Hiss, Wait }

        /// <summary>Player action. Returns false when it was not a legal move (no turn spent).</summary>
        public bool Act(Kind kind, int dx = 0, int dy = 0)
        {
            // a direction of (0,0) would be a free "move" in place, and the yarn ball would never leave the cat's cell (endless loop)
            if ((kind == Kind.Dir || kind == Kind.Yarn) && dx == 0 && dy == 0) return false;
            if (kind == Kind.Dir)
            {
                int tx = R.px + dx, ty = R.py + dy;
                if (!InB(tx, ty) || Obs(tx, ty)) return false;   // blocked: the view shows the cat nudging the wall, no error sound
                if (dx != 0) R.face = dx;
                var e = At(tx, ty);
                if (e != null)
                {
                    fx.Swipe(tx, ty, dx, dy); fx.Sound("punch");
                    Damage(e, 1 + S.Up(Defs.UpId.Claw)); Push(e, dx, dy);
                }
                else { R.px = tx; R.py = ty; fx.Sound("step"); CatLanded(dx, dy); }
            }
            else if (kind == Kind.Yarn)
            {
                if (!YarnOpen || R.cdYarn > 0) return false;
                int x = R.px + dx, y = R.py + dy; EnemyData hit = null;
                while (InB(x, y) && !Obs(x, y)) { hit = At(x, y); if (hit != null) break; x += dx; y += dy; }
                if (hit == null) { fx.Banner("그 줄에는 적이 없어요", ""); return false; }
                if (dx != 0) R.face = dx;
                fx.Yarn(R.px, R.py, hit.x, hit.y); fx.Sound("throw");
                var behind = R.cat == (int)Breed.Cheese ? At(hit.x + dx, hit.y + dy) : null;   // cheese: the yarn goes one cell further
                Damage(hit, 1);
                if (behind != null && R.enemies.Contains(behind)) Damage(behind, 1, "관통");
                Push(hit, dx, dy);
                R.cdYarn = S.YarnCd;
            }
            else if (kind == Kind.Hiss)
            {
                if (!HissOpen || R.cdHiss > 0) return false;
                var near = R.enemies.FindAll(HissTarget);
                if (near.Count == 0) return false;
                fx.Sound("hiss"); fx.Ring(R.px, R.py);
                foreach (var e in near) Push(e, Math.Sign(e.x - R.px), Math.Sign(e.y - R.py));
                R.cdHiss = S.HissCd;
            }
            else fx.Sound("click");
            R.turn++;
            return true;
        }

        int[] Bfs(bool diag)
        {
            var dist = new int[N * N];
            for (int i = 0; i < dist.Length; i++) dist[i] = int.MaxValue;
            var q = new Queue<int>();
            dist[Idx(R.px, R.py)] = 0; q.Enqueue(Idx(R.px, R.py));
            var dirs = diag ? Dirs8 : Dirs4;
            while (q.Count > 0)
            {
                int c = q.Dequeue(), x = c % N, y = c / N, d = dist[c];
                foreach (var dd in dirs)
                {
                    int nx = x + dd[0], ny = y + dd[1];
                    if (!InB(nx, ny) || Obs(nx, ny) || dist[Idx(nx, ny)] <= d + 1) continue;
                    dist[Idx(nx, ny)] = d + 1; q.Enqueue(Idx(nx, ny));
                }
            }
            return dist;
        }

        /// <summary>True when every non-furniture cell is 4-connected to the cat's start cell.</summary>
        public bool AllFreeConnected()
        {
            var seen = new bool[N * N]; var q = new Queue<int>();
            seen[Idx(3, 3)] = true; q.Enqueue(Idx(3, 3)); int reach = 1, free = 0;
            for (int i = 0; i < N * N; i++) if (R.obs[i] == 0) free++;
            while (q.Count > 0)
            {
                int c = q.Dequeue(), x = c % N, y = c / N;
                foreach (var dd in Dirs4)
                {
                    int nx = x + dd[0], ny = y + dd[1];
                    if (!InB(nx, ny) || Obs(nx, ny) || seen[Idx(nx, ny)]) continue;
                    seen[Idx(nx, ny)] = true; reach++; q.Enqueue(Idx(nx, ny));
                }
            }
            return reach == free;
        }

        /// <summary>Safety net for runs saved before the connectivity rule: an enemy the cat can never reach
        /// clears one piece of furniture next to it (the robot vacuum sucks it up), opening a path.</summary>
        int[] UnstickEnemies(int[] d4)
        {
            foreach (var e in new List<EnemyData>(R.enemies))
            {
                if (d4[Idx(e.x, e.y)] != int.MaxValue) continue;
                // Look at every piece of furniture around the enemy's whole closed-off pocket (not only the cells
                // next to the enemy: it may sit deep inside a pocket of several cells). Pick the piece whose other
                // side is closest to the cat; the flood starts at the enemy, so on a tie the nearest piece wins.
                int ox = -1, oy = -1, best = int.MaxValue;
                var seen = new bool[N * N]; var q = new Queue<int>();
                seen[Idx(e.x, e.y)] = true; q.Enqueue(Idx(e.x, e.y));
                while (q.Count > 0)
                {
                    int c = q.Dequeue(), cx = c % N, cy = c / N;
                    foreach (var dd in Dirs4)
                    {
                        int x = cx + dd[0], y = cy + dd[1];
                        if (!InB(x, y) || seen[Idx(x, y)]) continue;
                        seen[Idx(x, y)] = true;
                        if (!Obs(x, y)) { q.Enqueue(Idx(x, y)); continue; }
                        int near = int.MaxValue;   // how close the far side of this furniture is to the cat
                        foreach (var d2 in Dirs4)
                        {
                            int nx = x + d2[0], ny = y + d2[1];
                            if (InB(nx, ny) && !Obs(nx, ny)) near = Math.Min(near, d4[Idx(nx, ny)]);
                        }
                        if (near < best || ox < 0) { best = near; ox = x; oy = y; }
                    }
                }
                if (ox < 0) continue;
                R.obs[Idx(ox, oy)] = 0;
                bool vac = e.type == (int)EType.Vacuum;
                fx.CellPuff(ox + 0.5f, oy + 0.5f, 10, 1.2f);
                fx.CellText(ox + 0.5f, oy + 0.3f, vac ? "흡입!" : "와장창!", Art.Butter, 28);
                fx.Sound("bump"); fx.Shake(6);
                d4 = Bfs(false);
            }
            return d4;
        }

        bool LineClear(int ax, int ay, int bx, int by)
        {
            int dx = Math.Sign(bx - ax), dy = Math.Sign(by - ay), x = ax + dx, y = ay + dy;
            while (x != bx || y != by) { if (Obs(x, y)) return false; x += dx; y += dy; }
            return true;
        }

        // Robot vacuum: lines up with the cat, aims for one turn, then charges straight. Hitting a wall or furniture stuns it.
        int VacuumTurn(EnemyData e, int[] d4)
        {
            if (e.stun > 0) { e.stun--; return 0; }
            if (e.charging)
            {
                int dx = e.cdx, dy = e.cdy; e.charging = false;
                if (dx != 0) e.face = dx;
                while (true)
                {
                    int nx = e.x + dx, ny = e.y + dy;
                    if (nx == R.px && ny == R.py) { fx.Lunge(e.id); fx.Shake(10); return EDef.Of(e.type).Dmg; }
                    if (!InB(nx, ny) || Obs(nx, ny)) { Damage(e, 2, "쾅"); if (R.enemies.Contains(e)) e.stun = 2; if (!BreakPot(nx, ny)) fx.Sound("bump"); fx.Shake(10); return 0; }
                    var o = At(nx, ny);
                    if (o != null) { Damage(o, 2, "쾅"); fx.Sound("bump"); return 0; }
                    e.x = nx; e.y = ny;
                }
            }
            if ((e.x == R.px || e.y == R.py) && LineClear(e.x, e.y, R.px, R.py))
            {
                e.charging = true; e.cdx = Math.Sign(R.px - e.x); e.cdy = Math.Sign(R.py - e.y); fx.Sound("boss"); return 0;
            }
            int cur = d4[Idx(e.x, e.y)];
            int bx = -1, by = -1; double best = double.MaxValue;
            foreach (var dd in Dirs4)
            {
                int nx = e.x + dd[0], ny = e.y + dd[1];
                if (!InB(nx, ny) || Obs(nx, ny) || At(nx, ny) != null || (nx == R.px && ny == R.py)) continue;
                int nd = d4[Idx(nx, ny)];
                double score = (nd == int.MaxValue ? 99 : nd) * 10 + ((nx == R.px || ny == R.py) ? -15 : 0) + Rf();
                if (score < best) { best = score; bx = nx; by = ny; }
            }
            if (bx >= 0 && (cur == int.MaxValue || best < cur * 10 + 5)) { if (bx != e.x) e.face = Math.Sign(bx - e.x); e.x = bx; e.y = by; }
            return 0;
        }

        /// <summary>Enemy phase. Returns hearts lost this phase (already subtracted).</summary>
        public int EnemyPhase()
        {
            var d4 = UnstickEnemies(Bfs(false)); var d8 = Bfs(true);
            bool hidden = Hidden;
            var order = new List<EnemyData>(R.enemies);
            order.Sort((a, b) => Manh(a.x, a.y, R.px, R.py).CompareTo(Manh(b.x, b.y, R.px, R.py)));
            int hurt = 0;
            foreach (var e in order)
            {
                if (!R.enemies.Contains(e)) continue;
                var d = EDef.Of(e.type);
                if (d.Slow) { if (e.rest) { e.rest = false; continue; } e.rest = true; }
                if (e.type == (int)EType.Vacuum) { hurt += VacuumTurn(e, d4); continue; }
                if (e.stun > 0) { e.stun--; continue; }   // soaked by a puddle
                if (e.threat && Adj(e, R.px, R.py)) { hurt += d.Dmg; fx.Lunge(e.id); continue; }
                var dist = d.Diag ? d8 : d4;
                var dirs = d.Diag ? Dirs8 : Dirs4;
                if (Adj(e, R.px, R.py)) continue;
                if (hidden && !d.Boss)
                {
                    // yard: the cat is hiding in the grass, so the enemy wanders instead of chasing (sometimes stays put)
                    if (Ri(3) == 0) continue;
                    var dd0 = dirs[Ri(dirs.Length)];
                    int wx = e.x + dd0[0], wy = e.y + dd0[1];
                    if (!InB(wx, wy) || Obs(wx, wy) || At(wx, wy) != null || (wx == R.px && wy == R.py)) continue;
                    if (wx != e.x) e.face = Math.Sign(wx - e.x);
                    e.x = wx; e.y = wy;
                    if (TileAt(wx, wy) == Tile.Puddle) Soak(e);
                    continue;
                }
                int cur = dist[Idx(e.x, e.y)];
                int bx = -1, by = -1; double best = double.MaxValue;
                foreach (var dd in dirs)
                {
                    int nx = e.x + dd[0], ny = e.y + dd[1];
                    if (!InB(nx, ny) || Obs(nx, ny) || At(nx, ny) != null || (nx == R.px && ny == R.py)) continue;
                    int nd = dist[Idx(nx, ny)];
                    double score = (nd == int.MaxValue ? 99 : nd) * 10 + Manh(nx, ny, R.px, R.py) + Rf() * 0.5;
                    bool better = cur != int.MaxValue
                        ? (nd < cur || (d.Diag && nd <= cur && Manh(nx, ny, R.px, R.py) < Manh(e.x, e.y, R.px, R.py)))
                        : true;
                    if (better && score < best) { best = score; bx = nx; by = ny; }
                }
                if (bx < 0) continue;
                if (bx != e.x) e.face = Math.Sign(bx - e.x);
                e.x = bx; e.y = by;
                if (TileAt(bx, by) == Tile.Puddle) Soak(e);
            }
            // bathtub boss releases flies
            foreach (var e in new List<EnemyData>(R.enemies))
            {
                if (e.type != (int)EType.Tub || R.turn % 4 != 0 || R.enemies.Count >= 4) continue;
                foreach (var dd in Dirs4)
                {
                    int x = e.x + dd[0], y = e.y + dd[1];
                    if (InB(x, y) && !Obs(x, y) && At(x, y) == null && !(x == R.px && y == R.py)) { AddEnemy((int)EType.Fly, x, y); fx.Sound("pop"); break; }
                }
            }
            // spawn markers
            foreach (var m in R.marks) m.t--;
            for (int i = R.marks.Count - 1; i >= 0; i--)
            {
                var m = R.marks[i];
                if (m.t > 0) continue;
                if (At(m.x, m.y) != null || (m.x == R.px && m.y == R.py))
                {
                    // the cell is taken (the cat or an enemy stands on it): move the warning to another free cell and
                    // show it for one more turn, so standing on a "?" can never hold a floor open
                    if (FreeCell(2, true, out int nx, out int ny) || FreeCell(1, false, out nx, out ny)) { m.x = nx; m.y = ny; m.t = 1; }
                    continue;
                }
                AddEnemy(m.type, m.x, m.y); R.marks.RemoveAt(i);
            }
            int want = R.floor >= 8 && !R.boss ? 2 : 1;
            if (R.toSpawn > 0 && (R.turn % 2 == 1 || (R.enemies.Count == 0 && R.marks.Count == 0)))
            {
                for (int k = 0; k < want && R.toSpawn > 0; k++)
                {
                    if (!FreeCell(2, true, out int x, out int y)) break;
                    int type = R.boss ? (Rf() < 0.5 ? (int)EType.Mouse : (int)EType.Fly) : PickType(R.floor);
                    R.marks.Add(new MarkData { x = x, y = y, t = 1, type = type });
                    R.toSpawn--;
                }
            }
            foreach (var e in R.enemies) e.threat = e.type != (int)EType.Vacuum && !e.rest && e.stun <= 0 && Adj(e, R.px, R.py);
            if (wet) { R.cdYarn = Math.Max(1, R.cdYarn); R.cdHiss = Math.Max(1, R.cdHiss); wet = false; }   // shaking off the water: cooldowns wait a turn
            else { R.cdYarn = Math.Max(0, R.cdYarn - 1); R.cdHiss = Math.Max(0, R.cdHiss - 1); }
            if (hurt > 0 && R.shield)
            {
                // calico: the first hit of the floor is shrugged off
                R.shield = false; hurt = 0;
                fx.CellText(R.px + 0.5f, R.py - 0.1f, "행운의 털!", Art.Butter, 30);
                fx.Sound("bump"); fx.Ring(R.px, R.py); fx.Shake(4);
            }
            if (hurt > 0)
            {
                R.hearts = Math.Max(0, R.hearts - hurt);
                fx.Sound("hurt"); fx.Shake(8);
                fx.CellText(R.px + 0.5f, R.py - 0.1f, $"-{hurt}♥", Art.Tomato, 34);
                if (R.hearts <= 0 && !R.reviveUsed && S.Up(Defs.UpId.Revive) > 0)
                {
                    R.reviveUsed = true; R.hearts = 1;
                    foreach (var e in R.enemies) e.threat = false;
                    fx.Banner("아홉 번째 목숨!", "하트 1개로 다시 일어났어요"); fx.Sound("level"); fx.Ring(R.px, R.py);
                }
            }
            return hurt;
        }

        public bool FloorDone => R.enemies.Count == 0 && R.toSpawn <= 0 && R.marks.Count == 0;

        /// <summary>Grants the floor reward, advances to the next floor and generates it.</summary>
        public FloorReward ClaimFloor()
        {
            var rw = new FloorReward { Floor = R.floor, WasBoss = R.boss };
            rw.Churu = Math.Round(Defs.FloorChuru(R.floor, R.boss) * (1 + S.Up(Defs.UpId.Loot) * 0.3) * S.ChuruMul);
            rw.Gift = Math.Min(6, 1 + R.floor / 4 + (R.boss ? 1 : 0));
            if (Rf() < S.Up(Defs.UpId.Lucky) * 0.15) { rw.Gift = Math.Min(7, rw.Gift + 1); rw.Lucky = true; }
            S.churu += rw.Churu; S.total += rw.Churu; S.gifts.Add(rw.Gift);
            rw.NewBest = R.floor > S.bestFloor; S.bestFloor = Math.Max(S.bestFloor, R.floor);
            // every floor starts with full hearts (the old boss "+1 heart" became the boss churu bonus)
            R.floor++; GenFloor();
            R.hearts = S.MaxHearts;
            return rw;
        }

        public void EndRun() { S.hasRun = false; S.run = new RunData(); }
    }
}
