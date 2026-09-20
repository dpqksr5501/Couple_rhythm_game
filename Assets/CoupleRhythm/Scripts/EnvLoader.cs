using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace CoupleRhythm
{
    public static class EnvLoader
    {
        private static readonly Dictionary<string, string> _envVars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static bool _isLoaded = false;

        public static bool IsLoaded => _isLoaded;

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#endif
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Initialize()
        {
            if (_isLoaded) return;
            LoadEnv();
        }

        public static void LoadEnv()
        {
            _envVars.Clear();

            List<string> candidatePaths = new List<string>
            {
                Path.Combine(Directory.GetCurrentDirectory(), ".env"),
                Path.Combine(Application.dataPath, "..", ".env"),
                Path.Combine(Application.dataPath, ".env"),
                Path.Combine(Application.persistentDataPath, ".env")
            };

            string foundPath = null;
            foreach (var path in candidatePaths)
            {
                try
                {
                    string fullPath = Path.GetFullPath(path);
                    if (File.Exists(fullPath))
                    {
                        foundPath = fullPath;
                        break;
                    }
                }
                catch { }
            }

            if (foundPath != null)
            {
                try
                {
                    ParseFile(foundPath);
                    _isLoaded = true;
                    Debug.Log($"[EnvLoader] ✅ .env 로드 성공: {foundPath}");
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[EnvLoader] .env 파싱 실패: {ex.Message}");
                }
            }
        }

        private static void ParseFile(string filePath)
        {
            string[] lines = File.ReadAllLines(filePath, Encoding.UTF8);
            foreach (string rawLine in lines)
            {
                string line = rawLine.Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith("#") || line.StartsWith("//"))
                    continue;

                int eqIdx = line.IndexOf('=');
                if (eqIdx <= 0) continue;

                string key = line.Substring(0, eqIdx).Trim();
                string val = line.Substring(eqIdx + 1).Trim();

                if ((val.StartsWith("\"") && val.EndsWith("\"")) || (val.StartsWith("'") && val.EndsWith("'")))
                {
                    if (val.Length >= 2) val = val.Substring(1, val.Length - 2);
                }

                if (!string.IsNullOrEmpty(key))
                {
                    _envVars[key] = val;
                    try { Environment.SetEnvironmentVariable(key, val); } catch { }
                }
            }
        }

        public static string Get(string key, string defaultValue = "")
        {
            if (!_isLoaded) LoadEnv();
            if (_envVars.TryGetValue(key, out string value) && !string.IsNullOrEmpty(value)) return value;
            string sysVal = Environment.GetEnvironmentVariable(key);
            if (!string.IsNullOrEmpty(sysVal)) return sysVal;
            return defaultValue;
        }
    }
}
