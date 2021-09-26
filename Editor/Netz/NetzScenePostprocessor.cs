#if UNITY_COLLECTIONS && UNITY_TRANSPORT

using System.Linq;
using UnityEngine;
using UnityEditor.Callbacks;
using System.Text;

namespace NoZ.Netz.Editor
{
    public static class NetzScenePostprocessor
    {
        private static StringBuilder _builder = null;

        private struct SortContext
        {
            public string id;
            public NetzObject obj;

            public SortContext(NetzObject obj)
            {
                this.obj = obj;

                _builder.Clear();
                for (var transform = obj.transform; transform != null; transform = transform.parent)
                {
                    _builder.Append(transform.GetSiblingIndex().ToString());
                    _builder.Append("_");
                }

                this.id = _builder.ToString();
            }
        }

        [PostProcessScene(int.MaxValue)]
        public static void ProcessScene()
        {
            ulong nextId = NetzConstants.SceneObjectInstanceId;

            _builder = new StringBuilder();
            foreach(var ctx in Object.FindObjectsOfType<NetzObject>().Select(o => new SortContext(o)).OrderBy(c => c.id))
                ctx.obj._networkInstanceId = nextId++;

            _builder = null;
        }
    }
}

#endif