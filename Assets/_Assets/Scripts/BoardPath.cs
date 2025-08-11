using System.Collections.Generic;
using UnityEngine;

public class BoardPath : MonoBehaviour
{
    [Tooltip("Add tiles here manually in the exact visiting order.")]
    public List<Transform> tiles = new List<Transform>();

    public int Count => tiles.Count;

    public Transform GetTile(int index)
    {
        if (Count == 0) return null;
        index = ((index % Count) + Count) % Count; // wrap around
        return tiles[index];
    }
}
