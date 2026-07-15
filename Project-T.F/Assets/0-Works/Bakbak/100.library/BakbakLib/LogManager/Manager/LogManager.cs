using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Bakbak.Editor
{
    [InitializeOnLoad]
    public static partial class LogManager
    {
        private readonly struct PendingUnityLog
        {
            public readonly string Condition;
            public readonly string StackTrace;
            public readonly LogType Type;

            public PendingUnityLog(string condition, string stackTrace, LogType type)
            {
                Condition = condition;
                StackTrace = stackTrace;
                Type = type;
            }
        }

        public static event Action LogsChanged;
        public static event Action TagsChanged;

        private static Dictionary<string, int> tagRegistry = new(); // tag (normalized) -> hash
        private static Dictionary<string, int> string_Hash_Pair = new(); // title -> hash
        private static List<HashSet<int>> logtagList = new(); // Tag ID -> HashSet<Title ID>
        private static List<List<LogInfo>> logList = new(); // Title ID -> List<LogInfo>
        private static List<LogInfo> allLogs = new(); // Global chronological log list
        private static readonly ConcurrentQueue<PendingUnityLog> pendingUnityLogs = new();

        // sequential hash maker: start from int.MinValue when it makes new value incrementally.
        private static int nextTagID = 0;
        private static int nextTitleID = 0;
        private static long nextSequenceID = DateTime.UtcNow.Ticks;

        static LogManager()
        {
            InitializeLogManager();

            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

            Application.logMessageReceived -= OnLogMessageReceived;
            Application.logMessageReceivedThreaded -= OnLogMessageReceived;
            Application.logMessageReceivedThreaded += OnLogMessageReceived;

            EditorApplication.update -= DrainUnityLogs;
            EditorApplication.update += DrainUnityLogs;

            EditorApplication.quitting -= OnEditorQuitting;
            EditorApplication.quitting += OnEditorQuitting;
        }

        private static void OnLogMessageReceived(string condition, string stackTrace, LogType type)
        {
            pendingUnityLogs.Enqueue(new PendingUnityLog(condition, stackTrace, type));
        }

        private static void DrainUnityLogs()
        {
            while (pendingUnityLogs.TryDequeue(out PendingUnityLog pending))
            {
                MakeLogInternal(
                    "UnityLog",
                    pending.Condition,
                    pending.StackTrace,
                    GetUnityLogTags(pending.Type));
            }
        }

        public static void ClearLogs()
        {
            while (pendingUnityLogs.TryDequeue(out _))
            {
            }

            foreach (List<LogInfo> logs in logList)
            {
                logs.Clear();
            }

            foreach (HashSet<int> taggedTitles in logtagList)
            {
                taggedTitles.Clear();
            }

            allLogs.Clear();
            LogsChanged?.Invoke();
        }

        private static string[] GetUnityLogTags(LogType type)
        {
            return new[] { type.ToString() };
        }

        private static void InitializeLogManager()
        {
            // LogManager Initialize
            TagData tagData = TagFileHandler.LoadTags();
            foreach (string rawTag in tagData.savedTags)
            {
                // 기존 코드에서 대소문자 비교를 ToUpper로 하고 있으므로 로드 시에도 동일하게 정규화
                string normalized = rawTag.ToUpperInvariant();
                // 이미 존재하면 건너뜀, 없으면 시퀀셜 해시 할당
                if (!tagRegistry.ContainsKey(normalized))
                {
                    int hashed = GetNextTagID();
                    tagRegistry.Add(normalized, hashed);
                }
            }
        }
        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            switch (state)
            {
                case PlayModeStateChange.ExitingEditMode:
                    // 에디터 모드를 종료하고 플레이 모드로 넘어가기 직전
                    // (필요 시 현재 로그 상태를 임시 파일에 백업)
                    break;
                case PlayModeStateChange.EnteredPlayMode:
                    // 플레이 모드 진입 완료
                    break;
                case PlayModeStateChange.ExitingPlayMode:
                    // 플레이 모드 종료 직전
                    break;
                case PlayModeStateChange.EnteredEditMode:
                    // 에디터 모드로 복귀 완료
                    break;
            }
        }
        private static void OnEditorQuitting()
        {
            DrainUnityLogs();
            TagFileHandler.SaveTags(new List<string>(tagRegistry.Keys));
            TagFileHandler.FlushPendingWrites();
        }

        // 주어진 딕셔너리에 대해 키가 있으면 기존 해시 반환, 없으면 새 시퀀셜 해시 생성 후 저장 및 반환
        private static int GetOrCreateTitleID(string title)
        {
            if (string_Hash_Pair.TryGetValue(title, out int existing))
                return existing;

            int newID = GetNextTitleID();
            string_Hash_Pair.Add(title, newID);
            return newID;
        }
        private static int GetNextTagID()
        {
            if (nextTagID == int.MaxValue)
                throw new InvalidOperationException("No more Tag IDs available.");

            int id = nextTagID++;
            logtagList.Add(new HashSet<int>()); // ID 발급과 동시에 List의 해당 인덱스에 빈 방 생성
            return id;
        }

        // safe sequential ID generation for titles, ensuring no overflow and maintaining a list for logs
        private static int GetNextTitleID()
        {
            if (nextTitleID == int.MaxValue)
                throw new InvalidOperationException("No more Title IDs available.");

            int id = nextTitleID++;
            logList.Add(new List<LogInfo>()); // ID 발급과 동시에 List의 해당 인덱스에 빈 방 생성
            return id;
        }


        /// <summary>
        /// Logs a message with a specific title.
        /// Creates an untagged log entry.
        /// </summary>
        /// <param name="title">The title of the log entry.</param>
        /// <param name="message">The main content of the log message.</param>
        public static void MakeLog(string title, string message)
        {
            MakeLog(title, message, Array.Empty<string>());
        }


        /// <summary>
        /// Logs a message with a specific title and one or more custom tags.
        /// </summary>
        /// <param name="title">The title of the log entry.</param>
        /// <param name="message">The main content of the log message.</param>
        /// <param name="tags">A list of tags to categorize this log (e.g., "UI", "Network", "Critical").</param>
        public static void MakeLog(string title, string message = "", params string[] tags)
        {
            if (tags == null || tags.Length == 0)
            {
                tags = Array.Empty<string>();
            }

            // IDE stack trace extraction
#if UNITY_EDITOR
            string stackTraceString = UnityEngine.StackTraceUtility.ExtractStackTrace();
#else
            string stackTraceString = string.Empty;
#endif

            MakeLogInternal(title, message, stackTraceString, tags);
        }

        private static void MakeLogInternal(string title, string message, string stackTrace, string[] tags)
        {
            DateTime now = DateTime.UtcNow;
            stackTrace = TrimInternalStackFrames(stackTrace);

            // get title id
            int logTitleID = GetOrCreateTitleID(title);

            LogInfo newLog = new LogInfo(
                logType: tags, // (주의) 구조체 정의에 맞게 기본값 할당
                message: message,
                stackTrace: stackTrace,
                context: null,
                loggerName: title,
                threadId: System.Threading.Thread.CurrentThread.ManagedThreadId,
                sequenceId: System.Threading.Interlocked.Increment(ref nextSequenceID),
                exception: null,
                timestamp: now
            );

            logList[logTitleID].Add(newLog);
            allLogs.Add(newLog);

            bool addedTag = false;

            foreach (string tag in tags)
            {
                string comparingTag = tag.ToUpperInvariant();
                int tagID;

                if (tagRegistry.TryGetValue(comparingTag, out int existingTagID))
                {
                    tagID = existingTagID;
                }
                else
                {
                    tagID = GetNextTagID();
                    tagRegistry.Add(comparingTag, tagID);
                    TagFileHandler.SaveTags(new List<string>(tagRegistry.Keys));
                    addedTag = true;
                }

                // Dictionary 조회 없이 인덱스로 바로 접근하여 타이틀 ID 추가
                logtagList[tagID].Add(logTitleID);
            }

            if (addedTag)
            {
                TagsChanged?.Invoke();
            }

            LogsChanged?.Invoke();
        }

        private static string TrimInternalStackFrames(string stackTrace)
        {
            if (string.IsNullOrEmpty(stackTrace))
                return string.Empty;

            StringBuilder result = null;
            int lineStart = 0;

            while (lineStart < stackTrace.Length)
            {
                int lineEnd = stackTrace.IndexOf('\n', lineStart);
                if (lineEnd < 0)
                    lineEnd = stackTrace.Length;

                int lineLength = lineEnd - lineStart;
                if (lineLength > 0 && stackTrace[lineStart + lineLength - 1] == '\r')
                    lineLength--;

                bool skipFrame = stackTrace.IndexOf(
                        "UnityEngine.", lineStart, lineLength, StringComparison.Ordinal) >= 0 ||
                    stackTrace.IndexOf(
                        "Bakbak.Editor.LogManager", lineStart, lineLength, StringComparison.Ordinal) >= 0;

                if (!skipFrame && lineLength > 0)
                {
                    result ??= new StringBuilder(stackTrace.Length);
                    if (result.Length > 0)
                        result.Append('\n');
                    result.Append(stackTrace, lineStart, lineLength);
                }

                lineStart = lineEnd + 1;
            }

            return result?.ToString() ?? string.Empty;
        }

        public static LogInfo[] GetLogInfos(string title,params string[] tags)
        {
            if(title == "" || tags.Length == 0)
            {
                return logList.SelectMany(logs => logs).ToArray(); // return all logs if no title or tags are specified
            }

            List<LogInfo> result = new List<LogInfo>();
            int titleID = GetOrCreateTitleID(title); // Ensure title exists and get its ID
            foreach (string rawTag in tags)
            {
                if(tagRegistry.TryGetValue(rawTag.ToUpperInvariant(), out int tagID))
                {
                    if (logtagList[tagID].Contains(titleID))
                    {
                        result.AddRange(logList[titleID]);
                    }
                }
            }
            return result.ToArray();
        }
        public static string[] GetTags()
        {
            return tagRegistry
                .OrderBy(pair => pair.Key)
                .Select(pair => pair.Key)
                .ToArray();
        }

        public static string[] GetLogTitles()
        {
            return string_Hash_Pair
                .OrderBy(pair => pair.Key)
                .Select(pair => pair.Key)
                .ToArray();
        }

        public static LogInfo[] FindLogs(string titleFilter = "", IEnumerable<string> tagFilters = null, string searchFilter = "")
        {
            List<LogInfo> results = new List<LogInfo>();
            FindLogs(results, titleFilter, tagFilters, searchFilter);
            return results.ToArray();
        }

        public static void FindLogs(
            List<LogInfo> results,
            string titleFilter = "",
            IEnumerable<string> tagFilters = null,
            string searchFilter = "")
        {
            if (results == null)
                throw new ArgumentNullException(nameof(results));

            results.Clear();

            HashSet<string> selectedTags = null;
            if (tagFilters is HashSet<string> tagSet &&
                tagSet.Comparer == StringComparer.OrdinalIgnoreCase)
            {
                if (tagSet.Count > 0)
                    selectedTags = tagSet;
            }
            else if (tagFilters != null)
            {
                selectedTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (string tag in tagFilters)
                {
                    if (!string.IsNullOrWhiteSpace(tag))
                        selectedTags.Add(tag);
                }

                if (selectedTags.Count == 0)
                    selectedTags = null;
            }

            string search = string.IsNullOrWhiteSpace(searchFilter)
                ? null
                : searchFilter.Trim();
            bool hasTitleFilter = !string.IsNullOrWhiteSpace(titleFilter);

            for (int i = allLogs.Count - 1; i >= 0; i--)
            {
                LogInfo log = allLogs[i];

                if (hasTitleFilter &&
                    !string.Equals(log.loggerName, titleFilter, StringComparison.Ordinal))
                    continue;

                if (selectedTags != null && !HasAnyTag(log, selectedTags))
                    continue;

                if (search != null && !MatchesSearch(log, search))
                    continue;

                results.Add(log);
            }
        }

        private static bool HasAnyTag(LogInfo log, HashSet<string> selectedTags)
        {
            if (log.logTypes == null)
                return false;

            foreach (string tag in log.logTypes)
            {
                if (selectedTags.Contains(tag))
                    return true;
            }

            return false;
        }

        private static bool MatchesSearch(LogInfo log, string search)
        {
            if (Contains(log.loggerName, search) ||
                Contains(log.message, search) ||
                Contains(log.stackTrace, search))
                return true;

            if (log.logTypes == null)
                return false;

            foreach (string tag in log.logTypes)
            {
                if (Contains(tag, search))
                    return true;
            }

            return false;
        }

        private static bool Contains(string value, string search)
        {
            return !string.IsNullOrEmpty(value) &&
                   value.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
