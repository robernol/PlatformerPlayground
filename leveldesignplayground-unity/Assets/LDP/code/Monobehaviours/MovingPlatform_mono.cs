using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DevDev.LDP
{
    public class MovingPlatform_mono : MonoBehaviour
    {
#if UNITY_EDITOR
        public bool alwaysDrawGizmos = true;
        Mesh shapeMesh;
        uint shapeHash;
#endif
        public MovingPlatform data;

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            bool isSelected = false;
            Transform[] selectedTransforms = Selection.transforms;
            for (int i = 0; i < selectedTransforms.Length; i++)
            {
                Transform t = selectedTransforms[i];
                if (t == transform || t.IsChildOf(transform))
                {
                    isSelected = true;
                    break;
                }
            }

            // Draw only when this object or one of its children is selected
            if (!alwaysDrawGizmos && !isSelected)
                return;


            if(isSelected)
                Gizmos.color = Color.green;
            else 
                Gizmos.color = Color.Lerp(Color.green, Color.blue, .6f);


            //Move platform to starting position
            if (!Application.isPlaying && data.platform != null)
                data.platform.position = Procs.MovingPlatform_EvaluatePosition(data, data.loopOffset);

            switch (data.trackType)
            {
                case PlatformTrackType.CIRCULAR:
                    {
                        if (data.pingpong_waypoint_0 != null && data.pingpong_waypoint_1 != null)
                        {
                            data.pingpong_waypoint_0.gameObject.SetActive(false);
                            data.pingpong_waypoint_1.gameObject.SetActive(false);
                        }
                        Procs.GizmosDrawXYCircle(transform.position, data.circular_radius, 10);
                    }
                    break;
                case PlatformTrackType.PINGPONG:
                    {
                        if (data.pingpong_waypoint_0 != null && data.pingpong_waypoint_1 != null)
                        {
                            data.pingpong_waypoint_0.gameObject.SetActive(true);
                            data.pingpong_waypoint_1.gameObject.SetActive(true);

                            Gizmos.DrawLine(data.pingpong_waypoint_0.position, data.pingpong_waypoint_1.position);
                            if (data.platform != null)
                            {
                                Collider2D col = data.platform.GetComponent<Collider2D>();
                                if (col != null && col.gameObject.activeInHierarchy)
                                {
                                    // NOTE(stef) :: We need to do this transform manipulation to get a shape hash that does not include transform changes, and so we get a local-space mesh.
                                    //               Unfortunately wwe cannot account for scale without unparenting the object, which we can't do because it's in a prefab, but this should be less of an issue.
                                    var t = col.transform.localPosition;
                                    var r = col.transform.localRotation;
                                    col.transform.position = Vector3.zero;
                                    col.transform.rotation = Quaternion.identity;
                                    // Force the collider to update with latest transform.
                                    col.enabled = false;
                                    col.enabled = true;
                                    
                                    var shapeHash = col.GetShapeHash();
                                    if (this.shapeHash != shapeHash) {
                                        this.shapeHash = shapeHash;
                                        if (shapeMesh) DestroyImmediate(shapeMesh);
                                        shapeMesh = col.CreateMesh(false, false);
                                        shapeMesh.RecalculateNormals();
                                        shapeMesh.name = $"Gizmo: {this.name}";
                                    }
                                    
                                    col.transform.localPosition = t;
                                    col.transform.localRotation = r;
                                    // Force the collider to update with latest transform.
                                    col.enabled = false;
                                    col.enabled = true;
                                    
                                    Gizmos.DrawWireMesh(shapeMesh, data.pingpong_waypoint_0.position);
                                    Gizmos.DrawWireMesh(shapeMesh, data.pingpong_waypoint_1.position);
                                }
                            }
                        }
                    }
                    break;
            }
        }
#endif
    }
    
}
