package com.superwall.sdk.unity

import com.superwall.sdk.models.entitlements.Entitlement
import com.superwall.sdk.models.product.Store
import com.superwall.sdk.store.abstractions.product.receipt.LatestPeriodType
import com.superwall.sdk.store.abstractions.product.receipt.LatestSubscriptionState
import org.json.JSONObject
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Test
import java.util.Date

/**
 * The entitlement JSON the Android bridge sends to C#. Every field `BridgeCallbackHandler.DeserializeEntitlement`
 * reads must be emitted, in the vocabulary of the C# enums (shared with the iOS bridge), with dates as epoch
 * milliseconds. Until the fix only `id`, a hardcoded `type`, `isActive` and `productIds` were sent.
 */
class EntitlementSerializationTest {
    private val bridge = SuperwallUnityBridge()

    private fun serialize(entitlement: Entitlement): JSONObject {
        val method = SuperwallUnityBridge::class.java.getDeclaredMethod("serializeEntitlement", Entitlement::class.java)
        method.isAccessible = true
        return method.invoke(bridge, entitlement) as JSONObject
    }

    @Test
    fun everyFieldIsEmitted() {
        val json = serialize(
            Entitlement(
                id = "premium",
                isActive = true,
                productIds = setOf("pro.yearly"),
                latestProductId = "pro.yearly",
                startsAt = Date(1761739200000L),
                renewedAt = Date(1761739200000L),
                expiresAt = Date(1793275200000L),
                isLifetime = false,
                willRenew = true,
                state = LatestSubscriptionState.SUBSCRIBED,
                offerType = LatestPeriodType.TRIAL,
                store = Store.PLAY_STORE,
            ),
        )

        assertEquals("premium", json.getString("id"))
        assertEquals("serviceLevel", json.getString("type"))
        assertEquals(true, json.getBoolean("isActive"))
        assertEquals("pro.yearly", json.getJSONArray("productIds").getString(0))
        assertEquals("pro.yearly", json.getString("latestProductId"))
        assertEquals("playStore", json.getString("store"))
        assertEquals(1761739200000L, json.getLong("startsAt"))
        assertEquals(1761739200000L, json.getLong("renewedAt"))
        assertEquals(1793275200000L, json.getLong("expiresAt"))
        assertEquals(false, json.getBoolean("isLifetime"))
        assertEquals(true, json.getBoolean("willRenew"))
        assertEquals("subscribed", json.getString("state"))
        assertEquals("trial", json.getString("offerType"))
    }

    @Test
    fun androidStatesMapToTheCSharpNames() {
        assertEquals("inGracePeriod", serialize(Entitlement(id = "e", state = LatestSubscriptionState.GRACE_PERIOD)).getString("state"))
        assertEquals("inBillingRetryPeriod", serialize(Entitlement(id = "e", state = LatestSubscriptionState.BILLING_RETRY)).getString("state"))
        assertEquals("revoked", serialize(Entitlement(id = "e", state = LatestSubscriptionState.REVOKED)).getString("state"))
    }

    @Test
    fun absentOptionalsAreOmittedRatherThanSentAsNull() {
        val json = serialize(Entitlement(id = "granted", isActive = true))

        for (key in listOf("latestProductId", "store", "startsAt", "renewedAt", "expiresAt", "isLifetime", "willRenew", "state", "offerType")) {
            assertFalse("$key must be omitted", json.has(key))
        }
    }

    @Test
    fun unknownStateIsDroppedSoTheCSharpNullableStaysNull() {
        assertFalse(serialize(Entitlement(id = "e", state = LatestSubscriptionState.UNKNOWN)).has("state"))
    }
}
