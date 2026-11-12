package com.superwall.sdk.unity

import com.superwall.sdk.config.options.AdConsentStatus
import com.superwall.sdk.config.options.EventTrackingBehavior
import com.superwall.sdk.logger.LogScope
import org.json.JSONObject
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test

/**
 * What the Android bridge makes of the values C# sends for the API added for parity with the native SDKs:
 * event tracking behaviour, ad consent and the log scopes added to the C# LogScope enum.
 */
class NativeApiTest {
    private val bridge = SuperwallUnityBridge()

    @Test
    fun eventTrackingBehavior_parsesTheNamesCSharpSends() {
        assertEquals(EventTrackingBehavior.ALL, bridge.parseEventTrackingBehavior("all"))
        assertEquals(EventTrackingBehavior.SUPERWALL_ONLY, bridge.parseEventTrackingBehavior("superwallOnly"))
        assertEquals(EventTrackingBehavior.NONE, bridge.parseEventTrackingBehavior("none"))
        // An unknown or missing value leaves the setting alone rather than guessing.
        assertNull(bridge.parseEventTrackingBehavior(""))
        assertNull(bridge.parseEventTrackingBehavior(null))
    }

    @Test
    fun adConsent_isTheConsentTheAppGave() {
        val consent = bridge.parseAdConsent(JSONObject("""{"adUserData":"denied","adPersonalization":"granted"}"""))
        assertEquals(AdConsentStatus.Denied, consent.adUserData)
        assertEquals(AdConsentStatus.Granted, consent.adPersonalization)
    }

    @Test
    fun newLogScopes_mapToTheAndroidScopes() {
        assertEquals(LogScope.webEntitlements, bridge.parseLogScope("WebEntitlements"))
        assertEquals(LogScope.customerCenter, bridge.parseLogScope("CustomerCenter"))
        assertEquals(LogScope.customerInfo, bridge.parseLogScope("CustomerInfo"))
        assertEquals(LogScope.jsEvaluator, bridge.parseLogScope("JsEvaluator"))
        assertEquals(LogScope.paywallTransactions, bridge.parseLogScope("PaywallTransactions"))
        assertEquals(LogScope.nativePurchaseController, bridge.parseLogScope("NativePurchaseController"))
        assertEquals(LogScope.deepLinks, bridge.parseLogScope("DeepLinks"))
        assertEquals(LogScope.billing, bridge.parseLogScope("Billing"))
        // iOS only: dropped here.
        assertNull(bridge.parseLogScope("Analytics"))
        assertNull(bridge.parseLogScope("GrantedEntitlements"))
    }
}
