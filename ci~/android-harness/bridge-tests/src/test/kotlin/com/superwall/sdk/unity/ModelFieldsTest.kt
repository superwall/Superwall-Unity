package com.superwall.sdk.unity

import com.superwall.sdk.analytics.superwall.SuperwallEvent
import com.superwall.sdk.customercenter.CustomerCenterAction
import com.superwall.sdk.customercenter.CustomerCenterRefundStatus
import com.superwall.sdk.customercenter.CustomerCenterScreenType
import com.superwall.sdk.models.customer.CustomerInfo
import com.superwall.sdk.models.customer.SubscriptionTransaction
import com.superwall.sdk.models.entitlements.Entitlement
import com.superwall.sdk.models.internal.PurchaserInfo
import com.superwall.sdk.models.internal.RedemptionInfo
import com.superwall.sdk.models.internal.RedemptionOwnership
import com.superwall.sdk.models.internal.RedemptionResult
import com.superwall.sdk.models.internal.StoreIdentifiers
import com.superwall.sdk.models.product.Store
import com.superwall.sdk.store.abstractions.product.receipt.LatestSubscriptionState
import org.json.JSONObject
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test
import java.util.Date

/**
 * Fields and event payloads the C# models gained, under the keys `BridgeCallbackHandler` reads and the iOS bridge
 * sends.
 */
class ModelFieldsTest {
    private val bridge = SuperwallUnityBridge()

    private fun serializeEntitlement(entitlement: Entitlement): JSONObject {
        val method = SuperwallUnityBridge::class.java.getDeclaredMethod("serializeEntitlement", Entitlement::class.java)
        method.isAccessible = true
        return method.invoke(bridge, entitlement) as JSONObject
    }

    private fun eventPayload(event: SuperwallEvent): JSONObject {
        val method = SuperwallUnityBridge::class.java.getDeclaredMethod(
            "appendEventPayload", SuperwallEvent::class.java, JSONObject::class.java,
        )
        method.isAccessible = true
        return JSONObject().also { method.invoke(bridge, event, it) }
    }

    @Test
    fun entitlement_carriesRenewalAndGracePeriod_andTheCustomStore() {
        val json = serializeEntitlement(
            Entitlement(
                id = "pro",
                isActive = true,
                store = Store.CUSTOM,
                willRenew = true,
                expiresAt = Date(1793275200000L),
                state = LatestSubscriptionState.GRACE_PERIOD,
            ),
        )
        assertEquals(1793275200000L, json.getLong("renewsAt"))
        assertTrue(json.getBoolean("isInGracePeriod"))
        // Android doesn't know whether an entitlement was revoked, so C# keeps null.
        assertFalse(json.has("isRevoked"))
        // Was sent as "other" while C# had no Custom store.
        assertEquals("custom", json.getString("store"))
    }

    @Test
    fun customerInfo_carriesActiveSubscriptionProductIds() {
        val transaction = { id: String, active: Boolean ->
            SubscriptionTransaction(
                transactionId = id,
                productId = "$id.product",
                purchaseDate = Date(0),
                willRenew = active,
                isRevoked = false,
                isInGracePeriod = false,
                isInBillingRetryPeriod = false,
                isActive = active,
                expirationDate = null,
            )
        }
        val json = bridge.serializeCustomerInfo(
            CustomerInfo(subscriptions = listOf(transaction("a", true), transaction("b", false)), nonSubscriptions = emptyList(), userId = "u", entitlements = emptyList()),
        )
        val ids = json.getJSONArray("activeSubscriptionProductIds")
        assertEquals(1, ids.length())
        assertEquals("a.product", ids.getString(0))
    }

    @Test
    fun redemptionPaywallInfo_carriesTheProductIdentifier() {
        val json = bridge.serializeRedemptionInfo(
            RedemptionInfo(
                ownership = RedemptionOwnership.Device(deviceId = "device-1"),
                purchaserInfo = PurchaserInfo(appUserId = "buyer", email = null, storeIdentifiers = StoreIdentifiers.Stripe("cus", emptyList())),
                paywallInfo = RedemptionResult.PaywallInfo(
                    identifier = "pw",
                    placementName = "campaign",
                    placementParams = emptyMap(),
                    variantId = "var",
                    experimentId = "exp",
                    productIdentifier = "pro_yearly",
                ),
                entitlements = emptyList(),
            ),
        )
        assertEquals("pro_yearly", json.getJSONObject("paywallInfo").getString("productIdentifier"))
    }

    @Test
    fun permissionAndPreloadEvents_carryTheirValues() {
        val permission = eventPayload(SuperwallEvent.PermissionGranted(permissionName = "notification", paywallIdentifier = "pw"))
        assertEquals("notification", permission.getString("permissionName"))
        assertEquals("pw", permission.getString("paywallIdentifier"))
        assertEquals(3, eventPayload(SuperwallEvent.PaywallPreloadStart(paywallCount = 3)).getInt("paywallCount"))
    }

    @Test
    fun customerCenterEvents_carryTheirValues() {
        assertEquals("noPurchases", eventPayload(SuperwallEvent.CustomerCenterOpen(CustomerCenterScreenType.NO_PURCHASES)).getString("customerCenterScreen"))

        val survey = eventPayload(
            SuperwallEvent.CustomerCenterSurveyResponse(
                surveyId = "cancel_survey",
                optionId = "too_expensive",
                action = CustomerCenterAction.ManageSubscription,
                pathId = "manage_subscription",
                productId = "pro_monthly",
            ),
        )
        assertEquals("cancel_survey", survey.getString("customerCenterSurveyId"))
        assertEquals("too_expensive", survey.getString("customerCenterSurveyOptionId"))
        assertEquals("manageSubscription", survey.getJSONObject("customerCenterAction").getString("type"))
        assertEquals("manage_subscription", survey.getString("customerCenterPathId"))
        assertEquals("pro_monthly", survey.getString("customerCenterProductId"))

        val refund = eventPayload(SuperwallEvent.CustomerCenterRefundRequest("pro_monthly", CustomerCenterRefundStatus.SUCCESS))
        assertEquals("success", refund.getString("customerCenterRefundStatus"))
    }
}
