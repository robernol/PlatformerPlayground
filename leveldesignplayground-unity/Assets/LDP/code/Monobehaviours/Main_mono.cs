using UnityEngine;

namespace DevDev.LDP
{
    public class Main_mono : MonoBehaviour
    {
        public GameParams_so gameParams;
        internal Game gameState;
        public bool drawGizmos;

        void Awake()
        {
            gameState = new Game();
        }

        void Update()
        {
            Procs.Main_Update(ref gameState, ref gameParams.data);
        }

        void OnDrawGizmos()
        {
            if (drawGizmos)
            {
                Procs.DrawGizmosBuffer(GlobalDebug.gizmos_navLineBuild);
                Procs.DrawGizmosBuffer(GlobalDebug.gizmos_footholds);
            }
        }        
    }
}
