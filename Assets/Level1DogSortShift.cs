using System.Collections.Generic;
using UnityEngine;

// req-342 -level1DogBehind 1: every SpriteRenderer under the Level 1 dog moves
// behind the hero (-21), including the ones built after BuildLevel1Dog (claw
// plates, puffs, joint markers). Picture only: no collider, layer, physics change.
public class Level1DogSortShift : MonoBehaviour
{
    public int shift = -21;

    private readonly List<SpriteRenderer> shifted = new List<SpriteRenderer>(48);
    private int count = -1;

    private void LateUpdate()
    {
        SpriteRenderer[] all = GetComponentsInChildren<SpriteRenderer>(true);
        if (all.Length == count)
            return;
        for (int i = 0; i < all.Length; i++)
        {
            SpriteRenderer sr = all[i];
            if (sr == null || shifted.Contains(sr))
                continue;
            sr.sortingOrder += shift;
            shifted.Add(sr);
        }
        count = all.Length;
    }
}
