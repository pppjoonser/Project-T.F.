using UnityEngine.UIElements;

namespace Bakbak.Editor
{
    public class LogPannel : VisualElement
    {
        private readonly VisualElement _row;
        private readonly Label _titleLabel;
        private readonly Label _messageLabel;
        private readonly Label _timeLabel;
        private readonly Label _countLabel;

        public LogInfo LogInfo { get; private set; }

        public LogPannel(LogInfo logInfo, VisualTreeAsset visualTreeAsset)
        {
            LogInfo = logInfo;

            if (visualTreeAsset != null)
            {
                visualTreeAsset.CloneTree(this);
            }
            else
            {
                BuildFallbackTree();
            }

            _row = this.Q<VisualElement>("LogRow");
            _titleLabel = this.Q<Label>("LogTitle");
            _messageLabel = this.Q<Label>("LogMessage");
            _timeLabel = this.Q<Label>("LogTime");
            _countLabel = this.Q<Label>("LogCount");

            SetLog(logInfo);
        }

        public void SetLog(LogInfo logInfo)
        {
            SetLog(logInfo, 1);
        }

        public void SetLog(LogInfo logInfo, int count)
        {
            LogInfo = logInfo;
            _titleLabel.text = string.IsNullOrEmpty(logInfo.loggerName) ? "(No Title)" : logInfo.loggerName;
            _messageLabel.text = string.IsNullOrEmpty(logInfo.message) ? "(No Message)" : logInfo.message;
            _timeLabel.text = logInfo.timestamp.ToLocalTime().ToString("HH:mm:ss");
            _countLabel.text = $"×{count}";
            _countLabel.style.display = count > 1
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        }

        public void SetSelected(bool selected)
        {
            _row.EnableInClassList("selected", selected);
        }

        private void BuildFallbackTree()
        {
            VisualElement row = new VisualElement { name = "LogRow" };
            row.AddToClassList("log-row");
            Add(row);

            VisualElement icon = new VisualElement { name = "LogIcon" };
            icon.AddToClassList("log-row-icon");
            row.Add(icon);

            VisualElement content = new VisualElement { name = "LogContent" };
            content.AddToClassList("log-row-content");
            row.Add(content);

            Label titleLabel = new Label { name = "LogTitle" };
            titleLabel.AddToClassList("log-row-title");
            content.Add(titleLabel);

            Label messageLabel = new Label { name = "LogMessage" };
            messageLabel.AddToClassList("log-row-message");
            content.Add(messageLabel);

            Label timeLabel = new Label { name = "LogTime" };
            timeLabel.AddToClassList("log-row-time");
            row.Add(timeLabel);

            Label countLabel = new Label { name = "LogCount" };
            countLabel.AddToClassList("log-row-count");
            row.Add(countLabel);
        }
    }
}
