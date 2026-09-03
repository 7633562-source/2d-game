using UnityEngine;

// Thin health. Not inventory, not a punch from the brain.
// Only a BeakStrike, JawStrike, or later a listed weapon calls Hurt.
public class Damageable : MonoBehaviour
{
    public float maxHealth = 10f;
    public float health = 10f;

    public int HurtCount { get; private set; }
    public bool IsDead => health <= 0f;

    public void Hurt(float amount, Transform from)
    {
        if (amount <= 0f || IsDead)
            return;

        health = Mathf.Max(0f, health - amount);
        HurtCount++;
    }
}
