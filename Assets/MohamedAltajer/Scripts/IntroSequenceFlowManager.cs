using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class IntroSequenceFlowManager : MonoBehaviour
{
    [HideInInspector] public IntroCutsceneCamera cutsceneRig;
    [HideInInspector] public GameObject gameplayCameraObject;
    [HideInInspector] public float cutsceneCameraDepth = 0f;
    [HideInInspector] public PlayerController playerController;
    [HideInInspector] public List<MonoBehaviour> extraPlayerBehavioursToDisable = new List<MonoBehaviour>();
    [HideInInspector] public float postIntroDelay = 0f;
    [HideInInspector] public float maxIntroDuration = 0f;
    [HideInInspector] public bool playOnStart = false;
    [HideInInspector] public bool logSteps = false;

    void Awake()
    {
        playOnStart = false;

        if (cutsceneRig != null)
        {
            Camera cutsceneCam = cutsceneRig.GetComponent<Camera>();
            if (cutsceneCam != null)
                cutsceneCam.enabled = false;
            cutsceneRig.gameObject.SetActive(false);
        }

        if (gameplayCameraObject != null)
            gameplayCameraObject.SetActive(true);

        if (playerController != null)
            playerController.enabled = true;

        for (int i = 0; i < extraPlayerBehavioursToDisable.Count; i++)
        {
            MonoBehaviour mb = extraPlayerBehavioursToDisable[i];
            if (mb != null)
                mb.enabled = true;
        }

        enabled = false;
    }

    public void BeginIntroFlow() { }
}
