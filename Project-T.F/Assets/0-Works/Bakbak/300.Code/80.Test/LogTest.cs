using Bakbak.Editor;
using System.Collections.Generic;
using UnityEngine;

public class LogTest : MonoBehaviour
{
    [ContextMenu("test")]
    private void TestLog()
    {
        LogManager.MakeLog("Test Log", "This is a test log message.", new List<string> { "Test", "Log" });
    }
}
