package com.clipbridge.app.sync

import com.clipbridge.app.protocol.ClipboardProtocol
import com.clipbridge.app.protocol.DecryptFailure
import com.clipbridge.app.protocol.DecryptResult
import com.clipbridge.app.protocol.Direction
import com.clipbridge.app.protocol.ReplayCache
import java.time.Instant

class InboundMessageProcessor(
    private val secret: ByteArray,
    private val clock: () -> Instant = { Instant.now() }
) {
    private val replayCache = ReplayCache()

    fun process(json: String): DecryptResult {
        val envelope = ClipboardProtocol.deserialize(json)
            ?: return DecryptResult(failure = DecryptFailure.INVALID_ENVELOPE)
        if (envelope.direction != Direction.WINDOWS_TO_ANDROID) {
            return DecryptResult(failure = DecryptFailure.INVALID_ENVELOPE)
        }
        return ClipboardProtocol.decrypt(envelope, secret, clock(), replayCache)
    }
}
