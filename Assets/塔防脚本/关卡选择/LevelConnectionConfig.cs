using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "LevelConnectionConfig", menuName = "魔塔/关卡连线配置", order = 2)]
public class LevelConnectionConfig : ScriptableObject
{
    [Serializable]
    public class Connection
    {
        [Tooltip("从第几关")]
        [Range(1, 16)]
        public int from = 1;

        [Tooltip("连接到第几关")]
        [Range(1, 16)]
        public int to = 2;
    }

    [Tooltip("定义所有关卡之间的连线关系。通关 from 关卡后解锁 to 关卡。")]
    public List<Connection> connections = new List<Connection>();

    public List<int> GetConnectionsFrom(int fromLevel)
    {
        var result = new List<int>();
        if (connections == null) return result;
        foreach (var conn in connections)
        {
            if (conn.from == fromLevel && !result.Contains(conn.to))
                result.Add(conn.to);
        }
        return result;
    }

    public List<int> GetConnectionsTo(int toLevel)
    {
        var result = new List<int>();
        if (connections == null) return result;
        foreach (var conn in connections)
        {
            if (conn.to == toLevel && !result.Contains(conn.from))
                result.Add(conn.from);
        }
        return result;
    }

    public bool HasAnyConnectionTo(int level)
    {
        if (connections == null) return false;
        foreach (var conn in connections)
        {
            if (conn.to == level) return true;
        }
        return false;
    }
}
