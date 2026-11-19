package com.superwall.sdk.unity

import android.app.Activity
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * A C# purchase controller can't answer while Unity's activity is paused, so the bridge fails a restore asked for
 * then instead of waiting. Only Unity's own activity counts: the screen on top resumes and pauses too.
 */
class UnityActivityPauseTrackerTest {
    private val unity = Activity()
    private val customerCenter = Activity()
    private val tracker = UnityActivityPauseTracker { unity }

    @Test
    fun `follows the Unity activity`() {
        assertFalse(tracker.isUnityPaused)
        tracker.onActivityPaused(unity)
        assertTrue(tracker.isUnityPaused)
        tracker.onActivityResumed(unity)
        assertFalse(tracker.isUnityPaused)
    }

    @Test
    fun `ignores other activities`() {
        tracker.onActivityPaused(customerCenter)
        assertFalse(tracker.isUnityPaused)

        tracker.onActivityPaused(unity)
        tracker.onActivityResumed(customerCenter)
        tracker.onActivityPaused(customerCenter)
        assertTrue(tracker.isUnityPaused)
    }
}
