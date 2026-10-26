package com.clipbridge.app.share

import com.clipbridge.app.protocol.ClipboardProtocol

object SharedTextValidator {
    fun extract(action: String?, type: String?, text: CharSequence?): String? {
        if (action != "android.intent.action.SEND" || type != "text/plain") return null
        val value = text?.toString().orEmpty()
        if (value.isEmpty() || value.toByteArray(Charsets.UTF_8).size > ClipboardProtocol.MAX_PAYLOAD_BYTES) return null
        return value
    }
}
