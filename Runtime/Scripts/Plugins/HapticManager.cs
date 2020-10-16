/*
 * 
 * Copyright (C) NoZ Games, LLC.  All rights reserved.
 *
 */

using System;
using System.Runtime.InteropServices;

namespace NoZ
{

    public enum NotificationFeedback
    {
        Success,
        Warning,
        Error
    }

    public enum ImpactFeedback
    {
        Light,
        Medium,
        Heavy
    }

    public static class HapticManager
    {
        public static bool Enabled { get; set; } = true;

        public static void Notification(NotificationFeedback feedback)
        {
            if (!Enabled)
                return;
            _unityTapticNotification((int)feedback);
        }

        public static void Impact(ImpactFeedback feedback)
        {
            if (!Enabled)
                return;
            _unityTapticImpact((int)feedback);
        }

        public static void Selection()
        {
            if (!Enabled)
                return;
            _unityTapticSelection();
        }

        public static bool IsSupported()
        {
            return _unityTapticIsSupport();
        }

        #region DllImport

#if UNITY_IPHONE && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void _unityTapticNotification(int type);
        [DllImport("__Internal")]
        private static extern void _unityTapticSelection();
        [DllImport("__Internal")]
        private static extern void _unityTapticImpact(int style);
        [DllImport("__Internal")]
        private static extern bool _unityTapticIsSupport();
#else
        private static void _unityTapticNotification(int type) { }

        private static void _unityTapticSelection() { }

        private static void _unityTapticImpact(int style) { }

        private static bool _unityTapticIsSupport() { return false; }
#endif

        #endregion // DllImport
    }

}