import Foundation
import XCTest
@testable import SuperwallBridge

/// The Customer Center restore question the iOS bridge asks C#: the restore waits for C#'s answer, and goes ahead
/// when the question can't be delivered, as SuperwallKit does without a delegate.
@MainActor
final class UnityCustomerCenterRestoresTests: XCTestCase {
    func testWaitsForTheAnswerOfCSharp() async throws {
        var sent: (method: String, data: [String: Any])?
        let restores = UnityCustomerCenterRestores { method, data in
            sent = (method, data)
            return true
        }

        let task = Task { @MainActor in await restores.request(presentationId: "cc-1") }
        while sent == nil {
            await Task.yield()
        }
        XCTAssertEqual(sent?.method, "customerCenterShouldRestorePurchases")
        XCTAssertEqual(sent?.data["presentationId"] as? String, "cc-1")
        let callbackId = try XCTUnwrap(sent?.data["callbackId"] as? String)

        restores.respond(callbackId: callbackId, proceed: false)
        let proceed = await task.value
        XCTAssertFalse(proceed)

        // A second answer for the same request is ignored.
        restores.respond(callbackId: callbackId, proceed: true)
    }

    func testRestoresWhenTheQuestionCannotReachCSharp() async {
        let restores = UnityCustomerCenterRestores { _, _ in false }
        let proceed = await restores.request(presentationId: "cc-1")
        XCTAssertTrue(proceed)
    }
}
