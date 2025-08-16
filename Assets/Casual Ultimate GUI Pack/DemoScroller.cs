using System.Collections.Generic;
using UnityEngine;

namespace Casual_Ultimate_GUI_Pack
{
    public class DemoScroller : MonoBehaviour
    {
        [SerializeField] private List<GameObject> elementsToIterate;

        private int current = 0;

        private void Start()
        {
            foreach (var elem in elementsToIterate)
            {
                elem.transform.localPosition = Vector3.zero;
                elem.SetActive(false);
            }
        
            elementsToIterate[current].SetActive(true);
        }

        public void MoveNext()
        {
            elementsToIterate[current].SetActive(false);
            current++;
            if (current >= elementsToIterate.Count)
            {
                current = 0;
            }
            elementsToIterate[current].SetActive(true);
        }

        public void MovePrev()
        {
            elementsToIterate[current].SetActive(false);
            current--;
            if (current < 0)
            {
                current = elementsToIterate.Count - 1;
            }
            elementsToIterate[current].SetActive(true);
        }
    }
}