using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 控制敌人在墙壁上不粘附的物理材质管理器
/// </summary>
public class NoStickWalls : MonoBehaviour
{
    /// <summary>
    /// 物理材质，用于控制摩擦力
    /// </summary>
    PhysicsMaterial2D myMaterial;
    
    /// <summary>
    /// 敌人AI组件，用于获取当前状态
    /// </summary>
    EnemyAI enemyMovement;
    
    /// <summary>
    /// 圆形碰撞体，用于应用物理材质
    /// </summary>
    CircleCollider2D myPoly;

    /// <summary>
    /// 初始化组件和物理材质
    /// </summary>
    void Start()
    {
        myMaterial = new PhysicsMaterial2D();
        enemyMovement = GetComponent<EnemyAI>();
        myPoly = GetComponent<CircleCollider2D>();
        myPoly.sharedMaterial = myMaterial;
    }

    /// <summary>
    /// 在每一帧结束时更新物理材质属性，根据敌人状态调整摩擦力
    /// </summary>
    void LateUpdate()
    {
        // 当敌人着地时，设置摩擦力为0.4并重置碰撞体
        if (enemyMovement.isGrounded)
        {
            myMaterial.friction = 0.4f;
            myPoly.enabled = false;
            myPoly.enabled = true; 

        }
        // 当敌人在空中时，设置摩擦力为0.0并重置碰撞体
        else if (enemyMovement.isInAir)
        {
            myMaterial.friction = 0.0f;
            myPoly.enabled = false;
            myPoly.enabled = true;
        }
    }
}
