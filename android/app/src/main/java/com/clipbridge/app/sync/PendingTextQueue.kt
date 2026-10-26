package com.clipbridge.app.sync

class PendingTextQueue(private val capacity: Int) {
    private val items = ArrayDeque<String>()

    val size: Int
        @Synchronized get() = items.size

    @Synchronized
    fun add(text: String) {
        while (items.size >= capacity) items.removeFirst()
        items.addLast(text)
    }

    @Synchronized
    fun drain(send: (String) -> Boolean) {
        while (items.isNotEmpty()) {
            val next = items.first()
            if (!send(next)) return
            items.removeFirst()
        }
    }
}
