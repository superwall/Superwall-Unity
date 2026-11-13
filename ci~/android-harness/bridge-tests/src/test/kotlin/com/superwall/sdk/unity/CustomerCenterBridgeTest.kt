package com.superwall.sdk.unity

import com.superwall.sdk.customercenter.CustomerCenterAction
import com.superwall.sdk.customercenter.CustomerCenterConfiguration
import com.superwall.sdk.customercenter.CustomerCenterPurchase
import com.superwall.sdk.customercenter.CustomerCenterRefundStatus
import com.superwall.sdk.models.customer.SubscriptionTransaction
import com.superwall.sdk.models.entitlements.Entitlement
import com.superwall.sdk.models.product.Store
import org.json.JSONObject
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test
import java.util.Date

/**
 * The Android half of the Customer Center: the configuration C# sends (the JSON `Superwall.SerializeCustomerCenterConfiguration`
 * produces) and what the delegate sends back, under the keys `BridgeCallbackHandler` reads and the iOS bridge uses.
 */
class CustomerCenterBridgeTest {
    private val bridge = SuperwallUnityBridge()

    @Test
    fun configuration_buildsEveryPathType() {
        val configuration = bridge.parseCustomerCenterConfiguration(JSONObject("""
            {
              "managementScreen": {
                "title": "Your plan",
                "paths": [
                  {"type": "restore"},
                  {"type": "refund", "windowMillis": 86400000},
                  {"type": "changePlan", "productIds": ["pro_yearly"]},
                  {"type": "manageSubscription", "survey": {"id": "cancel_survey", "options": [{"id": "too_expensive"}, {"id": "other", "title": "Other"}]}},
                  {"type": "url", "url": "https://example.com/faq", "openMethod": "external", "title": "FAQ"},
                  {"type": "custom", "identifier": "sign_out", "id": "logout"},
                  {"type": "url", "title": "No URL"},
                  {"type": "contactSupport"}
                ]
              },
              "support": {"email": "help@example.com", "warnsAboutUpdates": false, "checksAppStoreForUpdates": true},
              "accent": {"light": "#FF0000", "dark": "#00FF00"},
              "showsAccountDetails": false,
              "warnsAboutDuplicateSubscriptions": true
            }
        """.trimIndent()))

        val paths = configuration.managementScreen.paths
        assertEquals("Your plan", configuration.managementScreen.title)
        // The URL path without a URL is left out.
        assertEquals(7, paths.size)
        assertEquals(CustomerCenterConfiguration.PathType.Restore, paths[0].type)
        assertEquals("restore", paths[0].id)
        assertNull(paths[0].title)
        assertEquals(CustomerCenterConfiguration.PathType.Refund(86_400_000L), paths[1].type)
        assertEquals(CustomerCenterConfiguration.PathType.ChangePlan(listOf("pro_yearly")), paths[2].type)
        val survey = paths[3].survey!!
        assertEquals("cancel_survey", survey.id)
        assertNull(survey.title)
        assertEquals(listOf(CustomerCenterConfiguration.FeedbackSurvey.Option("too_expensive"), CustomerCenterConfiguration.FeedbackSurvey.Option("other", "Other")), survey.options)
        assertEquals(
            CustomerCenterConfiguration.PathType.Url("https://example.com/faq", CustomerCenterConfiguration.OpenMethod.EXTERNAL),
            paths[4].type,
        )
        assertEquals("FAQ", paths[4].title)
        assertEquals(CustomerCenterConfiguration.PathType.Custom("sign_out"), paths[5].type)
        assertEquals("logout", paths[5].id)
        // A missing screen is the SDK's default.
        assertEquals(CustomerCenterConfiguration.default.noPurchasesScreen, configuration.noPurchasesScreen)
        assertEquals("help@example.com", configuration.support.email)
        assertFalse(configuration.support.warnsAboutUpdates)
        assertEquals(CustomerCenterConfiguration.Appearance.ColorPair("#FF0000", "#00FF00"), configuration.appearance.accent)
        assertFalse(configuration.showsAccountDetails)
    }

    @Test
    fun actions_useTheCSharpTypeNames() {
        assertEquals("""{"type":"manageSubscription"}""", bridge.serializeCustomerCenterAction(CustomerCenterAction.ManageSubscription).toString())
        val url = bridge.serializeCustomerCenterAction(CustomerCenterAction.Url("https://example.com"))
        assertEquals("url", url.getString("type"))
        assertEquals("https://example.com", url.getString("url"))
        val custom = bridge.serializeCustomerCenterAction(CustomerCenterAction.Custom("sign_out"))
        assertEquals("sign_out", custom.getString("identifier"))
        assertEquals("userCancelled", bridge.serializeCustomerCenterRefundStatus(CustomerCenterRefundStatus.USER_CANCELLED))
    }

    @Test
    fun purchase_carriesItsStoreEntitlementsAndTransaction() {
        val json = bridge.serializeCustomerCenterPurchase(
            CustomerCenterPurchase(
                productId = "pro_monthly",
                store = Store.PLAY_STORE,
                entitlements = setOf(Entitlement(id = "pro")),
                subscription = SubscriptionTransaction(
                    transactionId = "GPA.1",
                    productId = "pro_monthly",
                    purchaseDate = Date(1761739200000L),
                    willRenew = true,
                    isRevoked = false,
                    isInGracePeriod = false,
                    isInBillingRetryPeriod = false,
                    isActive = true,
                    expirationDate = null,
                ),
            ),
        )
        assertEquals("pro_monthly", json.getString("productId"))
        assertEquals("playStore", json.getString("store"))
        assertEquals("pro", json.getJSONArray("entitlements").getJSONObject(0).getString("id"))
        assertEquals("GPA.1", json.getJSONObject("subscription").getString("transactionId"))
        assertEquals(1761739200000L, json.getJSONObject("subscription").getLong("purchaseDate"))
        assertFalse(json.has("nonSubscription"))
    }

    @Test
    fun presentations_refuseASecondWhileOneIsOpen() {
        val presentations = UnityCustomerCenterPresentations()
        assertTrue(presentations.begin("first"))
        // The SDK would refuse this one without calling its onDismiss, so the bridge calls it.
        assertFalse(presentations.begin("second"))
        // Ending a presentation that never opened leaves the open one alone.
        presentations.end("second")
        assertFalse(presentations.begin("third"))
        presentations.end("first")
        assertTrue(presentations.begin("fourth"))
    }

    @Test
    fun presentations_clearHandsBackOneThatNeverEnded() {
        val presentations = UnityCustomerCenterPresentations()
        assertNull(presentations.clear())
        presentations.begin("refused")
        // A dismissal finished while it was still marked presented: the SDK refused it, so it never ended.
        assertEquals("refused", presentations.clear())
        assertTrue(presentations.begin("next"))
        presentations.end("next")
        assertNull(presentations.clear())
    }
}
