using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Path : MonoBehaviour
{
    public GameObject[] wayPoint;
    public Vector3 GetPosition(int index)
    {
        if (wayPoint == null) return transform.position;

        if (index < 0 || index >= wayPoint.Length) return transform.position;

        if (wayPoint[index] == null) return transform.position;

        return wayPoint[index].transform.position;
    }
    private void OnDrawGizmos()
    {
        if (wayPoint != null && wayPoint.Length > 0)
        {
            for(int i = 0;i<wayPoint.Length;i++)
            {
                if (wayPoint[i] == null) continue;

#if UNITY_EDITOR
                // 仅在编辑器里画点位名称（避免打包时报 UnityEditor 相关错误）
                UnityEditor.Handles.Label(
                    wayPoint[i].transform.position + Vector3.up * 0.7f,
                    wayPoint[i].name
                );
#endif
                if(i<wayPoint.Length-1)
                {
                    Gizmos.color = Color.yellow;
                    if (wayPoint[i + 1] != null)
                    {
                        Gizmos.DrawLine(wayPoint[i].transform.position, wayPoint[i+1].transform.position);
                    }
                }
                
            }
        }
    }
}
