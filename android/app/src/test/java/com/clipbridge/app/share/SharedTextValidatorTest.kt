package com.clipbridge.app.share

import com.clipbridge.app.protocol.ClipboardProtocol
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test

class SharedTextValidatorTest {
    @Test
    fun acceptsSendTextPlainAndTrimsNothing() {
        assertEquals("  exact text  ", SharedTextValidator.extract("android.intent.action.SEND", "text/plain", "  exact text  "))
    }

    @Test
    fun rejectsMalformedEmptyAndOversizedShares() {
        assertNull(SharedTextValidator.extract("wrong", "text/plain", "text"))
        assertNull(SharedTextValidator.extract("android.intent.action.SEND", "image/png", "text"))
        assertNull(SharedTextValidator.extract("android.intent.action.SEND", "text/plain", ""))
        assertNull(
            SharedTextValidator.extract(
                "android.intent.action.SEND",
                "text/plain",
                "x".repeat(ClipboardProtocol.MAX_PAYLOAD_BYTES + 1)
            )
        )
    }
}
