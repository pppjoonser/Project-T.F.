using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bakbak.Editor
{
    public class LogToggle : EditorWindow
    {
        [SerializeField] private VisualTreeAsset visualTreeAsset = default;
    }
}