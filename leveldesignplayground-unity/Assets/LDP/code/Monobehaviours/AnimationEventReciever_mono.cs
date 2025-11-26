using UnityEngine;

public class AnimationEventReciever_mono : MonoBehaviour
{
    internal bool flagged;
    public void SetFlag()
    {
        flagged = true;
    }
    internal bool ConsumeFlag()
    {
        if(flagged)
        {
            flagged = false;
            return true;
        }
        return false;
    }

}
