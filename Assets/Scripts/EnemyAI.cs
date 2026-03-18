using Pathfinding;
using System.Collections;
using UnityEngine;

/// <summary>
/// 敌人AI控制器，负责敌人的寻路、移动和跳跃行为
/// </summary>
public class EnemyAI : MonoBehaviour
{
    [Header("Pathfinding")]
    public Transform target; // 目标对象，敌人将跟随此对象
    public float activateDistance = 50f; // 激活距离，在此距离内开始跟随目标
    public float pathUpdateSeconds = 0.5f; // 路径更新间隔时间

    [Header("Physics")]
    public float speed = 200f, jumpForce = 100f; // 移动速度和跳跃力度
    public float nextWaypointDistance = 3f; // 到达下一个路径点的距离阈值
    public float jumpNodeHeightRequirement = 0.8f; // 跳跃所需的最小高度差
    public float jumpModifier = 0.3f; // 跳跃修正系数
    public float jumpCheckOffset = 0.1f; // 跳跃检测偏移量

    [Header("Custom Behavior")]
    public bool followEnabled = true; // 是否启用跟随功能
    public bool jumpEnabled = true, isJumping, isInAir; // 是否启用跳跃功能，是否正在跳跃，是否在空中
    public bool directionLookEnabled = true; // 是否启用方向朝向功能

    [SerializeField] Vector3 startOffset; // 射线检测起始偏移位置

    private Path path; // 寻找到的路径
    private int currentWaypoint = 0; // 当前路径点索引
    [SerializeField] public RaycastHit2D isGrounded; // 地面检测结果
    Seeker seeker; // A*寻路组件
    Rigidbody2D rb; // 2D刚体组件
    private bool isOnCoolDown; // 是否处于冷却状态

    /// <summary>
    /// 初始化方法，设置组件引用并启动路径更新循环
    /// </summary>
    public void Start()
    {
        seeker = GetComponent<Seeker>();
        rb = GetComponent<Rigidbody2D>();
        isJumping = false;
        isInAir = false;
        isOnCoolDown = false; 

        InvokeRepeating("UpdatePath", 0f, pathUpdateSeconds);
    }

    /// <summary>
    /// 固定更新方法，处理敌人跟随逻辑
    /// </summary>
    private void FixedUpdate()
    {
        if (TargetInDistance() && followEnabled)
        {
            PathFollow();
        }
    }

    /// <summary>
    /// 更新路径的方法，定期重新计算到目标的路径
    /// </summary>
    private void UpdatePath()
    {
        if (followEnabled && TargetInDistance() && seeker.IsDone())
        {
            seeker.StartPath(rb.position, target.position, OnPathComplete);
        }
    }

    /// <summary>
    /// 执行路径跟随逻辑，包括移动、跳跃和方向控制
    /// </summary>
    private void PathFollow()
    {
        if (path == null)
        {
            return;
        }

        if (currentWaypoint >= path.vectorPath.Count)
        {
            return;
        }

        startOffset = transform.position - new Vector3(0f, GetComponent<Collider2D>().bounds.extents.y + jumpCheckOffset, transform.position.z);
        isGrounded = Physics2D.Raycast(startOffset, -Vector3.up, 0.05f);

        Vector2 direction = ((Vector2)path.vectorPath[currentWaypoint] - rb.position).normalized;
        Vector2 force = direction * speed;

        if (jumpEnabled && isGrounded && !isInAir && !isOnCoolDown)
        {
            if (direction.y > jumpNodeHeightRequirement)
            {
                if (isInAir) return; 
                isJumping = true;
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
                StartCoroutine(JumpCoolDown());

            }
        }
        if (isGrounded)
        {
            isJumping = false;
            isInAir = false; 
        }
        else
        {
            isInAir = true;
        }

        rb.linearVelocity = new Vector2(force.x, rb.linearVelocity.y);

        float distance = Vector2.Distance(rb.position, path.vectorPath[currentWaypoint]);
        if (distance < nextWaypointDistance)
        {
            currentWaypoint++;
        }

        if (directionLookEnabled)
        {
            if (rb.linearVelocity.x > 0.05f)
            {
                transform.localScale = new Vector3(-1f * Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);
            }
            else if (rb.linearVelocity.x < -0.05f)
            {
                transform.localScale = new Vector3(Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);
            }
        }
    }

    /// <summary>
    /// 检查目标是否在激活距离内
    /// </summary>
    /// <returns>如果目标在激活距离内则返回true，否则返回false</returns>
    private bool TargetInDistance()
    {
        return Vector2.Distance(transform.position, target.transform.position) < activateDistance;
    }

    /// <summary>
    /// 路径完成回调方法，处理寻路结果
    /// </summary>
    /// <param name="p">寻找到的路径对象</param>
    private void OnPathComplete(Path p)
    {
        if (!p.error)
        {
            path = p;
            currentWaypoint = 0;
        }
    }

    /// <summary>
    /// 跳跃冷却协程，控制跳跃频率
    /// </summary>
    /// <returns>等待时间结束后的协程对象</returns>
    IEnumerator JumpCoolDown()
    {
        isOnCoolDown = true; 
        yield return new WaitForSeconds(1f);
        isOnCoolDown = false;
    }
}
