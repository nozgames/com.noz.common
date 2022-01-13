#if UNITY_COLLECTIONS && UNITY_TRANSPORT

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NoZ.Netz
{
    [DefaultExecutionOrder(int.MinValue)]
    public class NetzTimeUpdater : MonoBehaviour
    {
        void Update()
        {
            if (NetzServer.isCreated)
                NetzTime.serverTime = Time.time;
            else if (NetzClient.isCreated)
                NetzTime.serverTime = Time.time + NetzClient.instance._serverTimeDelta;
            else
                NetzTime.serverTime = 0;
        }
    }
}

#endif
