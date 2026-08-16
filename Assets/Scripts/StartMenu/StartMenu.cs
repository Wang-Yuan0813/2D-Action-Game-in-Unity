using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;


public class StartMenu : MonoBehaviour
{
    static StartMenu instance;
    public GameObject firstChose;
    public GameObject buttons;

    [Header("Button Selection Sound")]
    [Tooltip("MP3 imported into Unity. Drag the AudioClip here.")]
    [SerializeField] private AudioClip buttonSelectedClip;
    [SerializeField, Range(0f, 1f)] private float buttonSelectedVolume = 1f;
    [Tooltip("Whether the initially selected button should play the sound when the scene opens.")]
    [SerializeField] private bool playInitialSelectionSound;
    [Tooltip("Prevents Pointer Enter and Select from playing the same sound twice.")]
    [SerializeField, Min(0f)] private float selectionSoundDebounce = 0.05f;
    [Tooltip("Optional. If left empty, a 2D AudioSource is created automatically at runtime.")]
    [SerializeField] private AudioSource uiAudioSource;

    private GameObject lastSoundButton;
    private float lastSelectionSoundTime = float.NegativeInfinity;
    private bool suppressInitialSelectionSound;
    private void Awake()
    {
        if (instance != null)
        {
            Destroy(this);
            return;
        }

        instance = this;

        if (uiAudioSource == null)
        {
            uiAudioSource = GetComponent<AudioSource>();
            if (uiAudioSource == null)
                uiAudioSource = gameObject.AddComponent<AudioSource>();
        }

        uiAudioSource.playOnAwake = false;
        uiAudioSource.loop = false;
        uiAudioSource.spatialBlend = 0f;

    }

    private void Start()
    {
        suppressInitialSelectionSound = !playInitialSelectionSound;
        EventSystem.current.SetSelectedGameObject(null);//清除选中的对象
        EventSystem.current.SetSelectedGameObject(firstChose);
    }

    public static void PlayButtonSelectedSound(GameObject selectedButton)
    {
        if (instance == null)
            return;

        instance.PlayButtonSelectedSoundInternal(selectedButton);
    }

    private void PlayButtonSelectedSoundInternal(GameObject selectedButton)
    {
        if (suppressInitialSelectionSound)
        {
            suppressInitialSelectionSound = false;
            return;
        }

        if (buttonSelectedClip == null || uiAudioSource == null)
            return;

        bool isDuplicateEvent = selectedButton == lastSoundButton &&
                                Time.unscaledTime - lastSelectionSoundTime < selectionSoundDebounce;
        if (isDuplicateEvent)
            return;

        lastSoundButton = selectedButton;
        lastSelectionSoundTime = Time.unscaledTime;
        uiAudioSource.PlayOneShot(buttonSelectedClip, buttonSelectedVolume);
    }

    public static void CloseChosenBlock()//关闭所有的选中框
    {
        for (int i = 0; i < instance.buttons.transform.childCount; i++)
        {
            instance.buttons.transform.GetChild(i).gameObject.transform.GetChild(0).gameObject.SetActive(false);
            Color color = instance.buttons.transform.GetChild(i).gameObject.GetComponent<Image>().color;
            color.a = 0.2f;
            instance.buttons.transform.GetChild(i).gameObject.GetComponent<Image>().color = color;
        }

    }
}
