using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = System.Random;

/// <summary>
/// 无人机AI控制器，负责处理无人机的移动、寻路、碰撞检测和音频管理
/// </summary>
public class Drone : MonoBehaviour
{
    [SerializeField] private float rotationSpeed = 5f;
    [SerializeField] private float velocity = 9f;
    [SerializeField] private float acceleration = 100f;
    [SerializeField] private float damage = 10f;

    [SerializeField] private Material debugMaterialOrange;
    [SerializeField] private Material debugMaterialGreen;
    [SerializeField] private Mesh debugMesh;
    [SerializeField] private bool debugEnabled = false;
    
    /// <summary>
    /// 获取目标位置（目标位置向上偏移一个单位）
    /// </summary>
    private Vector3 TargetPosition { get => target.position + Vector3.up; }
    private List<Vector3> waypoints = new List<Vector3>();

    private Transform target;
    private Animator animator;
    private Rigidbody rb;
    private Health selfHealth;
    private AudioSource audioSource;

    private float stuckTimer = -1;
    private Vector3 lastPosition;
    private bool wiggleWaypointExists;
    private float orgDrag;
    private LineRenderer debugLineRenderer;
    private Material debugLineMaterialGreen;
    private Material debugLineMaterialOrange;

    private int worldLayerMask;
    private int novaLayerId;

    /// <summary>
    /// 初始化无人机组件和变量
    /// </summary>
    private void Start()
    {
        animator = GetComponent<Animator>();
        rb = GetComponent<Rigidbody>();
        worldLayerMask = LayerMask.GetMask("World");
        novaLayerId = LayerMask.NameToLayer("Nova");
        selfHealth = GetComponentInChildren<Health>();
        audioSource = GetComponent<AudioSource>();
        orgDrag = rb.linearDamping;
    }

    /// <summary>
    /// 处理无人机碰撞事件
    /// 当无人机与Nova层对象碰撞时，对目标造成伤害并销毁自身
    /// </summary>
    /// <param name="collision">碰撞信息</param>
    private void OnCollisionEnter(Collision collision)
    {
        // 如果无人机与Nova发生碰撞...
        if (collision.collider.gameObject.layer == novaLayerId)
        {
            // 对Nova造成伤害
            collision.collider.gameObject.GetComponent<Health>()?.Damage(damage, collision.contacts[0].normal);

            // 确保自身死亡
            selfHealth.Damage(selfHealth.CurrentHealth + 1, collision.contacts[0].normal);
        }
    }

    /// <summary>
    /// 更新无人机状态，包括路径规划、目标追踪和调试绘制
    /// </summary>
    private void Update()
    {
        if (debugEnabled) DrawDebug();
        
        if (target == null) return;

        HandleAudio();

        // 如果到目标有清晰的视线...
        var targetDirection = (TargetPosition - transform.position).normalized;
        if (!Physics.SphereCast(new Ray(transform.position, targetDirection), 0.5f,
                Vector3.Distance(transform.position, TargetPosition), worldLayerMask) &&
            !Physics.CheckSphere(transform.position + targetDirection, 0.5f, worldLayerMask))
        {
            // 清除所有路点
            waypoints.Clear();
            
            // 将目标位置添加为路点
            waypoints.Add(TargetPosition);
        }
        
        // 如果已有路点（即已发现目标）且最后一个路点与目标之间的距离超过阈值...
        if (waypoints.Count > 0 && Vector3.Distance(waypoints[waypoints.Count - 1], TargetPosition) > 1f)
        {
            // 将目标的当前位置添加到路点列表中
            waypoints.Add(TargetPosition);
        }
        
        // 如果没有路点，则不执行其他操作
        if (waypoints.Count == 0) return;
        
        // 如果当前位置足够接近下一个路点...
        if (Vector3.Distance(transform.position, waypoints[0]) < 2f)
        {
            // 移除列表中的第一个路点
            waypoints.RemoveAt(0);
            
            // 如果之前卡住，则取消任何尝试挣脱的操作
            wiggleWaypointExists = false;
        }
    }

    /// <summary>
    /// 固定更新循环，处理无人机物理移动、旋转和卡住检测
    /// </summary>
    private void FixedUpdate()
    {
        // 强制刚体Z轴位置为0，适用于2.5D平台游戏，冻结位置不够可靠
        rb.MovePosition(new Vector3(rb.position.x, rb.position.y, 0));
        
        // 如果没有路点...
        if (waypoints.Count == 0)
        {
            // 使无人机完全停止并返回
            rb.linearDamping = 1f;
            return;
        }
        else
        {
            rb.linearDamping = orgDrag;
            
            // 以rotationSpeed旋转无人机朝向目标
            rb.MoveRotation(Quaternion.Slerp(rb.rotation, Quaternion.LookRotation(waypoints[0] - rb.position), rotationSpeed * Time.deltaTime));
        }
        
        // 施加力使无人机向前加速
        rb.AddForce(transform.forward * acceleration, ForceMode.Acceleration);
        
        // 确保无人机不超过最大速度
        if (rb.linearVelocity.magnitude > velocity)
        {
            rb.linearVelocity = rb.linearVelocity.normalized * velocity;
        }
        
        // 检测无人机是否卡住的代码
        
        // 检查自上一物理帧以来移动的距离
        var distanceTravelled = Vector3.Distance(lastPosition, rb.position);
        lastPosition = rb.position;
        
        // 如果移动距离 < 1单位/秒...
        if (distanceTravelled < Time.fixedDeltaTime)
        {
            // 如果卡住计时器未启动，则启动它
            if (stuckTimer < 0) stuckTimer = Time.time;
            
            // 如果无人机卡住超过1秒...
            if (Time.time > stuckTimer + 1)
            {
                // 在无人机周围半径为4的圆内随机选择一个位置
                var randomInCircle = UnityEngine.Random.insideUnitCircle.normalized * 4;
                var wigglePosition = rb.position + new Vector3(randomInCircle.x, randomInCircle.y, 0);

                // 如果该位置不与世界图层重叠...
                if (!Physics.CheckSphere(wigglePosition, 0.5f, worldLayerMask))
                {
                    // 如果尚未设置挣脱路点
                    if (!wiggleWaypointExists)
                    {
                        // 插入一个新的路点，让无人机优先前往这个新的附近随机位置
                        waypoints.Insert(0, wigglePosition);
                        wiggleWaypointExists = true;
                    }
                    else
                    {
                        // 之前的挣脱尝试失败，用新的位置替换挣脱路点
                        waypoints[0] = wigglePosition;
                    }

                    // 重置卡住计时器
                    stuckTimer = -1;
                }
            }
        }
    }

    /// <summary>
    /// 根据无人机的速度和与目标的距离调整音频音量和音调
    /// </summary>
    private void HandleAudio()
    {
        var dist = Vector3.Distance(TargetPosition, transform.position);
        var mul = (1f - Mathf.Clamp01(dist / 10f));
        audioSource.volume = Mathf.Clamp01(rb.linearVelocity.magnitude / velocity * 0.1f * mul) + 0.02f;
        audioSource.pitch = Mathf.Clamp(((rb.linearVelocity.magnitude) * mul * 1f) + 0.5f, 0, 1);
    }

    /// <summary>
    /// 目标进入感知范围时激活无人机
    /// </summary>
    /// <param name="gameObject">进入感知范围的游戏对象</param>
    private void OnProximityEnter(Object gameObject)
    {
        target = ((GameObject)gameObject).transform;
        animator.SetTrigger("Activate");
        // Debug.Log("OnProximityEnter " + ((GameObject) gameObject).name);
    }
    
    /// <summary>
    /// 目标离开感知范围时停用无人机
    /// </summary>
    /// <param name="gameObject">离开感知范围的游戏对象</param>
    private void OnProximityExit(Object gameObject)
    {
        animator.SetTrigger("Deactivate");
        // target = null;
        // Debug.Log("OnProximityExit " + ((GameObject) gameObject).name);
    }

    /// <summary>
    /// 绘制调试信息，包括路径线和路点标记
    /// </summary>
    private void DrawDebug()
    {
        if (debugLineRenderer == null)
        {
            debugLineMaterialOrange = new Material(debugMaterialOrange);
            debugLineMaterialOrange.color = new Color(debugLineMaterialOrange.color.r, debugLineMaterialOrange.color.g,
                debugLineMaterialOrange.color.b, 0.1f);
            debugLineMaterialGreen = new Material(debugMaterialGreen);
            debugLineMaterialGreen.color = new Color(debugLineMaterialGreen.color.r, debugLineMaterialGreen.color.g,
                debugLineMaterialGreen.color.b, 0.1f);
            debugLineRenderer = gameObject.AddComponent<LineRenderer>();
            debugLineRenderer.material = debugLineMaterialOrange;
            debugLineRenderer.startWidth = debugLineRenderer.endWidth = 0.5f;
        }

        for (int i = 0; i < waypoints.Count; i++)
        {
            Graphics.DrawMesh(debugMesh, waypoints[i], Quaternion.identity, i == 0 ? debugMaterialGreen : debugMaterialOrange, 0);
            if (Physics.SphereCast(new Ray(transform.position, (TargetPosition - transform.position).normalized), 0.5f,
                    out RaycastHit hit, Vector3.Distance(transform.position, TargetPosition), worldLayerMask))
            {
                debugLineRenderer.sharedMaterial = debugLineMaterialOrange;
                debugLineRenderer.SetPositions(new Vector3[] { transform.position, hit.point });
            }
            else
            {
                debugLineRenderer.sharedMaterial = debugLineMaterialGreen;
                debugLineRenderer.SetPositions(new Vector3[] { transform.position, TargetPosition });
            }
        }
    }
}
