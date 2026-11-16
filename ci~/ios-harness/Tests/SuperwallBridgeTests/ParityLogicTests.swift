import GameController
import SuperwallKit
import UIKit
import XCTest
@testable import SuperwallBridge

/// The logic of the bridge calls that can't be watched without a device: where an embedded paywall is laid out and
/// what C# hears about it, what C#'s Configure completion is told, and how game controller input is passed on.
/// What SuperwallKit and UIKit do with a real paywall view controller is not covered here.
@MainActor
final class EmbeddedPaywallTests: XCTestCase {
    private let bottomHalf = CGRect(x: 0, y: 0.5, width: 1, height: 0.5)

    func testFrameIsTheFractionOfTheContainer() {
        XCTAssertEqual(
            embeddedPaywallFrame(normalizedRect: bottomHalf, in: CGRect(x: 0, y: 0, width: 390, height: 844)),
            CGRect(x: 0, y: 422, width: 390, height: 422)
        )
        XCTAssertEqual(
            embeddedPaywallFrame(
                normalizedRect: CGRect(x: 0.1, y: 0, width: 0.8, height: 0.25),
                in: CGRect(x: 0, y: 0, width: 400, height: 800)
            ),
            CGRect(x: 40, y: 0, width: 320, height: 200)
        )
        // The whole container, whatever its origin in its parent.
        XCTAssertEqual(
            embeddedPaywallFrame(
                normalizedRect: CGRect(x: 0, y: 0, width: 1, height: 1),
                in: CGRect(x: 20, y: 30, width: 400, height: 800)
            ),
            CGRect(x: 0, y: 0, width: 400, height: 800)
        )
    }

    func testFrameKeepsItsShareWhenTheContainerRotates() {
        XCTAssertEqual(
            embeddedPaywallFrame(normalizedRect: bottomHalf, in: CGRect(x: 0, y: 0, width: 844, height: 390)),
            CGRect(x: 0, y: 195, width: 844, height: 195)
        )
    }

    func testContainerLaysThePaywallOutAndFollowsItsOwnSizeAndTheRect() {
        let container = UnityEmbeddedPaywallContainer(frame: CGRect(x: 0, y: 0, width: 400, height: 800))
        let paywall = UIView()
        container.addSubview(paywall)
        container.paywallView = paywall
        container.normalizedRect = bottomHalf
        container.layoutIfNeeded()
        XCTAssertEqual(paywall.frame, CGRect(x: 0, y: 400, width: 400, height: 400))

        // Rotation resizes the container, which covers the game's view.
        container.frame = CGRect(x: 0, y: 0, width: 800, height: 400)
        container.layoutIfNeeded()
        XCTAssertEqual(paywall.frame, CGRect(x: 0, y: 200, width: 800, height: 200))

        // SetRect from C#.
        container.normalizedRect = CGRect(x: 0.25, y: 0, width: 0.5, height: 1)
        container.layoutIfNeeded()
        XCTAssertEqual(paywall.frame, CGRect(x: 200, y: 0, width: 400, height: 400))
    }

    func testTouchesOutsideThePaywallGoThroughToTheGame() {
        let container = UnityEmbeddedPaywallContainer(frame: CGRect(x: 0, y: 0, width: 400, height: 800))
        let paywall = UIView()
        container.addSubview(paywall)
        container.paywallView = paywall
        container.normalizedRect = bottomHalf
        container.layoutIfNeeded()

        XCTAssertTrue(container.point(inside: CGPoint(x: 200, y: 600), with: nil))
        XCTAssertFalse(container.point(inside: CGPoint(x: 200, y: 100), with: nil))
        // nil lets UIKit offer the touch to the view underneath: Unity's.
        XCTAssertNil(container.hitTest(CGPoint(x: 200, y: 100), with: nil))
        XCTAssertTrue(container.hitTest(CGPoint(x: 200, y: 600), with: nil) === paywall)
    }

    func testSkipTellsCSharpTheReason() throws {
        let noMatch = embeddedPaywallSkipPayload(viewId: "view-1", reason: .noAudienceMatch)
        XCTAssertEqual(noMatch["viewId"] as? String, "view-1")
        XCTAssertEqual(noMatch["reason"] as? String, "noAudienceMatch")
        XCTAssertNil(noMatch["experiment"])

        XCTAssertEqual(
            embeddedPaywallSkipPayload(viewId: "view-1", reason: .placementNotFound)["reason"] as? String,
            "placementNotFound"
        )
    }

    func testHoldoutSkipCarriesItsExperiment() throws {
        // Experiment has no public initializer; this is the shape SuperwallKit decodes it from.
        let json = """
        {"experiment_id": "exp", "trigger_experiment_group_id": "group", "variant_id": "var",
         "variant_type": "HOLDOUT", "paywall_identifier": "pw"}
        """
        let experiment = try JSONDecoder().decode(Experiment.self, from: Data(json.utf8))

        let payload = embeddedPaywallSkipPayload(viewId: "view-1", reason: .holdout(experiment))

        XCTAssertEqual(payload["reason"] as? String, "holdout")
        let sent = try XCTUnwrap(payload["experiment"] as? [String: Any])
        XCTAssertEqual(sent["id"] as? String, "exp")
        XCTAssertEqual(sent["groupId"] as? String, "group")
        let variant = try XCTUnwrap(sent["variant"] as? [String: Any])
        XCTAssertEqual(variant["id"] as? String, "var")
        XCTAssertEqual(variant["type"] as? String, "holdout")
    }

    func testFinishedTellsCSharpTheResultAndWhetherThePaywallWent() throws {
        let declined = embeddedPaywallFinishedPayload(
            viewId: "view-1",
            paywallInfo: ["identifier": "pw"],
            result: .declined,
            shouldDismiss: true
        )
        XCTAssertEqual(declined["viewId"] as? String, "view-1")
        XCTAssertEqual((declined["paywallInfo"] as? [String: Any])?["identifier"] as? String, "pw")
        XCTAssertEqual((declined["result"] as? [String: Any])?["type"] as? String, "declined")
        XCTAssertEqual(declined["shouldDismiss"] as? Bool, true)

        let restored = embeddedPaywallFinishedPayload(
            viewId: "view-1",
            paywallInfo: [:],
            result: .restored,
            shouldDismiss: false
        )
        XCTAssertEqual((restored["result"] as? [String: Any])?["type"] as? String, "restored")
        XCTAssertEqual(restored["shouldDismiss"] as? Bool, false)
        // What the bridge sends must be JSON, or C# never hears of it.
        XCTAssertTrue(JSONSerialization.isValidJSONObject(restored))
    }
}

final class ConfigureCompletionTests: XCTestCase {
    func testAFailedConfigurationIsReportedAsAFailure() throws {
        let response = configureCompletionResponse(status: .failed)
        XCTAssertEqual(response["success"] as? Bool, false)
        // Superwall.ParseConfigurationResult turns this into ConfigurationResult.Failed with this message.
        XCTAssertFalse(try XCTUnwrap(response["error"] as? String).isEmpty)
    }

    func testAnythingElseIsASuccess() {
        for status in [ConfigurationStatus.configured, .pending] {
            let response = configureCompletionResponse(status: status)
            XCTAssertEqual(response["success"] as? Bool, true)
            XCTAssertNil(response["error"])
        }
    }
}

@MainActor
final class UnityGameControllerForwarderTests: XCTestCase {
    private var forwarded: [GCControllerElement] = []

    private func makeForwarder() -> UnityGameControllerForwarder {
        return UnityGameControllerForwarder { [weak self] _, element in
            self?.forwarded.append(element)
        }
    }

    /// A controller with an extended gamepad that needs no hardware.
    private func makeGamepad() throws -> (GCController, GCExtendedGamepad) {
        let controller = GCController.withExtendedGamepad()
        guard let gamepad = controller.extendedGamepad else {
            throw XCTSkip("This runtime has no virtual extended gamepad.")
        }
        return (controller, gamepad)
    }

    func testTheGamesOwnHandlerIsStillCalledAndTheInputIsForwarded() throws {
        let (controller, gamepad) = try makeGamepad()
        var gameHeard: [GCControllerElement] = []
        gamepad.valueChangedHandler = { _, element in gameHeard.append(element) }
        let forwarder = makeForwarder()

        forwarder.attach(controller)
        // What GameController calls when the player presses A.
        gamepad.valueChangedHandler?(gamepad, gamepad.buttonA)

        XCTAssertEqual(gameHeard.count, 1)
        XCTAssertTrue(gameHeard.first === gamepad.buttonA)
        XCTAssertEqual(forwarded.count, 1)
        XCTAssertTrue(forwarded.first === gamepad.buttonA)
        XCTAssertEqual(forwarder.attachedGamepadCount, 1)
    }

    func testAttachingTwiceForwardsOnce() throws {
        let (controller, gamepad) = try makeGamepad()
        let forwarder = makeForwarder()

        forwarder.attach(controller)
        forwarder.attach(controller)
        gamepad.valueChangedHandler?(gamepad, gamepad.leftThumbstick)

        XCTAssertEqual(forwarded.count, 1)
        XCTAssertEqual(forwarder.attachedGamepadCount, 1)
    }

    func testStoppingPutsTheGamesHandlerBack() throws {
        let (controller, gamepad) = try makeGamepad()
        var gameHeard = 0
        gamepad.valueChangedHandler = { _, _ in gameHeard += 1 }
        let forwarder = makeForwarder()
        forwarder.attach(controller)

        forwarder.stop()
        gamepad.valueChangedHandler?(gamepad, gamepad.buttonB)

        XCTAssertEqual(gameHeard, 1)
        XCTAssertTrue(forwarded.isEmpty)
        XCTAssertEqual(forwarder.attachedGamepadCount, 0)
    }

    func testAGamepadWithNoHandlerGetsOneAndLosesItAgain() throws {
        let (controller, gamepad) = try makeGamepad()
        gamepad.valueChangedHandler = nil
        let forwarder = makeForwarder()

        forwarder.attach(controller)
        gamepad.valueChangedHandler?(gamepad, gamepad.buttonX)
        XCTAssertEqual(forwarded.count, 1)

        // A controller that disconnects is let go, with its handler as it was.
        forwarder.detach(controller)
        XCTAssertNil(gamepad.valueChangedHandler)
        XCTAssertEqual(forwarder.attachedGamepadCount, 0)
    }

    func testAHandlerTheGameSetsLaterIsLeftAloneWhenForwardingStops() throws {
        let (controller, gamepad) = try makeGamepad()
        gamepad.valueChangedHandler = nil
        let forwarder = makeForwarder()
        forwarder.attach(controller)

        // The game's input code takes the gamepad over after forwarding started, e.g. from its own connect observer.
        var gameHeard = 0
        gamepad.valueChangedHandler = { _, _ in gameHeard += 1 }
        forwarder.stop()
        gamepad.valueChangedHandler?(gamepad, gamepad.buttonY)

        XCTAssertEqual(gameHeard, 1, "stopping must not put back the handler from before the game set its own")
        XCTAssertTrue(forwarded.isEmpty)
        XCTAssertEqual(forwarder.attachedGamepadCount, 0)
    }

    func testAHandlerTheGameSetsLaterIsLeftAloneWhenTheControllerDisconnects() throws {
        let (controller, gamepad) = try makeGamepad()
        gamepad.valueChangedHandler = nil
        let forwarder = makeForwarder()
        forwarder.attach(controller)

        var gameHeard = 0
        gamepad.valueChangedHandler = { _, _ in gameHeard += 1 }
        forwarder.detach(controller)
        gamepad.valueChangedHandler?(gamepad, gamepad.buttonY)

        XCTAssertEqual(gameHeard, 1)
        XCTAssertEqual(forwarder.attachedGamepadCount, 0)
    }
}

@MainActor
final class UnityDismissalsTests: XCTestCase {
    func testADismissalOfAPaywallAlreadyClosingWaitsForThatOne() async {
        let dismissals = UnityDismissals()
        let paywall = NSObject()
        var started = 0
        var finished: [String] = []
        var release: CheckedContinuation<Void, Never>?
        let start: @MainActor () async -> Void = {
            started += 1
            await withCheckedContinuation { release = $0 }
        }
        let bothFinished = expectation(description: "both finished")
        bothFinished.expectedFulfillmentCount = 2
        dismissals.dismiss(paywall, start: start) { finished.append("first"); bothFinished.fulfill() }
        dismissals.dismiss(paywall, start: start) { finished.append("second"); bothFinished.fulfill() }
        while release == nil { await Task.yield() }

        // SuperwallKit would replace the first dismissal's completion with the second's, so it's started only once.
        XCTAssertEqual(started, 1)
        XCTAssertEqual(finished, [])
        release?.resume()
        await fulfillment(of: [bothFinished], timeout: 5)
        XCTAssertEqual(finished, ["first", "second"])

        // Once it has finished, the next dismissal of the same paywall starts its own.
        let third = expectation(description: "third")
        dismissals.dismiss(paywall, start: { started += 1 }) { third.fulfill() }
        await fulfillment(of: [third], timeout: 5)
        XCTAssertEqual(started, 2)
    }

    func testADismissalOfAnotherPaywallOrOfNothingStartsItsOwn() async {
        let dismissals = UnityDismissals()
        var started = 0
        var release: CheckedContinuation<Void, Never>?
        dismissals.dismiss(NSObject(), start: {
            started += 1
            await withCheckedContinuation { release = $0 }
        }) {}
        while release == nil { await Task.yield() }

        let others = expectation(description: "others")
        others.expectedFulfillmentCount = 3
        dismissals.dismiss(NSObject(), start: { started += 1 }) { others.fulfill() }
        dismissals.dismiss(nil, start: { started += 1 }) { others.fulfill() }
        dismissals.dismiss(nil, start: { started += 1 }) { others.fulfill() }
        await fulfillment(of: [others], timeout: 5)
        XCTAssertEqual(started, 4)
        release?.resume()
    }
}
