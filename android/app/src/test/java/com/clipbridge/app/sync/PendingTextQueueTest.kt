package com.clipbridge.app.sync

import org.junit.Assert.assertEquals
import org.junit.Test

class PendingTextQueueTest {
    @Test
    fun connectedSendDrainsImmediately() {
        val queue = PendingTextQueue(3)
        queue.add("shared")
        val sent = mutableListOf<String>()

        queue.drain { sent.add(it); true }

        assertEquals(listOf("shared"), sent)
        assertEquals(0, queue.size)
    }

    @Test
    fun queueIsBoundedAndFailedSendRemainsPending() {
        val queue = PendingTextQueue(2)
        queue.add("old")
        queue.add("new")
        queue.add("newest")

        queue.drain { false }

        assertEquals(2, queue.size)
        val sent = mutableListOf<String>()
        queue.drain { sent.add(it); true }
        assertEquals(listOf("new", "newest"), sent)
    }
}
