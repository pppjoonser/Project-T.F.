using Bakbak.Editor;
using System.Collections.Generic;
using UnityEngine;

public class LogTest : MonoBehaviour
{
    [ContextMenu("testLog")]
    private void TestLog()
    {
        for (int i = 0; i < 150000; i++)
        {
            LogManager.MakeLog("Test Log", "This is a test log message.", "Test");
        }
    }

    private void Start()
    {
        TestLog();
    }

    [ContextMenu("normalLog")]
    private void NormalLog()
    {
        Debug.Log("This is a normal log message.");
    }
}
