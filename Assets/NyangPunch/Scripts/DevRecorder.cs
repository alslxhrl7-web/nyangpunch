#if UNITY_EDITOR
using System;
using System.Collections;
using System.Reflection;
using UnityEditor.Media;
using UnityEngine;

namespace NyangPunch
{
    /// <summary>
    /// Editor-only: plays scripted gameplay scenes and records them to MP4 (portrait 720x1280, 30 fps),
    /// so each in-game situation can be scored with a sound generator. Not compiled into builds.
    /// Game time is locked to the video frame rate (Time.captureDeltaTime), so animations match the video
    /// even when the editor renders slowly in the background.
    /// Use: NyangPunch.DevRecorder.Record("hideout") ... poll DevRecorder.Status until it starts with "done".
    /// </summary>
    public class DevRecorder : MonoBehaviour
    {
        public const int W = 720, H = 1280, Fps = 30;
        public static string Status = "idle";
        public static readonly string[] Scenes = { "hideout", "punch", "skills", "hurt", "boss", "clear" };

        const BindingFlags BF = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        Game g; MediaEncoder enc; RenderTexture rt; Texture2D tex; int frames; bool rec;
        Canvas canvas; RenderMode oldMode; Camera cam;

        public static void Record(string scene, string outDir = null)
        {
            if (string.IsNullOrEmpty(outDir)) outDir = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../Captures/video"));   // <project>/Captures/video
            var go = new GameObject("DevRecorder");
            go.AddComponent<DevRecorder>().StartCoroutine(go.GetComponent<DevRecorder>().Run(scene, outDir));
        }

        object Call(string m, params object[] a) => typeof(Game).GetMethod(m, BF).Invoke(g, a);
        SaveData S => (SaveData)typeof(Game).GetField("S", BF).GetValue(g);
        RunData R => S.run;
        IEnumerator Sec(float s) { yield return new WaitForSeconds(s); }
        void Act(Puzzle.Kind k, int dx = 0, int dy = 0) => Call("DoAct", k, dx, dy);

        IEnumerator Run(string scene, string outDir)
        {
            Status = "running " + scene;
            g = FindAnyObjectByType<Game>();
            Application.runInBackground = true;
            Time.captureDeltaTime = 1f / Fps;
            Exception err = null;
            try { Setup(scene); } catch (Exception e) { err = e; }
            if (err != null) { Finish(); Status = "done ERROR setup " + err; Destroy(gameObject); yield break; }
            yield return null; yield return null;               // let the UI lay out at the video size

            System.IO.Directory.CreateDirectory(outDir);
            string path = outDir + "/" + scene + ".mp4";
            var attrs = new VideoTrackAttributes { frameRate = new MediaRational(Fps), width = W, height = H, includeAlpha = false, bitRateMode = UnityEditor.VideoBitrateMode.High };
            enc = new MediaEncoder(path, attrs);
            tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            rec = true; StartCoroutine(Grab());

            IEnumerator steps = Steps(scene);
            while (true)
            {
                bool more;
                try { more = steps.MoveNext(); } catch (Exception e) { err = e; more = false; }
                if (!more) break;
                yield return steps.Current;
            }
            yield return Sec(1.2f);   // tail, and keeps every clip over Varco Sound's 5 s minimum
            rec = false; yield return null;
            enc.Dispose(); enc = null;
            Finish();
            Status = err != null ? "done ERROR " + err.Message : $"done {scene} {frames} frames -> {path}";
            Destroy(gameObject);
        }

        void Setup(string scene)
        {
            cam = Camera.main;
            rt = new RenderTexture(W, H, 24); cam.targetTexture = rt;
            canvas = FindAnyObjectByType<Canvas>(); oldMode = canvas.renderMode;
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = cam; canvas.planeDistance = 5;
            Call("CloseOverlay");
            if (scene == "hideout")
            {
                S.board = new int[S.board.Length]; S.board[0] = 1; S.board[5] = 1; S.board[2] = 2; S.board[9] = 2;
                S.maxTier = 2; S.stock = 5; S.gifts.Clear();
                Call("SetView", "hide");
                return;
            }
            S.maxTier = Math.Max(S.maxTier, 4);   // both skills unlocked
            Call("SetView", "puz");
            Call("StartRun", "");
            Call("CloseOverlay");
            var r = R; r.obs = new int[49]; r.enemies.Clear(); r.marks.Clear(); r.toSpawn = 0; r.cdYarn = 0; r.cdHiss = 0; r.face = 1;
            void E(int id, EType t, int x, int y, int hp) => r.enemies.Add(new EnemyData { id = id, type = (int)t, x = x, y = y, hp = hp, max = hp, face = x > r.px ? -1 : 1 });
            switch (scene)
            {
                case "punch":   // walk, punch a rat into a plant pot, finish it off
                    r.px = 2; r.py = 3; r.obs[3 * 7 + 5] = 2; r.obs[1 * 7 + 1] = 1;
                    E(10, EType.Rat, 4, 3, 3); r.toSpawn = 1; break;
                case "skills":  // yarn ball up the column, then hiss at two neighbours
                    r.px = 3; r.py = 4; r.obs[1 * 7 + 5] = 2;
                    E(20, EType.Fly, 3, 0, 1); E(21, EType.Mouse, 2, 4, 1); E(22, EType.Mouse, 4, 4, 1); E(23, EType.Cucumber, 6, 6, 2); break;
                case "hurt":    // two mice next to the cat, cat just waits and gets hit until it falls
                    r.px = 3; r.py = 3; r.hearts = 4; r.obs[5 * 7 + 1] = 1; S.up[(int)Defs.UpId.Revive] = 0;
                    E(30, EType.Mouse, 2, 3, 1); E(31, EType.Rat, 3, 2, 3);
                    foreach (var e in r.enemies) e.threat = true; break;
                case "boss":    // vacuum lines up, aims, the cat steps aside, it charges into the wall, cat punches it
                    r.floor = 5; r.boss = true; r.px = 2; r.py = 3; r.hearts = 4; r.obs[0 * 7 + 4] = 2; r.obs[6 * 7 + 2] = 1;
                    E(40, EType.Vacuum, 5, 3, 6); break;
                case "clear":   // last mouse of the floor, one punch, floor-clear reward screen
                    r.px = 3; r.py = 3; r.obs[1 * 7 + 5] = 1;
                    E(50, EType.Mouse, 4, 3, 1); break;
            }
            Call("RefreshUI");
        }

        IEnumerator Steps(string scene)
        {
            yield return Sec(0.8f);
            switch (scene)
            {
                case "hideout":
                    Call("Deliver"); yield return Sec(0.9f);
                    Call("Deliver"); yield return Sec(0.9f);
                    Call("Merge", 0, 5); yield return Sec(1.0f);          // two small boxes -> tier 2
                    Call("Merge", 2, 5); yield return Sec(1.4f);          // tier 2 + tier 2 -> tier 3 (new box banner)
                    Call("Deliver"); yield return Sec(1.2f);
                    break;
                case "punch":
                    Act(Puzzle.Kind.Dir, 1, 0); yield return Sec(0.9f);   // step right
                    Act(Puzzle.Kind.Dir, 1, 0); yield return Sec(0.9f);   // punch -> rat slams into the pot
                    Act(Puzzle.Kind.Dir, 1, 0); yield return Sec(0.9f);
                    Act(Puzzle.Kind.Dir, 1, 0); yield return Sec(1.2f);
                    break;
                case "skills":
                    Act(Puzzle.Kind.Yarn, 0, -1); yield return Sec(1.2f); // yarn ball up the column hits the fly
                    Act(Puzzle.Kind.Hiss); yield return Sec(1.4f);        // hiss pushes both mice away
                    Act(Puzzle.Kind.Wait); yield return Sec(1.2f);
                    break;
                case "hurt":
                    Act(Puzzle.Kind.Wait); yield return Sec(1.2f);        // both enemies hit the cat
                    Act(Puzzle.Kind.Wait); yield return Sec(2.2f);        // second hit -> knocked out
                    break;
                case "boss":
                    Act(Puzzle.Kind.Wait); yield return Sec(1.0f);        // vacuum lines up and aims (boss sting)
                    Act(Puzzle.Kind.Dir, 0, -1); yield return Sec(1.4f);  // cat steps aside, vacuum charges into the wall
                    Act(Puzzle.Kind.Dir, -1, 0); yield return Sec(0.8f);  // walk over while it is stunned
                    Act(Puzzle.Kind.Dir, 0, 1); yield return Sec(0.8f);
                    for (int i = 0; i < 2; i++) { Act(Puzzle.Kind.Dir, -1, 0); yield return Sec(0.9f); }  // punch it
                    break;
                case "clear":
                    Act(Puzzle.Kind.Dir, 1, 0); yield return Sec(3.5f);   // last enemy down -> clear screen
                    break;
            }
        }

        IEnumerator Grab()
        {
            var eof = new WaitForEndOfFrame();
            while (rec)
            {
                yield return eof;
                if (!rec || enc == null) break;
                // the editor skips rendering (and uGUI mesh rebuilds) while it is in the background, so do both ourselves
                Canvas.ForceUpdateCanvases();
                cam.Render();
                var prev = RenderTexture.active; RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, W, H), 0, 0); tex.Apply(false);
                RenderTexture.active = prev;
                enc.AddFrame(tex); frames++;
            }
        }

        void Finish()
        {
            Time.captureDeltaTime = 0;
            if (cam != null) cam.targetTexture = null;
            if (canvas != null) { canvas.renderMode = oldMode; canvas.worldCamera = null; }
            if (rt != null) rt.Release();
        }
    }
}
#endif
