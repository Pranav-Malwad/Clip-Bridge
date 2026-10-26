package com.clipbridge.app.sync

import com.clipbridge.app.pairing.PairingInfo
import com.clipbridge.app.protocol.DiscoveryProtocol
import java.net.DatagramPacket
import java.net.DatagramSocket
import java.net.InetAddress
import java.time.Instant

object DiscoveryClient {
    fun discover(pairing: PairingInfo): PairingInfo? = runCatching {
        val request = DiscoveryProtocol.createRequest(pairing.pairingId)
        val bytes = DiscoveryProtocol.serialize(request).toByteArray()
        DatagramSocket().use { socket ->
            socket.broadcast = true
            socket.soTimeout = 1_500
            socket.send(DatagramPacket(bytes, bytes.size, InetAddress.getByName("255.255.255.255"), DiscoveryProtocol.PORT))
            val buffer = ByteArray(2048)
            val packet = DatagramPacket(buffer, buffer.size)
            socket.receive(packet)
            val response = DiscoveryProtocol.parseResponse(String(packet.data, 0, packet.length)) ?: return null
            if (!DiscoveryProtocol.verify(request, response, Instant.now().epochSecond, pairing.secret)) return null
            pairing.copy(host = response.host, port = response.port)
        }
    }.getOrNull()
}
