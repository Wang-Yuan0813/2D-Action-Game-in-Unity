using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BossBloodBoom : MonoBehaviour
{
    [Header("攻击属性")]
    [SerializeField, Min(1)] private int damage = 10;
    public float smash;
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.gameObject.CompareTag("Player"))//攻击命中了玩家对象，需要在这里判定一下是否有攻击
        {
            Player_Control player = other.GetComponentInParent<Player_Control>();
            if (player == null ||
                player.ResolveIncomingAttack(EnemyAttackType.Hazard, false, transform.position.x) != PlayerDefenseResult.Hit)
                return;

            player.TakeDamage(damage, smash, transform.position.x);
        }
    }
    public void AnimEnd()
    {
        Destroy(gameObject);
    }
    public void DirectionChange()
    {
        GameObject boss = GameObject.Find("Boss").gameObject;
        float distance = boss.transform.position.x - transform.position.x;
        if(boss.GetComponent<Boss_Control>().superBloodBoom)
            transform.localScale = new Vector2(transform.localScale.x * (1 + Mathf.Abs(distance)/15), transform.localScale.y * (1 + Mathf.Abs(distance) / 15));
        if (distance > 0)
            transform.localScale = new Vector2(-transform.localScale.x, transform.localScale.y);

    }
}
