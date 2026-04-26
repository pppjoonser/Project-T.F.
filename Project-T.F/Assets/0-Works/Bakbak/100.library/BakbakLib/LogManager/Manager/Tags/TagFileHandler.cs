using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Bakbak.Editor
{
    public class TagFileHandler
    {
        private static string SavePath => Path.Combine(Directory.GetCurrentDirectory(), "ProjectSettings", "BetterLogsTag.json");

        public static TagData LoadTags()
        {
            if(!File.Exists(SavePath)) return new TagData();

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
            try
            {
                TagData data = new TagData { savedTags = tags };
                string json = JsonUtility.ToJson(data, true);

                string tempPath = SavePath + ".tmp";
                File.WriteAllText(tempPath, json);

                if (File.Exists(SavePath))
                {
                    File.Replace(tempPath, SavePath, null);
                }
                File.Move(tempPath, SavePath);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to save tags: {ex.Message}");
            }
        }
    }
}