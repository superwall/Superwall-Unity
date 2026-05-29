package com.unity3d.player;

import android.app.Activity;

// CI-only stub. Mirrors the surface of Unity's real UnityPlayer used by
// SuperwallSDK.androidlib/src/main/kotlin/.../SuperwallUnityBridge.kt so the
// module compiles in isolation. Never shipped — provided as compileOnly only.
public final class UnityPlayer {
    public static Activity currentActivity;

    public static void UnitySendMessage(String gameObject, String method, String message) {
    }

    private UnityPlayer() {}
}
