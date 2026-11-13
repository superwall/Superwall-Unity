package com.superwall.sdk.unity

import com.superwall.sdk.store.ProductReplacement
import org.json.JSONObject
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

// The keys must match what BridgeCallbackHandler reads (Tests/Runtime/BridgeContractTests.cs
// sends the same shapes to the C# side).
class PurchaseRequestTest {
    private val upgrade = ProductReplacement(
        mode = ProductReplacement.Mode.CHARGE_PRORATED,
        oldProductId = "com.app.monthly",
        oldPurchaseToken = "token-123",
    )

    @Test
    fun `custom product request carries the product and its transaction id`() {
        val request = customProductPurchaseRequest("cb-1", "web_annual", "txn-abc", replacement = null)

        assertEquals("web_annual", request.getString("productId"))
        assertEquals("txn-abc", request.getString("customTransactionId"))
        assertEquals("cb-1", request.getString("callbackId"))
        assertFalse(request.has("replacement"))
        // Play-only fields don't belong in a custom product request.
        assertFalse(request.has("basePlanId"))
        assertFalse(request.has("offerId"))
    }

    @Test
    fun `custom product request sends a missing transaction id as null`() {
        val request = customProductPurchaseRequest("cb-1", "web_annual", null, replacement = null)

        assertTrue(request.isNull("customTransactionId"))
    }

    @Test
    fun `custom product request carries a replacement without a purchase token`() {
        val replacement = ProductReplacement(ProductReplacement.Mode.DEFAULT, "web_monthly", null)
        val request = customProductPurchaseRequest("cb-1", "web_annual", "txn-abc", replacement)

        val sent = request.getJSONObject("replacement")
        assertEquals("default", sent.getString("mode"))
        assertEquals("web_monthly", sent.getString("oldProductId"))
        assertTrue(sent.isNull("oldPurchaseToken"))
    }

    @Test
    fun `google play request keeps its shape`() {
        val request = googlePlayPurchaseRequest("cb-2", "com.app.annual", "annual", null, upgrade)

        assertEquals("com.app.annual", request.getString("productId"))
        assertEquals("annual", request.getString("basePlanId"))
        // No offer is null, not an empty string: C# passes it on as null.
        assertTrue(request.isNull("offerId"))
        assertEquals("cb-2", request.getString("callbackId"))
        assertFalse(request.has("customTransactionId"))
        val sent: JSONObject = request.getJSONObject("replacement")
        assertEquals("charge_prorated", sent.getString("mode"))
        assertEquals("com.app.monthly", sent.getString("oldProductId"))
        assertEquals("token-123", sent.getString("oldPurchaseToken"))
    }
}
