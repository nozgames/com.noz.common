using UnityEngine;

namespace NoZ
{
    public class GameObjectPool : MonoBehaviour
    {
        [SerializeField] private GameObject _prefab = null;

        public void Release(GameObject go)
        {
            go.SetActive(false);
            go.transform.SetParent(transform);
        }

        public GameObject Alloc (Transform parent = null)
        {
            if (transform.childCount == 0)
                return Instantiate(_prefab, parent);

            var go = transform.GetChild(transform.childCount - 1).gameObject;
            go.transform.SetParent(parent);
            go.SetActive(true);
            return go;
        }

        public T Alloc<T>(Transform parent = null) where T : MonoBehaviour =>
            Alloc(parent).GetComponent<T>();
    }
}
