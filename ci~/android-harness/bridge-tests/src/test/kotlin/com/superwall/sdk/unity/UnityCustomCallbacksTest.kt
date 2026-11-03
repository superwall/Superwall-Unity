package com.superwall.sdk.unity

import com.superwall.sdk.paywall.presentation.CustomCallback
import com.superwall.sdk.paywall.presentation.CustomCallbackResult
import com.superwall.sdk.paywall.presentation.CustomCallbackResultStatus
import kotlinx.coroutines.CoroutineStart
import kotlinx.coroutines.async
import kotlinx.coroutines.test.runTest
import org.json.JSONObject
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

class UnityCustomCallbacksTest {
    private val sent = mutableListOf<Pair<String, JSONObject>>()
    private val callback = CustomCallback(
        name = "createAccount",
        variables = mapOf("email" to "a@b.c", "age" to 30, "nested" to mapOf("ok" to true)),
    )

    private fun callbacks(sendSucceeds: Boolean = true) = UnityCustomCallbacks { method, data ->
        sent += method to data
        sendSucceeds
    }

    private fun lastCallbackId() = sent.last().second.getString("callbackId")

    @Test
    fun `sends the shape BridgeCallbackHandler reads`() = runTest {
        val callbacks = callbacks()
        val request = async(start = CoroutineStart.UNDISPATCHED) { callbacks.request("handler-1", callback) }

        val (method, data) = sent.single()
        assertEquals("onCustomCallback", method)
        assertEquals("handler-1", data.getString("handlerId"))
        assertTrue(data.getString("callbackId").isNotEmpty())
        val customCallback = data.getJSONObject("customCallback")
        assertEquals("createAccount", customCallback.getString("name"))
        val variables = customCallback.getJSONObject("variables")
        assertEquals("a@b.c", variables.getString("email"))
        assertEquals(30, variables.getInt("age"))
        assertTrue(variables.getJSONObject("nested").getBoolean("ok"))

        callbacks.respond(lastCallbackId(), """{"status":"success"}""")
        request.await()
    }

    @Test
    fun `omits variables when the paywall sent none`() = runTest {
        val callbacks = callbacks()
        val request = async(start = CoroutineStart.UNDISPATCHED) {
            callbacks.request("handler-1", CustomCallback("noVars", null))
        }

        assertFalse(sent.single().second.getJSONObject("customCallback").has("variables"))

        callbacks.respond(lastCallbackId(), """{"status":"failure"}""")
        request.await()
    }

    @Test
    fun `waits for the reply and returns its status and nested data`() = runTest {
        val callbacks = callbacks()
        val request = async(start = CoroutineStart.UNDISPATCHED) { callbacks.request("handler-1", callback) }
        assertFalse("request must wait for C#", request.isCompleted)

        callbacks.respond(
            lastCallbackId(),
            """{"status":"success","data":{"token":"abc","count":2,"gone":null,"user":{"id":7},"tags":["x",1,null]}}""",
        )

        val result = request.await()
        assertEquals(CustomCallbackResultStatus.SUCCESS, result.status)
        val data = assertNotNullData(result)
        assertEquals("abc", data["token"])
        assertEquals(2, data["count"])
        assertFalse("JSON null can't live in a Map<String, Any>", data.containsKey("gone"))
        assertEquals(mapOf("id" to 7), data["user"])
        assertEquals(listOf("x", 1), data["tags"])
    }

    @Test
    fun `failure keeps its data so the paywall can show the error`() = runTest {
        val callbacks = callbacks()
        val request = async(start = CoroutineStart.UNDISPATCHED) { callbacks.request("handler-1", callback) }

        callbacks.respond(lastCallbackId(), """{"status":"failure","data":{"message":"Email taken"}}""")

        val result = request.await()
        assertEquals(CustomCallbackResultStatus.FAILURE, result.status)
        assertEquals("Email taken", assertNotNullData(result)["message"])
    }

    @Test
    fun `malformed reply fails instead of leaving the paywall waiting`() = runTest {
        val callbacks = callbacks()
        val request = async(start = CoroutineStart.UNDISPATCHED) { callbacks.request("handler-1", callback) }

        callbacks.respond(lastCallbackId(), "{not json")

        val result = request.await()
        assertEquals(CustomCallbackResultStatus.FAILURE, result.status)
        assertNull(result.data)
    }

    @Test
    fun `unknown status is a failure`() = runTest {
        val callbacks = callbacks()
        val request = async(start = CoroutineStart.UNDISPATCHED) { callbacks.request("handler-1", callback) }

        callbacks.respond(lastCallbackId(), """{"status":"maybe"}""")

        assertEquals(CustomCallbackResultStatus.FAILURE, request.await().status)
    }

    @Test
    fun `undeliverable request fails without waiting`() = runTest {
        val result = callbacks(sendSucceeds = false).request("handler-1", callback)
        assertEquals(CustomCallbackResultStatus.FAILURE, result.status)
    }

    @Test
    fun `activity-hosted paywall fails without asking C#`() = runTest {
        val callbacks = callbacks().apply { unityPausedWhilePresenting = true }

        val result = callbacks.request("handler-1", callback)

        assertEquals(CustomCallbackResultStatus.FAILURE, result.status)
        assertTrue("C# is paused, so nothing may be sent", sent.isEmpty())
    }

    @Test
    fun `a second or unknown reply is ignored`() = runTest {
        val callbacks = callbacks()
        val request = async(start = CoroutineStart.UNDISPATCHED) { callbacks.request("handler-1", callback) }
        val id = lastCallbackId()

        callbacks.respond("someone-else", """{"status":"failure"}""")
        assertFalse(request.isCompleted)

        callbacks.respond(id, """{"status":"success"}""")
        callbacks.respond(id, """{"status":"failure"}""")

        assertEquals(CustomCallbackResultStatus.SUCCESS, request.await().status)
    }

    @Test
    fun `concurrent callbacks each get their own reply`() = runTest {
        val callbacks = callbacks()
        val first = async(start = CoroutineStart.UNDISPATCHED) { callbacks.request("handler-1", callback) }
        val firstId = lastCallbackId()
        val second = async(start = CoroutineStart.UNDISPATCHED) { callbacks.request("handler-2", callback) }
        val secondId = lastCallbackId()

        callbacks.respond(secondId, """{"status":"failure"}""")
        callbacks.respond(firstId, """{"status":"success"}""")

        assertEquals(CustomCallbackResultStatus.SUCCESS, first.await().status)
        assertEquals(CustomCallbackResultStatus.FAILURE, second.await().status)
    }

    private fun assertNotNullData(result: CustomCallbackResult): Map<String, Any> {
        assertNotNull(result.data)
        return result.data!!
    }
}
