using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(1000)]
public sealed class ParallaxManager : MonoBehaviour
{
    [Header("移动参考")]
    [Tooltip("用于判断当前地图的输出相机。为空时自动使用 Camera.main。")]
    [SerializeField] private Camera outputCamera;

    [Tooltip("用于计算视差位移的参考对象。为空时使用输出相机。若要排除镜头震动，可以改为无震动的相机跟随锚点。")]
    [SerializeField] private Transform movementReference;

    [Header("地图")]
    [SerializeField] private List<MapParallaxGroup> mapGroups =
        new List<MapParallaxGroup>();

    [Tooltip("进入 Play Mode 后首先激活的地图。为空时根据相机所在边界自动选择。")]
    [SerializeField] private MapParallaxGroup initialMap;

    [Tooltip("自动搜索场景中未手动加入列表的 MapParallaxGroup，包括非活动对象。")]
    [SerializeField] private bool includeSceneMapGroups = true;

    [Tooltip("根据输出相机中心所在的地图边界自动切换地图。")]
    [SerializeField] private bool autoSwitchByCameraBoundary = true;

    [Header("调试")]
    [SerializeField] private bool logMapChanges;

    private MapParallaxGroup activeMap;
    private bool teleportSyncPending;
    private MapParallaxGroup pendingTeleportMap;

    public MapParallaxGroup ActiveMap => activeMap;

    private void Awake()
    {
        if (outputCamera == null)
        {
            outputCamera = Camera.main;
        }

        if (includeSceneMapGroups)
        {
            AddSceneMapGroups();
        }
    }

    private void Start()
    {
        DeactivateAllMaps();

        MapParallaxGroup firstMap = initialMap;
        if (firstMap == null && outputCamera != null)
        {
            firstMap = FindMapContaining(outputCamera.transform.position);
        }

        if (firstMap != null)
        {
            ActivateMap(firstMap);
        }
    }

    private void LateUpdate()
    {
        if (outputCamera == null)
        {
            outputCamera = Camera.main;
            if (outputCamera == null)
            {
                return;
            }
        }

        ProcessPendingTeleportSync();

        if (autoSwitchByCameraBoundary)
        {
            TrySwitchMapByCameraPosition();
        }

        if (activeMap != null)
        {
            activeMap.UpdateParallax(GetMovementReferencePosition());
        }
    }

    public void ActivateMap(MapParallaxGroup mapGroup)
    {
        if (mapGroup == null)
        {
            return;
        }

        if (mapGroup == activeMap)
        {
            return;
        }

        if (!mapGroups.Contains(mapGroup))
        {
            mapGroups.Add(mapGroup);
        }

        if (activeMap != null && activeMap != mapGroup)
        {
            activeMap.Deactivate();
        }

        activeMap = mapGroup;
        activeMap.Activate(GetMovementReferencePosition());

        if (logMapChanges)
        {
            Debug.Log($"视差地图已切换到：{activeMap.MapId}", activeMap);
        }
    }

    public void RefreshActiveMap()
    {
        if (activeMap != null)
        {
            activeMap.RefreshParallax(GetMovementReferencePosition());
        }
    }

    public void NotifyCameraTeleported()
    {
        if (outputCamera == null)
        {
            outputCamera = Camera.main;
        }

        if (autoSwitchByCameraBoundary && outputCamera != null)
        {
            MapParallaxGroup destinationMap =
                FindMapContaining(outputCamera.transform.position);

            if (destinationMap != null && destinationMap != activeMap)
            {
                ActivateMap(destinationMap);
                return;
            }
        }

        RefreshActiveMap();
    }

    public void ScheduleTeleportSync(MapParallaxGroup destinationMap = null)
    {
        pendingTeleportMap = destinationMap;
        teleportSyncPending = true;
    }

    private void TrySwitchMapByCameraPosition()
    {
        Vector3 cameraPosition = outputCamera.transform.position;

        if (activeMap != null && activeMap.ContainsWorldPoint(cameraPosition))
        {
            return;
        }

        MapParallaxGroup nextMap = FindMapContaining(cameraPosition);
        if (nextMap != null && nextMap != activeMap)
        {
            ActivateMap(nextMap);
        }
    }

    private MapParallaxGroup FindMapContaining(Vector3 worldPoint)
    {
        for (int i = 0; i < mapGroups.Count; i++)
        {
            MapParallaxGroup mapGroup = mapGroups[i];
            if (mapGroup != null && mapGroup.ContainsWorldPoint(worldPoint))
            {
                return mapGroup;
            }
        }

        return null;
    }

    private Vector3 GetMovementReferencePosition()
    {
        if (movementReference != null)
        {
            return movementReference.position;
        }

        return outputCamera != null ? outputCamera.transform.position : Vector3.zero;
    }

    private void AddSceneMapGroups()
    {
        MapParallaxGroup[] sceneGroups =
            FindObjectsOfType<MapParallaxGroup>(true);

        for (int i = 0; i < sceneGroups.Length; i++)
        {
            if (!mapGroups.Contains(sceneGroups[i]))
            {
                mapGroups.Add(sceneGroups[i]);
            }
        }
    }

    private void ProcessPendingTeleportSync()
    {
        if (!teleportSyncPending)
        {
            return;
        }

        teleportSyncPending = false;
        MapParallaxGroup destinationMap = pendingTeleportMap;
        pendingTeleportMap = null;

        if (destinationMap != null)
        {
            if (destinationMap == activeMap)
            {
                RefreshActiveMap();
            }
            else
            {
                ActivateMap(destinationMap);
            }
        }
        else
        {
            NotifyCameraTeleported();
        }
    }

    private void DeactivateAllMaps()
    {
        for (int i = 0; i < mapGroups.Count; i++)
        {
            mapGroups[i]?.Deactivate();
        }
    }
}
