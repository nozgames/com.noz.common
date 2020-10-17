using UnityEngine;

namespace NoZ.Rift
{
    public class DeactivateAfterFrame : MonoBehaviour
    {
        private void LateUpdate()
        {
            gameObject.SetActive(false);
        }
    }
}