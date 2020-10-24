using System.Collections;
using UnityEngine;

namespace NoZ.Rift
{
    public class ActivateOnInterval : ActorComponent
    {
        public float interval = 1.0f;
        public GameObject target = null;

        protected override void OnEnable()
        {
            base.OnEnable();
            StartCoroutine(IntervalCoroutine());
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            StopAllCoroutines();
        }

        private IEnumerator IntervalCoroutine()
        {
            while (isActiveAndEnabled)
            {
                yield return new WaitForSeconds(interval);
                if(null != target)
                    target.SetActive(true);
                
                break;
            }
        }
    }
}