using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Bakbak.Editor
{
    [InitializeOnLoad]
    public static partial class LogManager
    {
        private static Dictionary<string, int> tagRegistry = new(); // tag (normalized) -> hash
        private static Dictionary<string, int> string_Hash_Pair = new(); // title -> hash
        private static Dictionary<int, HashSet<int>> logtagDict = new(); // taghash -> lognamehash
        private static Dictionary<int, List<LogInfo>> logDict = new(); // namehash -> log

        // 시퀀셜 해시 생성기: int.MinValue 부터 시작하여 새로운 값이 추가될 때마다 1씩 증가
        private static int nextSequentialHash = int.MinValue;

        static LogManager()
        {
            InitializeLogManager();

            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

            EditorApplication.quitting -= OnEditorQuitting;
            EditorApplication.quitting += OnEditorQuitting;
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
                    int hashed = GetNextSequentialHash();
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
            throw new NotImplementedException();
        }

        // 주어진 딕셔너리에 대해 키가 있으면 기존 해시 반환, 없으면 새 시퀀셜 해시 생성 후 저장 및 반환
        private static int GetOrCreateHashForString(string key, Dictionary<string, int> dict)
        {
            if (dict.TryGetValue(key, out int existing))
                return existing;

            int newHash = GetNextSequentialHash();
            dict.Add(key, newHash);
            return newHash;
        }

        // 시퀀셜 해시를 안전하게 생성 (오버플로우 체크 포함)
        private static int GetNextSequentialHash()
        {
            if (nextSequentialHash == int.MaxValue)
                throw new InvalidOperationException("No more sequential hashes available.");

            return nextSequentialHash++;
        }

        public static void MakeLog(string title, string message = "", List<string> tags = null)
        {
            if (tags == null)
            {
                tags = new List<string>() { "None" };
            }

            System.Diagnostics.StackTrace stackTrace = new System.Diagnostics.StackTrace();
            string stackTraceString = stackTrace.ToString();

            // 제목에 대한 해시: 제목별로 고유한 시퀀셜 정수 사용
            int logHash = GetOrCreateHashForString(title, string_Hash_Pair);

            if (string_Hash_Pair.ContainsKey(title) == false)
            {
                // 이미 GetOrCreateHashForString에서 추가되었으므로 이 체크는 중복일 수 있으나 남겨둠
                string_Hash_Pair.Add(title, logHash);
            }

            LogInfo newLog = new LogInfo(
                logType: tags,
                message: message,
                stackTrace: stackTraceString,
                context: null,
                loggerName: title,
                threadId: System.Threading.Thread.CurrentThread.ManagedThreadId,
                sequenceId: DateTime.UtcNow.Ticks,
                exception: null,
                timestamp: DateTime.UtcNow
            ); //initialize log;

            foreach (string tag in tags)
            {
                string compareingTag = tag.ToUpperInvariant(); // convert to uppercase for case-insensitive handling
                int tagHashResult;
                if (tagRegistry.TryGetValue(compareingTag, out int taghash))
                {
                    tagHashResult = taghash;
                }
                else
                {
                    // Animator.StringToHash 대체: 시퀀셜 해시 사용
                    tagHashResult = GetNextSequentialHash();
                    tagRegistry.Add(compareingTag, tagHashResult);
                    TagFileHandler.SaveTags(new List<string>(tagRegistry.Keys));
                }
                if (logtagDict.ContainsKey(tagHashResult) == false)
                {
                    logtagDict[tagHashResult] = new HashSet<int>();
                }

                logtagDict[tagHashResult].Add(logHash);
            }

            //TODO: 여기서부터 로그 저장 개발
            if (logDict.ContainsKey(logHash))
            {
                logDict[logHash].Add(newLog);
            }
            else
            {
                logDict[logHash] = new List<LogInfo>() { newLog };
            }
        }
    }
}