package com.superwall.sdk.unity

import com.superwall.sdk.logger.LogScope
import com.superwall.sdk.models.customer.CustomerInfo
import com.superwall.sdk.models.customer.NonSubscriptionTransaction
import com.superwall.sdk.models.customer.SubscriptionTransaction
import com.superwall.sdk.models.entitlements.Entitlement
import com.superwall.sdk.models.internal.PurchaserInfo
import com.superwall.sdk.models.internal.RedemptionInfo
import com.superwall.sdk.models.internal.RedemptionOwnership
import com.superwall.sdk.models.internal.RedemptionResult
import com.superwall.sdk.models.internal.StoreIdentifiers
import com.superwall.sdk.models.product.Store
import com.superwall.sdk.store.abstractions.product.receipt.LatestPeriodType
import org.json.JSONArray
import org.json.JSONObject
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test
import java.util.Date

/**
 * Payloads the Android bridge sends to C# that used to leave fields of the C# models empty: customer info
 * transactions, the redemption info of a web checkout, and the log scopes and attribute maps C# sends in.
 * Keys and vocabulary match the iOS bridge and what `BridgeCallbackHandler` reads.
 */
class BridgePayloadSerializationTest {
    private val bridge = SuperwallUnityBridge()

    @Test
    fun customerInfo_carriesSubscriptionAndNonSubscriptionTransactions() {
        val json = bridge.serializeCustomerInfo(
            CustomerInfo(
                subscriptions = listOf(
                    SubscriptionTransaction(
                        transactionId = "GPA.1",
                        productId = "pro.yearly",
                        purchaseDate = Date(1761739200000L),
                        willRenew = true,
                        isRevoked = false,
                        isInGracePeriod = false,
                        isInBillingRetryPeriod = false,
                        isActive = true,
                        expirationDate = Date(1793275200000L),
                        offerType = LatestPeriodType.TRIAL,
                    ),
                ),
                nonSubscriptions = listOf(
                    NonSubscriptionTransaction(
                        transactionId = "GPA.2",
                        productId = "coins",
                        purchaseDate = Date(1761739200000L),
                        isConsumable = true,
                        isRevoked = false,
                        store = Store.STRIPE,
                    ),
                ),
                userId = "user-1",
                entitlements = listOf(Entitlement(id = "premium")),
            ),
        )

        assertEquals("user-1", json.getString("userId"))
        val subscription = json.getJSONArray("subscriptions").getJSONObject(0)
        assertEquals("GPA.1", subscription.getString("transactionId"))
        assertEquals("pro.yearly", subscription.getString("productId"))
        assertEquals(1761739200000L, subscription.getLong("purchaseDate"))
        assertEquals(1793275200000L, subscription.getLong("expirationDate"))
        assertTrue(subscription.getBoolean("willRenew"))
        assertTrue(subscription.getBoolean("isActive"))
        assertEquals("trial", subscription.getString("offerType"))
        assertEquals("playStore", subscription.getString("store"))
        assertFalse("absent optionals are omitted", subscription.has("subscriptionGroupId"))

        val nonSubscription = json.getJSONArray("nonSubscriptions").getJSONObject(0)
        assertEquals("coins", nonSubscription.getString("productId"))
        assertTrue(nonSubscription.getBoolean("isConsumable"))
        assertEquals("stripe", nonSubscription.getString("store"))
    }

    @Test
    fun redemptionInfo_carriesOwnershipPurchaserAndPaywall() {
        val json = bridge.serializeRedemptionInfo(
            RedemptionInfo(
                ownership = RedemptionOwnership.AppUser(appUserId = "user-1"),
                purchaserInfo = PurchaserInfo(
                    appUserId = "buyer-1",
                    email = "buyer@example.com",
                    storeIdentifiers = StoreIdentifiers.Stripe(
                        stripeCustomerId = "cus_1",
                        subscriptionIds = listOf("sub_1"),
                    ),
                ),
                paywallInfo = RedemptionResult.PaywallInfo(
                    identifier = "pw_1",
                    placementName = "campaign_trigger",
                    placementParams = emptyMap(),
                    variantId = "var_1",
                    experimentId = "exp_1",
                ),
                entitlements = listOf(Entitlement(id = "premium")),
            ),
        )

        val ownership = json.getJSONObject("ownership")
        assertEquals("appUser", ownership.getString("type"))
        assertEquals("user-1", ownership.getString("appUserId"))

        val purchaser = json.getJSONObject("purchaserInfo")
        assertEquals("buyer-1", purchaser.getString("appUserId"))
        assertEquals("buyer@example.com", purchaser.getString("email"))
        val ids = purchaser.getJSONObject("storeIdentifiers")
        assertEquals("stripe", ids.getString("type"))
        assertEquals("cus_1", ids.getString("customerId"))
        assertEquals("sub_1", ids.getJSONArray("subscriptionIds").getString(0))

        val paywall = json.getJSONObject("paywallInfo")
        assertEquals("pw_1", paywall.getString("identifier"))
        assertEquals("campaign_trigger", paywall.getString("placementName"))
        assertEquals("var_1", paywall.getString("variantId"))
        assertEquals("exp_1", paywall.getString("experimentId"))
        assertEquals("premium", json.getJSONArray("entitlements").getJSONObject(0).getString("id"))
    }

    @Test
    fun redemptionInfo_deviceOwnershipAndNoPaywall() {
        val json = bridge.serializeRedemptionInfo(
            RedemptionInfo(
                ownership = RedemptionOwnership.Device(deviceId = "device-1"),
                purchaserInfo = PurchaserInfo(
                    appUserId = "buyer-1",
                    storeIdentifiers = StoreIdentifiers.Paddle(
                        paddleCustomerId = "ctm_1",
                        paddleSubscriptionIds = listOf("sub_2"),
                    ),
                ),
                entitlements = emptyList(),
            ),
        )

        assertEquals("device", json.getJSONObject("ownership").getString("type"))
        assertEquals("device-1", json.getJSONObject("ownership").getString("deviceId"))
        assertEquals("paddle", json.getJSONObject("purchaserInfo").getJSONObject("storeIdentifiers").getString("type"))
        assertFalse(json.getJSONObject("purchaserInfo").has("email"))
        assertFalse(json.has("paywallInfo"))
    }

    @Test
    fun everyCSharpLogScopeHasAnAndroidScope() {
        // The members of the C# LogScope enum (Runtime/Models/Enums.cs). Only 12 used to be parsed; the rest
        // were dropped, so asking for them alone logged every scope.
        val cSharpScopes = listOf(
            "LocalizationManager", "BounceButton", "CoreData", "ConfigManager", "IdentityManager", "DebugManager",
            "DebugViewController", "LocalizationViewController", "GameControllerManager", "Device", "Network",
            "PaywallEvents", "ProductsManager", "StoreKitManager", "Placements", "Receipts", "SuperwallCore",
            "PaywallPresentation", "Transactions", "PaywallViewController", "Cache", "All",
        )
        for (scope in cSharpScopes) {
            assertNotNull(scope, bridge.parseLogScope(scope))
        }
        assertEquals(LogScope.billing, bridge.parseLogScope("StoreKitManager"))
        assertEquals(LogScope.paywallView, bridge.parseLogScope("PaywallViewController"))
    }

    @Test
    fun jsonToMap_keepsTopLevelNullsAndUnwrapsNestedValues() {
        val map = jsonToMap(JSONObject("""{"removed":null,"level":3,"profile":{"tier":"gold","gone":null},"tags":["a","b"]}"""))

        assertTrue("a null value must reach the SDK as null, which removes the attribute", map.containsKey("removed"))
        assertNull(map["removed"])
        assertEquals(3, map["level"])
        assertEquals(mapOf("tier" to "gold"), map["profile"])
        assertEquals(listOf("a", "b"), map["tags"])
    }

    @Test
    fun jsonObjectOf_convertsValuesJsonCannotHold() {
        val json = jsonObjectOf(
            mapOf(
                "when" to Date(1761739200000L),
                "ratio" to Double.NaN,
                "nested" to mapOf("list" to listOf(1, null)),
                "missing" to null,
            ),
        )

        assertEquals(1761739200000L, json.getLong("when"))
        assertEquals("NaN", json.getString("ratio"))
        val list: JSONArray = json.getJSONObject("nested").getJSONArray("list")
        assertEquals(1, list.getInt(0))
        assertTrue(list.isNull(1))
        assertTrue(json.isNull("missing"))
        // org.json refuses NaN, so the converted object must serialize.
        JSONObject(json.toString())
    }
}
