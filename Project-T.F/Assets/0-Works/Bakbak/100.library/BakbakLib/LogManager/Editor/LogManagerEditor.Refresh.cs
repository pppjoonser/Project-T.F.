using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bakbak.Editor
{
    public partial class LogManagerEditor
    {
        private void ConfigureLogList()
        {
            _logView.itemsSource = _displayLogs;
            _logView.fixedItemHeight = 48f;
            _logView.virtualizationMethod = CollectionVirtualizationMethod.FixedHeight;
            _logView.selectionType = SelectionType.Single;
            _logView.makeItem = () => new LogPannel(default, logPannelTreeAsset);
            _logView.bindItem = (element, index) =>
            {
                DisplayLog item = _displayLogs[index];
                ((LogPannel)element).SetLog(item.Log, item.Count);
            };
            _logView.RegisterCallback<PointerDownEvent>(OnLogPointerDown, TrickleDown.TrickleDown);
            _logView.selectedIndicesChanged += OnLogSelectedIndicesChanged;
        }

        private void OnLogPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0 || evt.target is not VisualElement target)
                return;

            LogPannel row = target as LogPannel ?? target.GetFirstAncestorOfType<LogPannel>();
            if (row == null)
                return;

            SetDetail(row.LogInfo);
            _detailPannel.MarkDirtyRepaint();
            _detailPane.MarkDirtyRepaint();
        }

        private void RefreshAll()
        {
            EnqueueRefresh(RefreshRequest.Tags);
            EnqueueRefresh(RefreshRequest.Logs);
        }

        public void RefreshList()
        {
            EnqueueRefresh(RefreshRequest.Tags);
            EnqueueRefresh(RefreshRequest.Logs);
        }

        private void EnqueueRefresh(RefreshRequest request)
        {
            switch (request)
            {
                case RefreshRequest.Tags when _tagsRefreshQueued:
                case RefreshRequest.Logs when _logsRefreshQueued:
                    return;
                case RefreshRequest.Tags:
                    _tagsRefreshQueued = true;
                    break;
                case RefreshRequest.Logs:
                    _logsRefreshQueued = true;
                    break;
            }

            _refreshQueue.Enqueue(request);

            if (_refreshScheduled)
                return;

            _refreshScheduled = true;
            EditorApplication.delayCall += ProcessRefreshQueue;
        }

        private void ProcessRefreshQueue()
        {
            EditorApplication.delayCall -= ProcessRefreshQueue;
            _refreshScheduled = false;

            bool refreshTags = false;
            bool refreshLogs = false;

            while (_refreshQueue.Count > 0)
            {
                switch (_refreshQueue.Dequeue())
                {
                    case RefreshRequest.Tags:
                        _tagsRefreshQueued = false;
                        refreshTags = true;
                        break;
                    case RefreshRequest.Logs:
                        _logsRefreshQueued = false;
                        refreshLogs = true;
                        break;
                }
            }

            if (refreshTags && _tagView != null)
                RefreshTags();

            if (refreshLogs && _logView != null)
                RefreshLogs();
        }

        private void RefreshTags()
        {
            _tagView?.Clear();
            _tagToggles.Clear();

            string search = _tagSearchField?.value ?? string.Empty;
            IEnumerable<string> tags = LogManager.GetTags();
            if (!string.IsNullOrWhiteSpace(search))
            {
                tags = tags.Where(tag => tag.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            foreach (string tag in tags)
            {
                LogToggle toggle = new LogToggle(tag, logToggleTreeAsset);
                toggle.SetValueWithoutNotify(_selectedTags.Contains(tag));
                toggle.RegisterValueChangedCallback(evt =>
                {
                    if (evt.newValue)
                        _selectedTags.Add(toggle.TagName);
                    else
                        _selectedTags.Remove(toggle.TagName);

                    EnqueueRefresh(RefreshRequest.Logs);
                });

                _tagToggles.Add(toggle);
                _tagView.Add(toggle);
            }
        }

        private void RefreshLogs()
        {
            if (_logView == null)
                return;

            LogManager.FindLogs(
                _filteredLogs,
                tagFilters: _selectedTags,
                searchFilter: _logSearchField?.value ?? string.Empty);

            BuildDisplayLogs();
            _logView.Rebuild();

            bool isEmpty = _displayLogs.Count == 0;
            _emptyLogsLabel.style.display = isEmpty ? DisplayStyle.Flex : DisplayStyle.None;

            if (isEmpty)
            {
                _selectionIndices.Clear();
                _logView.SetSelectionWithoutNotify(_selectionIndices);
                ClearDetail();
                return;
            }

            int selectedIndex = FindSelectedLogIndex();
            if (selectedIndex < 0)
            {
                selectedIndex = 0;
                SetDetail(_displayLogs[selectedIndex].Log);
            }

            _selectionIndices.Clear();
            _selectionIndices.Add(selectedIndex);
            _logView.SetSelectionWithoutNotify(_selectionIndices);
        }

        private int FindSelectedLogIndex()
        {
            if (!_selectedSequenceId.HasValue)
                return -1;

            long sequenceId = _selectedSequenceId.Value;
            for (int i = 0; i < _displayLogs.Count; i++)
            {
                if (_displayLogs[i].Log.sequenceId == sequenceId)
                    return i;
            }

            if (collapseLogs && _selectedLog.HasValue)
            {
                LogInfo selectedLog = _selectedLog.Value;
                for (int i = 0; i < _displayLogs.Count; i++)
                {
                    if (LogsMatchForCollapse(_displayLogs[i].Log, selectedLog))
                        return i;
                }
            }

            return -1;
        }

        private void BuildDisplayLogs()
        {
            _displayLogs.Clear();

            if (!collapseLogs)
            {
                foreach (LogInfo log in _filteredLogs)
                    _displayLogs.Add(new DisplayLog(log, 1));

                return;
            }

            _collapseIndices.Clear();
            foreach (LogInfo log in _filteredLogs)
            {
                CollapseKey key = new CollapseKey(log);
                if (_collapseIndices.TryGetValue(key, out int index))
                {
                    DisplayLog existing = _displayLogs[index];
                    _displayLogs[index] = new DisplayLog(existing.Log, existing.Count + 1);
                    continue;
                }

                _collapseIndices.Add(key, _displayLogs.Count);
                _displayLogs.Add(new DisplayLog(log, 1));
            }
        }

        private static bool LogsMatchForCollapse(LogInfo left, LogInfo right)
        {
            if (!string.Equals(left.loggerName, right.loggerName, StringComparison.Ordinal) ||
                !string.Equals(left.message, right.message, StringComparison.Ordinal) ||
                !string.Equals(left.stackTrace, right.stackTrace, StringComparison.Ordinal) ||
                left.threadId != right.threadId)
            {
                return false;
            }

            if (ReferenceEquals(left.logTypes, right.logTypes))
                return true;

            if (left.logTypes == null || right.logTypes == null || left.logTypes.Length != right.logTypes.Length)
                return false;

            for (int i = 0; i < left.logTypes.Length; i++)
            {
                if (!string.Equals(left.logTypes[i], right.logTypes[i], StringComparison.Ordinal))
                    return false;
            }

            return true;
        }

        private void OnLogSelectedIndicesChanged(IEnumerable<int> indices)
        {
            foreach (int index in indices)
            {
                if ((uint)index >= (uint)_displayLogs.Count)
                    continue;

                LogInfo log = _displayLogs[index].Log;
                if (_selectedSequenceId != log.sequenceId)
                    SetDetail(log);

                return;
            }
        }

        private void SetDetail(LogInfo log)
        {
            _selectedSequenceId = log.sequenceId;
            _selectedLog = log;
            _detailPannel.SetLog(log);
            _detailHost.scrollOffset = Vector2.zero;
        }

        private void ClearDetail()
        {
            _selectedSequenceId = null;
            _selectedLog = null;
            _detailPannel.ClearLog();
        }
    }
}
