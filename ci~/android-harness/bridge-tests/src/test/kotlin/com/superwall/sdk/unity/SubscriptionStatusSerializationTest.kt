package com.superwall.sdk.unity

import com.superwall.sdk.models.entitlements.Entitlement
import com.superwall.sdk.models.entitlements.SubscriptionStatus
import org.json.JSONObject
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Test

/**
 * The JSON `getSubscriptionStatus()` hands to C#. `BridgeCallbackHandler.DeserializeSubscriptionStatus` reads
 * `type` and, for `active`, the `entitlements` array; without the array C# reads `Active []` straight after
 * `setSubscriptionStatus(Active [premium])`, which looks like a lost entitlement to any app that re-checks.
 */
class SubscriptionStatusSerializationTest {
    private val bridge = SuperwallUnityBridge()

    private fun serialize(status: SubscriptionStatus): JSONObject {
        val method = SuperwallUnityBridge::class.java
            .getDeclaredMethod("serializeSubscriptionStatus", SubscriptionStatus::class.java)
        method.isAccessible = true
        return method.invoke(bridge, status) as JSONObject
    }

    @Test
    fun active_carriesEveryEntitlement() {
        val json = serialize(
            SubscriptionStatus.Active(
                setOf(
                    Entitlement(id = "premium", isActive = true, productIds = setOf("pro.yearly")),
                    Entitlement(id = "extras", isActive = true),
                ),
            ),
        )

        assertEquals("active", json.getString("type"))
        val entitlements = json.getJSONArray("entitlements")
        val ids = (0 until entitlements.length()).map { entitlements.getJSONObject(it).getString("id") }.toSet()
        assertEquals(setOf("premium", "extras"), ids)
    }

    @Test
    fun active_entitlementUsesTheFullEntitlementShape() {
        val json = serialize(
            SubscriptionStatus.Active(setOf(Entitlement(id = "premium", isActive = true, productIds = setOf("pro.yearly")))),
        )

        val premium = json.getJSONArray("entitlements").getJSONObject(0)
        assertEquals("premium", premium.getString("id"))
        assertEquals("serviceLevel", premium.getString("type"))
        assertEquals(true, premium.getBoolean("isActive"))
        assertEquals("pro.yearly", premium.getJSONArray("productIds").getString(0))
    }

    @Test
    fun inactiveAndUnknown_haveNoEntitlementsKey() {
        val inactive = serialize(SubscriptionStatus.Inactive)
        val unknown = serialize(SubscriptionStatus.Unknown)

        assertEquals("inactive", inactive.getString("type"))
        assertEquals("unknown", unknown.getString("type"))
        assertFalse(inactive.has("entitlements"))
        assertFalse(unknown.has("entitlements"))
    }
}
