using System;
using UnityEngine;

// Sits on the root of one pooled pattern copy. Remembers where each child was
// authored, takes children back when they're done instead of destroying them,
// and tells ObjectSpawner when every child is home so the copy can be reused.
public class PatternCopy : MonoBehaviour
{
    public GameObject Prefab { get; private set; } // which pattern this is a copy of
    public event Action<PatternCopy> OnReleased; // the copy is free to reuse

    private Transform[] children;
    private Vector3[] homePositions;
    private Quaternion[] homeRotations;
    private int childrenOut;  // revealed and not yet home
    private bool revealing;   // the spawner is still revealing this copy's children

    // Called once by ObjectSpawner, right after it creates this copy and before anything is revealed.
    public void Init(GameObject prefab)
    {
        Prefab = prefab;

        int count = transform.childCount;
        children = new Transform[count];
        homePositions = new Vector3[count];
        homeRotations = new Quaternion[count];
        for (int i = 0; i < count; i++)
        {
            children[i] = transform.GetChild(i);
            homePositions[i] = children[i].localPosition;
            homeRotations[i] = children[i].localRotation;
        }
    }

    // ObjectSpawner calls these while it reveals this copy.
    public void BeginReveal() => revealing = true;
    public void ChildRevealed() => childrenOut++;

    public void EndReveal()
    {
        revealing = false;
        ReleaseIfDone();
    }

    // Free only when the spawner is done with it AND every revealed child is home.
    private void ReleaseIfDone()
    {
        if (!revealing && childrenOut == 0)
            OnReleased?.Invoke(this);
    }

    // Takes a child back: switches it off and puts it at its authored spot.
    private void Return(Transform child)
    {
        int i = Array.IndexOf(children, child);
        child.gameObject.SetActive(false);
        child.localPosition = homePositions[i];
        child.localRotation = homeRotations[i];
        childrenOut--;
        ReleaseIfDone();
    }

    // Use instead of Destroy for anything a pattern spawns: it goes home if it
    // belongs to a pooled pattern copy, and is destroyed as before if not.
    public static void Despawn(GameObject obj)
    {
        PatternCopy copy = obj.GetComponentInParent<PatternCopy>();
        if (copy != null) copy.Return(obj.transform);
        else Destroy(obj);
    }
}
