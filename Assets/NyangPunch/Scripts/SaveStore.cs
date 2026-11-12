using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

namespace NyangPunch
{
    /// <summary>
    /// Where the save JSON lives. Editor/desktop: PlayerPrefs only.
    /// Web: also browser localStorage under a fixed key, because itch.io gives every upload a new URL and
    /// Unity's web PlayerPrefs are keyed by URL (so they reset on each update). On a first web run with no
    /// localStorage save yet, the newest save left by an older build in /idbfs/*/PlayerPrefs is picked up.
    /// When both copies exist the newer one (SaveData.last) wins. Only the tab that loaded last keeps saving.
    /// </summary>
    public static class SaveStore
    {
        // localStorage is shared by every game on itch.io's html origin, so the key carries our page name.
        const string WebPrefix = "na-min-jea/nyangpunch:";

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern string NP_StorageGet(string key);
        [DllImport("__Internal")] static extern int NP_StorageSet(string key, string value);
        [DllImport("__Internal")] static extern void NP_StorageRemove(string key);
        [DllImport("__Internal")] static extern void NP_HookPageHide(string gameObject, string method);
        [DllImport("__Internal")] static extern void NP_Reload();
        static bool warnedWrite;
#endif

        public static string Read(string key)
        {
            string json = null;
#if UNITY_WEBGL && !UNITY_EDITOR
            string ls = null;
            try { ls = NP_StorageGet(WebPrefix + key); } catch (Exception e) { Debug.LogWarning("NyangPunch: localStorage read failed " + e.Message); }
            // Both copies are written on every save. If a localStorage write failed (the 5 MB quota is shared with
            // every other itch.io game), localStorage still holds an older copy: take whichever save is newer.
            json = Newer(ls, PlayerPrefs.GetString(key, ""));
            if (string.IsNullOrEmpty(json)) { json = FromOlderBuilds(key); if (!string.IsNullOrEmpty(json)) Debug.Log("NyangPunch: 예전 웹 버전 저장을 가져왔습니다"); }
#else
            json = PlayerPrefs.GetString(key, "");
#endif
            return json;
        }

        public static void Write(string key, string json)
        {
            PlayerPrefs.SetString(key, json);
            PlayerPrefs.Save();
#if UNITY_WEBGL && !UNITY_EDITOR
            int ok = 0;
            try { ok = NP_StorageSet(WebPrefix + key, json); } catch (Exception e) { Debug.LogWarning("NyangPunch: localStorage write failed " + e.Message); }
            if (ok == 0)
            {
                // don't leave a stale copy behind that a later Read could prefer; PlayerPrefs holds the new save
                try { NP_StorageRemove(WebPrefix + key); } catch { }
                if (!warnedWrite) { warnedWrite = true; Debug.LogWarning("NyangPunch: localStorage write failed (quota?), using PlayerPrefs only"); }
            }
#endif
        }

        /// <summary>Of two save JSON strings, the one with the larger SaveData.last (an unreadable one loses).</summary>
        public static string Newer(string a, string b)
        {
            if (string.IsNullOrEmpty(a)) return b;
            if (string.IsNullOrEmpty(b)) return a;
            long la = LastOf(a), lb = LastOf(b);
            if (la == long.MinValue && lb == long.MinValue) return a;   // neither parses: let Load keep a copy of the first
            return lb > la ? b : a;
        }

        static long LastOf(string json)
        {
            try { var s = JsonUtility.FromJson<SaveData>(json); return s != null ? s.last : long.MinValue; }
            catch { return long.MinValue; }
        }

        // ---------------------------------------------------------------- one tab at a time (web)

        // Two tabs of the game would overwrite each other's save every few seconds. The tab that loaded last owns
        // the save (it read the newest state); an older tab notices on its next save and stops writing.
        static readonly string Session = Guid.NewGuid().ToString("N");

        /// <summary>Marks this tab as the owner of the save. Call right after reading it.</summary>
        public static void ClaimSession(string key)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            // if the owner key cannot be written (quota), remove it: an old tab's id left there would otherwise
            // make this tab think it lost the save and stop writing
            try { if (NP_StorageSet(WebPrefix + key + ".owner", Session) == 0) NP_StorageRemove(WebPrefix + key + ".owner"); } catch { }
#endif
        }

        /// <summary>False when another tab has loaded the save since this one did (web only; always true elsewhere).</summary>
        public static bool OwnsSession(string key)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            string owner = null;
            try { owner = NP_StorageGet(WebPrefix + key + ".owner"); } catch { }
            return string.IsNullOrEmpty(owner) || owner == Session;   // no owner key (storage blocked): nothing to compare
#else
            return true;
#endif
        }

        /// <summary>Web: calls gameObject.method() when the page is hidden or closed. No-op elsewhere.</summary>
        public static void HookPageHide(string gameObject, string method)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            try { NP_HookPageHide(gameObject, method); } catch (Exception e) { Debug.LogWarning("NyangPunch: page hide hook failed " + e.Message); }
#endif
        }

        /// <summary>Web: reloads the page. No-op elsewhere.</summary>
        public static void ReloadPage()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            NP_Reload();
#endif
        }

        /// <summary>Newest save (by SaveData.last) found in any build's PlayerPrefs file under /idbfs.</summary>
        static string FromOlderBuilds(string key)
        {
            string best = null; long bestLast = long.MinValue;
            try
            {
                if (!Directory.Exists("/idbfs")) return null;
                foreach (var file in Directory.GetFiles("/idbfs", "PlayerPrefs", SearchOption.AllDirectories))
                {
                    string json = ExtractJson(File.ReadAllBytes(file), key);
                    if (json == null) continue;
                    long last;
                    try { var s = JsonUtility.FromJson<SaveData>(json); if (s == null) continue; last = s.last; }
                    catch { continue; }
                    if (last > bestLast) { bestLast = last; best = json; }
                }
            }
            catch (Exception e) { Debug.LogWarning("NyangPunch: old save scan failed " + e.Message); }
            return best;
        }

        /// <summary>The PlayerPrefs file stores each key followed by its string value; ours is one JSON object.</summary>
        public static string ExtractJson(byte[] data, string key)
        {
            string text = System.Text.Encoding.UTF8.GetString(data);
            int k = text.IndexOf(key, StringComparison.Ordinal);
            if (k < 0) return null;
            // a few length bytes sit between the key and the value; the JSON itself starts with {"
            for (int start = k + key.Length; start < Math.Min(text.Length - 1, k + key.Length + 16); start++)
            {
                if (text[start] != '{' || text[start + 1] != '"') continue;
                int depth = 0; bool inStr = false, esc = false;
                for (int i = start; i < text.Length; i++)
                {
                    char c = text[i];
                    if (inStr) { if (esc) esc = false; else if (c == '\\') esc = true; else if (c == '"') inStr = false; continue; }
                    if (c == '"') inStr = true;
                    else if (c == '{') depth++;
                    else if (c == '}' && --depth == 0) return text.Substring(start, i - start + 1);
                }
            }
            return null;
        }
    }
}
