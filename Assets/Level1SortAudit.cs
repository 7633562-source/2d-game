using System.Collections.Generic;
using UnityEngine;

// req-342 Do 7: one sorting audit line at t = 1 s and one at the end, only
// while a level 1 look switch is on. Reads orders, changes nothing.
public class Level1SortAudit : MonoBehaviour
{
    public Transform hero;
    public Transform dog;
    public Transform fire;

    private bool first;

    private void Update()
    {
        if (first || Time.timeSinceLevelLoad < 1f)
            return;
        first = true;
        Dump("t=1");
    }

    private void OnApplicationQuit()
    {
        Dump("end");
    }

    private void Dump(string when)
    {
        if (!Level1LookFlags.Any)
            return;
        if (hero == null)
        {
            Human h = Object.FindFirstObjectByType<Human>();
            if (h != null)
                hero = h.transform;
        }
        if (dog == null)
        {
            GameObject d = GameObject.Find("Level1Dog");
            if (d != null)
                dog = d.transform;
        }
        if (fire == null)
        {
            Campfire f = Object.FindFirstObjectByType<Campfire>();
            if (f != null)
                fire = f.transform;
        }
        Debug.Log("LEVEL1 LOOK sort " + when
            + " fence=" + Named("BackFence")
            + " fire=" + Range(fire)
            + " dog=" + Range(dog)
            + " hero=" + Range(hero)
            + " crows=" + Crows());
    }

    private static string Range(Transform root)
    {
        if (root == null)
            return "none";
        Renderer[] all = root.GetComponentsInChildren<Renderer>(true);
        int lo = int.MaxValue, hi = int.MinValue;
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] == null)
                continue;
            lo = Mathf.Min(lo, all[i].sortingOrder);
            hi = Mathf.Max(hi, all[i].sortingOrder);
        }
        return lo == int.MaxValue ? "none" : lo + ".." + hi;
    }

    private static string Named(string prefix)
    {
        Renderer[] all = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
        int lo = int.MaxValue, hi = int.MinValue;
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] == null || !Level1LookFlags.IsBackFence(all[i].gameObject.name))
                continue;
            lo = Mathf.Min(lo, all[i].sortingOrder);
            hi = Mathf.Max(hi, all[i].sortingOrder);
        }
        return lo == int.MaxValue ? "none" : lo + ".." + hi;
    }

    private static string Crows()
    {
        Bird[] birds = Object.FindObjectsByType<Bird>(FindObjectsSortMode.None);
        int lo = int.MaxValue, hi = int.MinValue;
        for (int i = 0; i < birds.Length; i++)
        {
            if (birds[i] == null)
                continue;
            Renderer[] all = birds[i].GetComponentsInChildren<Renderer>(true);
            for (int k = 0; k < all.Length; k++)
            {
                if (all[k] == null)
                    continue;
                lo = Mathf.Min(lo, all[k].sortingOrder);
                hi = Mathf.Max(hi, all[k].sortingOrder);
            }
        }
        return lo == int.MaxValue ? "none" : lo + ".." + hi;
    }
}
