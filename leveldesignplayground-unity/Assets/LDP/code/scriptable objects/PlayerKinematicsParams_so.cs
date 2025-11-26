using UnityEngine;

namespace DevDev.LDP
{
    [CreateAssetMenu(menuName = Defines.assetMenu_Root + Defines.assetMenu_GameplayParams + "PlayerKinematicsParams")]
    public class PlayerKinematicsParams_so : ScriptableObject
    {
        public PawnKinematicsParams data;
    }
}
