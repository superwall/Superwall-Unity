import Foundation
import SuperwallKit

// UnitySendMessage is a C function provided by the Unity runtime at link time.
@_silgen_name("UnitySendMessage")
func UnitySendMessage(_ obj: UnsafePointer<CChar>, _ method: UnsafePointer<CChar>, _ msg: UnsafePointer<CChar>)

// MARK: - Helpers

private func toSwiftString(_ cStr: UnsafePointer<CChar>?) -> String? {
    guard let cStr = cStr else { return nil }
    return String(cString: cStr)
}

private func toCString(_ str: String?) -> UnsafePointer<CChar>? {
    guard let str = str else { return nil }
    return UnsafePointer(strdup(str))
}

private func toJsonString(_ obj: Any) -> String? {
    guard let data = try? JSONSerialization.data(withJSONObject: obj),
          let str = String(data: data, encoding: .utf8) else { return nil }
    return str
}

private func parseJson(_ jsonStr: String?) -> [String: Any]? {
    guard let jsonStr = jsonStr,
          let data = jsonStr.data(using: .utf8),
          let obj = try? JSONSerialization.jsonObject(with: data) as? [String: Any] else { return nil }
    return obj
}

private func parseJsonArray(_ jsonStr: String?) -> [Any]? {
    guard let jsonStr = jsonStr,
          let data = jsonStr.data(using: .utf8),
          let obj = try? JSONSerialization.jsonObject(with: data) as? [Any] else { return nil }
    return obj
}

private func sendToUnity(method: String, data: [String: Any]) {
    let payload: [String: Any] = ["method": method, "data": data]
    guard let json = toJsonString(payload) else { return }
    let cStr = (json as NSString).utf8String!
    UnitySendMessage("SuperwallBridge", "OnCallback", cStr)
}

private func sendAsyncResponse(callbackId: String, data: [String: Any]) {
    var responseData = data
    responseData["callbackId"] = callbackId
    sendToUnity(method: "asyncResponse", data: responseData)
}

// MARK: - Serialization

private func serializeExperiment(_ experiment: Experiment?) -> [String: Any]? {
    guard let exp = experiment else { return nil }
    return [
        "id": exp.id,
        "groupId": exp.groupId,
        "variant": [
            "id": exp.variant.id,
            "type": exp.variant.type == .treatment ? "treatment" : "holdout",
            "paywallId": exp.variant.paywallId as Any
        ] as [String: Any]
    ]
}

private func serializeEntitlement(_ entitlement: Entitlement) -> [String: Any] {
    return [
        "id": entitlement.id,
        "type": "serviceLevel",
        "isActive": entitlement.isActive,
        "productIds": Array(entitlement.productIds)
    ]
}

private func serializeEntitlements(_ entitlements: Set<Entitlement>) -> [[String: Any]] {
    return entitlements.map { serializeEntitlement($0) }
}

private func serializePaywallInfo(_ info: PaywallInfo) -> [String: Any] {
    var dict: [String: Any] = [
        "identifier": info.identifier,
        "name": info.name,
        "url": info.url.absoluteString,
        "productIds": info.productIds
    ]
    if let experiment = info.experiment {
        dict["experiment"] = serializeExperiment(experiment)
    }
    return dict
}

private func serializeSubscriptionStatus(_ status: SubscriptionStatus) -> [String: Any] {
    switch status {
    case .active(let entitlements):
        return ["type": "active", "entitlements": serializeEntitlements(entitlements)]
    case .inactive:
        return ["type": "inactive"]
    case .unknown:
        return ["type": "unknown"]
    @unknown default:
        return ["type": "unknown"]
    }
}

private func serializeEventInfo(_ info: SuperwallEventInfo) -> [String: Any] {
    var dict: [String: Any] = [
        "params": info.params
    ]
    // Use the string representation of the event
    dict["eventType"] = String(describing: info.event)
    return dict
}

private func serializeCustomerInfo(_ info: CustomerInfo) -> [String: Any] {
    return [
        "userId": info.userId,
        "entitlements": info.entitlements.map { serializeEntitlement($0) }
    ]
}

private func serializeRedemptionResult(_ result: RedemptionResult) -> [String: Any] {
    switch result {
    case .success(let code, let info):
        return ["type": "success", "code": code, "redemptionInfo": serializeRedemptionInfo(info)]
    case .error(let code, let error):
        return ["type": "error", "code": code, "error": error.message]
    case .expiredCode(let code, let info):
        return ["type": "expiredCode", "code": code, "resent": info.resent, "obfuscatedEmail": info.obfuscatedEmail as Any]
    case .invalidCode(let code):
        return ["type": "invalidCode", "code": code]
    case .expiredSubscription(let code, let info):
        return ["type": "expiredSubscription", "code": code, "redemptionInfo": serializeRedemptionInfo(info)]
    @unknown default:
        return ["type": "unknown"]
    }
}

private func serializeRedemptionInfo(_ info: RedemptionResult.RedemptionInfo) -> [String: Any] {
    return [
        "entitlements": serializeEntitlements(info.entitlements)
    ]
}

// MARK: - Delegate

private class SuperwallUnityDelegate: SuperwallDelegate {
    func subscriptionStatusDidChange(from oldValue: SubscriptionStatus, to newValue: SubscriptionStatus) {
        sendToUnity(method: "subscriptionStatusDidChange", data: [
            "from": serializeSubscriptionStatus(oldValue),
            "to": serializeSubscriptionStatus(newValue)
        ])
    }

    func handleSuperwallEvent(withInfo eventInfo: SuperwallEventInfo) {
        sendToUnity(method: "handleSuperwallEvent", data: serializeEventInfo(eventInfo))
    }

    func handleCustomPaywallAction(withName name: String) {
        sendToUnity(method: "handleCustomPaywallAction", data: ["name": name])
    }

    func willDismissPaywall(withInfo paywallInfo: PaywallInfo) {
        sendToUnity(method: "willDismissPaywall", data: serializePaywallInfo(paywallInfo))
    }

    func willPresentPaywall(withInfo paywallInfo: PaywallInfo) {
        sendToUnity(method: "willPresentPaywall", data: serializePaywallInfo(paywallInfo))
    }

    func didDismissPaywall(withInfo paywallInfo: PaywallInfo) {
        sendToUnity(method: "didDismissPaywall", data: serializePaywallInfo(paywallInfo))
    }

    func didPresentPaywall(withInfo paywallInfo: PaywallInfo) {
        sendToUnity(method: "didPresentPaywall", data: serializePaywallInfo(paywallInfo))
    }

    func paywallWillOpenURL(url: URL) {
        sendToUnity(method: "paywallWillOpenURL", data: ["url": url.absoluteString])
    }

    func paywallWillOpenDeepLink(url: URL) {
        sendToUnity(method: "paywallWillOpenDeepLink", data: ["url": url.absoluteString])
    }

    func handleLog(level: String, scope: String, message: String?, info: [String: Any]?, error: Swift.Error?) {
        sendToUnity(method: "handleLog", data: [
            "level": level,
            "scope": scope,
            "message": message as Any,
            "error": error?.localizedDescription as Any
        ])
    }

    func willRedeemLink() {
        sendToUnity(method: "willRedeemLink", data: [:])
    }

    func didRedeemLink(result: RedemptionResult) {
        sendToUnity(method: "didRedeemLink", data: serializeRedemptionResult(result))
    }

    func customerInfoDidChange(from oldValue: CustomerInfo, to newValue: CustomerInfo) {
        sendToUnity(method: "customerInfoDidChange", data: [
            "from": serializeCustomerInfo(oldValue),
            "to": serializeCustomerInfo(newValue)
        ])
    }
}

// MARK: - Stored State

private var unityDelegate: SuperwallUnityDelegate?
private var pendingFeatureHandlers: [String: () -> Void] = [:]

// MARK: - Extern C Functions

@_cdecl("_SuperwallBridge_Configure")
public func _SuperwallBridge_Configure(
    _ apiKey: UnsafePointer<CChar>,
    _ optionsJson: UnsafePointer<CChar>?,
    _ hasPurchaseController: Bool,
    _ completionCallbackId: UnsafePointer<CChar>?
) {
    let key = String(cString: apiKey)
    let callbackId = toSwiftString(completionCallbackId)

    var options: SuperwallOptions? = nil
    if let json = toSwiftString(optionsJson), let dict = parseJson(json) {
        options = SuperwallOptions()
        if let testMode = dict["testModeBehavior"] as? String {
            switch testMode.lowercased() {
            case "always": options?.testModeBehavior = .always
            case "never": options?.testModeBehavior = .never
            case "whenenabledforuser": options?.testModeBehavior = .whenEnabledForUser
            default: options?.testModeBehavior = .automatic
            }
        }
        if let locale = dict["localeIdentifier"] as? String {
            options?.localeIdentifier = locale
        }
        if let externalData = dict["isExternalDataCollectionEnabled"] as? Bool {
            options?.isExternalDataCollectionEnabled = externalData
        }
        if let gameController = dict["isGameControllerEnabled"] as? Bool {
            options?.isGameControllerEnabled = gameController
        }
        if let logging = dict["logging"] as? [String: Any] {
            if let level = logging["level"] as? String {
                switch level.lowercased() {
                case "debug": options?.logging.level = .debug
                case "info": options?.logging.level = .info
                case "warn": options?.logging.level = .warn
                case "error": options?.logging.level = .error
                case "none": options?.logging.level = .none
                default: break
                }
            }
        }
    }

    let completion: (() -> Void)? = callbackId.map { cbId in
        return {
            sendAsyncResponse(callbackId: cbId, data: ["success": true])
        }
    }

    _ = Superwall.configure(apiKey: key, options: options, completion: completion)
}

@_cdecl("_SuperwallBridge_Reset")
public func _SuperwallBridge_Reset() {
    Superwall.shared.reset()
}

@_cdecl("_SuperwallBridge_SetDelegate")
public func _SuperwallBridge_SetDelegate(_ hasDelegate: Bool) {
    if hasDelegate {
        unityDelegate = SuperwallUnityDelegate()
        Superwall.shared.delegate = unityDelegate
    } else {
        unityDelegate = nil
        Superwall.shared.delegate = nil
    }
}

@_cdecl("_SuperwallBridge_Identify")
public func _SuperwallBridge_Identify(_ userId: UnsafePointer<CChar>, _ identityOptionsJson: UnsafePointer<CChar>?) {
    let id = String(cString: userId)
    var options: IdentityOptions? = nil
    if let json = toSwiftString(identityOptionsJson), let dict = parseJson(json) {
        if let restore = dict["restorePaywallAssignments"] as? Bool {
            options = IdentityOptions(restorePaywallAssignments: restore)
        }
    }
    Superwall.shared.identify(userId: id, options: options)
}

@_cdecl("_SuperwallBridge_GetUserId")
public func _SuperwallBridge_GetUserId() -> UnsafePointer<CChar>? {
    return toCString(Superwall.shared.userId)
}

@_cdecl("_SuperwallBridge_GetIsLoggedIn")
public func _SuperwallBridge_GetIsLoggedIn() -> Bool {
    return Superwall.shared.isLoggedIn
}

@_cdecl("_SuperwallBridge_GetIsInitialized")
public func _SuperwallBridge_GetIsInitialized() -> Bool {
    return Superwall.isInitialized
}

@_cdecl("_SuperwallBridge_SetUserAttributes")
public func _SuperwallBridge_SetUserAttributes(_ attributesJson: UnsafePointer<CChar>) {
    let json = String(cString: attributesJson)
    if let dict = parseJson(json) {
        Superwall.shared.setUserAttributes(dict)
    }
}

@_cdecl("_SuperwallBridge_GetUserAttributes")
public func _SuperwallBridge_GetUserAttributes() -> UnsafePointer<CChar>? {
    let attrs = Superwall.shared.userAttributes
    return toCString(toJsonString(attrs))
}

@_cdecl("_SuperwallBridge_SetIntegrationAttribute")
public func _SuperwallBridge_SetIntegrationAttribute(_ attribute: UnsafePointer<CChar>, _ value: UnsafePointer<CChar>?) {
    // TODO: Map string attribute name to IntegrationAttribute enum
}

@_cdecl("_SuperwallBridge_SetIntegrationAttributes")
public func _SuperwallBridge_SetIntegrationAttributes(_ attributesJson: UnsafePointer<CChar>) {
    // TODO: Map string attribute names to IntegrationAttribute enum
}

@_cdecl("_SuperwallBridge_GetDeviceAttributes")
public func _SuperwallBridge_GetDeviceAttributes(_ callbackId: UnsafePointer<CChar>) {
    let cbId = String(cString: callbackId)
    Task {
        let attrs = await Superwall.shared.getDeviceAttributes()
        sendAsyncResponse(callbackId: cbId, data: attrs)
    }
}

@_cdecl("_SuperwallBridge_GetLocaleIdentifier")
public func _SuperwallBridge_GetLocaleIdentifier() -> UnsafePointer<CChar>? {
    return toCString(Superwall.shared.localeIdentifier)
}

@_cdecl("_SuperwallBridge_SetLocaleIdentifier")
public func _SuperwallBridge_SetLocaleIdentifier(_ localeIdentifier: UnsafePointer<CChar>?) {
    Superwall.shared.localeIdentifier = toSwiftString(localeIdentifier)
}

@_cdecl("_SuperwallBridge_GetLogLevel")
public func _SuperwallBridge_GetLogLevel() -> UnsafePointer<CChar>? {
    let level = Superwall.shared.logLevel
    let str: String
    switch level {
    case .debug: str = "debug"
    case .info: str = "info"
    case .warn: str = "warn"
    case .error: str = "error"
    case .none: str = "none"
    @unknown default: str = "warn"
    }
    return toCString(str)
}

@_cdecl("_SuperwallBridge_SetLogLevel")
public func _SuperwallBridge_SetLogLevel(_ logLevel: UnsafePointer<CChar>) {
    let level = String(cString: logLevel).lowercased()
    switch level {
    case "debug": Superwall.shared.logLevel = .debug
    case "info": Superwall.shared.logLevel = .info
    case "warn": Superwall.shared.logLevel = .warn
    case "error": Superwall.shared.logLevel = .error
    case "none": Superwall.shared.logLevel = .none
    default: break
    }
}

@_cdecl("_SuperwallBridge_GetEntitlements")
public func _SuperwallBridge_GetEntitlements() -> UnsafePointer<CChar>? {
    let entitlements = Superwall.shared.entitlements
    let active = serializeEntitlements(entitlements.active)
    let inactive = serializeEntitlements(entitlements.inactive)
    let all = serializeEntitlements(entitlements.all)
    let dict: [String: Any] = ["active": active, "inactive": inactive, "all": all]
    return toCString(toJsonString(dict))
}

@_cdecl("_SuperwallBridge_GetEntitlementsByProductIds")
public func _SuperwallBridge_GetEntitlementsByProductIds(_ productIdsJson: UnsafePointer<CChar>) -> UnsafePointer<CChar>? {
    // TODO: Implement when API is available
    return toCString("[]")
}

@_cdecl("_SuperwallBridge_GetCustomerInfo")
public func _SuperwallBridge_GetCustomerInfo(_ callbackId: UnsafePointer<CChar>) {
    let cbId = String(cString: callbackId)
    Task {
        let info = await Superwall.shared.getCustomerInfo()
        sendAsyncResponse(callbackId: cbId, data: serializeCustomerInfo(info))
    }
}

@_cdecl("_SuperwallBridge_GetSubscriptionStatus")
public func _SuperwallBridge_GetSubscriptionStatus() -> UnsafePointer<CChar>? {
    let status = Superwall.shared.subscriptionStatus
    return toCString(toJsonString(serializeSubscriptionStatus(status)))
}

@_cdecl("_SuperwallBridge_SetSubscriptionStatus")
public func _SuperwallBridge_SetSubscriptionStatus(_ statusJson: UnsafePointer<CChar>) {
    let json = String(cString: statusJson)
    if let dict = parseJson(json), let type = dict["type"] as? String {
        switch type.lowercased() {
        case "active":
            // TODO: Parse entitlements from JSON
            Superwall.shared.subscriptionStatus = .active(Set<Entitlement>())
        case "inactive":
            Superwall.shared.subscriptionStatus = .inactive
        default:
            Superwall.shared.subscriptionStatus = .unknown
        }
    }
}

@_cdecl("_SuperwallBridge_GetConfigurationStatus")
public func _SuperwallBridge_GetConfigurationStatus() -> UnsafePointer<CChar>? {
    let status = Superwall.shared.configurationStatus
    let str: String
    switch status {
    case .configured: str = "configured"
    case .pending: str = "pending"
    case .failed: str = "failed"
    @unknown default: str = "pending"
    }
    return toCString(str)
}

@_cdecl("_SuperwallBridge_GetIsConfigured")
public func _SuperwallBridge_GetIsConfigured() -> Bool {
    return Superwall.shared.configurationStatus == .configured
}

@_cdecl("_SuperwallBridge_GetIsPaywallPresented")
public func _SuperwallBridge_GetIsPaywallPresented() -> Bool {
    return Superwall.shared.isPaywallPresented
}

@_cdecl("_SuperwallBridge_PreloadAllPaywalls")
public func _SuperwallBridge_PreloadAllPaywalls() {
    Superwall.shared.preloadAllPaywalls()
}

@_cdecl("_SuperwallBridge_PreloadPaywallsForPlacements")
public func _SuperwallBridge_PreloadPaywallsForPlacements(_ placementNamesJson: UnsafePointer<CChar>) {
    let json = String(cString: placementNamesJson)
    if let names = parseJsonArray(json) as? [String] {
        Superwall.shared.preloadPaywalls(forPlacements: Set(names))
    }
}

@_cdecl("_SuperwallBridge_HandleDeepLink")
public func _SuperwallBridge_HandleDeepLink(_ url: UnsafePointer<CChar>) -> Bool {
    let urlStr = String(cString: url)
    guard let url = URL(string: urlStr) else { return false }
    return Superwall.handleDeepLink(url)
}

@_cdecl("_SuperwallBridge_TogglePaywallSpinner")
public func _SuperwallBridge_TogglePaywallSpinner(_ isHidden: Bool) {
    Superwall.shared.togglePaywallSpinner(isHidden: isHidden)
}

@_cdecl("_SuperwallBridge_GetLatestPaywallInfo")
public func _SuperwallBridge_GetLatestPaywallInfo() -> UnsafePointer<CChar>? {
    guard let info = Superwall.shared.latestPaywallInfo else { return nil }
    return toCString(toJsonString(serializePaywallInfo(info)))
}

@_cdecl("_SuperwallBridge_RegisterPlacement")
public func _SuperwallBridge_RegisterPlacement(
    _ placement: UnsafePointer<CChar>,
    _ paramsJson: UnsafePointer<CChar>?,
    _ handlerId: UnsafePointer<CChar>?,
    _ featureId: UnsafePointer<CChar>?,
    _ callbackId: UnsafePointer<CChar>?
) {
    let placementName = String(cString: placement)
    let params = toSwiftString(paramsJson).flatMap { parseJson($0) }
    let hId = toSwiftString(handlerId)
    let fId = toSwiftString(featureId)
    let cbId = toSwiftString(callbackId)

    var handler: PaywallPresentationHandler? = nil
    if hId != nil {
        handler = PaywallPresentationHandler()
        handler?.onPresent { info in
            sendToUnity(method: "onPresent", data: [
                "handlerId": hId!,
                "paywallInfo": serializePaywallInfo(info)
            ])
        }
        handler?.onDismiss { info, result in
            var data: [String: Any] = [
                "handlerId": hId!,
                "paywallInfo": serializePaywallInfo(info)
            ]
            switch result {
            case .purchased(let productId):
                data["result"] = ["type": "purchased", "productId": productId]
            case .declined:
                data["result"] = ["type": "declined"]
            case .restored:
                data["result"] = ["type": "restored"]
            @unknown default:
                data["result"] = ["type": "declined"]
            }
            sendToUnity(method: "onDismiss", data: data)
        }
        handler?.onError { error in
            sendToUnity(method: "onError", data: [
                "handlerId": hId!,
                "error": error.localizedDescription
            ])
        }
        handler?.onSkip { reason in
            let reasonStr: String
            switch reason {
            case .holdout: reasonStr = "holdout"
            case .noAudienceMatch: reasonStr = "noAudienceMatch"
            case .placementNotFound: reasonStr = "placementNotFound"
            @unknown default: reasonStr = "noAudienceMatch"
            }
            sendToUnity(method: "onSkip", data: [
                "handlerId": hId!,
                "reason": reasonStr
            ])
        }
    }

    let feature: (() -> Void)? = fId.map { featureId in
        return {
            sendToUnity(method: "onFeature", data: ["handlerId": featureId])
        }
    }

    if let feature = feature {
        Superwall.shared.register(placement: placementName, params: params, handler: handler, feature: feature)
    } else {
        Superwall.shared.register(placement: placementName, params: params, handler: handler)
    }

    if let cbId = cbId {
        sendAsyncResponse(callbackId: cbId, data: ["success": true])
    }
}

@_cdecl("_SuperwallBridge_Dismiss")
public func _SuperwallBridge_Dismiss() {
    Superwall.shared.dismiss()
}

@_cdecl("_SuperwallBridge_GetPresentationResult")
public func _SuperwallBridge_GetPresentationResult(
    _ placement: UnsafePointer<CChar>,
    _ paramsJson: UnsafePointer<CChar>?,
    _ callbackId: UnsafePointer<CChar>
) {
    let placementName = String(cString: placement)
    let params = toSwiftString(paramsJson).flatMap { parseJson($0) }
    let cbId = String(cString: callbackId)

    Task {
        let result = await Superwall.shared.getPresentationResult(forPlacement: placementName, params: params)
        var data: [String: Any] = [:]
        switch result {
        case .placementNotFound:
            data["type"] = "placementNotFound"
        case .noAudienceMatch:
            data["type"] = "noAudienceMatch"
        case .paywall(let experiment):
            data["type"] = "paywall"
            data["experiment"] = serializeExperiment(experiment)
        case .holdout(let experiment):
            data["type"] = "holdout"
            data["experiment"] = serializeExperiment(experiment)
        case .paywallNotAvailable:
            data["type"] = "paywallNotAvailable"
        @unknown default:
            data["type"] = "unknown"
        }
        sendAsyncResponse(callbackId: cbId, data: data)
    }
}

@_cdecl("_SuperwallBridge_ConfirmAllAssignments")
public func _SuperwallBridge_ConfirmAllAssignments(_ callbackId: UnsafePointer<CChar>) {
    let cbId = String(cString: callbackId)
    Task {
        let assignments = await Superwall.shared.confirmAllAssignments()
        let serialized = assignments.map { assignment -> [String: Any] in
            return [
                "experimentId": assignment.experimentId,
                "variant": [
                    "id": assignment.variant.id,
                    "type": assignment.variant.type == .treatment ? "treatment" : "holdout",
                    "paywallId": assignment.variant.paywallId as Any
                ] as [String: Any]
            ]
        }
        sendAsyncResponse(callbackId: cbId, data: ["assignments": serialized])
    }
}

@_cdecl("_SuperwallBridge_RestorePurchases")
public func _SuperwallBridge_RestorePurchases(_ callbackId: UnsafePointer<CChar>) {
    let cbId = String(cString: callbackId)
    Task {
        let result = await Superwall.shared.restorePurchases()
        switch result {
        case .restored:
            sendAsyncResponse(callbackId: cbId, data: ["type": "restored"])
        case .failed(let error):
            sendAsyncResponse(callbackId: cbId, data: ["type": "failed", "error": error?.localizedDescription ?? ""])
        @unknown default:
            sendAsyncResponse(callbackId: cbId, data: ["type": "failed", "error": "unknown"])
        }
    }
}

@_cdecl("_SuperwallBridge_GetOverrideProductsByName")
public func _SuperwallBridge_GetOverrideProductsByName() -> UnsafePointer<CChar>? {
    // TODO: Implement when API is available
    return nil
}

@_cdecl("_SuperwallBridge_SetOverrideProductsByName")
public func _SuperwallBridge_SetOverrideProductsByName(_ productsJson: UnsafePointer<CChar>?) {
    // TODO: Implement when API is available
}

@_cdecl("_SuperwallBridge_Consume")
public func _SuperwallBridge_Consume(_ purchaseToken: UnsafePointer<CChar>, _ callbackId: UnsafePointer<CChar>) {
    // iOS does not use consume — this is Android-only
    let cbId = String(cString: callbackId)
    sendAsyncResponse(callbackId: cbId, data: ["result": "not_applicable"])
}

@_cdecl("_SuperwallBridge_RespondToPurchaseController")
public func _SuperwallBridge_RespondToPurchaseController(_ callbackId: UnsafePointer<CChar>, _ resultJson: UnsafePointer<CChar>) {
    // TODO: Implement purchase controller flow
}

@_cdecl("_SuperwallBridge_RespondToRestorePurchases")
public func _SuperwallBridge_RespondToRestorePurchases(_ callbackId: UnsafePointer<CChar>, _ resultJson: UnsafePointer<CChar>) {
    // TODO: Implement restore flow
}
