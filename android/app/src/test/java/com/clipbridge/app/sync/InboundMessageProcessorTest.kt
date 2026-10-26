package com.clipbridge.app.sync

import com.clipbridge.app.protocol.ClipboardProtocol
import com.clipbridge.app.protocol.DecryptFailure
import com.clipbridge.app.protocol.Direction
import java.time.Instant
import org.junit.Assert.assertEquals
import org.junit.Test

class InboundMessageProcessorTest {
    private val secret = ByteArray(32) { it.toByte() }
    private val now = Instant.ofEpochSecond(1_800_000_000)

    @Test
    fun acceptsWindowsMessageAndRejectsWrongDirectionAndMalformedJson() {
        val processor = InboundMessageProcessor(secret) { now }
        val valid = ClipboardProtocol.encrypt("from laptop", Direction.WINDOWS_TO_ANDROID, secret, now)
        val wrong = ClipboardProtocol.encrypt("wrong", Direction.ANDROID_TO_WINDOWS, secret, now)

        assertEquals("from laptop", processor.process(ClipboardProtocol.serialize(valid)).text)
        assertEquals(DecryptFailure.INVALID_ENVELOPE, processor.process(ClipboardProtocol.serialize(wrong)).failure)
        assertEquals(DecryptFailure.INVALID_ENVELOPE, processor.process("not-json").failure)
    }
}
