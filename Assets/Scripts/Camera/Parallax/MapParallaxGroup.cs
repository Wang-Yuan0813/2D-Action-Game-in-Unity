using System.Collections.Generic;
using UnityEngine;

public sealed class MapParallaxGroup : MonoBehaviour
{
    [Header("地图识别")]
    [Tooltip("仅用于在 Inspector 和日志中区分地图。")]
    [SerializeField] private string mapId;

    [Tooltip("用于自动判断相机是否进入该地图。可以使用 BoxCollider2D，并勾选 Is Trigger。")]
    [SerializeField] private Collider2D activationBoundary;

    [Tooltip("该地图固定的视差参考点。参考对象位于此处时，背景保持编辑器中记录的位置。")]
    [SerializeField] private Transform parallaxOrigin;

    [Header("视差层")]
    [SerializeField] private List<ParallaxLayer> layers = new List<ParallaxLayer>();

    [Header("非活动地图")]
    [Tooltip("启用后，非当前地图的视差视觉根节点会被关闭。MapParallaxGroup 和地图边界不要放进这个节点。")]
    [SerializeField] private bool hideVisualsWhileInactive;

    [SerializeField] private GameObject visualsRoot;

    private bool hasCapturedLayers;
    private bool isActiveMap;
    private bool hasWarnedMissingOrigin;

    public string MapId => string.IsNullOrWhiteSpace(mapId) ? name : mapId;
    public bool IsActiveMap => isActiveMap;
    public bool HasActivationBoundary => activationBoundary != null;
    public Transform ParallaxOrigin => parallaxOrigin;
    public bool HasParallaxOrigin => parallaxOrigin != null;

    private void Awake()
    {
        CaptureLayerPositions();
    }

    public bool ContainsWorldPoint(Vector3 worldPoint)
    {
        return activationBoundary != null
            && activationBoundary.enabled
            && activationBoundary.OverlapPoint(worldPoint);
    }

    public void Activate(Vector3 movementReferencePosition)
    {
        EnsureLayersCaptured();

        if (hideVisualsWhileInactive && visualsRoot != null)
        {
            visualsRoot.SetActive(true);
        }

        RestoreLayerPositions();
        isActiveMap = true;
        UpdateParallax(movementReferencePosition);
    }

    public void Deactivate()
    {
        isActiveMap = false;

        if (hideVisualsWhileInactive && visualsRoot != null)
        {
            visualsRoot.SetActive(false);
        }
    }

    public void RefreshParallax(Vector3 movementReferencePosition)
    {
        EnsureLayersCaptured();
        UpdateParallax(movementReferencePosition);
    }

    public void UpdateParallax(Vector3 movementReferencePosition)
    {
        if (!isActiveMap)
        {
            return;
        }

        if (parallaxOrigin == null)
        {
            if (!hasWarnedMissingOrigin)
            {
                Debug.LogWarning(
                    $"{name} 没有配置 Parallax Origin，已跳过该地图的视差计算。",
                    this);
                hasWarnedMissingOrigin = true;
            }

            return;
        }

        hasWarnedMissingOrigin = false;
        Vector3 cameraDisplacement =
            movementReferencePosition - parallaxOrigin.position;

        for (int i = 0; i < layers.Count; i++)
        {
            layers[i]?.ApplyCameraDisplacement(cameraDisplacement);
        }
    }

    [ContextMenu("重新记录当前背景位置")]
    public void CaptureLayerPositions()
    {
        for (int i = 0; i < layers.Count; i++)
        {
            layers[i]?.CaptureAuthoredPosition();
        }

        hasCapturedLayers = true;
    }

    [ContextMenu("恢复记录的背景位置")]
    public void RestoreLayerPositions()
    {
        EnsureLayersCaptured();

        for (int i = 0; i < layers.Count; i++)
        {
            layers[i]?.RestoreAuthoredPosition();
        }
    }

    private void EnsureLayersCaptured()
    {
        if (!hasCapturedLayers)
        {
            CaptureLayerPositions();
        }
    }

    private void OnValidate()
    {
        if (visualsRoot == gameObject)
        {
            Debug.LogWarning(
                $"{nameof(MapParallaxGroup)} 的 Visuals Root 不能设置为自身，否则会连同地图边界一起关闭。",
                this);
            visualsRoot = null;
        }

        if (parallaxOrigin != null && parallaxOrigin.IsChildOf(transform))
        {
            for (int i = 0; i < layers.Count; i++)
            {
                Transform layerRoot = layers[i]?.LayerRoot;
                if (layerRoot != null && parallaxOrigin.IsChildOf(layerRoot))
                {
                    Debug.LogWarning(
                        $"{name} 的 Parallax Origin 不能放在会移动的视差层下面。",
                        this);
                    break;
                }
            }
        }
    }
}
