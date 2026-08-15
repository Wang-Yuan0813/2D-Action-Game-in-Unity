using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Boss Speech", menuName = "Boss/Speech Data")]
public sealed class BossSpeechData : ScriptableObject
{
    [TextArea(1, 3)] public List<string> lines = new List<string>();
}
