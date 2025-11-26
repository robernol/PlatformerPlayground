using UnityEngine;

namespace DevDev.LDP
{
    [CreateAssetMenu(menuName = Defines.assetMenu_Root + "GameParams")]
    public class GameParams_so : ScriptableObject
    {
        public GameParams data;

        public void OnValidate()
        {
            if (data.fixedTickRate < data.frameRateMin)
                data.fixedTickRate = data.frameRateMin;

            if (data.frameRateMax > data.fixedTickRate)
                data.frameRateMax = data.fixedTickRate;

            data.dynamicRate_minTimeStep = 1f / data.frameRateMax;
            data.dynamicRate_maxTimeStep = 1f / data.frameRateMin;
            data.fixedRate_timeStep = 1f /data.fixedTickRate;
            data.fixedRate_maxTimeAccum = data.dynamicRate_minTimeStep;
        }
    }
}
