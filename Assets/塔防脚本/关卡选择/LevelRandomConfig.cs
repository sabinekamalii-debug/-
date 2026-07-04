using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "LevelRandomConfig", menuName = "魔塔/关卡随机配置", order = 2)]
public class LevelRandomConfig : ScriptableObject
{
    [Serializable]
    public class LevelRange
    {
        [Tooltip("起始关卡（包含）")]
        public int startLevel = 1;
        
        [Tooltip("结束关卡（包含）")]
        public int endLevel = 3;
        
        [Tooltip("这个范围内有多少个商店")]
        public int shopCount = 0;
        
        [Tooltip("这个范围内有多少个精英关卡")]
        public int eliteCount = 0;
        
        [Tooltip("这个范围内有多少个Boss关卡")]
        public int bossCount = 0;
        
        [Tooltip("这个范围内有多少个随机事件")]
        public int randomEventCount = 0;
        
        [Tooltip("这个范围内有多少个休息点")]
        public int restCount = 0;
    }

    public List<LevelRange> ranges = new List<LevelRange>();
}
