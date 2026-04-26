#if UNITY_EDITOR
using UnityEngine;
#pragma warning disable CS0414
[DisallowMultipleComponent]
public class Comment : MonoBehaviour
{
    [SerializeField]
    [TextArea(3, 30)]
    [Tooltip("add your comment")]
    private string note = "type here";
}
#endif