using UnityEngine;
using System.Collections.Generic;

// Вычисляет общий центр масс человека и его проекцию относительно опоры.
// Использует все Rigidbody2D дочерних сегментов (они добавляются в HumanSegment).
public class CenterOfMassCalculator : MonoBehaviour
{
    private List<Rigidbody2D> bodies = new List<Rigidbody2D>();

    // Инициализация: собрать все Rigidbody2D дочерних сегментов
    public void Initialize()
    {
        bodies.Clear();
        foreach (Rigidbody2D rb in GetComponentsInChildren<Rigidbody2D>())
        {
            // Исключаем собственный Rigidbody2D, если есть
            if (rb != null && rb.gameObject != gameObject)
                bodies.Add(rb);
        }
    }

    // Возвращает мировой центр масс
    public Vector2 GetCenterOfMass()
    {
        if (bodies.Count == 0) return (Vector2)transform.position;

        Vector2 weightedSum = Vector2.zero;
        float totalMass = 0f;

        foreach (var rb in bodies)
        {
            if (rb == null) continue;
            weightedSum += rb.position * rb.mass;
            totalMass += rb.mass;
        }

        if (totalMass < 0.001f) return (Vector2)transform.position;
        return weightedSum / totalMass;
    }

    // Смещение CoM от середины опоры по краям коллайдеров стоп.
    public float GetCoMOffsetX()
    {
        if (!TryGetSupportBounds(out float minX, out float maxX))
            return 0f;

        Vector2 com = GetCenterOfMass();
        return com.x - (minX + maxX) * 0.5f;
    }

    public float GetSupportWidth()
    {
        if (!TryGetSupportBounds(out float minX, out float maxX))
            return 0.1f;
        return maxX - minX;
    }

    private bool TryGetSupportBounds(out float minX, out float maxX)
    {
        minX = 0f;
        maxX = 0f;
        bool found = false;
        ExpandBounds("LeftLegFoot", ref found, ref minX, ref maxX);
        ExpandBounds("RightLegFoot", ref found, ref minX, ref maxX);
        return found;
    }

    private void ExpandBounds(string childName, ref bool found, ref float minX, ref float maxX)
    {
        Transform t = transform.Find(childName);
        if (t == null) return;
        Collider2D col = t.GetComponent<Collider2D>();
        if (col == null) return;
        Bounds b = col.bounds;
        minX = found ? Mathf.Min(minX, b.min.x) : b.min.x;
        maxX = found ? Mathf.Max(maxX, b.max.x) : b.max.x;
        found = true;
    }
}