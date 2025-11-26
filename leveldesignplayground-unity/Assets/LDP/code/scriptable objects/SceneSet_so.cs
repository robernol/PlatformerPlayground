using UnityEngine;

namespace DevDev.LDP
{
    [CreateAssetMenu(menuName = Defines.assetMenu_Root + "SceneSet")]
    public class SceneSet_so : ScriptableObject
    {
        public SceneSet data;

#if UNITY_EDITOR
        public void OnValidate()
        {
            Procs.ValidateSceneSet(ref data);
        }
#endif
    }
}