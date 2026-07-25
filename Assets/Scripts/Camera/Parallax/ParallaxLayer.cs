using System;
using UnityEngine;

[Serializable]
public sealed class ParallaxLayer
{
    [Tooltip("需要产生视差移动的背景根节点。")]
    [SerializeField] private Transform layerRoot;

    [Tooltip("背景在屏幕上的移动比例。0 表示固定在屏幕上，1 表示与普通世界一致，大于 1 表示比普通世界移动得更快。")]
    [SerializeField] private Vector2 screenMotionRatio = new Vector2(0.25f, 1f);

    private Vector3 authoredLocalPosition;
    private bool hasCapturedPosition;

    public Transform LayerRoot => layerRoot;

    public void CaptureAuthoredPosition()
    {
        if (layerRoot == null)
        {
            return;
        }

        authoredLocalPosition = layerRoot.localPosition;
        hasCapturedPosition = true;
    }

    public void RestoreAuthoredPosition()
    {
        if (layerRoot == null || !hasCapturedPosition)
        {
            return;
        }

        layerRoot.localPosition = authoredLocalPosition;
    }

    public void ApplyCameraDisplacement(Vector3 cameraDisplacement)
    {
        if (layerRoot == null || !hasCapturedPosition)
        {
            return;
        }

        Vector3 worldOffset = new Vector3(
            cameraDisplacement.x * (1f - screenMotionRatio.x),
            cameraDisplacement.y * (1f - screenMotionRatio.y),
            0f);

        Transform parent = layerRoot.parent;
        Vector3 localOffset = parent == null
            ? worldOffset
            : parent.InverseTransformVector(worldOffset);

        layerRoot.localPosition = authoredLocalPosition + localOffset;
    }
}
