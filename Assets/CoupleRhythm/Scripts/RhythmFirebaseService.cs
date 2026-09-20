using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace CoupleRhythm
{
    public class RhythmFirebaseService : MonoBehaviour
    {
        private static RhythmFirebaseService instance;
        public static RhythmFirebaseService Instance
        {
            get
            {
                if (instance == null)
                {
                    GameObject go = new GameObject("RhythmFirebaseService");
                    instance = go.AddComponent<RhythmFirebaseService>();
                    DontDestroyOnLoad(go);
                }
                return instance;
            }
        }

        [Header("Firebase Config")]
        public string firebaseProjectId = "your-firebase-project-id";
        private string BaseUrl => $"https://firestore.googleapis.com/v1/projects/{firebaseProjectId}/databases/(default)/documents";

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            DontDestroyOnLoad(gameObject);
            LoadConfig();
        }

        private void LoadConfig()
        {
            string envPid = EnvLoader.Get("FIREBASE_PROJECT_ID");
            if (!string.IsNullOrEmpty(envPid) && envPid != "your-firebase-project-id")
            {
                firebaseProjectId = envPid;
                Debug.Log($"[RhythmFirebase] ✅ .env 에서 Firebase Project ID 로드: {firebaseProjectId}");
                return;
            }

            TextAsset configAsset = Resources.Load<TextAsset>("FirebaseConfig");
            if (configAsset != null)
            {
                try
                {
                    var parsed = JsonUtility.FromJson<FirebaseConfigData>(configAsset.text);
                    if (parsed != null && !string.IsNullOrEmpty(parsed.firebaseProjectId) && parsed.firebaseProjectId != "your-firebase-project-id")
                    {
                        firebaseProjectId = parsed.firebaseProjectId;
                        Debug.Log($"[RhythmFirebase] ✅ Resources/FirebaseConfig 에서 Project ID 로드: {firebaseProjectId}");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[RhythmFirebase] FirebaseConfig 로드 실패: {ex.Message}");
                }
            }
        }

        [Serializable]
        private class FirebaseConfigData
        {
            public string firebaseProjectId;
        }

        /// <summary>
        /// 결제 완료 또는 게임 시작 시 부스 전체 플레이 수와 매출을 증가시킵니다.
        /// </summary>
        public void RecordGameStart(int revenueAmount = 1000)
        {
            if (string.IsNullOrEmpty(firebaseProjectId) || firebaseProjectId == "your-firebase-project-id") return;
            StartCoroutine(RecordGameStartRoutine(revenueAmount));
        }

        private IEnumerator RecordGameStartRoutine(int revenueAmount)
        {
            string url = $"{BaseUrl}/GameState/stats";

            int totalPlays = 0;
            int totalRevenue = 0;

            using (UnityWebRequest getReq = UnityWebRequest.Get(url))
            {
                yield return getReq.SendWebRequest();
                if (getReq.result == UnityWebRequest.Result.Success)
                {
                    string text = getReq.downloadHandler.text;
                    totalPlays = ExtractIntValue(text, "totalPlays");
                    totalRevenue = ExtractIntValue(text, "totalRevenue");
                }
            }

            totalPlays++;
            totalRevenue += revenueAmount;

            string patchUrl = $"{url}?updateMask.fieldPaths=totalPlays&updateMask.fieldPaths=totalRevenue";
            string patchJson = $"{{\"fields\":{{\"totalPlays\":{{\"integerValue\":\"{totalPlays}\"}},\"totalRevenue\":{{\"integerValue\":\"{totalRevenue}\"}}}}}}";

            using (UnityWebRequest patchReq = new UnityWebRequest(patchUrl, "PATCH"))
            {
                patchReq.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(patchJson));
                patchReq.downloadHandler = new DownloadHandlerBuffer();
                patchReq.SetRequestHeader("Content-Type", "application/json");
                yield return patchReq.SendWebRequest();

                if (patchReq.result == UnityWebRequest.Result.Success)
                {
                    Debug.Log($"[RhythmFirebase] 부스 통계 갱신 성공: 플레이 {totalPlays}회, 누적 매출 {totalRevenue}원");
                }
            }
        }

        /// <summary>
        /// 곡 종료 시 커플의 성적을 실시간 랭킹 컬렉션(RhythmLeaderboard)에 저장합니다.
        /// </summary>
        public void SubmitScore(string songId, string songTitle, float accuracy, int maxCombo, int perfect, int good, int miss, int wrongPress)
        {
            if (string.IsNullOrEmpty(firebaseProjectId) || firebaseProjectId == "your-firebase-project-id") return;
            StartCoroutine(SubmitScoreRoutine(songId, songTitle, accuracy, maxCombo, perfect, good, miss, wrongPress));
        }

        private IEnumerator SubmitScoreRoutine(string songId, string songTitle, float accuracy, int maxCombo, int perfect, int good, int miss, int wrongPress)
        {
            string url = $"{BaseUrl}/RhythmLeaderboard";
            string timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");

            string jsonPayload = "{\"fields\":{" +
                $"\"songId\":{{\"stringValue\":\"{songId}\"}}," +
                $"\"songTitle\":{{\"stringValue\":\"{songTitle}\"}}," +
                $"\"accuracy\":{{\"doubleValue\":{accuracy.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)}}}," +
                $"\"maxCombo\":{{\"integerValue\":\"{maxCombo}\"}}," +
                $"\"perfectCount\":{{\"integerValue\":\"{perfect}\"}}," +
                $"\"goodCount\":{{\"integerValue\":\"{good}\"}}," +
                $"\"missCount\":{{\"integerValue\":\"{miss}\"}}," +
                $"\"wrongPressCount\":{{\"integerValue\":\"{wrongPress}\"}}," +
                $"\"timestamp\":{{\"stringValue\":\"{timestamp}\"}}" +
            "}}";

            using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonPayload));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");

                yield return request.SendWebRequest();
                if (request.result == UnityWebRequest.Result.Success)
                {
                    Debug.Log($"[RhythmFirebase] 커플 랭킹 저장 완료: {songTitle} ({accuracy:0.0}%)");
                }
                else
                {
                    Debug.LogWarning($"[RhythmFirebase] 랭킹 저장 실패: {request.error}");
                }
            }
        }

        private int ExtractIntValue(string json, string fieldName)
        {
            try
            {
                var match = System.Text.RegularExpressions.Regex.Match(json, $"\"{fieldName}\":\\s*{{\\s*\"integerValue\":\\s*\"(\\d+)\"");
                if (match.Success) return int.Parse(match.Groups[1].Value);
            }
            catch { }
            return 0;
        }
    }
}
