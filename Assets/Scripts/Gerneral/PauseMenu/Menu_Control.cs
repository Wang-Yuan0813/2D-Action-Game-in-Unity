using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class Menu_Control : MonoBehaviour
{
    public GameObject pauseMenuBackground;
    public GameObject menuList;
    public GameObject exitOption;

    [Header("Pause Background Capture")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private Camera blurCamera;
    [SerializeField] private Image pauseBackgroundImage;
    [SerializeField, Range(1, 4)] private int renderTextureDownsample = 2;

    private GameManager gameManager;
    private RenderTexture pauseRenderTexture;
    private Material runtimeBlurMaterial;
    // Start is called before the first frame update
    void Start()
    {
        gameManager = GameManager.GetInstance();//获取GameManager
        //gameManager = GameObject.Find("GameManager").GetComponent<GameManager>();//获取GameManager
        ResolveBlurReferences();
        if (blurCamera != null)
            blurCamera.enabled = false;
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetButtonDown("Menu"))
        {
            SwitchPause(exitOption);
        }
    }
    private void SwitchPause(GameObject selectOption)
    {
        if (!pauseMenuBackground.activeSelf)
        {
            CapturePauseBackground();
            pauseMenuBackground.SetActive(true);
            menuList.SetActive(true);
            gameManager.PauseGame(true);
            EventSystem.current.SetSelectedGameObject(null);//清除选中的对象
            EventSystem.current.SetSelectedGameObject(selectOption);

        }
        else
        {
            pauseMenuBackground.SetActive(false);
            menuList.SetActive(false);
            gameManager.PauseGame(false);
        }
    }

    private void ResolveBlurReferences()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

        if (blurCamera == null && mainCamera != null)
        {
            Transform blurTransform = mainCamera.transform.Find("BlurCamera");
            if (blurTransform != null)
                blurCamera = blurTransform.GetComponent<Camera>();
        }

        if (pauseBackgroundImage == null && pauseMenuBackground != null)
            pauseBackgroundImage = pauseMenuBackground.GetComponent<Image>();

        if (pauseBackgroundImage != null && runtimeBlurMaterial == null)
        {
            Material sourceMaterial = pauseBackgroundImage.material;
            if (sourceMaterial != null)
            {
                runtimeBlurMaterial = new Material(sourceMaterial)
                {
                    name = sourceMaterial.name + " (Runtime)"
                };
                pauseBackgroundImage.material = runtimeBlurMaterial;
            }
        }
    }

    private void CapturePauseBackground()
    {
        ResolveBlurReferences();
        if (mainCamera == null || blurCamera == null || runtimeBlurMaterial == null)
            return;

        EnsureRenderTexture();

        blurCamera.transform.SetPositionAndRotation(
            mainCamera.transform.position,
            mainCamera.transform.rotation);
        blurCamera.orthographic = mainCamera.orthographic;
        blurCamera.orthographicSize = mainCamera.orthographicSize;
        blurCamera.fieldOfView = mainCamera.fieldOfView;
        blurCamera.nearClipPlane = mainCamera.nearClipPlane;
        blurCamera.farClipPlane = mainCamera.farClipPlane;
        blurCamera.clearFlags = mainCamera.clearFlags;
        blurCamera.backgroundColor = mainCamera.backgroundColor;
        blurCamera.cullingMask = mainCamera.cullingMask;
        blurCamera.targetTexture = pauseRenderTexture;

        blurCamera.Render();
        blurCamera.enabled = false;
        runtimeBlurMaterial.mainTexture = pauseRenderTexture;
    }

    private void EnsureRenderTexture()
    {
        int downsample = Mathf.Max(1, renderTextureDownsample);
        int width = Mathf.Max(1, Screen.width / downsample);
        int height = Mathf.Max(1, Screen.height / downsample);

        if (pauseRenderTexture != null &&
            pauseRenderTexture.width == width &&
            pauseRenderTexture.height == height)
        {
            return;
        }

        ReleaseRenderTexture();
        pauseRenderTexture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32)
        {
            name = "Pause Background RT",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            useMipMap = false,
            autoGenerateMips = false
        };
        pauseRenderTexture.Create();
    }

    private void ReleaseRenderTexture()
    {
        if (pauseRenderTexture == null)
            return;

        if (blurCamera != null && blurCamera.targetTexture == pauseRenderTexture)
            blurCamera.targetTexture = null;

        pauseRenderTexture.Release();
        Destroy(pauseRenderTexture);
        pauseRenderTexture = null;
    }

    private void OnDestroy()
    {
        ReleaseRenderTexture();
        if (runtimeBlurMaterial != null)
            Destroy(runtimeBlurMaterial);
    }
}
