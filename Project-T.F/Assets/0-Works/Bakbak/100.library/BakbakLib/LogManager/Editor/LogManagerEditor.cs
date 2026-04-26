using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bakbak.Editor
{
    public class LogManagerEditor : EditorWindow
    {
        [SerializeField] private VisualTreeAsset visualTreeAsset = default;
        [SerializeField] private VisualTreeAsset itemAsset;
        [SerializeField] private VisualTreeAsset selectionAsset;

        private const string EditorName = "LogManagerEditor";
        private const string logviewName = "LogView";

        private string _rootFolderPath;
        private ScrollView _logView;
        private ScrollView _selectionView;
        private LogPannel _log;
        private List<LogPannel> _logList;
        private LogToggle _logSelector;

        [MenuItem("Tools/LogManger")]
        public static void ShowWindow()
        {
            LogManagerEditor wnd = GetWindow<LogManagerEditor>();
            wnd.titleContent = new GUIContent(EditorName);
        }
        public void CreateGUI()
        {
            InitializeWindow();

            VisualElement root = rootVisualElement;

            visualTreeAsset.CloneTree(root);
            SetElements(root);
        }

        private void SetElements(VisualElement root)
        {
            _logView = root.Q<ScrollView>(logviewName);
            _logList = new List<LogPannel>();
        }

        private void InitializeWindow()
        {
            
        }
    }
}