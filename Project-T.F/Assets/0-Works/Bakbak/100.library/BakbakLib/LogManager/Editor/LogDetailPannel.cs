using System;
using System.IO;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bakbak.Editor
{
    public class LogDetailPannel : VisualElement
    {
        private readonly Label _titleLabel;
        private readonly Label _metaLabel;
        private readonly Label _tagsLabel;
        private readonly TextField _messageField;
        private readonly VisualElement _stackTraceContainer;
        private LogInfo? _currentLog;

        public LogDetailPannel(VisualTreeAsset visualTreeAsset)
        {
            AddToClassList("log-detail-pannel");
            BuildDetailTree();

            _titleLabel = this.Q<Label>("DetailTitle");
            _metaLabel = this.Q<Label>("DetailMeta");
            _tagsLabel = this.Q<Label>("DetailTags");
            _messageField = this.Q<TextField>("DetailMessage");
            _stackTraceContainer = this.Q<VisualElement>("DetailStackTrace");

            _messageField.isReadOnly = true;
            _messageField.AddToClassList("detail-message-field");
            _messageField.Q(className: "unity-text-input")?.AddToClassList("detail-message-input");
            this.AddManipulator(new ContextualMenuManipulator(evt =>
                evt.menu.AppendAction("Copy Log", _ => CopyCurrentLog())));
        }

        public void SetLog(LogInfo log)
        {
            _currentLog = log;
            _titleLabel.text = string.IsNullOrEmpty(log.loggerName) ? "(No Title)" : log.loggerName;
            _metaLabel.text = $"{log.timestamp.ToLocalTime():yyyy-MM-dd HH:mm:ss} | Thread {log.threadId} | Seq {log.sequenceId}";
            _tagsLabel.text = log.logTypes == null || log.logTypes.Length == 0
                ? "Tags: None"
                : $"Tags: {string.Join(", ", log.logTypes)}";
            _messageField.SetValueWithoutNotify(log.message ?? string.Empty);
            SetStackTrace(log.stackTrace);
        }

        public void ClearLog()
        {
            _currentLog = null;
            _titleLabel.text = "Select a log";
            _metaLabel.text = string.Empty;
            _tagsLabel.text = string.Empty;
            _messageField.SetValueWithoutNotify(string.Empty);
            _stackTraceContainer.Clear();
        }

        public bool CopyCurrentLog()
        {
            if (!_currentLog.HasValue)
                return false;

            LogInfo log = _currentLog.Value;
            string title = string.IsNullOrEmpty(log.loggerName) ? "(No Title)" : log.loggerName;
            string message = log.message ?? string.Empty;
            string stackTrace = log.stackTrace ?? string.Empty;

            EditorGUIUtility.systemCopyBuffer = string.IsNullOrEmpty(stackTrace)
                ? $"{title}: {message}"
                : $"{title}: {message}\n{stackTrace}";
            return true;
        }

        private TextField MakeDetailSection(string title, string className)
        {
            VisualElement section = new VisualElement();
            section.AddToClassList("detail-section");
            section.AddToClassList(className);
            Add(section);

            Label titleLabel = new Label(title);
            titleLabel.AddToClassList("detail-section-title");
            section.Add(titleLabel);

            TextField valueField = new TextField
            {
                multiline = true,
                isReadOnly = true
            };
            valueField.AddToClassList("detail-section-value");
            section.Add(valueField);

            return valueField;
        }

        private void BuildDetailTree()
        {
            Label titleLabel = new Label("Select a log") { name = "DetailTitle" };
            titleLabel.AddToClassList("detail-title");
            Add(titleLabel);

            Label metaLabel = new Label { name = "DetailMeta" };
            metaLabel.AddToClassList("detail-meta");
            Add(metaLabel);

            Label tagsLabel = new Label { name = "DetailTags" };
            tagsLabel.AddToClassList("detail-tags");
            Add(tagsLabel);

            MakeDetailSection("Message", "detail-message").name = "DetailMessage";

            VisualElement stackSection = new VisualElement();
            stackSection.AddToClassList("detail-section");
            stackSection.AddToClassList("detail-stack");
            Add(stackSection);

            Label stackTitle = new Label("Stack Trace");
            stackTitle.AddToClassList("detail-section-title");
            stackSection.Add(stackTitle);

            VisualElement stackContainer = new VisualElement { name = "DetailStackTrace" };
            stackContainer.AddToClassList("detail-stack-container");
            stackSection.Add(stackContainer);
        }

        private void SetStackTrace(string stackTrace)
        {
            _stackTraceContainer.Clear();

            if (string.IsNullOrWhiteSpace(stackTrace))
            {
                Label empty = new Label("(No stack trace)");
                empty.AddToClassList("detail-stack-empty");
                _stackTraceContainer.Add(empty);
                return;
            }

            string[] frames = stackTrace.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string frame in frames)
            {
                _stackTraceContainer.Add(new StackFrameLabel(frame));
            }
        }

        private sealed class StackFrameLabel : Label
        {
            private readonly string _filePath;
            private readonly int _lineNumber;

            public StackFrameLabel(string frame) : base(frame)
            {
                AddToClassList("detail-stack-frame");
                focusable = true;
                this.AddManipulator(new ContextualMenuManipulator(evt =>
                    evt.menu.AppendAction("Copy Frame", _ => EditorGUIUtility.systemCopyBuffer = text)));
                RegisterCallback<PointerDownEvent>(evt =>
                {
                    if (evt.button == 0)
                        Focus();
                });

                if (!TryParseLocation(frame, out _filePath, out _lineNumber))
                    return;

                AddToClassList("clickable");
                tooltip = "Double-click to open in IDE";
                RegisterCallback<ClickEvent>(OnClick);
            }

            private void OnClick(ClickEvent evt)
            {
                if (evt.clickCount != 2)
                    return;

                string path = Path.IsPathRooted(_filePath)
                    ? _filePath
                    : Path.GetFullPath(_filePath);
                InternalEditorUtility.OpenFileAtLineExternal(path, _lineNumber);
                evt.StopPropagation();
            }

            private static bool TryParseLocation(string frame, out string path, out int lineNumber)
            {
                path = null;
                lineNumber = 0;

                int marker = frame.LastIndexOf("(at ", StringComparison.Ordinal);
                int end = frame.LastIndexOf(')');
                if (marker < 0 || end <= marker)
                    return false;

                int colon = frame.LastIndexOf(':', end - 1);
                if (colon <= marker + 4 || !int.TryParse(frame.Substring(colon + 1, end - colon - 1), out lineNumber))
                    return false;

                path = frame.Substring(marker + 4, colon - marker - 4);
                return !string.IsNullOrWhiteSpace(path);
            }
        }
    }
}
