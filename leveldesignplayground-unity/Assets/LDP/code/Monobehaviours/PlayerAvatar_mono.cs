using UnityEngine;
using Unity.Mathematics;

namespace DevDev.LDP
{
    public class PlayerAvatar_mono : MonoBehaviour
    {
        public PlayerAvatar data;
        public void OnDrawGizmosSelected()
        {
            Pawn pawn = data.pawn;
            /// move params back into pawn from asset refs
            if (
                   pawn.rootTransform != null
                && pawn.hips != null
                && pawn.bottom != null
                && pawn.kinematicsParams != null
                && pawn.spriteRenderer != null
                && pawn.animator != null
                )
            {
                GlobalDebug.gizmos_footholds.nCommands = 0;
                float3x2 seg = 0;
                float3 up = pawn.rootTransform.up;
                float3 forward = pawn.rootTransform.right;
                Gizmos.color = Colors.coral;
                PawnKinematicsParams kinParams = pawn.kinematicsParams.data;
                Foothold foothold_near = default;
                Foothold foothold_forward = default;
                float2 gravityDir = math.normalize(Physics2D.gravity);
                float2 forwardDir = new float2(1, 0);
                float2 hipsPosition = (Vector2)pawn.hips.position;
                float halfWidth = kinParams.legReach_pawnWidth * .5f;
                RaycastHit2D[] cache_raycastHits = new RaycastHit2D[100];
                pawn.hipsHeight = hipsPosition.y - pawn.bottom.position.y;


                float forwardAdd = kinParams.legReach_extraWhenRunning * 1;
                Procs.RaycastRangeForFootholds(
                      ref foothold_near
                    , ref foothold_forward
                    , pawn
                    , kinParams
                    , hipsPosition
                    , forwardDir
                    , gravityDir
                    , rangeBehind: halfWidth + math.min(forwardAdd, 0)
                    , rangeAhead: 0
                    , skipFirstRay: false
                    , layerMask: ~0
                    , cache_raycastHits
                    );

                // search ahead
                Procs.RaycastRangeForFootholds(
                      ref foothold_near
                    , ref foothold_forward
                    , pawn
                    , kinParams
                    , hipsPosition
                    , forwardDir
                    , gravityDir
                    , rangeBehind: 0
                    , rangeAhead: halfWidth + math.max(0, forwardAdd)
                    , skipFirstRay: true
                    , layerMask: ~0
                    , cache_raycastHits
                    );

#if false
                // draw box for maximum leg reach
                {
                    seg.c0 = (float3)data.hips.position + forward * kinParams.legReach_x_max;
                    seg.c1 = seg.c0 + -up * reach_y;

                    Gizmos.DrawLine(seg.c0, seg.c1);

                    seg.c0 = seg.c1;
                    seg.c1 = seg.c0 + -forward * (kinParams.legReach_x_max + kinParams.legReach_x_behind);

                    Gizmos.DrawLine(seg.c0, seg.c1);

                    seg.c0 = seg.c1;
                    seg.c1 = seg.c0 + up * reach_y;

                    Gizmos.DrawLine(seg.c0, seg.c1);

                    seg.c0 = seg.c1;
                    seg.c1 = (float3)data.hips.position + forward * kinParams.legReach_x_max;

                    Gizmos.DrawLine(seg.c0, seg.c1);

                }

                // Draw addiitional line for center line
                {
                    seg.c0 = (float3)data.hips.position;
                    seg.c1 = seg.c0 + -up * reach_y;

                    Gizmos.DrawLine(seg.c0, seg.c1);
                }

                // Draw addiitional line for minimum reach
                {
                    seg.c0 = (float3)data.hips.position + forward * kinParams.legReach_x_min;
                    seg.c1 = seg.c0 + -up * reach_y;

                    Gizmos.DrawLine(seg.c0, seg.c1);
                }
#endif

            }
        }
    }
}

