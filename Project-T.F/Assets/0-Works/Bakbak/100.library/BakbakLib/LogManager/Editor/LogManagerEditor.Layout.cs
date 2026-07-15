using UnityEngine;
using UnityEngine.UIElements;

namespace Bakbak.Editor
{
    public partial class LogManagerEditor
    {
        private void OnCopyShortcut(KeyDownEvent evt)
        {
            if ((!evt.ctrlKey && !evt.commandKey) || evt.keyCode != KeyCode.C || evt.target is TextField)
                return;

            if (_detailPannel.CopyCurrentLog())
                evt.StopPropagation();
        }

        private void OnWindowGeometryChanged(GeometryChangedEvent evt)
        {
            UpdateResponsiveLayout(evt.newRect.width);
        }

        private void UpdateResponsiveLayout(float width)
        {
            if (float.IsNaN(width) || width <= 0f)
                return;

            bool useCompactLayout = width < CompactLayoutWidth;
            if (useCompactLayout == _compactLayoutActive)
                return;

            _compactLayoutActive = useCompactLayout;

            if (useCompactLayout)
            {
                _tagsPane.RemoveFromHierarchy();
                _logsPane.RemoveFromHierarchy();
                _detailPane.RemoveFromHierarchy();
                _compactPaneHost.Add(_tagsPane);
                _compactLogsSlot.Add(_logsPane);
                _compactDetailSlot.Add(_detailPane);
                _tagsPane.style.width = Length.Percent(100f);

                _outerSplit.style.display = DisplayStyle.None;
                _compactLayout.style.display = DisplayStyle.Flex;
                _compactTabs.style.display = DisplayStyle.Flex;
                _compactSearchHost.style.display = DisplayStyle.Flex;
                _rootContainer.EnableInClassList("compact-mode", true);
                _tagsPane.EnableInClassList("compact-tags-pane", true);
                _tagView.mode = ScrollViewMode.Vertical;
                SetCompactTab(_compactTagsTabActive);
                return;
            }

            _tagsPane.RemoveFromHierarchy();
            _logsPane.RemoveFromHierarchy();
            _detailPane.RemoveFromHierarchy();
            _tagsPane.style.width = new StyleLength(StyleKeyword.Null);
            _tagSearchRow.RemoveFromHierarchy();
            _logSearchRow.RemoveFromHierarchy();
            _desktopTagsSlot.Add(_tagsPane);
            _desktopLogsSlot.Add(_logsPane);
            _desktopDetailSlot.Add(_detailPane);
            _tagsPane.Insert(1, _tagSearchRow);
            _logsPane.Insert(1, _logSearchRow);

            _compactLayout.style.display = DisplayStyle.None;
            _compactTabs.style.display = DisplayStyle.None;
            _compactSearchHost.style.display = DisplayStyle.None;
            _outerSplit.style.display = DisplayStyle.Flex;
            _rootContainer.EnableInClassList("compact-mode", false);
            _tagsPane.EnableInClassList("compact-tags-pane", false);
            _tagView.mode = ScrollViewMode.Vertical;
            _tagsPane.style.display = DisplayStyle.Flex;
            _logsPane.style.display = DisplayStyle.Flex;
            _detailPane.style.display = DisplayStyle.Flex;
        }

        private void SetCompactTab(bool showTags)
        {
            _compactTagsTabActive = showTags;

            if (!_compactLayoutActive)
                return;

            _compactLogsTab.EnableInClassList("selected", !showTags);
            _compactTagsTab.EnableInClassList("selected", showTags);
            _tagSearchRow.RemoveFromHierarchy();
            _logSearchRow.RemoveFromHierarchy();
            _compactSearchHost.Add(showTags ? _tagSearchRow : _logSearchRow);
            _tagsPane.style.display = showTags ? DisplayStyle.Flex : DisplayStyle.None;
            _compactLogDetailSplit.style.display = showTags ? DisplayStyle.None : DisplayStyle.Flex;
            _logsPane.style.display = DisplayStyle.Flex;
            _detailPane.style.display = DisplayStyle.Flex;
        }
    }
}
