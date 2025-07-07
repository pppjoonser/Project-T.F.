using System;
using System.Collections.Generic;
using System.IO;
using Bakbak.Editor;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
public class LogManager : EditorWindow
{
    [SerializeField] private VisualTreeAsset visualTreeAsset = default;
    [SerializeField] private VisualTreeAsset itemAsset;
}
