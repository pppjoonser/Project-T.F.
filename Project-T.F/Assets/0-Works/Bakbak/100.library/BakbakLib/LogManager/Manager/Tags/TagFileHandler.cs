using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Bakbak.Editor
{
    public class TagFileHandler
    {
        private static readonly object WriteLock = new();
        private static readonly string SavePath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "ProjectSettings",
            "BetterLogsTag.json");

        private static Task _writerTask = Task.CompletedTask;
        private static bool _writerRunning;
        private static string _pendingJson;
        private static Exception _pendingWriteException;

        static TagFileHandler()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= FlushPendingWrites;
            AssemblyReloadEvents.beforeAssemblyReload += FlushPendingWrites;
        }

        public static TagData LoadTags()
        {
            FlushPendingWrites();

            if (!File.Exists(SavePath))
                return new TagData();

            try
            {
                string json = File.ReadAllText(SavePath);
                return JsonUtility.FromJson<TagData>(json);
            }
            catch
            {
                return new TagData();
            }
        }

        public static void SaveTags(List<string> tags)
        {
            ReportPendingWriteException();

            try
            {
                TagData data = new TagData { savedTags = tags };
                string json = JsonUtility.ToJson(data, true);

                lock (WriteLock)
                {
                    _pendingJson = json;
                    if (!_writerRunning)
                    {
                        _writerRunning = true;
                        _writerTask = Task.Run(ProcessPendingWrites);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to prepare tags for saving: {ex.Message}");
            }
        }

        public static void FlushPendingWrites()
        {
            Task writerTask;
            lock (WriteLock)
            {
                writerTask = _writerTask;
            }

            writerTask.GetAwaiter().GetResult();
            ReportPendingWriteException();
        }

        private static void ProcessPendingWrites()
        {
            while (true)
            {
                string json;
                lock (WriteLock)
                {
                    json = _pendingJson;
                    _pendingJson = null;

                    if (json == null)
                    {
                        _writerRunning = false;
                        return;
                    }
                }

                try
                {
                    WriteJson(json);
                }
                catch (Exception ex)
                {
                    lock (WriteLock)
                    {
                        _pendingWriteException = ex;
                    }
                }
            }
        }

        private static void WriteJson(string json)
        {
            string tempPath = SavePath + ".tmp";
            File.WriteAllText(tempPath, json);

            if (File.Exists(SavePath))
            {
                File.Replace(tempPath, SavePath, null);
                return;
            }

            File.Move(tempPath, SavePath);
        }

        private static void ReportPendingWriteException()
        {
            Exception exception;
            lock (WriteLock)
            {
                exception = _pendingWriteException;
                _pendingWriteException = null;
            }

            if (exception != null)
            {
                Debug.LogError($"Failed to save tags: {exception.Message}");
            }
        }
    }
}
