import Foundation
import SuperwallKit
import XCTest
@testable import SuperwallBridge

// Unity provides this symbol at link time in a real player. Here it records what the bridge sent.
private var messagesToUnity: [[String: Any]] = []

@_cdecl("UnitySendMessage")
public func UnitySendMessage(
    _ obj: UnsafePointer<CChar>,
    _ method: UnsafePointer<CChar>,
    _ msg: UnsafePointer<CChar>
) {
    let data = String(cString: msg).data(using: .utf8)!
    messagesToUnity.append(try! JSONSerialization.jsonObject(with: data) as! [String: Any])
}

@MainActor
final class UnityCustomCallbacksTests: XCTestCase {
    private let callback = CustomCallback(
        name: "createAccount",
        variables: ["email": "a@b.c", "age": 30, "nested": ["ok": true]]
    )

    override func setUp() async throws {
        messagesToUnity = []
    }

    /// Starts a request through the real bridge singleton and waits for it to reach "C#".
    private func startRequest(
        handlerId: String = "handler-1",
        callback: CustomCallback? = nil
    ) async throws -> (task: Task<CustomCallbackResult, Never>, callbackId: String, data: [String: Any]) {
        let sentBefore = messagesToUnity.count
        let callback = callback ?? self.callback
        let task = Task { @MainActor in
            await unityCustomCallbacks.request(handlerId: handlerId, callback: callback)
        }
        while messagesToUnity.count == sentBefore {
            await Task.yield()
        }
        let message = try XCTUnwrap(messagesToUnity.last)
        XCTAssertEqual(message["method"] as? String, "onCustomCallback")
        let data = try XCTUnwrap(message["data"] as? [String: Any])
        return (task, try XCTUnwrap(data["callbackId"] as? String), data)
    }

    private func reply(_ callbackId: String, _ json: String) {
        _SuperwallBridge_RespondToCustomCallback(callbackId, json)
    }

    func testSendsTheShapeBridgeCallbackHandlerReads() async throws {
        let request = try await startRequest()

        XCTAssertEqual(request.data["handlerId"] as? String, "handler-1")
        let customCallback = try XCTUnwrap(request.data["customCallback"] as? [String: Any])
        XCTAssertEqual(customCallback["name"] as? String, "createAccount")
        let variables = try XCTUnwrap(customCallback["variables"] as? [String: Any])
        XCTAssertEqual(variables["email"] as? String, "a@b.c")
        XCTAssertEqual(variables["age"] as? Int, 30)
        XCTAssertEqual((variables["nested"] as? [String: Any])?["ok"] as? Bool, true)

        reply(request.callbackId, #"{"status":"success"}"#)
        _ = await request.task.value
    }

    func testOmitsVariablesWhenThePaywallSentNone() async throws {
        let request = try await startRequest(callback: CustomCallback(name: "noVars"))

        let customCallback = try XCTUnwrap(request.data["customCallback"] as? [String: Any])
        XCTAssertNil(customCallback["variables"])

        reply(request.callbackId, #"{"status":"failure"}"#)
        _ = await request.task.value
    }

    func testReturnsReplyStatusAndNestedData() async throws {
        let request = try await startRequest()

        reply(
            request.callbackId,
            #"{"status":"success","data":{"token":"abc","count":2,"user":{"id":7},"tags":["x",1]}}"#
        )

        let result = await request.task.value
        XCTAssertEqual(result.status, .success)
        let data = try XCTUnwrap(result.data)
        XCTAssertEqual(data["token"] as? String, "abc")
        XCTAssertEqual(data["count"] as? Int, 2)
        XCTAssertEqual((data["user"] as? [String: Any])?["id"] as? Int, 7)
        XCTAssertEqual((data["tags"] as? [Any])?.count, 2)
        XCTAssertTrue(JSONSerialization.isValidJSONObject(data), "SuperwallKit serializes this without a guard")
    }

    func testFailureKeepsItsDataSoThePaywallCanShowTheError() async throws {
        let request = try await startRequest()

        reply(request.callbackId, #"{"status":"failure","data":{"message":"Email taken"}}"#)

        let result = await request.task.value
        XCTAssertEqual(result.status, .failure)
        XCTAssertEqual(result.data?["message"] as? String, "Email taken")
    }

    func testMalformedReplyFailsInsteadOfLeavingThePaywallWaiting() async throws {
        let request = try await startRequest()

        reply(request.callbackId, "{not json")

        let result = await request.task.value
        XCTAssertEqual(result.status, .failure)
        XCTAssertNil(result.data)
    }

    func testUnknownStatusIsAFailure() async throws {
        let request = try await startRequest()

        reply(request.callbackId, #"{"status":"maybe"}"#)

        let status = await request.task.value.status
        XCTAssertEqual(status, .failure)
    }

    func testUndeliverableRequestFailsWithoutWaiting() async {
        let callbacks = UnityCustomCallbacks { _, _ in false }

        let result = await callbacks.request(handlerId: "handler-1", callback: callback)

        XCTAssertEqual(result.status, .failure)
    }

    func testVariablesThatJsonCannotHoldStillReachUnity() async throws {
        // sendToUnity sanitizes first; JSONSerialization would otherwise raise an NSException.
        let request = try await startRequest(
            callback: CustomCallback(name: "weird", variables: ["url": URL(string: "https://a.b")!])
        )

        let customCallback = try XCTUnwrap(request.data["customCallback"] as? [String: Any])
        let variables = try XCTUnwrap(customCallback["variables"] as? [String: Any])
        XCTAssertEqual(variables["url"] as? String, "https://a.b")

        reply(request.callbackId, #"{"status":"success"}"#)
        _ = await request.task.value
    }

    func testASecondOrUnknownReplyIsIgnored() async throws {
        let request = try await startRequest()

        reply("someone-else", #"{"status":"failure"}"#)
        reply(request.callbackId, #"{"status":"success"}"#)
        reply(request.callbackId, #"{"status":"failure"}"#) // Would crash if it resumed twice.

        let status = await request.task.value.status
        XCTAssertEqual(status, .success)
    }

    func testConcurrentCallbacksEachGetTheirOwnReply() async throws {
        let first = try await startRequest(handlerId: "handler-1")
        let second = try await startRequest(handlerId: "handler-2")

        reply(second.callbackId, #"{"status":"failure"}"#)
        reply(first.callbackId, #"{"status":"success"}"#)

        let firstStatus = await first.task.value.status
        let secondStatus = await second.task.value.status
        XCTAssertEqual(firstStatus, .success)
        XCTAssertEqual(secondStatus, .failure)
    }
}
