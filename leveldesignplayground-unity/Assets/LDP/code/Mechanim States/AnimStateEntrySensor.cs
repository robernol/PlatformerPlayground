using UnityEngine;

namespace DevDev.LDP
{
    public class AnimStateEntrySensor : StateMachineBehaviour
    {
        // NOTE(stef): Unfortunately, there is no apparent way to get the name of the state that this is attahed to from outside of the message overrides below, so we need to manually this in the editor
        public string sensorName;
        public bool stateEntered;

        public bool ConsumeFlag()
        {
            bool ret = stateEntered;
            stateEntered = false;
            return ret;
        }

        public override void OnStateMachineEnter(Animator animator, int stateMachinePathHash)
        {
            stateEntered = false;
        }

        // OnStateEnter is called when a transition starts and the state machine starts to evaluate this state
        override public void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            stateEntered = true;
            // Force animation state to finish. Apparently no other way to get a zero-length state.
            animator.ForceStateNormalizedTime(1f);
            // The "correct" way to do it, doen't work. Animation state will not transition and gets stuck.
            // animator.Play(stateInfo.fullPathHash, layerIndex, 1f);
        }
    }
}
