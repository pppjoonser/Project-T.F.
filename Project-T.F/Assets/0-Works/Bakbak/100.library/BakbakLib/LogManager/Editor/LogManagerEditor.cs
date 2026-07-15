using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bakbak.Editor
{
    public partial class LogManagerEditor : EditorWindow
    {
        private readonly struct DisplayLog
        {
            public readonly LogInfo Log;
            public readonly int Count;

            public DisplayLog(LogInfo log, int count)
            {
                Log = log;
                Count = count;
            }
        }

        private readonly struct CollapseKey : IEquatable<CollapseKey>
        {
            private readonly LogInfo _log;

            public CollapseKey(LogInfo log)
            {
                _log = log;
            }

            public bool Equals(CollapseKey other)
            {
                return LogsMatchForCollapse(_log, other._log);
            }

            public override bool Equals(object obj)
            {
                return obj is CollapseKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = 17;
                    hash = hash * 31 + StringComparer.Ordinal.GetHashCode(_log.loggerName ?? string.Empty);
                    hash = hash * 31 + StringComparer.Ordinal.GetHashCode(_log.message ?? string.Empty);
                    hash = hash * 31 + StringComparer.Ordinal.GetHashCode(_log.stackTrace ?? string.Empty);
                    hash = hash * 31 + _log.threadId;

                    if (_log.logTypes != null)
                    {
                        foreach (string tag in _log.logTypes)
                        {
                            hash = hash * 31 + StringComparer.Ordinal.GetHashCode(tag ?? string.Empty);
                        }
                    }

                    return hash;
                }
            }
        }

        private enum RefreshRequest
        {
            Tags,
            Logs
        }

        private const string EditorName = "LogViewer";
        private const float CompactLayoutWidth = 720f;
        private const string ManagerTreeGuid = "e75c0e4f96740df4eac64582ecffadfc";
        private const string ManagerStyleGuid = "e3df395236c381e459d7459c7d50a9ce";
        private const string LogPannelTreeGuid = "3dab7bd6923b07244bbe68ec8cdd11ee";
        private const string LogToggleTreeGuid = "7b76fc68cba4d98489781ec1c7ba40de";
        private const string LogDetailPannelTreeGuid = "7da3d92156f34594bbff4ff2c24c8e0d";

        [SerializeField] private VisualTreeAsset managerTreeAsset;
        [SerializeField] private StyleSheet managerStyleSheet;
        [SerializeField] private VisualTreeAsset logPannelTreeAsset;
        [SerializeField] private VisualTreeAsset logToggleTreeAsset;
        [SerializeField] private VisualTreeAsset logDetailPannelTreeAsset;
        [SerializeField] private bool collapseLogs;

        private readonly List<LogToggle> _tagToggles = new();
        private readonly List<LogInfo> _filteredLogs = new();
        private readonly List<DisplayLog> _displayLogs = new();
        private readonly Dictionary<CollapseKey, int> _collapseIndices = new();
        private readonly List<int> _selectionIndices = new(1);
        private readonly HashSet<string> _selectedTags = new(System.StringComparer.OrdinalIgnoreCase);
        private readonly Queue<RefreshRequest> _refreshQueue = new();

        private bool _refreshScheduled;
        private bool _tagsRefreshQueued;
        private bool _logsRefreshQueued;
        private long? _selectedSequenceId;
        private bool _compactLayoutActive;
        private bool _compactTagsTabActive;

        private TextField _tagSearchField;
        private TextField _logSearchField;
        private Toggle _collapseToggle;
        private ScrollView _tagView;
        private ListView _logView;
        private SplitView _outerSplit;
        private SplitView _rightSplit;
        private SplitView _compactLogDetailSplit;
        private VisualElement _compactLayout;
        private VisualElement _compactTabs;
        private VisualElement _compactSearchHost;
        private VisualElement _compactPaneHost;
        private VisualElement _desktopTagsSlot;
        private VisualElement _desktopLogsSlot;
        private VisualElement _desktopDetailSlot;
        private VisualElement _compactLogsSlot;
        private VisualElement _compactDetailSlot;
        private VisualElement _rootContainer;
        private VisualElement _tagsPane;
        private VisualElement _logsPane;
        private VisualElement _detailPane;
        private VisualElement _tagSearchRow;
        private VisualElement _logSearchRow;
        private ScrollView _detailHost;
        private Button _compactLogsTab;
        private Button _compactTagsTab;
        private Label _emptyLogsLabel;
        private LogDetailPannel _detailPannel;
        private LogInfo? _selectedLog;

        [MenuItem("Tools/LogManger")]
        public static void ShowWindow()
        {
            LogManagerEditor wnd = GetWindow<LogManagerEditor>();
            wnd.titleContent = new GUIContent(EditorName);
            wnd.minSize = new Vector2(420f, 260f);
        }

        public void CreateGUI()
        {
            BuildWindow();
            EnqueueRefresh(RefreshRequest.Tags);
            EnqueueRefresh(RefreshRequest.Logs);
        }

        private void OnEnable()
        {
            LogManager.LogsChanged -= OnLogsChanged;
            LogManager.LogsChanged += OnLogsChanged;
            LogManager.TagsChanged -= OnTagsChanged;
            LogManager.TagsChanged += OnTagsChanged;
        }

        private void OnDisable()
        {
            LogManager.LogsChanged -= OnLogsChanged;
            LogManager.TagsChanged -= OnTagsChanged;
            EditorApplication.delayCall -= ProcessRefreshQueue;
            rootVisualElement.UnregisterCallback<GeometryChangedEvent>(OnWindowGeometryChanged);
            _refreshScheduled = false;
            _tagsRefreshQueued = false;
            _logsRefreshQueued = false;
            _refreshQueue.Clear();
        }

        private void OnLogsChanged()
        {
            EnqueueRefresh(RefreshRequest.Logs);
        }

        private void OnTagsChanged()
        {
            EnqueueRefresh(RefreshRequest.Tags);
        }

        private void BuildWindow()
        {
            rootVisualElement.Clear();

            ResolveAssetReferences();

            if (managerTreeAsset == null)
            {
                rootVisualElement.Add(new Label("LogManager visual tree reference missing."));
                return;
            }

            if (managerStyleSheet != null)
            {
                rootVisualElement.styleSheets.Add(managerStyleSheet);
            }

            TemplateContainer tree = managerTreeAsset.CloneTree();
            tree.style.flexGrow = 1;
            rootVisualElement.Add(tree);

            Button refreshButton = tree.Q<Button>("RefreshButton");
            refreshButton.clicked += RefreshAll;
            refreshButton.Clear();
            refreshButton.Add(new Image
            {
                image = EditorGUIUtility.IconContent("Refresh").image,
                scaleMode = ScaleMode.ScaleToFit
            });

            Button clearButton = tree.Q<Button>("ClearButton");
            clearButton.clicked += LogManager.ClearLogs;
            clearButton.Clear();
            clearButton.Add(new Image
            {
                image = EditorGUIUtility.IconContent("TreeEditor.Trash").image,
                scaleMode = ScaleMode.ScaleToFit
            });

            _tagSearchField = tree.Q<TextField>("TagSearchField");
            _logSearchField = tree.Q<TextField>("LogSearchField");
            _collapseToggle = tree.Q<Toggle>("CollapseToggle");
            _tagView = tree.Q<ScrollView>("TagView");
            _logView = tree.Q<ListView>("LogView");
            _outerSplit = tree.Q<SplitView>("OuterSplit");
            _rightSplit = tree.Q<SplitView>("RightSplit");
            _compactLogDetailSplit = tree.Q<SplitView>("CompactLogDetailSplit");
            _compactLayout = tree.Q<VisualElement>("CompactLayout");
            _compactTabs = tree.Q<VisualElement>("CompactTabs");
            _compactSearchHost = tree.Q<VisualElement>("CompactSearchHost");
            _compactPaneHost = tree.Q<VisualElement>("CompactPaneHost");
            _desktopTagsSlot = tree.Q<VisualElement>("DesktopTagsSlot");
            _desktopLogsSlot = tree.Q<VisualElement>("DesktopLogsSlot");
            _desktopDetailSlot = tree.Q<VisualElement>("DesktopDetailSlot");
            _compactLogsSlot = tree.Q<VisualElement>("CompactLogsSlot");
            _compactDetailSlot = tree.Q<VisualElement>("CompactDetailSlot");
            _rootContainer = tree.Q<VisualElement>("Root");
            _tagsPane = tree.Q<VisualElement>("TagsPane");
            _logsPane = tree.Q<VisualElement>("LogsPane");
            _detailPane = tree.Q<VisualElement>("DetailPane");
            _tagSearchRow = tree.Q<VisualElement>("TagSearchRow");
            _logSearchRow = tree.Q<VisualElement>("LogSearchRow");
            _compactLogsTab = tree.Q<Button>("CompactLogsTab");
            _compactTagsTab = tree.Q<Button>("CompactTagsTab");
            _emptyLogsLabel = tree.Q<Label>("EmptyLogsLabel");

            _compactLogsTab.clicked += () => SetCompactTab(false);
            _compactTagsTab.clicked += () => SetCompactTab(true);

            ConfigureLogList();
            _collapseToggle.SetValueWithoutNotify(collapseLogs);
            _collapseToggle.RegisterValueChangedCallback(evt =>
            {
                collapseLogs = evt.newValue;
                EnqueueRefresh(RefreshRequest.Logs);
            });

            BindSearchField(_tagSearchField, tree.Q<Button>("TagClearButton"), tree.Q<Image>("TagSearchIcon"),
                () => EnqueueRefresh(RefreshRequest.Tags));
            BindSearchField(_logSearchField, tree.Q<Button>("LogClearButton"), tree.Q<Image>("LogSearchIcon"),
                () => EnqueueRefresh(RefreshRequest.Logs));

            _detailHost = tree.Q<ScrollView>("DetailHost");
            _detailPannel = new LogDetailPannel(logDetailPannelTreeAsset);
            _detailHost.Add(_detailPannel);
            rootVisualElement.UnregisterCallback<KeyDownEvent>(OnCopyShortcut, TrickleDown.TrickleDown);
            rootVisualElement.RegisterCallback<KeyDownEvent>(OnCopyShortcut, TrickleDown.TrickleDown);

            rootVisualElement.UnregisterCallback<GeometryChangedEvent>(OnWindowGeometryChanged);
            rootVisualElement.RegisterCallback<GeometryChangedEvent>(OnWindowGeometryChanged);
            _compactLayoutActive = false;
            UpdateResponsiveLayout(rootVisualElement.resolvedStyle.width);
        }

        private void ResolveAssetReferences()
        {
            managerTreeAsset = ResolveAsset(managerTreeAsset, ManagerTreeGuid);
            managerStyleSheet = ResolveAsset(managerStyleSheet, ManagerStyleGuid);
            logPannelTreeAsset = ResolveAsset(logPannelTreeAsset, LogPannelTreeGuid);
            logToggleTreeAsset = ResolveAsset(logToggleTreeAsset, LogToggleTreeGuid);
            logDetailPannelTreeAsset = ResolveAsset(logDetailPannelTreeAsset, LogDetailPannelTreeGuid);
        }

        private static T ResolveAsset<T>(T asset, string guid) where T : UnityEngine.Object
        {
            if (asset != null)
            {
                return asset;
            }

            string path = AssetDatabase.GUIDToAssetPath(guid);
            return string.IsNullOrEmpty(path)
                ? null
                : AssetDatabase.LoadAssetAtPath<T>(path);
        }

        private void BindSearchField(TextField field, Button clearButton, Image searchIcon, System.Action onChanged)
        {
            field.label = string.Empty;
            field.SetValueWithoutNotify(string.Empty);
            field.Q(className: "unity-text-input")?.AddToClassList("search-input");

            searchIcon.image = EditorGUIUtility.IconContent("Search Icon").image;
            searchIcon.scaleMode = ScaleMode.ScaleToFit;

            clearButton.style.display = DisplayStyle.None;
            clearButton.clicked += () =>
            {
                field.SetValueWithoutNotify(string.Empty);
                clearButton.style.display = DisplayStyle.None;
                onChanged?.Invoke();
            };

            field.RegisterValueChangedCallback(evt =>
            {
                clearButton.style.display = string.IsNullOrEmpty(evt.newValue)
                    ? DisplayStyle.None
                    : DisplayStyle.Flex;
                onChanged?.Invoke();
            });
        }

    }
}
