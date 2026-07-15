using UnityEngine.UIElements;

namespace Bakbak.Editor
{
    public class LogToggle : VisualElement
    {
        private readonly Label _tagNameLabel;
        private readonly Toggle _toggle;

        public string TagName { get; }

        public LogToggle(string tagName, VisualTreeAsset visualTreeAsset)
        {
            TagName = tagName;
            AddToClassList("tag-toggle-item");

            if (visualTreeAsset != null)
            {
                visualTreeAsset.CloneTree(this);
            }
            else
            {
                BuildFallbackTree();
            }

            _tagNameLabel = this.Q<Label>("TagName");
            _toggle = this.Q<Toggle>("TagToggle");
            _tagNameLabel.text = tagName;
        }

        public void SetValueWithoutNotify(bool value)
        {
            _toggle.SetValueWithoutNotify(value);
        }

        public void RegisterValueChangedCallback(EventCallback<ChangeEvent<bool>> callback)
        {
            _toggle.RegisterValueChangedCallback(callback);
        }

        private void BuildFallbackTree()
        {
            VisualElement row = new VisualElement { name = "TagRow" };
            row.AddToClassList("tag-toggle");
            Add(row);

            Label label = new Label { name = "TagName" };
            label.AddToClassList("tag-toggle-label");
            row.Add(label);

            Toggle toggle = new Toggle { name = "TagToggle" };
            toggle.AddToClassList("tag-toggle-check");
            row.Add(toggle);
        }
    }
}
