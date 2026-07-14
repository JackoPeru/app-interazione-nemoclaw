package com.nemoclaw.chat

import android.content.pm.ActivityInfo
import android.view.Surface
import org.junit.Assert.assertEquals
import org.junit.Test

class FullscreenVideoOrientationTest {
    @Test
    fun `auto rotate uses sensor landscape without allowing portrait`() {
        assertEquals(
            ActivityInfo.SCREEN_ORIENTATION_SENSOR_LANDSCAPE,
            fullscreenLandscapeOrientation(autoRotateEnabled = true, displayRotation = Surface.ROTATION_0)
        )
    }

    @Test
    fun `rotation lock chooses stable standard landscape`() {
        assertEquals(
            ActivityInfo.SCREEN_ORIENTATION_LANDSCAPE,
            fullscreenLandscapeOrientation(autoRotateEnabled = false, displayRotation = Surface.ROTATION_0)
        )
    }

    @Test
    fun `rotation lock preserves reverse landscape side`() {
        assertEquals(
            ActivityInfo.SCREEN_ORIENTATION_REVERSE_LANDSCAPE,
            fullscreenLandscapeOrientation(autoRotateEnabled = false, displayRotation = Surface.ROTATION_270)
        )
    }
}
