#if UNITY_EDITOR
using UnityEngine;
#pragma warning disable CS0414
[DisallowMultipleComponent]
public class Comment : MonoBehaviour
{
    [SerializeField]
    [TextArea(3, 30)]
    [Tooltip("순수 주석용 스크립트")]
    private string note = "type here";
}
#endif