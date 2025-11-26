using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Text = TMPro.TMP_Text;

namespace DevDev.LDP.UI
{
    public enum ActionType
    {
        START,
        RESUME,
        RESET,
        QUIT,
        SETVOL_MUSIC,
        SETVOL_SFX,
        TOGGLE_FULLSCREEN
    }

    [Serializable]
    public struct Action
    {
        public ActionType type;
        public Slider slider;
    }

    [Serializable]
    public class UIMain
    {
        public Canvas canvas;
        public Screen_LevelStart_mono screen_levelStart;
        public Screen_HUD_mono screen_hud;
        public Screen_Pause_mono screen_pause;
        public Screen_LevelComplete_mono screen_levelComplete;
        public Screen_DebugInfo_mono screen_debug;
        public UnityEngine.EventSystems.EventSystem eventSystem;
        public AudioSource interactionAudioSource;
        internal bool initialized;
        internal Queue<Action> inputActions;
    }

    [Serializable]
    public class Screen_DebugInfo
    {
        public GameObject rootGameObject;
        public BarGraph_mono frameTimeGraph;
    }

    [Serializable]
    public class BarGraph
    {
        public GameObject rootGameObject;
        public Image image;
        [Range(0, 512)]
        public int nSamples;
        internal int propID_samples;
        internal int propID_goodThreshold;
        internal int propID_cautionThreshold;
        internal int propID_highValue;
        internal int samples_lastWritten;
        internal float[] samples;
    }

    [Serializable]
    public class Screen_Pause
    {
        public GameObject rootGameObject;
        public GameObject defaultSelection;
        public Animator animator;
        public Slider musicVolumeSlider;
        public Slider sfxVolumeSlider;
        public Text elapsedTime;
        public Text collectableCount;
    }

    [Serializable]
    public class Screen_LevelStart
    {
        public GameObject rootGameObject;
        public GameObject defaultSelection;
        public Animator animator;
        public Slider musicVolumeSlider;
        public Slider sfxVolumeSlider;
    }

    [Serializable]
    public class Screen_LevelComplete
    {
        public GameObject rootGameObject;
        public GameObject defaultSelection;
        public Animator animator;
        public Text elapsedTime;
        public Text collectableCount;
    }

    [Serializable]
    public class Screen_HUD
    {
        public GameObject rootGameObject;
        public Text elapsedTime;
        public Text collectedCount;
    }
}
