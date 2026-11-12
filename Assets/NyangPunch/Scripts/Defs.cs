using System;
using System.Collections.Generic;

namespace NyangPunch
{
    /// <summary>Static game data: boxes, skills, upgrades, enemies. Mirrors the HTML prototype values.</summary>
    public static class Defs
    {
        public const int Cols = 4, BaseRows = 5, MaxRows = 7, BaseStockCap = 5, MaxTier = 8;
        public const double OfflineCapSeconds = 8 * 3600;
        public const string SaveKey = "nyangpunch.idle.v1";

        /// <summary>Bump whenever balance numbers change. In the editor and development builds a save made with another
        /// version starts over, so every test plays the current balance from the beginning. Released builds keep the
        /// player's save (see Game.Load).</summary>
        public const int BalanceVersion = 2;

        public static readonly string[] BoxName = { "", "작은 상자", "택배 상자", "큰 박스", "박스 집", "박스 2층집", "박스 성", "박스 타워", "박스 궁전" };
        // Halved from the HTML prototype so the living-room defense is the main churu source.
        public static readonly double[] BoxProd = { 0, 0.05, 0.15, 0.45, 1.35, 4, 12, 36, 110 };

        public const int YarnNeed = 3, YarnBaseCd = 3;
        public const int HissNeed = 4, HissBaseCd = 5;

        public static int DelivTime(int level) => 20 - level * 2;

        /// <summary>Churu for clearing a floor before upgrades (about 1.5x the prototype).</summary>
        public static double FloorChuru(int floor, bool boss) => (15 + floor * 8) * (boss ? 3 : 1);

        // Indices are stored in the save file: only ever append.
        public enum UpId { Deliv = 0, Quality = 1, Room = 2, Hp = 3, Claw = 4, StockCap = 5, Hunter = 6, Loot = 7, YarnCd = 8, HissCd = 9, Revive = 10, Lucky = 11 }

        public class Upgrade
        {
            public UpId Id;
            public string Name;
            public bool Puzzle;                 // shown under the living-room defense section
            public int Max;
            public Func<int, double> Cost;
            public Func<int, string> Txt;
            public Func<int, int> NeedFloor;    // best floor required to buy level l+1 (0 = none)
        }

        static Func<int, double> Table(params double[] v) => l => v[Math.Min(l, v.Length - 1)];
        static Func<int, int> Need(params int[] v) => l => v[Math.Min(l, v.Length - 1)];

        public static readonly Upgrade[] Ups =
        {
            // ---- 은신처
            new Upgrade { Id = UpId.Deliv, Name = "택배 기사 친구", Max = 5, Cost = l => Math.Round(15 * Math.Pow(2.1, l)), Txt = l => $"{DelivTime(l)}초마다 충전", NeedFloor = Need(0, 0, 3, 6, 10) },
            new Upgrade { Id = UpId.Quality, Name = "고급 택배", Max = 5, Cost = l => Math.Round(150 * Math.Pow(2.6, l)), Txt = l => $"2단계 상자 {l * 10}%", NeedFloor = Need(0, 2, 4, 7, 10) },
            new Upgrade { Id = UpId.StockCap, Name = "큰 택배함", Max = 3, Cost = Table(80, 400, 1600), Txt = l => $"택배 최대 {BaseStockCap + l}개", NeedFloor = Need(0, 2, 6) },
            new Upgrade { Id = UpId.Room, Name = "은신처 확장", Max = 2, Cost = Table(300, 2500), Txt = l => $"상자 칸 {Cols * (BaseRows + l)}개", NeedFloor = Need(0, 5) },
            // ---- 거실 방어
            new Upgrade { Id = UpId.Hp, Puzzle = true, Name = "튼튼한 뱃살", Max = 3, Cost = Table(60, 300, 1500), Txt = l => $"시작 하트 {3 + l}개", NeedFloor = Need(0, 0, 4) },
            new Upgrade { Id = UpId.Claw, Puzzle = true, Name = "날카로운 발톱", Max = 2, Cost = Table(250, 2500), Txt = l => $"펀치 피해 {1 + l}", NeedFloor = Need(0, 6) },
            new Upgrade { Id = UpId.Hunter, Puzzle = true, Name = "츄르 사냥꾼", Max = 3, Cost = Table(50, 250, 1200), Txt = l => $"처치 츄르 {1 + l}개" },
            new Upgrade { Id = UpId.Loot, Puzzle = true, Name = "전리품 가방", Max = 3, Cost = Table(100, 500, 2500), Txt = l => $"층 보상 +{l * 30}%", NeedFloor = Need(0, 3, 7) },
            new Upgrade { Id = UpId.Lucky, Puzzle = true, Name = "행운의 선물", Max = 3, Cost = Table(200, 1000, 4000), Txt = l => $"큰 선물 확률 {l * 15}%", NeedFloor = Need(2, 5, 9) },
            new Upgrade { Id = UpId.YarnCd, Puzzle = true, Name = "털뭉치 연습", Max = 1, Cost = Table(400), Txt = l => $"털뭉치 {YarnBaseCd - l}턴마다", NeedFloor = Need(3) },
            new Upgrade { Id = UpId.HissCd, Puzzle = true, Name = "하악질 연습", Max = 2, Cost = Table(500, 2000), Txt = l => $"하악질 {HissBaseCd - l}턴마다", NeedFloor = Need(4, 8) },
            new Upgrade { Id = UpId.Revive, Puzzle = true, Name = "아홉 번째 목숨", Max = 1, Cost = Table(1500), Txt = l => l > 0 ? "판마다 1번 부활" : "부활 없음", NeedFloor = Need(5) },
        };

        public static readonly int UpCount = Enum.GetValues(typeof(UpId)).Length;

        public static string Fmt(double n)
        {
            if (double.IsNaN(n)) return "0";
            if (double.IsInfinity(n)) return "최대";
            n = Math.Floor(n);
            if (n < 1e4) return n.ToString("#,0");
            // truncate (never round up) so 99,999 reads 9.9만, not 10.0만
            double unit = n >= 1e12 ? 1e12 : n >= 1e8 ? 1e8 : 1e4;
            string name = n >= 1e12 ? "조" : n >= 1e8 ? "억" : "만";
            double v = n / unit;
            return (v < 10 ? (Math.Floor(v * 10) / 10).ToString("0.0") : Math.Floor(v).ToString("#,0")) + name;
        }

        public static string FmtRate(double r) => r >= 100 ? Fmt(r) : r >= 10 ? r.ToString("0") : r.ToString("0.0");

        public static string FmtDur(double s)
        {
            long t = (long)s; long h = t / 3600, m = t % 3600 / 60;
            return h > 0 ? $"{h}시간 {m}분" : m > 0 ? $"{m}분" : $"{t}초";
        }
    }

    public enum EType { Mouse = 0, Fly = 1, Cucumber = 2, Rat = 3, Vacuum = 4, Tub = 5 }

    public class EDef
    {
        public string Name, Sprite;
        public int Hp, Dmg;
        public bool Diag, Slow, Boss, Fixed;

        public static readonly EDef[] All =
        {
            new EDef { Name = "생쥐", Sprite = "enemy_mouse", Hp = 1, Dmg = 1 },
            new EDef { Name = "파리", Sprite = "enemy_fly", Hp = 1, Dmg = 1, Diag = true },
            new EDef { Name = "오이", Sprite = "enemy_cucumber", Hp = 2, Dmg = 1, Slow = true },
            new EDef { Name = "엘리트 쥐", Sprite = "enemy_rat", Hp = 3, Dmg = 2 },
            new EDef { Name = "로봇청소기", Sprite = "enemy_vacuum", Hp = 6, Dmg = 2, Boss = true, Fixed = true },
            new EDef { Name = "목욕통", Sprite = "enemy_tub", Hp = 7, Dmg = 2, Slow = true, Boss = true, Fixed = true },
        };

        public static EDef Of(int type) => All[type];
    }

    [Serializable]
    public class EnemyData
    {
        public int id, type, x, y, hp, max, stun, face = 1;
        public bool threat, rest, charging;
        public int cdx, cdy;
    }

    [Serializable]
    public class MarkData
    {
        public int x, y, t, type;
    }

    [Serializable]
    public class RunData
    {
        public int floor, hearts, kills, turn, px, py, face = 1, nextId, toSpawn, cdYarn, cdHiss;
        public bool boss, reviveUsed;
        public int[] obs = new int[49];
        public List<EnemyData> enemies = new List<EnemyData>();
        public List<MarkData> marks = new List<MarkData>();
    }

    [Serializable]
    public class SaveData
    {
        public double churu, total, stockT;
        public int[] board = new int[Defs.Cols * Defs.MaxRows];
        public int[] up = new int[Defs.UpCount];
        public int stock = 3, maxTier = 1, bestFloor, merged, balance;
        public List<int> gifts = new List<int>();
        public bool hasRun, muted;
        public RunData run = new RunData();
        public long last;

        public static SaveData Fresh()
        {
            var s = new SaveData();
            s.board[5] = 1; s.board[6] = 1;
            s.last = NowMs();
            s.balance = Defs.BalanceVersion;
            return s;
        }

        public static long NowMs() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        public int Up(Defs.UpId id) => up[(int)id];
        public int Rows => Defs.BaseRows + Up(Defs.UpId.Room);
        public int Slots => Defs.Cols * Rows;
        public double FloorBonus => 1 + bestFloor * 0.05;
        public int MaxHearts => 3 + Up(Defs.UpId.Hp);
        public int StockDT => Defs.DelivTime(Up(Defs.UpId.Deliv));
        public int StockCap => Defs.BaseStockCap + Up(Defs.UpId.StockCap);
        public int YarnCd => Defs.YarnBaseCd - Up(Defs.UpId.YarnCd);
        public int HissCd => Defs.HissBaseCd - Up(Defs.UpId.HissCd);

        public double Rate()
        {
            double r = 0;
            for (int i = 0; i < Slots; i++) if (board[i] > 0) r += Defs.BoxProd[board[i]];
            return r * FloorBonus;
        }
    }
}
