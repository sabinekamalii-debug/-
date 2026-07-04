using UnityEngine;
using System.Collections.Generic;

public class OperatorBrain : MonoBehaviour
{
    public enum State { Deploying, Moving, Fighting, Arrived }

    [Header("状态")]
    public State currentState = State.Deploying;
    [Tooltip("干员移动速度，当前为默认值减半")]
    public float moveSpeed = 1.0f;

    private List<Vector3> path;
    private int targetIndex;
    private Vector3 directTarget; 
    private bool useDirectMove = false; 

    private UnitBlocker blocker;
    private RangedAttacker attacker;
    private OperatorUnit unitData;

    void Awake()
    {
        blocker = GetComponent<UnitBlocker>();
        attacker = GetComponent<RangedAttacker>();
        unitData = GetComponent<OperatorUnit>();
    }

    // （Start方法已被彻底删除，不会再一开局就乱变色了！）

    public void Initialize(Vector3 destination)
    {
        // 玩家点完地图，干员收到目的地准备起步时，立马恢复原色！
        if (TilemapTinter.Instance != null)
        {
            TilemapTinter.Instance.ResetColor();
        }

        destination.z = 0; 

        if (GridSystem.Instance != null)
        {
            path = GridSystem.Instance.FindPath(transform.position, destination);
        }

        if (path != null && path.Count > 0)
        {
            targetIndex = 0;
            useDirectMove = false;
            SwitchState(State.Moving);
        }
        else
        {
            directTarget = destination;
            useDirectMove = true;
            SwitchState(State.Moving);
        }
    }

    void Update()
    {
        switch (currentState)
        {
            case State.Moving:
                if (useDirectMove) HandleDirectMove(); 
                else HandlePathMove();                
                break;
            case State.Fighting:
                HandleFighting();
                break;
            case State.Arrived:
                break;
        }
    }

    void HandlePathMove()
    {
        if (CheckCombat()) return; 

        if (path == null) return;
        Vector3 currentWaypoint = path[targetIndex];

        transform.position = Vector3.MoveTowards(transform.position, currentWaypoint, moveSpeed * Time.deltaTime);

        if (Vector3.Distance(transform.position, currentWaypoint) < 0.05f)
        {
            targetIndex++;
            if (targetIndex >= path.Count) SwitchState(State.Arrived);
        }
    }

    void HandleDirectMove()
    {
        if (CheckCombat()) return; 

        transform.position = Vector3.MoveTowards(transform.position, directTarget, moveSpeed * Time.deltaTime);

        if (Vector3.Distance(transform.position, directTarget) < 0.1f)
        {
            SwitchState(State.Arrived);
        }
    }

    bool CheckCombat()
    {
        if (blocker != null && blocker.blockedEnemies.Count > 0)
        {
            SwitchState(State.Fighting);
            return true;
        }
        return false;
    }

    void HandleFighting()
    {
        bool isBlocked = (blocker != null && blocker.blockedEnemies.Count > 0);
        bool hasTarget = (attacker != null && attacker.HasTarget());

        if (!isBlocked && !hasTarget)
        {
            SwitchState(State.Moving);
        }
    }

    void SwitchState(State newState)
    {
        if (currentState == newState) return;
        currentState = newState;

        if (currentState == State.Arrived)
        {
            if (unitData != null) unitData.MaximizeSP();
        }
    }
}