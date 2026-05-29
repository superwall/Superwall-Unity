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

private func parseLogScope(_ str: String) -> LogScope? {
    switch str.lowercased() {
    case "localizationmanager": return .localizationManager
    case "analytics": return .analytics
    case "bouncebutton": return .bounceButton
    case "coredata": return .coreData
    case "configmanager": return .configManager
    case "identitymanager": return .identityManager
    case "debugmanager": return .debugManager
    case "debugviewcontroller": return .debugViewController
    case "localizationviewcontroller": return .localizationViewController
    case "gamecontrollermanager": return .gameControllerManager
    case "device": return .device
    case "network": return .network
    case "paywallevents": return .paywallEvents
    case "productsmanager": return .productsManager
    case "storekitmanager": return .storeKitManager
    case "placements": return .placements
    case "receipts": return .receipts
    case "superwallcore": return .superwallCore
    case "paywallpresentation": return .paywallPresentation
    case "transactions": return .transactions
    case "paywallviewcontroller": return .paywallViewController
    case "cache": return .cache
    case "webentitlements": return .webEntitlements
    case "all": return .all
    default: return nil
    }
}

private func parseIntegrationAttribute(_ str: String) -> IntegrationAttribute? {
    switch str {
    case "adjustId": return .adjustId
    case "amplitudeDeviceId": return .amplitudeDeviceId
    case "amplitudeUserId": return .amplitudeUserId
    case "appsflyerId": return .appsflyerId
    case "brazeAliasName": return .brazeAliasName
    case "brazeAliasLabel": return .brazeAliasLabel
    case "onesignalId": return .onesignalId
    case "fbAnonId": return .fbAnonId
    case "firebaseAppInstanceId": return .firebaseAppInstanceId
    case "firebaseInstallationId": return .firebaseInstallationId
    case "iterableUserId": return .iterableUserId
    case "iterableCampaignId": return .iterableCampaignId
    case "iterableTemplateId": return .iterableTemplateId
    case "mixpanelDistinctId": return .mixpanelDistinctId
    case "mparticleId": return .mparticleId
    case "clevertapId": return .clevertapId
    case "airshipChannelId": return .airshipChannelId
    case "kochavaDeviceId": return .kochavaDeviceId
    case "tenjinId": return .tenjinId
    case "posthogUserId": return .posthogUserId
    case "customerioId": return .customerioId
    case "appstackId": return .appstackId
    default: return nil
    }
}

private func parseEntitlementsFromJson(_ array: [[String: Any]]) -> Set<Entitlement> {
    var entitlements = Set<Entitlement>()
    for item in array {
        if let id = item["id"] as? String {
            entitlements.insert(Entitlement(id: id))
        }
    }
    return entitlements
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
        "productIds": info.productIds,
        "products": info.products.map { product -> [String: Any] in
            var p: [String: Any] = [
                "id": product.id,
                "entitlements": product.entitlements.map { serializeEntitlement($0) }
            ]
            if let name = product.name { p["name"] = name }
            return p
        },
        "presentedBy": info.presentedBy,
        "isFreeTrialAvailable": info.isFreeTrialAvailable,
        "featureGatingBehavior": info.featureGatingBehavior == .gated ? "gated" : "nonGated",
        "closeReason": serializePaywallCloseReason(info.closeReason),
        "state": info.state,
        "localNotifications": info.localNotifications.map { serializeLocalNotification($0) },
        "computedPropertyRequests": info.computedPropertyRequests.map { serializeComputedPropertyRequest($0) },
        "surveys": info.surveys.map { serializeSurvey($0) }
    ]
    if let name = info.presentedByPlacementWithName { dict["presentedByPlacementWithName"] = name }
    if let id = info.presentedByPlacementWithId { dict["presentedByPlacementWithId"] = id }
    if let at = info.presentedByPlacementAt { dict["presentedByPlacementAt"] = at }
    if let src = info.presentationSourceType { dict["presentationSourceType"] = src }
    if let v = info.paywalljsVersion { dict["paywalljsVersion"] = v }
    if let s = info.responseLoadStartTime { dict["responseLoadStartTime"] = s }
    if let s = info.responseLoadCompleteTime { dict["responseLoadCompleteTime"] = s }
    if let s = info.responseLoadFailTime { dict["responseLoadFailTime"] = s }
    if let d = info.responseLoadDuration { dict["responseLoadDuration"] = d }
    if let s = info.webViewLoadStartTime { dict["webViewLoadStartTime"] = s }
    if let s = info.webViewLoadCompleteTime { dict["webViewLoadCompleteTime"] = s }
    if let s = info.webViewLoadFailTime { dict["webViewLoadFailTime"] = s }
    if let d = info.webViewLoadDuration { dict["webViewLoadDuration"] = d }
    if let s = info.productsLoadStartTime { dict["productsLoadStartTime"] = s }
    if let s = info.productsLoadCompleteTime { dict["productsLoadCompleteTime"] = s }
    if let s = info.productsLoadFailTime { dict["productsLoadFailTime"] = s }
    if let d = info.productsLoadDuration { dict["productsLoadDuration"] = d }
    if let experiment = info.experiment {
        dict["experiment"] = serializeExperiment(experiment)
    }
    return dict
}

private func serializePaywallCloseReason(_ reason: PaywallCloseReason) -> String {
    switch reason {
    case .systemLogic: return "systemLogic"
    case .forNextPaywall: return "forNextPaywall"
    case .webViewFailedToLoad: return "webViewFailedToLoad"
    case .manualClose: return "manualClose"
    case .none: return "none"
    @unknown default: return "none"
    }
}

private func serializeLocalNotification(_ n: LocalNotification) -> [String: Any] {
    var dict: [String: Any] = [
        "type": n.type == .trialStarted ? "trialStarted" : "unsupported",
        "title": n.title,
        "body": n.body,
        "delay": n.delay
    ]
    if let s = n.subtitle { dict["subtitle"] = s }
    return dict
}

private func serializeComputedPropertyRequest(_ c: ComputedPropertyRequest) -> [String: Any] {
    let typeStr: String
    switch c.type {
    case .minutesSince: typeStr = "minutesSince"
    case .hoursSince: typeStr = "hoursSince"
    case .daysSince: typeStr = "daysSince"
    case .monthsSince: typeStr = "monthsSince"
    case .yearsSince: typeStr = "yearsSince"
    case .placementsInHour: typeStr = "placementsInHour"
    case .placementsInDay: typeStr = "placementsInDay"
    case .placementsInWeek: typeStr = "placementsInWeek"
    case .placementsInMonth: typeStr = "placementsInMonth"
    case .placementsSinceInstall: typeStr = "placementsSinceInstall"
    @unknown default: typeStr = "minutesSince"
    }
    return ["type": typeStr, "eventName": c.placementName]
}

private func serializeSurvey(_ s: Survey) -> [String: Any] {
    let conditionStr: String
    switch s.presentationCondition {
    case .onManualClose: conditionStr = "onManualClose"
    case .onPurchase: conditionStr = "onPurchase"
    @unknown default: conditionStr = "onManualClose"
    }
    return [
        "id": s.id,
        "title": s.title,
        "message": s.message,
        "options": s.options.map { ["id": $0.id, "text": $0.title] },
        "presentationCondition": conditionStr,
        "presentationProbability": s.presentationProbability,
        "includeOtherOption": s.includeOtherOption,
        "includeCloseOption": s.includeCloseOption
    ]
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

    func handleSuperwallDeepLink(
        _ fullURL: URL,
        pathComponents: [String],
        queryParameters: [String: String]
    ) {
        sendToUnity(method: "handleSuperwallDeepLink", data: [
            "fullURL": fullURL.absoluteString,
            "pathComponents": pathComponents,
            "queryParameters": queryParameters
        ])
    }

    func userAttributesDidChange(newAttributes: [String: Any]) {
        sendToUnity(method: "userAttributesDidChange", data: [
            "newAttributes": newAttributes
        ])
    }
}

// MARK: - Unity Purchase Controller

private class UnityPurchaseController: PurchaseController {
    private var pendingPurchaseContinuations: [String: CheckedContinuation<PurchaseResult, Never>] = [:]
    private var pendingRestoreContinuations: [String: CheckedContinuation<RestorationResult, Never>] = [:]
    private let lock = NSLock()

    @MainActor
    func purchase(product: StoreProduct) async -> PurchaseResult {
        let callbackId = UUID().uuidString
        return await withCheckedContinuation { continuation in
            lock.lock()
            pendingPurchaseContinuations[callbackId] = continuation
            lock.unlock()

            sendToUnity(method: "purchaseFromAppStore", data: [
                "productId": product.productIdentifier,
                "callbackId": callbackId
            ])
        }
    }

    @MainActor
    func restorePurchases() async -> RestorationResult {
        let callbackId = UUID().uuidString
        return await withCheckedContinuation { continuation in
            lock.lock()
            pendingRestoreContinuations[callbackId] = continuation
            lock.unlock()

            sendToUnity(method: "restorePurchases", data: [
                "callbackId": callbackId
            ])
        }
    }

    func respondToPurchase(callbackId: String, result: PurchaseResult) {
        lock.lock()
        let continuation = pendingPurchaseContinuations.removeValue(forKey: callbackId)
        lock.unlock()
        continuation?.resume(returning: result)
    }

    func respondToRestore(callbackId: String, result: RestorationResult) {
        lock.lock()
        let continuation = pendingRestoreContinuations.removeValue(forKey: callbackId)
        lock.unlock()
        continuation?.resume(returning: result)
    }
}

// MARK: - Stored State

private var unityDelegate: SuperwallUnityDelegate?
private var unityPurchaseController: UnityPurchaseController?
private var pendingFeatureHandlers: [String: () -> Void] = [:]
private var pendingLocalResources: [String: URL] = [:]
private var didConfigure = false

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
        if let shouldObserve = dict["shouldObservePurchases"] as? Bool {
            options?.shouldObservePurchases = shouldObserve
        }
        if let shouldBypass = dict["shouldBypassAppTransactionCheck"] as? Bool {
            options?.shouldBypassAppTransactionCheck = shouldBypass
        }
        if let maxRetry = dict["maxConfigRetryCount"] as? Int {
            options?.maxConfigRetryCount = maxRetry
        }
        // networkEnvironment
        if let networkEnv = dict["networkEnvironment"] as? String {
            switch networkEnv.lowercased() {
            case "release": options?.networkEnvironment = .release
            case "developer": options?.networkEnvironment = .developer
            case "releasecandidate": options?.networkEnvironment = .releaseCandidate
            default: break
            }
        }
        // logging
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
            if let scopes = logging["scopes"] as? [String] {
                var logScopes: Set<LogScope> = []
                for scope in scopes {
                    if let mapped = parseLogScope(scope) {
                        logScopes.insert(mapped)
                    }
                }
                if !logScopes.isEmpty {
                    options?.logging.scopes = logScopes
                }
            }
        }
        // paywalls
        if let paywalls = dict["paywalls"] as? [String: Any] {
            if let haptic = paywalls["isHapticFeedbackEnabled"] as? Bool {
                options?.paywalls.isHapticFeedbackEnabled = haptic
            }
            if let purchaseFailure = paywalls["shouldShowPurchaseFailureAlert"] as? Bool {
                options?.paywalls.shouldShowPurchaseFailureAlert = purchaseFailure
            }
            if let preload = paywalls["shouldPreload"] as? Bool {
                options?.paywalls.shouldPreload = preload
            }
            if let autoDismiss = paywalls["automaticallyDismiss"] as? Bool {
                options?.paywalls.automaticallyDismiss = autoDismiss
            }
            if let webRestore = paywalls["shouldShowWebRestorationAlert"] as? Bool {
                options?.paywalls.shouldShowWebRestorationAlert = webRestore
            }
            if let webPurchaseConfirm = paywalls["shouldShowWebPurchaseConfirmationAlert"] as? Bool {
                options?.paywalls.shouldShowWebPurchaseConfirmationAlert = webPurchaseConfirm
            }
            if let bgView = paywalls["transactionBackgroundView"] as? String {
                switch bgView.lowercased() {
                case "spinner": options?.paywalls.transactionBackgroundView = .spinner
                case "none": options?.paywalls.transactionBackgroundView = .none
                default: break
                }
            }
            if let restoreFailed = paywalls["restoreFailed"] as? [String: Any] {
                if let title = restoreFailed["title"] as? String {
                    options?.paywalls.restoreFailed.title = title
                }
                if let message = restoreFailed["message"] as? String {
                    options?.paywalls.restoreFailed.message = message
                }
                if let closeButton = restoreFailed["closeButtonTitle"] as? String {
                    options?.paywalls.restoreFailed.closeButtonTitle = closeButton
                }
            }
            if let overrides = paywalls["overrideProductsByName"] as? [String: String] {
                options?.paywalls.overrideProductsByName = overrides
            }
        }
        // Note: passIdentifiersToPlayStore is Android-only, skipped on iOS.
        // Note: useMockReviews is not available in SuperwallKit for iOS, skipped.
    }

    // Set up purchase controller if C# side has one
    var purchaseController: PurchaseController? = nil
    if hasPurchaseController {
        let controller = UnityPurchaseController()
        unityPurchaseController = controller
        purchaseController = controller
    }

    if !pendingLocalResources.isEmpty {
        if options == nil { options = SuperwallOptions() }
        var resourceMap: [String: AssetResource] = [:]
        for (k, v) in pendingLocalResources { resourceMap[k] = v }
        options?.localResources = resourceMap
    }

    let completion: (() -> Void)? = callbackId.map { cbId in
        return {
            sendAsyncResponse(callbackId: cbId, data: ["success": true])
        }
    }

    didConfigure = true
    _ = Superwall.configure(apiKey: key, purchaseController: purchaseController, options: options, completion: completion)
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
    let attrName = String(cString: attribute)
    let attrValue = toSwiftString(value)
    if let integrationAttr = parseIntegrationAttribute(attrName) {
        Superwall.shared.setIntegrationAttribute(integrationAttr, attrValue)
    }
}

@_cdecl("_SuperwallBridge_SetIntegrationAttributes")
public func _SuperwallBridge_SetIntegrationAttributes(_ attributesJson: UnsafePointer<CChar>) {
    let json = String(cString: attributesJson)
    if let dict = parseJson(json) {
        var props: [IntegrationAttribute: String?] = [:]
        for (key, value) in dict {
            if let attr = parseIntegrationAttribute(key) {
                props[attr] = value as? String
            }
        }
        if !props.isEmpty {
            Superwall.shared.setIntegrationAttributes(props)
        }
    }
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
    let json = String(cString: productIdsJson)
    guard let productIds = parseJsonArray(json) as? [String] else {
        return toCString("[]")
    }
    let entitlements = Superwall.shared.entitlements.byProductIds(Set(productIds))
    let serialized = serializeEntitlements(entitlements)
    return toCString(toJsonString(serialized))
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
            var entitlements = Set<Entitlement>()
            if let entitlementsArray = dict["entitlements"] as? [[String: Any]] {
                entitlements = parseEntitlementsFromJson(entitlementsArray)
            }
            Superwall.shared.subscriptionStatus = .active(entitlements)
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
    guard let overrides = Superwall.shared.overrideProductsByName else { return nil }
    return toCString(toJsonString(overrides))
}

@_cdecl("_SuperwallBridge_SetOverrideProductsByName")
public func _SuperwallBridge_SetOverrideProductsByName(_ productsJson: UnsafePointer<CChar>?) {
    if let json = toSwiftString(productsJson), let dict = parseJson(json) {
        var overrides: [String: String] = [:]
        for (key, value) in dict {
            if let strValue = value as? String {
                overrides[key] = strValue
            }
        }
        Superwall.shared.overrideProductsByName = overrides
    } else {
        Superwall.shared.overrideProductsByName = nil
    }
}

@_cdecl("_SuperwallBridge_Purchase")
public func _SuperwallBridge_Purchase(
    _ productId: UnsafePointer<CChar>,
    _ callbackId: UnsafePointer<CChar>
) {
    let prodId = String(cString: productId)
    let cbId = String(cString: callbackId)

    Task {
        let products = await Superwall.shared.products(for: Set([prodId]))
        guard let product = products.first else {
            sendAsyncResponse(callbackId: cbId, data: [
                "type": "failed",
                "error": "Product not found: \(prodId)"
            ])
            return
        }
        let result = await Superwall.shared.purchase(product)
        switch result {
        case .purchased:
            sendAsyncResponse(callbackId: cbId, data: ["type": "purchased"])
        case .cancelled:
            sendAsyncResponse(callbackId: cbId, data: ["type": "cancelled"])
        case .pending:
            sendAsyncResponse(callbackId: cbId, data: ["type": "pending"])
        case .failed(let error):
            sendAsyncResponse(callbackId: cbId, data: [
                "type": "failed",
                "error": error.localizedDescription
            ])
        @unknown default:
            sendAsyncResponse(callbackId: cbId, data: ["type": "failed", "error": "unknown"])
        }
    }
}

@_cdecl("_SuperwallBridge_GetProducts")
public func _SuperwallBridge_GetProducts(
    _ productIdsJson: UnsafePointer<CChar>,
    _ callbackId: UnsafePointer<CChar>
) {
    let json = String(cString: productIdsJson)
    let cbId = String(cString: callbackId)

    guard let productIds = parseJsonArray(json) as? [String] else {
        sendAsyncResponse(callbackId: cbId, data: ["products": []])
        return
    }

    Task {
        let products = await Superwall.shared.products(for: Set(productIds))
        var serialized: [String: Any] = [:]
        for product in products {
            serialized[product.productIdentifier] = serializeStoreProduct(product)
        }
        sendAsyncResponse(callbackId: cbId, data: ["products": serialized])
    }
}

private func serializeStoreProduct(_ product: StoreProduct) -> [String: Any] {
    var dict: [String: Any] = [
        "productIdentifier": product.productIdentifier,
        "localizedPrice": product.localizedPrice,
        "localizedSubscriptionPeriod": product.localizedSubscriptionPeriod,
        "period": product.period,
        "periodly": product.periodly,
        "periodDays": product.periodDays,
        "periodDaysString": product.periodDaysString,
        "periodWeeks": product.periodWeeks,
        "periodWeeksString": product.periodWeeksString,
        "periodMonths": product.periodMonths,
        "periodMonthsString": product.periodMonthsString,
        "periodYears": product.periodYears,
        "periodYearsString": product.periodYearsString,
        "dailyPrice": product.dailyPrice,
        "weeklyPrice": product.weeklyPrice,
        "monthlyPrice": product.monthlyPrice,
        "yearlyPrice": product.yearlyPrice,
        "hasFreeTrial": product.hasFreeTrial,
        "trialPeriodEndDateString": product.trialPeriodEndDateString,
        "localizedTrialPeriodPrice": product.localizedTrialPeriodPrice,
        "trialPeriodPrice": NSDecimalNumber(decimal: product.trialPeriodPrice).doubleValue,
        "trialPeriodDays": product.trialPeriodDays,
        "trialPeriodDaysString": product.trialPeriodDaysString,
        "trialPeriodWeeks": product.trialPeriodWeeks,
        "trialPeriodWeeksString": product.trialPeriodWeeksString,
        "trialPeriodMonths": product.trialPeriodMonths,
        "trialPeriodMonthsString": product.trialPeriodMonthsString,
        "trialPeriodYears": product.trialPeriodYears,
        "trialPeriodYearsString": product.trialPeriodYearsString,
        "trialPeriodText": product.trialPeriodText,
        "locale": product.locale,
        "isFamilyShareable": product.isFamilyShareable,
        "price": NSDecimalNumber(decimal: product.price).doubleValue,
        "attributes": product.attributes,
        "entitlements": product.entitlements.map { serializeEntitlement($0) }
    ]
    if let groupId = product.subscriptionGroupIdentifier {
        dict["subscriptionGroupIdentifier"] = groupId
    }
    if let lang = product.languageCode {
        dict["languageCode"] = lang
    }
    if let currency = product.currencyCode {
        dict["currencyCode"] = currency
    }
    if let symbol = product.currencySymbol {
        dict["currencySymbol"] = symbol
    }
    if let region = product.regionCode {
        dict["regionCode"] = region
    }
    if let endDate = product.trialPeriodEndDate {
        dict["trialPeriodEndDate"] = ISO8601DateFormatter().string(from: endDate)
    }
    return dict
}

@_cdecl("_SuperwallBridge_GetAssignments")
public func _SuperwallBridge_GetAssignments(_ callbackId: UnsafePointer<CChar>) {
    let cbId = String(cString: callbackId)
    let assignments = Superwall.shared.getAssignments()
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

@_cdecl("_SuperwallBridge_RefreshConfiguration")
public func _SuperwallBridge_RefreshConfiguration() {
    Task {
        await Superwall.shared.refreshConfiguration()
    }
}

@_cdecl("_SuperwallBridge_Consume")
public func _SuperwallBridge_Consume(_ purchaseToken: UnsafePointer<CChar>, _ callbackId: UnsafePointer<CChar>) {
    // iOS does not use consume — this is Android-only
    let cbId = String(cString: callbackId)
    sendAsyncResponse(callbackId: cbId, data: ["result": "not_applicable"])
}

@_cdecl("_SuperwallBridge_RespondToPurchaseController")
public func _SuperwallBridge_RespondToPurchaseController(_ callbackId: UnsafePointer<CChar>, _ resultJson: UnsafePointer<CChar>) {
    let cbId = String(cString: callbackId)
    let json = String(cString: resultJson)
    guard let dict = parseJson(json), let type = dict["type"] as? String else { return }

    let result: PurchaseResult
    switch type.lowercased() {
    case "purchased":
        result = .purchased
    case "cancelled":
        result = .cancelled
    case "pending":
        result = .pending
    case "failed":
        let errorMessage = dict["error"] as? String ?? "Unknown error"
        result = .failed(NSError(domain: "com.superwall.unity", code: -1, userInfo: [NSLocalizedDescriptionKey: errorMessage]))
    default:
        result = .cancelled
    }

    unityPurchaseController?.respondToPurchase(callbackId: cbId, result: result)
}

@_cdecl("_SuperwallBridge_RespondToRestorePurchases")
public func _SuperwallBridge_RespondToRestorePurchases(_ callbackId: UnsafePointer<CChar>, _ resultJson: UnsafePointer<CChar>) {
    let cbId = String(cString: callbackId)
    let json = String(cString: resultJson)
    guard let dict = parseJson(json), let type = dict["type"] as? String else { return }

    let result: RestorationResult
    switch type.lowercased() {
    case "restored":
        result = .restored
    case "failed":
        let errorMessage = dict["error"] as? String
        let error: Error? = errorMessage.map { NSError(domain: "com.superwall.unity", code: -1, userInfo: [NSLocalizedDescriptionKey: $0]) }
        result = .failed(error)
    default:
        result = .failed(nil)
    }

    unityPurchaseController?.respondToRestore(callbackId: cbId, result: result)
}

@_cdecl("_SuperwallBridge_ShowAlert")
public func _SuperwallBridge_ShowAlert(_ alertJson: UnsafePointer<CChar>?) {
    // SuperwallKit (iOS) does not expose a public showAlert API. The bridge accepts the
    // call so the DllImport symbol resolves, logs a warning, then fires the close
    // callback immediately so the C# side cleans up its pending callback entry.
    NSLog("[SuperwallUnityBridge] ShowAlert called (no-op on iOS)")
    guard let json = toSwiftString(alertJson), let dict = parseJson(json) else { return }
    if let closeId = dict["onCloseCallbackId"] as? String {
        sendAsyncResponse(callbackId: closeId, data: ["action": "closed"])
    }
}

@_cdecl("_SuperwallBridge_SetLocalResources")
public func _SuperwallBridge_SetLocalResources(_ resourcesJson: UnsafePointer<CChar>) {
    let json = String(cString: resourcesJson)
    guard let dict = parseJson(json) else {
        pendingLocalResources = [:]
        return
    }
    var resources: [String: URL] = [:]
    for (key, value) in dict {
        guard let path = value as? String, !path.isEmpty else { continue }
        let url: URL?
        if let parsed = URL(string: path), parsed.scheme != nil {
            url = parsed
        } else {
            url = URL(fileURLWithPath: path)
        }
        if let url = url {
            resources[key] = url
        }
    }
    pendingLocalResources = resources
    if didConfigure {
        NSLog("[SuperwallUnityBridge] SetLocalResources called after Configure — on iOS, local resources must be set before Configure or they will not be picked up.")
    }
}
