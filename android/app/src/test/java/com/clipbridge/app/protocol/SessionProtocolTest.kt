package com.clipbridge.app.protocol

import org.junit.Assert.assertArrayEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNotEquals
import org.junit.Assert.assertTrue
import org.junit.Test

class SessionProtocolTest {
    private val secret = ByteArray(32) { it.toByte() }

    @Test
    fun authenticatedEphemeralHandshakeDerivesMatchingDirectionalKeys() {
        val client = SessionProtocol.createEphemeralKey()
        val server = SessionProtocol.createEphemeralKey()
        val clientHello = SessionProtocol.createClientHello("device-123", secret, client, ByteArray(16) { it.toByte() })

        assertTrue(SessionProtocol.verifyClientHello(clientHello, secret))
        val serverResult = SessionProtocol.createServerHello(
            clientHello, secret, server, ByteArray(16) { (it + 16).toByte() }
        )
        val clientResult = SessionProtocol.completeClientHandshake(clientHello, serverResult.hello, secret, client)

        assertTrue(clientResult.isSuccess)
        assertArrayEquals(serverResult.keys.windowsToAndroid, clientResult.keys!!.windowsToAndroid)
        assertArrayEquals(serverResult.keys.androidToWindows, clientResult.keys.androidToWindows)
        assertNotEquals(serverResult.keys.windowsToAndroid.toList(), serverResult.keys.androidToWindows.toList())
    }

    @Test
    fun modifiedHelloIsRejected() {
        val client = SessionProtocol.createEphemeralKey()
        val hello = SessionProtocol.createClientHello("device-123", secret, client, ByteArray(16))

        assertFalse(SessionProtocol.verifyClientHello(hello.copy(pairingId = "attacker"), secret))
    }
}
