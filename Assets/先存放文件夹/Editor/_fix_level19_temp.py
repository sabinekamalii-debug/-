from pathlib import Path

hex_grid = "01000000" * 160 + "00000000" * 80
content = f"""%YAML 1.1
%TAG !u! tag:yousandi.cn,2023:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 0}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: Xn0ZsyOqUnI84nhU04S26W8h0ExrZCwtLngvGiCKsHQlm1oT6w7WWNQ=, type: 3}}
  m_Name: Level_19_Battle
  m_EditorClassIdentifier: 
  levelId: 19
  displayName: "\\u65b0\\u6218\\u6597\\u5173\\u5361 19"
  levelType: 0
  gridWidth: 20
  gridHeight: 12
  gridData: {hex_grid}
  mapTheme: 0
  path0Waypoints:
  - {{x: -9.5, y: -5.5, z: 0}}
  - {{x: -9.5, y: -3.5, z: 0}}
  - {{x: -6.5, y: -3.5, z: 0}}
  - {{x: -3.5, y: 0.5, z: 0}}
  path1Waypoints:
  - {{x: -9.5, y: -5.5, z: 0}}
  - {{x: -6.5, y: -5.5, z: 0}}
  - {{x: -3.5, y: -1.5, z: 0}}
  - {{x: -1.5, y: 0.5, z: 0}}
  path2Waypoints: []
  path3Waypoints: []
  waveGroups:
  - delayBeforeGroup: 1
    entries:
    - enemyTypeInt: 16
      spawnInterval: 0.7
      count: 5
      pathIndex: 0
    - enemyTypeInt: 16
      spawnInterval: 0.7
      count: 5
      pathIndex: 1
  - delayBeforeGroup: 2
    entries:
    - enemyTypeInt: 17
      spawnInterval: 1.2
      count: 3
      pathIndex: 0
    - enemyTypeInt: 17
      spawnInterval: 1.2
      count: 3
      pathIndex: 1
  - delayBeforeGroup: 2.5
    entries:
    - enemyTypeInt: 18
      spawnInterval: 1.4
      count: 2
      pathIndex: 0
    - enemyTypeInt: 18
      spawnInterval: 1.4
      count: 2
      pathIndex: 1
  specialWaveIndex: 2
  specialEnemyColor: {{r: 0.6, g: 0, b: 1, a: 1}}
  availableEnemyTypes: []
  bannedOperatorTypes: []
  maxDeployCount: 0
  startDP: 20
  maxLifePoint: 5
  enemyHpMultiplier: 1
  enemySpeedMultiplier: 1
  afterLevelLabel: AfterLevel19
  cardToUnlockOnWin: {{fileID: 0}}
"""

out = Path(r"d:\unity\mowang\Assets\Resources\LevelConfigs\Level_19_Battle.asset")
out.write_text(content, encoding="utf-8", newline="\n")
print("written", out, "bytes", out.stat().st_size)
