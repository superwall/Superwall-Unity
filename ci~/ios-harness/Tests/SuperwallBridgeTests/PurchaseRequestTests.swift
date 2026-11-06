import XCTest
@testable import SuperwallBridge

// The keys must match what BridgeCallbackHandler reads (Tests/Runtime/BridgeContractTests.cs
// sends the same shapes to the C# side).
final class PurchaseRequestTests: XCTestCase {
    func testCustomProductGoesToPurchaseCustomProduct() {
        let request = purchaseRequest(
            productIdentifier: "web_annual",
            customTransactionId: "txn-abc",
            callbackId: "cb-1"
        )

        XCTAssertEqual(request.method, "purchaseCustomProduct")
        XCTAssertEqual(request.data["productId"] as? String, "web_annual")
        XCTAssertEqual(request.data["customTransactionId"] as? String, "txn-abc")
        XCTAssertEqual(request.data["callbackId"] as? String, "cb-1")
    }

    func testAppStoreProductGoesToPurchaseFromAppStore() {
        let request = purchaseRequest(
            productIdentifier: "com.app.monthly",
            customTransactionId: nil,
            callbackId: "cb-2"
        )

        XCTAssertEqual(request.method, "purchaseFromAppStore")
        XCTAssertEqual(request.data["productId"] as? String, "com.app.monthly")
        XCTAssertEqual(request.data["callbackId"] as? String, "cb-2")
        XCTAssertNil(request.data["customTransactionId"])
    }
}
