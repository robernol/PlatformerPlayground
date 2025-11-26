using UnityEngine;

namespace DevDev.LDP.UI
{
    // NOTE(stef): This is necessary because like many things, unity's UI button events are half-baked and don't support passing enums, let alone other data.
    //              Instead, the ui control points to this script, which then sends it's action data to the UI root.
    //              Since we're going through the trouble, we might as well support passing arbitrary data, so we pass a struct instead of an enum.
    public class UIActionSubmitter : MonoBehaviour
    {
        public Action actionData;
        public bool playAudio;
        internal UIMain uiMain;

        public void SubmitAction()
        {
            if(uiMain == null)
            {
                Main_mono main = FindAnyObjectByType<Main_mono>();
                if (main != null && main.gameState.initialized)
                    uiMain = main.gameState.uiMain;
            }

            if (uiMain != null)
            {
                uiMain.inputActions.Enqueue(actionData);
                if (playAudio)
                    uiMain.interactionAudioSource.Play();
            }
        }
    }
}
