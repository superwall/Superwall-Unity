import Foundation
import GameController
import SuperwallKit
import UIKit
import UserNotifications

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

// Coerce any value JSONSerialization would reject (Swift enums/structs, URL,
// __SwiftValue, etc.) into a JSON-legal form. JSONSerialization raises an
// Objective-C NSException on invalid input, which `try?` cannot catch, so the
// input must be made valid BEFORE serialization. Forwarded event params (e.g.
// enrichment) can carry such non-JSON leaves and otherwise crash the app.
private func sanitizeForJson(_ value: Any) -> Any {
    switch value {
    case let dict as [String: Any]:
        var out = [String: Any](minimumCapacity: dict.count)
        for (key, element) in dict {
            out[key] = sanitizeForJson(element)
        }
        return out
    case let array as [Any]:
        return array.map { sanitizeForJson($0) }
    case is NSNull, is NSNumber, is String:
        return value
    default:
        return String(describing: value)
    }
}

private func toJsonString(_ obj: Any) -> String? {
    let sanitized = sanitizeForJson(obj)
    guard JSONSerialization.isValidJSONObject(sanitized),
          let data = try? JSONSerialization.data(withJSONObject: sanitized),
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
    case "grantedentitlements": return .grantedEntitlements
    case "customercenter": return .customerCenter
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
    case "singularDeviceId": return .singularDeviceId
    case "airbridgeId": return .airbridgeId
    default: return nil
    }
}

private func parseEventTrackingBehavior(_ str: String?) -> EventTrackingBehavior? {
    switch str {
    case "all": return .all
    case "superwallOnly": return .superwallOnly
    case "none": return EventTrackingBehavior.none
    default: return nil
    }
}

private func serializeEventTrackingBehavior(_ behavior: EventTrackingBehavior) -> String {
    switch behavior {
    case .all: return "all"
    case .superwallOnly: return "superwallOnly"
    case .none: return "none"
    @unknown default: return "all"
    }
}

private func parseAdConsent(_ dict: [String: Any]) -> AdConsent {
    func status(_ key: String) -> AdConsentStatus {
        return dict[key] as? String == "denied" ? .denied : .granted
    }
    return AdConsent(adUserData: status("adUserData"), adPersonalization: status("adPersonalization"))
}

private func serializeAdConsentStatus(_ status: AdConsentStatus) -> String {
    switch status {
    case .denied: return "denied"
    default: return "granted"
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

// Returns false if the payload could not be serialized, so C# will never see it. Most callers
// ignore this; custom callbacks use it so they never wait on a request C# never got.
@discardableResult
private func sendToUnity(method: String, data: [String: Any]) -> Bool {
    let payload: [String: Any] = ["method": method, "data": data]
    guard let json = toJsonString(payload) else { return false }
    let cStr = (json as NSString).utf8String!
    UnitySendMessage("SuperwallBridge", "OnCallback", cStr)
    return true
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

private func epochMilliseconds(_ date: Date) -> Int64 {
    return Int64((date.timeIntervalSince1970 * 1000).rounded())
}

// `ProductStore` and `EntitlementType` are `@objc` Int enums, which `String(describing:)` prints as
// the type name, not the case, so C# could never parse `Entitlement.Store`. Name the cases here, in
// the vocabulary the Android bridge uses.
private func serializeProductStore(_ store: ProductStore) -> String {
    switch store {
    case .appStore: return "appStore"
    case .stripe: return "stripe"
    case .paddle: return "paddle"
    case .playStore: return "playStore"
    case .superwall: return "superwall"
    case .custom: return "custom"
    case .other: return "other"
    @unknown default: return "other"
    }
}

// Mirrors every field of the C# `Entitlement` model (see Runtime/Models/Entitlement.cs), which
// `BridgeCallbackHandler.DeserializeEntitlement` already reads. Enum values are emitted as their
// case names, which the C# side parses case-insensitively (`playStore` -> `ProductStore.PlayStore`,
// `inGracePeriod` -> `LatestSubscriptionState.InGracePeriod`, `trial` ->
// `LatestSubscriptionOfferType.Trial`). Dates are epoch milliseconds. Optional fields are omitted
// rather than sent as null so the C# nullable stays null.
private func serializeEntitlement(_ entitlement: Entitlement) -> [String: Any] {
    var dict: [String: Any] = [
        "id": entitlement.id,
        "type": "serviceLevel",
        "isActive": entitlement.isActive,
        "productIds": Array(entitlement.productIds)
    ]
    if let latestProductId = entitlement.latestProductId {
        dict["latestProductId"] = latestProductId
    }
    if let store = entitlement.store {
        dict["store"] = serializeProductStore(store)
    }
    if let startsAt = entitlement.startsAt {
        dict["startsAt"] = epochMilliseconds(startsAt)
    }
    if let renewedAt = entitlement.renewedAt {
        dict["renewedAt"] = epochMilliseconds(renewedAt)
    }
    if let expiresAt = entitlement.expiresAt {
        dict["expiresAt"] = epochMilliseconds(expiresAt)
    }
    if let isLifetime = entitlement.isLifetime {
        dict["isLifetime"] = isLifetime
    }
    if let willRenew = entitlement.willRenew {
        dict["willRenew"] = willRenew
    }
    if let state = entitlement.state {
        dict["state"] = state.rawValue
    }
    if let offerType = entitlement.offerType {
        dict["offerType"] = offerType.rawValue
    }
    if let isRevoked = entitlement.isRevoked {
        dict["isRevoked"] = isRevoked
    }
    if let renewsAt = entitlement.renewsAt {
        dict["renewsAt"] = epochMilliseconds(renewsAt)
    }
    if let isInGracePeriod = entitlement.isInGracePeriod {
        dict["isInGracePeriod"] = isInGracePeriod
    }
    return dict
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
        "products": info.products.map { serializeProduct($0) },
        "presentedBy": info.presentedBy,
        "isFreeTrialAvailable": info.isFreeTrialAvailable,
        "featureGatingBehavior": info.featureGatingBehavior == .gated ? "gated" : "nonGated",
        "closeReason": serializePaywallCloseReason(info.closeReason),
        "state": info.state,
        "localNotifications": info.localNotifications.map { serializeLocalNotification($0) },
        "computedPropertyRequests": info.computedPropertyRequests.map { serializeComputedPropertyRequest($0) },
        "surveys": info.surveys.map { serializeSurvey($0) },
        "cacheKey": info.cacheKey,
        "buildId": info.buildId,
        "isScrollEnabled": info.isScrollEnabled,
        "isLocal": info.isLocal,
        "presentation": serializePresentation(info.presentation),
        "introOfferEligibility": serializeIntroOfferEligibility(info.introOfferEligibility)
    ]
    if let s = info.shimmerLoadStartTime { dict["shimmerLoadStartTime"] = s }
    if let s = info.shimmerLoadCompleteTime { dict["shimmerLoadCompleteTime"] = s }
    if let id = info.presentationId { dict["presentationId"] = id }
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

// `id` and `productIdentifier` are both the bare product identifier here; Android's `id` is its composite one.
private func serializeProduct(_ product: Product) -> [String: Any] {
    var dict: [String: Any] = [
        "id": product.id,
        "productIdentifier": product.id,
        "entitlements": product.entitlements.map { serializeEntitlement($0) }
    ]
    if let name = product.name { dict["name"] = name }
    switch product.type {
    case .appStore(let appStoreProduct):
        dict["store"] = "appStore"
        var details: [String: Any] = ["id": appStoreProduct.id]
        if let billingPlanType = appStoreProduct.billingPlanType {
            details["billingPlanType"] = serializeBillingPlanType(billingPlanType)
        }
        dict["appStoreProduct"] = details
    case .stripe(let stripeProduct):
        dict["store"] = "stripe"
        var details: [String: Any] = ["id": stripeProduct.id]
        if let trialDays = stripeProduct.trialDays { details["trialDays"] = trialDays }
        dict["stripeProduct"] = details
    case .paddle(let paddleProduct):
        dict["store"] = "paddle"
        dict["paddleProduct"] = ["id": paddleProduct.id]
    case .custom(let customProduct):
        dict["store"] = "custom"
        dict["customProduct"] = ["id": customProduct.id]
    }
    return dict
}

private func serializeBillingPlanType(_ type: AppStoreProduct.BillingPlanType) -> String {
    switch type {
    case .upFront: return "upFront"
    case .monthly: return "monthly"
    @unknown default: return "upFront"
    }
}

private func serializePresentation(_ presentation: PaywallPresentationInfo) -> [String: Any] {
    var dict: [String: Any] = ["delay": presentation.delay]
    switch presentation.style {
    case .modal: dict["style"] = "modal"
    case .fullscreen: dict["style"] = "fullscreen"
    case .fullscreenNoAnimation: dict["style"] = "fullscreenNoAnimation"
    case .push: dict["style"] = "push"
    case .popup(let height, let width, let cornerRadius):
        dict["style"] = "popup"
        dict["height"] = height
        dict["width"] = width
        dict["cornerRadius"] = cornerRadius
    case .drawer(let height, let cornerRadius):
        dict["style"] = "drawer"
        dict["height"] = height
        dict["cornerRadius"] = cornerRadius
    case .none: dict["style"] = "none"
    @unknown default: dict["style"] = "none"
    }
    return dict
}

private func serializeIntroOfferEligibility(_ eligibility: IntroOfferEligibility) -> String {
    switch eligibility {
    case .eligible: return "eligible"
    case .ineligible: return "ineligible"
    case .automatic: return "automatic"
    @unknown default: return "automatic"
    }
}

private func serializeSubscriptionPeriodUnit(_ unit: SubscriptionPeriod.Unit) -> String {
    switch unit {
    case .day: return "day"
    case .week: return "week"
    case .month: return "month"
    case .year: return "year"
    @unknown default: return "day"
    }
}

private func serializeSubscriptionPeriod(_ period: SubscriptionPeriod) -> [String: Any] {
    return ["value": period.value, "unit": serializeSubscriptionPeriodUnit(period.unit)]
}

private func serializeDiscount(_ discount: StoreProductDiscount) -> [String: Any] {
    var dict: [String: Any] = [
        "price": NSDecimalNumber(decimal: discount.price).doubleValue,
        "localizedPriceString": discount.localizedPriceString,
        "subscriptionPeriod": serializeSubscriptionPeriod(discount.subscriptionPeriod),
        "numberOfPeriods": discount.numberOfPeriods
    ]
    if let offerIdentifier = discount.offerIdentifier { dict["offerIdentifier"] = offerIdentifier }
    if let currencyCode = discount.currencyCode { dict["currencyCode"] = currencyCode }
    switch discount.paymentMode {
    case .payAsYouGo: dict["paymentMode"] = "payAsYouGo"
    case .payUpFront: dict["paymentMode"] = "payUpFront"
    case .freeTrial: dict["paymentMode"] = "freeTrial"
    @unknown default: dict["paymentMode"] = "payAsYouGo"
    }
    switch discount.type {
    case .introductory: dict["type"] = "introductory"
    case .promotional: dict["type"] = "promotional"
    @unknown default: dict["type"] = "introductory"
    }
    return dict
}

// The provider and confidence go out under SuperwallKit's raw values (`mmp`, `apple_search_ads`, `high`).
private func serializeAttributionMatchInfo(_ info: AttributionMatchInfo) -> [String: Any] {
    var dict: [String: Any] = [
        "provider": info.provider.rawValue,
        "matched": info.matched
    ]
    if let source = info.source { dict["source"] = source }
    if let confidence = info.confidence { dict["confidence"] = confidence.rawValue }
    if let matchScore = info.matchScore { dict["matchScore"] = matchScore }
    if let reason = info.reason { dict["reason"] = reason }
    return dict
}

private func serializePageViewData(_ data: PageViewData) -> [String: Any] {
    var dict: [String: Any] = [
        "pageNodeId": data.pageNodeId,
        "flowPosition": data.flowPosition,
        "pageName": data.pageName,
        "navigationNodeId": data.navigationNodeId,
        "navigationType": data.navigationType
    ]
    if let previousPageNodeId = data.previousPageNodeId { dict["previousPageNodeId"] = previousPageNodeId }
    if let previousFlowPosition = data.previousFlowPosition { dict["previousFlowPosition"] = previousFlowPosition }
    if let timeOnPreviousPageMs = data.timeOnPreviousPageMs { dict["timeOnPreviousPageMs"] = timeOnPreviousPageMs }
    return dict
}

private func serializeTransactionProduct(_ product: TransactionProduct) -> [String: Any] {
    var dict: [String: Any] = [
        "id": product.id,
        "price": [
            "raw": NSDecimalNumber(decimal: product.price.raw).doubleValue,
            "localized": product.price.localized,
            "daily": product.price.daily,
            "weekly": product.price.weekly,
            "monthly": product.price.monthly,
            "yearly": product.price.yearly
        ] as [String: Any],
        "locale": product.locale
    ]
    if let trialPeriod = product.trialPeriod {
        var trial: [String: Any] = [
            "days": trialPeriod.days,
            "weeks": trialPeriod.weeks,
            "months": trialPeriod.months,
            "years": trialPeriod.years,
            "text": trialPeriod.text
        ]
        if let endAt = trialPeriod.endAt { trial["endAt"] = epochMilliseconds(endAt) }
        dict["trialPeriod"] = trial
    }
    if let period = product.period {
        dict["period"] = [
            "alt": period.alt,
            "ly": period.ly,
            "unit": serializeSubscriptionPeriodUnit(period.unit),
            "days": period.days,
            "weeks": period.weeks,
            "months": period.months,
            "years": period.years
        ] as [String: Any]
    }
    if let languageCode = product.languageCode { dict["languageCode"] = languageCode }
    var currency: [String: Any] = [:]
    if let code = product.currency.code { currency["code"] = code }
    if let symbol = product.currency.symbol { currency["symbol"] = symbol }
    dict["currency"] = currency
    return dict
}

private func serializeStatusReason(_ reason: PaywallPresentationRequestStatusReason) -> [String: Any] {
    switch reason {
    case .debuggerPresented: return ["type": "debuggerPresented"]
    case .paywallAlreadyPresented: return ["type": "paywallAlreadyPresented"]
    case .holdout(let experiment):
        return ["type": "holdout", "experiment": serializeExperiment(experiment) as Any]
    case .noAudienceMatch: return ["type": "noAudienceMatch"]
    case .placementNotFound: return ["type": "placementNotFound"]
    case .noPaywallViewController: return ["type": "noPaywallViewController"]
    case .noPresenter: return ["type": "noPresenter"]
    case .noConfig: return ["type": "noConfig"]
    case .subscriptionStatusTimeout: return ["type": "subscriptionStatusTimeout"]
    @unknown default: return [:]
    }
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
        "assignmentKey": s.assignmentKey,
        "title": s.title,
        "message": s.message,
        "options": s.options.map { serializeSurveyOption($0) },
        "presentationCondition": conditionStr,
        "presentationProbability": s.presentationProbability,
        "includeOtherOption": s.includeOtherOption,
        "includeCloseOption": s.includeCloseOption
    ]
}

private func serializeSurveyOption(_ option: SurveyOption) -> [String: Any] {
    return ["id": option.id, "text": option.title]
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

private func iso8601String(_ date: Date) -> String {
    return ISO8601DateFormatter().string(from: date)
}

// Mirrors the C# `StoreTransaction` model (Runtime/Models/StoreTransaction.cs). Dates are ISO 8601
// strings, matching the model's string fields. Optional values are omitted rather than sent as null.
private func serializeStoreTransaction(_ transaction: StoreTransaction) -> [String: Any] {
    var dict: [String: Any] = [
        "configRequestId": transaction.configRequestId,
        "appSessionId": transaction.appSessionId,
        "originalTransactionIdentifier": transaction.originalTransactionIdentifier
    ]
    if let transactionDate = transaction.transactionDate {
        dict["transactionDate"] = iso8601String(transactionDate)
    }
    if let storeTransactionId = transaction.storeTransactionId {
        dict["storeTransactionId"] = storeTransactionId
    }
    if let originalTransactionDate = transaction.originalTransactionDate {
        dict["originalTransactionDate"] = iso8601String(originalTransactionDate)
    }
    if let webOrderLineItemID = transaction.webOrderLineItemID {
        dict["webOrderLineItemID"] = webOrderLineItemID
    }
    if let appBundleId = transaction.appBundleId {
        dict["appBundleId"] = appBundleId
    }
    if let subscriptionGroupId = transaction.subscriptionGroupId {
        dict["subscriptionGroupId"] = subscriptionGroupId
    }
    if let isUpgraded = transaction.isUpgraded {
        dict["isUpgraded"] = isUpgraded
    }
    if let expirationDate = transaction.expirationDate {
        dict["expirationDate"] = iso8601String(expirationDate)
    }
    if let offerId = transaction.offerId {
        dict["offerId"] = offerId
    }
    if let revocationDate = transaction.revocationDate {
        dict["revocationDate"] = iso8601String(revocationDate)
    }
    // The raw values are upper case ("PURCHASED"); C# parses either.
    dict["state"] = transaction.state.rawValue.lowercased()
    var payment: [String: Any] = [
        "productIdentifier": transaction.payment.productIdentifier,
        "quantity": transaction.payment.quantity
    ]
    if let discountIdentifier = transaction.payment.discountIdentifier {
        payment["discountIdentifier"] = discountIdentifier
    }
    dict["payment"] = payment
    if let appAccountToken = transaction.appAccountToken {
        dict["appAccountToken"] = appAccountToken.uuidString
    }
    return dict
}

// The associated values of a `SuperwallEvent` are what a consumer needs to act on a purchase
// (the product, the store transaction, the paywall). `params` carries flattened analytics values
// only, so without this the C# `SuperwallEventInfo.Product` / `.Transaction` / `.PaywallInfo` were
// always null. Keys match the fields `BridgeCallbackHandler.DeserializeSuperwallEventInfo` reads.
private func appendEventPayload(_ event: SuperwallEvent, to dict: inout [String: Any]) {
    switch event {
    case .paywallOpen(let paywallInfo),
         .paywallClose(let paywallInfo),
         .paywallDecline(let paywallInfo),
         .transactionTimeout(let paywallInfo),
         .paywallWebviewLoadStart(let paywallInfo),
         .paywallWebviewLoadFail(let paywallInfo),
         .paywallWebviewLoadComplete(let paywallInfo),
         .paywallWebviewLoadTimeout(let paywallInfo),
         .paywallWebviewLoadFallback(let paywallInfo),
         .paywallWebviewProcessTerminated(let paywallInfo),
         .stripeCheckoutStart(let paywallInfo),
         .stripeCheckoutSubmit(let paywallInfo),
         .stripeCheckoutComplete(let paywallInfo),
         .stripeCheckoutFail(let paywallInfo),
         .teleportOpen(let paywallInfo):
        dict["paywallInfo"] = serializePaywallInfo(paywallInfo)

    case .paywallPageView(let paywallInfo, let data):
        dict["paywallInfo"] = serializePaywallInfo(paywallInfo)
        dict["pageViewData"] = serializePageViewData(data)

    case .attributionMatch(let info):
        dict["attributionMatchInfo"] = serializeAttributionMatchInfo(info)

    case .teleportReturn(let paywallInfo):
        if let paywallInfo = paywallInfo {
            dict["paywallInfo"] = serializePaywallInfo(paywallInfo)
        }

    case .deviceAttributes(let attributes):
        dict["deviceAttributes"] = attributes

    case .userAttributes(let attributes):
        dict["userAttributes"] = attributes

    case .integrationAttributes(let attributes):
        dict["integrationAttributes"] = attributes

    case .deepLink(let url):
        dict["deepLinkUrl"] = url.absoluteString

    case .triggerFire(let placementName, let result):
        dict["placementName"] = placementName
        dict["result"] = serializeTriggerResult(result)

    case .restoreFail(let message):
        dict["message"] = message

    case .paywallResponseLoadStart(let triggeredPlacementName),
         .paywallResponseLoadNotFound(let triggeredPlacementName),
         .paywallResponseLoadFail(let triggeredPlacementName),
         .paywallProductsLoadComplete(let triggeredPlacementName):
        if let name = triggeredPlacementName { dict["triggeredPlacementName"] = name }

    case .paywallResponseLoadComplete(let triggeredPlacementName, let paywallInfo),
         .paywallProductsLoadStart(let triggeredPlacementName, let paywallInfo),
         .paywallProductsLoadFail(let triggeredPlacementName, let paywallInfo):
        if let name = triggeredPlacementName { dict["triggeredPlacementName"] = name }
        dict["paywallInfo"] = serializePaywallInfo(paywallInfo)

    case .paywallProductsLoadRetry(let triggeredPlacementName, let paywallInfo, let attempt):
        if let name = triggeredPlacementName { dict["triggeredPlacementName"] = name }
        dict["paywallInfo"] = serializePaywallInfo(paywallInfo)
        dict["attempt"] = attempt

    case .paywallProductsLoadMissingProducts(let triggeredPlacementName, let paywallInfo, let identifiers):
        if let name = triggeredPlacementName { dict["triggeredPlacementName"] = name }
        dict["paywallInfo"] = serializePaywallInfo(paywallInfo)
        dict["missingProductIdentifiers"] = Array(identifiers)

    case .paywallPresentationRequest(let status, let reason):
        switch status {
        case .presentation: dict["status"] = "presentation"
        case .noPresentation: dict["status"] = "noPresentation"
        case .timeout: dict["status"] = "timeout"
        @unknown default: break
        }
        if let reason = reason {
            dict["statusReason"] = serializeStatusReason(reason)
        }

    case .surveyResponse(let survey, let selectedOption, let customResponse, let paywallInfo):
        dict["survey"] = serializeSurvey(survey)
        dict["selectedOption"] = serializeSurveyOption(selectedOption)
        if let customResponse = customResponse { dict["customResponse"] = customResponse }
        dict["paywallInfo"] = serializePaywallInfo(paywallInfo)

    case .customPlacement(let name, let params, let paywallInfo):
        dict["name"] = name
        dict["placementParams"] = params
        dict["paywallInfo"] = serializePaywallInfo(paywallInfo)

    case .adServicesTokenRequestFail(let error):
        dict["error"] = error.localizedDescription

    case .adServicesTokenRequestComplete(let token):
        dict["token"] = token

    case .enrichmentComplete(let userEnrichment, let deviceEnrichment):
        if let userEnrichment = userEnrichment { dict["userEnrichment"] = userEnrichment }
        if let deviceEnrichment = deviceEnrichment { dict["deviceEnrichment"] = deviceEnrichment }

    case .reviewRequested(let count):
        dict["reviewRequestedCount"] = count
        dict["reviewCount"] = count

    case .transactionStart(let product, let paywallInfo),
         .transactionAbandon(let product, let paywallInfo),
         .subscriptionStart(let product, let paywallInfo),
         .freeTrialStart(let product, let paywallInfo):
        dict["product"] = serializeStoreProduct(product)
        dict["paywallInfo"] = serializePaywallInfo(paywallInfo)

    case .transactionComplete(let transaction, let product, let type, let paywallInfo):
        if let transaction = transaction {
            dict["transaction"] = serializeStoreTransaction(transaction)
        }
        switch type {
        case .nonRecurringProductPurchase: dict["transactionType"] = "nonRecurringProductPurchase"
        case .freeTrialStart: dict["transactionType"] = "freeTrialStart"
        case .subscriptionStart: dict["transactionType"] = "subscriptionStart"
        @unknown default: break
        }
        dict["product"] = serializeStoreProduct(product)
        dict["paywallInfo"] = serializePaywallInfo(paywallInfo)

    case .transactionFail(let error, let paywallInfo):
        dict["error"] = error.localizedDescription
        dict["paywallInfo"] = serializePaywallInfo(paywallInfo)

    case .transactionRestore(let restoreType, let paywallInfo):
        switch restoreType {
        case .viaPurchase(let transaction):
            dict["restoreType"] = "viaPurchase"
            if let transaction = transaction {
                dict["transaction"] = serializeStoreTransaction(transaction)
            }
        case .viaRestore:
            dict["restoreType"] = "viaRestore"
        }
        dict["paywallInfo"] = serializePaywallInfo(paywallInfo)

    case .nonRecurringProductPurchase(let product, let paywallInfo):
        // `TransactionProduct` is a slimmer type than `StoreProduct`; the identifier is what it shares.
        dict["product"] = ["productIdentifier": product.id]
        dict["transactionProduct"] = serializeTransactionProduct(product)
        dict["paywallInfo"] = serializePaywallInfo(paywallInfo)

    case .permissionRequested(let permissionName, let paywallIdentifier),
         .permissionGranted(let permissionName, let paywallIdentifier),
         .permissionDenied(let permissionName, let paywallIdentifier):
        dict["permissionName"] = permissionName
        dict["paywallIdentifier"] = paywallIdentifier

    case .paywallPreloadStart(let paywallCount),
         .paywallPreloadComplete(let paywallCount):
        dict["paywallCount"] = paywallCount

    case .customerCenterOpen(let screen, let presentation):
        switch screen {
        case .management: dict["customerCenterScreen"] = "management"
        case .noPurchases: dict["customerCenterScreen"] = "noPurchases"
        }
        dict["customerCenterPresentationStyle"] = presentation == .embedded ? "embedded" : "sheet"

    case .customerCenterAction(let action, let pathId, let productId):
        dict["customerCenterAction"] = serializeCustomerCenterAction(action)
        dict["customerCenterPathId"] = pathId
        if let productId = productId { dict["customerCenterProductId"] = productId }

    case .customerCenterSurveyResponse(let surveyId, let optionId, let action, let pathId, let productId):
        dict["customerCenterSurveyId"] = surveyId
        dict["customerCenterSurveyOptionId"] = optionId
        dict["customerCenterAction"] = serializeCustomerCenterAction(action)
        dict["customerCenterPathId"] = pathId
        if let productId = productId { dict["customerCenterProductId"] = productId }

    case .customerCenterRefundRequest(let productId, let status):
        dict["customerCenterProductId"] = productId
        dict["customerCenterRefundStatus"] = serializeCustomerCenterRefundStatus(status)

    default:
        break
    }
}

private func serializeTriggerResult(_ result: TriggerResult) -> [String: Any] {
    switch result {
    case .placementNotFound:
        return ["type": "placementNotFound"]
    case .noAudienceMatch:
        return ["type": "noAudienceMatch"]
    case .paywall(let experiment):
        return ["type": "paywall", "experiment": serializeExperiment(experiment) as Any]
    case .holdout(let experiment):
        return ["type": "holdout", "experiment": serializeExperiment(experiment) as Any]
    case .error(let error):
        return ["type": "error", "error": error.localizedDescription]
    @unknown default:
        return ["type": "error"]
    }
}

private func serializeEventInfo(_ info: SuperwallEventInfo) -> [String: Any] {
    var dict: [String: Any] = [
        "params": info.params
    ]
    // `String(describing:)` is not a stable name for this enum. Recent SuperwallKit releases make
    // `SuperwallEvent` CustomStringConvertible and describe it as its snake_case placement name
    // ("transaction_complete"), but older 4.x releases fall back to Swift's default description,
    // which for a case with associated values is the whole payload:
    //
    //     transactionComplete(transaction: Optional(SuperwallKit.StoreTransaction), product: ..., ...)
    //
    // That never matched the C# `EventType` enum, so every delegate event arrived typed as the
    // default (`FirstSeen`). Keep only the leading identifier: both shapes then parse on the C# side,
    // which also normalises Android's snake_case `rawName`.
    let described = String(describing: info.event)
    dict["eventType"] = String(described.prefix(while: { $0 != "(" }))
    appendEventPayload(info.event, to: &dict)
    return dict
}

// Dates are epoch milliseconds, as for entitlements. Optional values are omitted rather than sent as null.
private func serializeCustomerInfo(_ info: CustomerInfo) -> [String: Any] {
    return [
        "userId": info.userId,
        "entitlements": info.entitlements.map { serializeEntitlement($0) },
        "activeSubscriptionProductIds": Array(info.activeSubscriptionProductIds),
        "subscriptions": info.subscriptions.map { serializeSubscriptionTransaction($0) },
        "nonSubscriptions": info.nonSubscriptions.map { serializeNonSubscriptionTransaction($0) }
    ]
}

private func serializeSubscriptionTransaction(_ transaction: SubscriptionTransaction) -> [String: Any] {
    var dict: [String: Any] = [
        "transactionId": transaction.transactionId,
        "productId": transaction.productId,
        "purchaseDate": epochMilliseconds(transaction.purchaseDate),
        "willRenew": transaction.willRenew,
        "isRevoked": transaction.isRevoked,
        "isInGracePeriod": transaction.isInGracePeriod,
        "isInBillingRetryPeriod": transaction.isInBillingRetryPeriod,
        "isActive": transaction.isActive,
        "store": serializeProductStore(transaction.store)
    ]
    if let expirationDate = transaction.expirationDate {
        dict["expirationDate"] = epochMilliseconds(expirationDate)
    }
    if let offerType = transaction.offerType {
        dict["offerType"] = offerType.rawValue
    }
    if let subscriptionGroupId = transaction.subscriptionGroupId {
        dict["subscriptionGroupId"] = subscriptionGroupId
    }
    return dict
}

private func serializeNonSubscriptionTransaction(_ transaction: NonSubscriptionTransaction) -> [String: Any] {
    return [
        "transactionId": transaction.transactionId,
        "productId": transaction.productId,
        "purchaseDate": epochMilliseconds(transaction.purchaseDate),
        "isConsumable": transaction.isConsumable,
        "isRevoked": transaction.isRevoked,
        "store": serializeProductStore(transaction.store)
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

// Same shape as the Android bridge: `type` tells the ownership and store identifier cases apart.
private func serializeRedemptionInfo(_ info: RedemptionResult.RedemptionInfo) -> [String: Any] {
    var dict: [String: Any] = [
        "entitlements": serializeEntitlements(info.entitlements)
    ]
    switch info.ownership {
    case .appUser(let appUserId):
        dict["ownership"] = ["type": "appUser", "appUserId": appUserId]
    case .device(let deviceId):
        dict["ownership"] = ["type": "device", "deviceId": deviceId]
    }

    var purchaserInfo: [String: Any] = ["appUserId": info.purchaserInfo.appUserId]
    if let email = info.purchaserInfo.email {
        purchaserInfo["email"] = email
    }
    switch info.purchaserInfo.storeIdentifiers {
    case .stripe(let customerId, let subscriptionIds):
        purchaserInfo["storeIdentifiers"] = ["type": "stripe", "customerId": customerId, "subscriptionIds": subscriptionIds] as [String: Any]
    case .paddle(let customerId, let subscriptionIds):
        purchaserInfo["storeIdentifiers"] = ["type": "paddle", "customerId": customerId, "subscriptionIds": subscriptionIds] as [String: Any]
    case .unknown(let store, let additionalInfo):
        purchaserInfo["storeIdentifiers"] = ["type": "unknown", "store": store, "additionalInfo": additionalInfo] as [String: Any]
    }
    dict["purchaserInfo"] = purchaserInfo

    if let paywallInfo = info.paywallInfo {
        var paywall: [String: Any] = [
            "identifier": paywallInfo.identifier,
            "placementName": paywallInfo.placementName,
            "placementParams": paywallInfo.placementParams,
            "variantId": paywallInfo.variantId,
            "experimentId": paywallInfo.experimentId
        ]
        // `productIdentifier` is deprecated in favour of `product.identifier`.
        if let productIdentifier = paywallInfo.product?.identifier {
            paywall["productIdentifier"] = productIdentifier
        }
        dict["paywallInfo"] = paywall
    }
    if let userAttributes = info.userAttributes {
        dict["userAttributes"] = userAttributes
    }
    return dict
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
        var data: [String: Any] = [
            "level": level,
            "scope": scope,
            "message": message as Any,
            "error": error?.localizedDescription as Any
        ]
        if let info = info {
            data["info"] = info
        }
        sendToUnity(method: "handleLog", data: data)
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

/// The C# purchase controller method a purchase goes to, and the request BridgeCallbackHandler
/// reads. `StoreProduct.isCustomProduct` isn't public, but SuperwallKit sets `customTransactionId`
/// before it calls the purchase controller for a custom product (store "Custom" on the
/// dashboard), and only for those, so it's what tells the two apart.
func purchaseRequest(
    productIdentifier: String,
    customTransactionId: String?,
    callbackId: String
) -> (method: String, data: [String: Any]) {
    if let customTransactionId {
        return ("purchaseCustomProduct", [
            "productId": productIdentifier,
            "customTransactionId": customTransactionId,
            "callbackId": callbackId
        ])
    }
    return ("purchaseFromAppStore", [
        "productId": productIdentifier,
        "callbackId": callbackId
    ])
}

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

            let request = purchaseRequest(
                productIdentifier: product.productIdentifier,
                customTransactionId: product.customTransactionId,
                callbackId: callbackId
            )
            sendToUnity(method: request.method, data: request.data)
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

// MARK: - Unity Custom Callbacks

/// Holds each paywall custom callback until C# replies. The paywall waits on `request`, and
/// C# answers through `respond` with the same callbackId.
final class UnityCustomCallbacks {
    private var pending: [String: CheckedContinuation<CustomCallbackResult, Never>] = [:]
    private let lock = NSLock()
    private let send: (String, [String: Any]) -> Bool

    init(send: @escaping (String, [String: Any]) -> Bool) {
        self.send = send
    }

    // No timeout, same as the native SDKs: a handler can legitimately take as long as it needs.
    @MainActor
    func request(handlerId: String, callback: CustomCallback) async -> CustomCallbackResult {
        let callbackId = UUID().uuidString
        return await withCheckedContinuation { continuation in
            lock.lock()
            pending[callbackId] = continuation
            lock.unlock()

            var customCallback: [String: Any] = ["name": callback.name]
            if let variables = callback.variables {
                customCallback["variables"] = variables
            }
            let sent = send("onCustomCallback", [
                "handlerId": handlerId,
                "callbackId": callbackId,
                "customCallback": customCallback
            ])
            if !sent {
                finish(callbackId: callbackId, result: .failure())
            }
        }
    }

    func respond(callbackId: String, resultJson: String) {
        finish(callbackId: callbackId, result: parseResult(resultJson))
    }

    private func finish(callbackId: String, result: CustomCallbackResult) {
        lock.lock()
        let continuation = pending.removeValue(forKey: callbackId)
        lock.unlock()
        continuation?.resume(returning: result)
    }

    private func parseResult(_ resultJson: String) -> CustomCallbackResult {
        guard let dict = parseJson(resultJson) else { return .failure() }
        let data = dict["data"] as? [String: Any]
        return dict["status"] as? String == "success" ? .success(data: data) : .failure(data: data)
    }
}

// MARK: - Customer Center

// The configuration C# sends (Superwall.SerializeCustomerCenterConfiguration). A path whose type can't be built
// here, such as a URL path whose URL doesn't parse, is left out.
private func parseCustomerCenterConfiguration(_ dict: [String: Any]) -> CustomerCenterConfiguration {
    let defaults = CustomerCenterConfiguration.default
    let support = dict["support"] as? [String: Any] ?? [:]
    var accent: CustomerCenterConfiguration.Appearance.ColorPair?
    if let pair = dict["accent"] as? [String: Any], let light = pair["light"] as? String, let dark = pair["dark"] as? String {
        accent = CustomerCenterConfiguration.Appearance.ColorPair(light: light, dark: dark)
    }
    return CustomerCenterConfiguration(
        managementScreen: (dict["managementScreen"] as? [String: Any]).map(parseCustomerCenterScreen) ?? defaults.managementScreen,
        noPurchasesScreen: (dict["noPurchasesScreen"] as? [String: Any]).map(parseCustomerCenterScreen) ?? defaults.noPurchasesScreen,
        support: CustomerCenterConfiguration.Support(
            email: support["email"] as? String,
            latestAppVersion: support["latestAppVersion"] as? String,
            warnsAboutUpdates: support["warnsAboutUpdates"] as? Bool ?? true,
            checksAppStoreForUpdates: support["checksAppStoreForUpdates"] as? Bool ?? true,
            webManagementURL: (support["webManagementUrl"] as? String).flatMap { URL(string: $0) }
        ),
        appearance: CustomerCenterConfiguration.Appearance(accent: accent),
        showsAccountDetails: dict["showsAccountDetails"] as? Bool ?? true,
        warnsAboutDuplicateSubscriptions: dict["warnsAboutDuplicateSubscriptions"] as? Bool ?? true
    )
}

private func parseCustomerCenterScreen(_ dict: [String: Any]) -> CustomerCenterConfiguration.Screen {
    let paths = (dict["paths"] as? [[String: Any]] ?? []).compactMap { path -> CustomerCenterConfiguration.Path? in
        guard let type = parseCustomerCenterPathType(path) else { return nil }
        var survey: CustomerCenterConfiguration.FeedbackSurvey?
        if let surveyDict = path["survey"] as? [String: Any], let surveyId = surveyDict["id"] as? String {
            let options = (surveyDict["options"] as? [[String: Any]] ?? []).compactMap { option -> CustomerCenterConfiguration.FeedbackSurvey.Option? in
                guard let id = option["id"] as? String else { return nil }
                return CustomerCenterConfiguration.FeedbackSurvey.Option(id: id, title: option["title"] as? String)
            }
            survey = CustomerCenterConfiguration.FeedbackSurvey(id: surveyId, title: surveyDict["title"] as? String, options: options)
        }
        return CustomerCenterConfiguration.Path(id: path["id"] as? String, type: type, title: path["title"] as? String, survey: survey)
    }
    return CustomerCenterConfiguration.Screen(title: dict["title"] as? String, subtitle: dict["subtitle"] as? String, paths: paths)
}

private func parseCustomerCenterPathType(_ dict: [String: Any]) -> CustomerCenterConfiguration.PathType? {
    switch dict["type"] as? String {
    case "restore": return .restore
    case "manageSubscription": return .manageSubscription
    // C# sends milliseconds, as Android takes them; SuperwallKit takes seconds.
    case "refund": return .refund(window: (dict["windowMillis"] as? Double).map { $0 / 1000 })
    case "changePlan": return .changePlan(productIds: dict["productIds"] as? [String])
    case "contactSupport": return .contactSupport
    case "url":
        guard let string = dict["url"] as? String, let url = URL(string: string) else { return nil }
        return .url(url, openMethod: dict["openMethod"] as? String == "external" ? .external : .inApp)
    case "custom":
        guard let identifier = dict["identifier"] as? String else { return nil }
        return .custom(identifier: identifier)
    default: return nil
    }
}

private func serializeCustomerCenterAction(_ action: CustomerCenterAction) -> [String: Any] {
    switch action {
    case .restore: return ["type": "restore"]
    case .manageSubscription: return ["type": "manageSubscription"]
    case .refund: return ["type": "refund"]
    case .changePlan: return ["type": "changePlan"]
    case .contactSupport: return ["type": "contactSupport"]
    case .url(let url): return ["type": "url", "url": url.absoluteString]
    case .custom(let identifier): return ["type": "custom", "identifier": identifier]
    }
}

private func serializeCustomerCenterRefundStatus(_ status: CustomerCenterRefundStatus) -> String {
    switch status {
    case .success: return "success"
    case .userCancelled: return "userCancelled"
    default: return "error"
    }
}

private func serializeCustomerCenterPurchase(_ purchase: CustomerCenterPurchase) -> [String: Any] {
    var dict: [String: Any] = [
        "store": serializeProductStore(purchase.store),
        "entitlements": serializeEntitlements(purchase.entitlements)
    ]
    if let productId = purchase.productId { dict["productId"] = productId }
    if let subscription = purchase.subscription { dict["subscription"] = serializeSubscriptionTransaction(subscription) }
    if let nonSubscription = purchase.nonSubscription { dict["nonSubscription"] = serializeNonSubscriptionTransaction(nonSubscription) }
    return dict
}

/// Holds each Customer Center restore until C# answers ShouldRestorePurchases. The restore waits on `request`, and
/// C# answers through `respond` with the same callbackId.
final class UnityCustomerCenterRestores {
    private var pending: [String: CheckedContinuation<Bool, Never>] = [:]
    private let lock = NSLock()
    private let send: (String, [String: Any]) -> Bool

    init(send: @escaping (String, [String: Any]) -> Bool) {
        self.send = send
    }

    @MainActor
    func request(presentationId: String) async -> Bool {
        let callbackId = UUID().uuidString
        return await withCheckedContinuation { continuation in
            lock.lock()
            pending[callbackId] = continuation
            lock.unlock()
            let sent = send("customerCenterShouldRestorePurchases", [
                "presentationId": presentationId,
                "callbackId": callbackId
            ])
            // C# never got the question, so restore, as SuperwallKit does without a delegate.
            if !sent {
                respond(callbackId: callbackId, proceed: true)
            }
        }
    }

    func respond(callbackId: String, proceed: Bool) {
        lock.lock()
        let continuation = pending.removeValue(forKey: callbackId)
        lock.unlock()
        continuation?.resume(returning: proceed)
    }
}

/// Forwards one presentation's Customer Center events to C#, tagged with the id C# registered its delegate under.
@MainActor
private final class UnityCustomerCenterDelegate: CustomerCenterDelegate {
    let presentationId: String

    init(presentationId: String) {
        self.presentationId = presentationId
    }

    func customerCenterShouldRestorePurchases() async -> Bool {
        return await unityCustomerCenterRestores.request(presentationId: presentationId)
    }

    func customerCenterDidSelectAction(_ action: CustomerCenterAction, pathId: String, purchase: CustomerCenterPurchase?) {
        var data: [String: Any] = [
            "presentationId": presentationId,
            "action": serializeCustomerCenterAction(action),
            "pathId": pathId
        ]
        if let purchase = purchase { data["purchase"] = serializeCustomerCenterPurchase(purchase) }
        sendToUnity(method: "customerCenterDidSelectAction", data: data)
    }

    func customerCenterDidCompleteSurvey(surveyId: String, optionId: String, action: CustomerCenterAction, pathId: String) {
        sendToUnity(method: "customerCenterDidCompleteSurvey", data: [
            "presentationId": presentationId,
            "surveyId": surveyId,
            "optionId": optionId,
            "action": serializeCustomerCenterAction(action),
            "pathId": pathId
        ])
    }

    func customerCenterDidCompleteRefundRequest(productId: String, status: CustomerCenterRefundStatus) {
        sendToUnity(method: "customerCenterDidCompleteRefundRequest", data: [
            "presentationId": presentationId,
            "productId": productId,
            "status": serializeCustomerCenterRefundStatus(status)
        ])
    }

    func customerCenterDidDismiss() {
        sendToUnity(method: "customerCenterDidDismiss", data: ["presentationId": presentationId])
    }
}

/// The Customer Center on screen in any window, if there is one.
@MainActor
private func presentedCustomerCenter() -> CustomerCenterViewController? {
    let windows = UIApplication.shared.connectedScenes
        .compactMap { $0 as? UIWindowScene }
        .flatMap { $0.windows }
    for window in windows {
        var controller = window.rootViewController
        while let current = controller {
            if let customerCenter = current as? CustomerCenterViewController {
                return customerCenter
            }
            controller = current.presentedViewController
        }
    }
    return nil
}

// MARK: - Stored State

private var unityDelegate: SuperwallUnityDelegate?
private var unityPurchaseController: UnityPurchaseController?
let unityCustomCallbacks = UnityCustomCallbacks { method, data in sendToUnity(method: method, data: data) }
let unityCustomerCenterRestores = UnityCustomerCenterRestores { method, data in sendToUnity(method: method, data: data) }
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
        // After the deprecated flag above, which it replaces.
        if let behavior = parseEventTrackingBehavior(dict["eventTrackingBehavior"] as? String) {
            options?.eventTrackingBehavior = behavior
        }
        // Only when C# set one: assigning marks consent as given by the app, which outranks a TCF banner.
        if let adConsent = dict["adConsent"] as? [String: Any] {
            options?.adConsent = parseAdConsent(adConsent)
        }
        switch dict["storeKitVersion"] as? String {
        case "storeKit1": options?.storeKitVersion = .storeKit1
        case "storeKit2": options?.storeKitVersion = .storeKit2
        default: break
        }
        if let experimental = dict["enableExperimentalDeviceVariables"] as? Bool {
            options?.enableExperimentalDeviceVariables = experimental
        }
        if let customerCenter = dict["customerCenter"] as? [String: Any] {
            options?.customerCenter = parseCustomerCenterConfiguration(customerCenter)
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
            case "local": options?.networkEnvironment = .local
            case "custom":
                // SuperwallKit takes one domain or URL; `baseHost` is what an app that only set the Android fields has.
                let custom = dict["customNetworkEnvironment"] as? [String: Any]
                if let domain = (custom?["domain"] as? String) ?? (custom?["baseHost"] as? String) {
                    options?.networkEnvironment = .custom(domain)
                }
            default: break
            }
        }
        if let devServer = dict["devServer"] as? [String: Any] {
            if let address = devServer["url"] as? String {
                if let url = URL(string: address) {
                    options?.devServer = .url(url)
                } else {
                    NSLog("[SuperwallUnityBridge] Ignoring DevServer.Url, which isn't a URL: %@", address)
                }
            } else {
                options?.devServer = .default
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
            if let denied = paywalls["notificationPermissionsDenied"] as? [String: Any] {
                let alert = PaywallOptions.NotificationPermissionsDenied()
                if let title = denied["title"] as? String { alert.title = title }
                if let message = denied["message"] as? String { alert.message = message }
                if let action = denied["actionButtonTitle"] as? String { alert.actionButtonTitle = action }
                if let close = denied["closeButtonTitle"] as? String { alert.closeButtonTitle = close }
                options?.paywalls.notificationPermissionsDenied = alert
            }
        }
        // Note: passIdentifiersToPlayStore and paywalls.optimisticLoading are Android-only, skipped on iOS.
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
            // SuperwallKit calls this whether or not it got its configuration, and sets `configurationStatus`
            // from the main queue. Read the status after what is already queued there, so a failure is seen.
            DispatchQueue.main.async {
                if Superwall.shared.configurationStatus == .failed {
                    sendAsyncResponse(callbackId: cbId, data: [
                        "success": false,
                        "error": "Superwall couldn't fetch its configuration."
                    ])
                } else {
                    sendAsyncResponse(callbackId: cbId, data: ["success": true])
                }
            }
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
        // JSON null arrives as NSNull, which SuperwallKit would store. nil is what removes an attribute.
        let attributes: [String: Any?] = dict.mapValues { value -> Any? in value is NSNull ? nil : value }
        Superwall.shared.setUserAttributes(attributes)
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
    let web = serializeEntitlements(entitlements.web)
    let dict: [String: Any] = ["active": active, "inactive": inactive, "all": all, "web": web]
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
        handler?.onWillDismiss { info, result in
            sendToUnity(method: "onWillDismiss", data: [
                "handlerId": hId!,
                "paywallInfo": serializePaywallInfo(info),
                "result": serializePaywallResult(result)
            ])
        }
        handler?.onDismiss { info, result in
            sendToUnity(method: "onDismiss", data: [
                "handlerId": hId!,
                "paywallInfo": serializePaywallInfo(info),
                "result": serializePaywallResult(result)
            ])
        }
        handler?.onError { error in
            sendToUnity(method: "onError", data: [
                "handlerId": hId!,
                "error": error.localizedDescription
            ])
        }
        handler?.onSkip { reason in
            var data: [String: Any] = ["handlerId": hId!]
            switch reason {
            case .holdout(let experiment):
                data["reason"] = "holdout"
                data["experiment"] = serializeExperiment(experiment)
            case .noAudienceMatch: data["reason"] = "noAudienceMatch"
            case .placementNotFound: data["reason"] = "placementNotFound"
            @unknown default: data["reason"] = "noAudienceMatch"
            }
            sendToUnity(method: "onSkip", data: data)
        }
        handler?.onCustomCallback { callback in
            await unityCustomCallbacks.request(handlerId: hId!, callback: callback)
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

private func serializePaywallResult(_ result: PaywallResult) -> [String: Any] {
    switch result {
    // `PaywallResult.purchased` carries a `StoreProduct`, not a `String` — the old binding name
    // was misleading. Passing the object straight into the payload meant JSON serialization
    // received a non-JSON leaf and fell back to its debug description, so Unity was handed
    // "<SWKStoreProduct: 0x14f80a980>" as `productId`. Any consumer matching that against a store
    // catalogue (revenue reporting, receipt lookup) finds nothing and silently drops the purchase.
    case .purchased(let product):
        return ["type": "purchased", "productId": product.productIdentifier]
    case .declined:
        return ["type": "declined"]
    case .restored:
        return ["type": "restored"]
    @unknown default:
        return ["type": "declined"]
    }
}

@_cdecl("_SuperwallBridge_Dismiss")
public func _SuperwallBridge_Dismiss() {
    Superwall.shared.dismiss()
}

@_cdecl("_SuperwallBridge_DismissWithCompletion")
public func _SuperwallBridge_DismissWithCompletion(_ callbackId: UnsafePointer<CChar>?) {
    let cbId = toSwiftString(callbackId)
    let completion: () -> Void = { if let cbId = cbId { sendAsyncResponse(callbackId: cbId, data: [:]) } }
    guard Superwall.isInitialized else {
        completion()
        return
    }
    Superwall.shared.dismiss(completion: completion)
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
        // iOS has no base plans or offers, so the full identifier is the product identifier.
        "fullIdentifier": product.productIdentifier,
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
    if let customTransactionId = product.customTransactionId {
        dict["customTransactionId"] = customTransactionId
    }
    if let subscriptionPeriod = product.subscriptionPeriod {
        dict["subscriptionPeriod"] = serializeSubscriptionPeriod(subscriptionPeriod)
    }
    if let introductoryDiscount = product.introductoryDiscount {
        dict["introductoryDiscount"] = serializeDiscount(introductoryDiscount)
    }
    dict["discounts"] = product.discounts.map { serializeDiscount($0) }
    if let billingPlanType = product.billingPlanType {
        dict["billingPlanType"] = serializeBillingPlanType(billingPlanType)
    }
    dict["isBillingPlanAvailable"] = product.isBillingPlanAvailable
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

@_cdecl("_SuperwallBridge_RespondToCustomCallback")
public func _SuperwallBridge_RespondToCustomCallback(_ callbackId: UnsafePointer<CChar>, _ resultJson: UnsafePointer<CChar>) {
    unityCustomCallbacks.respond(callbackId: String(cString: callbackId), resultJson: String(cString: resultJson))
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
public func _SuperwallBridge_SetLocalResources(_ resourcesJson: UnsafePointer<CChar>?) {
    // C# passes null to clear the resources.
    guard let dict = parseJson(toSwiftString(resourcesJson)) else {
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

@_cdecl("_SuperwallBridge_SetPlatformWrapper")
public func _SuperwallBridge_SetPlatformWrapper(_ wrapper: UnsafePointer<CChar>, _ version: UnsafePointer<CChar>) {
    guard Superwall.isInitialized else { return }
    Superwall.shared.setPlatformWrapper(String(cString: wrapper), version: String(cString: version))
}

@_cdecl("_SuperwallBridge_GetIntegrationAttributes")
public func _SuperwallBridge_GetIntegrationAttributes() -> UnsafePointer<CChar>? {
    return toCString(toJsonString(Superwall.shared.integrationAttributes))
}

@_cdecl("_SuperwallBridge_RefreshCustomerInfo")
public func _SuperwallBridge_RefreshCustomerInfo(_ callbackId: UnsafePointer<CChar>) {
    let cbId = String(cString: callbackId)
    Task {
        let info = await Superwall.shared.refreshCustomerInfo()
        sendAsyncResponse(callbackId: cbId, data: serializeCustomerInfo(info))
    }
}

@_cdecl("_SuperwallBridge_GetEventTrackingBehavior")
public func _SuperwallBridge_GetEventTrackingBehavior() -> UnsafePointer<CChar>? {
    return toCString(serializeEventTrackingBehavior(Superwall.shared.eventTrackingBehavior))
}

@_cdecl("_SuperwallBridge_SetEventTrackingBehavior")
public func _SuperwallBridge_SetEventTrackingBehavior(_ behavior: UnsafePointer<CChar>) {
    guard let parsed = parseEventTrackingBehavior(String(cString: behavior)) else { return }
    Superwall.shared.eventTrackingBehavior = parsed
}

@_cdecl("_SuperwallBridge_GetAdConsent")
public func _SuperwallBridge_GetAdConsent() -> UnsafePointer<CChar>? {
    let consent = Superwall.shared.adConsent
    return toCString(toJsonString([
        "adUserData": serializeAdConsentStatus(consent.adUserData),
        "adPersonalization": serializeAdConsentStatus(consent.adPersonalization)
    ]))
}

@_cdecl("_SuperwallBridge_SetAdConsent")
public func _SuperwallBridge_SetAdConsent(_ adConsentJson: UnsafePointer<CChar>) {
    guard let dict = parseJson(String(cString: adConsentJson)) else { return }
    Superwall.shared.adConsent = parseAdConsent(dict)
}

@_cdecl("_SuperwallBridge_SetInterfaceStyle")
public func _SuperwallBridge_SetInterfaceStyle(_ style: UnsafePointer<CChar>?) {
    // C# passes null to follow the system setting again.
    switch toSwiftString(style) {
    case "light": Superwall.shared.setInterfaceStyle(to: .light)
    case "dark": Superwall.shared.setInterfaceStyle(to: .dark)
    default: Superwall.shared.setInterfaceStyle(to: nil)
    }
}

@_cdecl("_SuperwallBridge_CancelAllScheduledNotifications")
public func _SuperwallBridge_CancelAllScheduledNotifications() {
    // SuperwallKit's counterpart of Android's cancelAllScheduledNotifications().
    UNUserNotificationCenter.current().removeAllPendingSuperwallNotificationRequests()
}

// Push notifications through Superwall are Android only (Firebase Cloud Messaging). These accept the
// calls so the DllImport symbols resolve, and do nothing.
@_cdecl("_SuperwallBridge_SetPushToken")
public func _SuperwallBridge_SetPushToken(_ token: UnsafePointer<CChar>) {}

@_cdecl("_SuperwallBridge_SetNotificationsEnabled")
public func _SuperwallBridge_SetNotificationsEnabled(_ enabled: Bool) {}

@_cdecl("_SuperwallBridge_HandleNotificationOpen")
public func _SuperwallBridge_HandleNotificationOpen(_ dataJson: UnsafePointer<CChar>) {}

@_cdecl("_SuperwallBridge_PresentCustomerCenter")
public func _SuperwallBridge_PresentCustomerCenter(
    _ configurationJson: UnsafePointer<CChar>?,
    _ presentationId: UnsafePointer<CChar>,
    _ hasDelegate: Bool
) {
    let json = toSwiftString(configurationJson)
    let id = String(cString: presentationId)
    let onDismiss: () -> Void = { sendToUnity(method: "customerCenterOnDismiss", data: ["presentationId": id]) }
    Task { @MainActor in
        guard Superwall.isInitialized else {
            onDismiss()
            return
        }
        let configuration = parseJson(json).map(parseCustomerCenterConfiguration)
        let previouslyPresented = presentedCustomerCenter()
        Superwall.shared.presentCustomerCenter(
            configuration: configuration,
            delegate: hasDelegate ? UnityCustomerCenterDelegate(presentationId: id) : nil,
            onDismiss: onDismiss
        )
        // SuperwallKit only logs when it can't present (one is already presented, or there's nothing to present
        // from) and then never calls onDismiss. Call it, so C# releases the delegate and runs its onDismiss, as
        // Android does.
        let presented = presentedCustomerCenter()
        if presented == nil || presented === previouslyPresented {
            onDismiss()
        }
    }
}

@_cdecl("_SuperwallBridge_DismissCustomerCenter")
public func _SuperwallBridge_DismissCustomerCenter(_ callbackId: UnsafePointer<CChar>?) {
    let cbId = toSwiftString(callbackId)
    let completion: () -> Void = { if let cbId = cbId { sendAsyncResponse(callbackId: cbId, data: [:]) } }
    Task { @MainActor in
        // SuperwallKit returns without calling the completion when it isn't configured.
        guard Superwall.isInitialized else {
            completion()
            return
        }
        Superwall.shared.dismissCustomerCenter(completion: completion)
    }
}

@_cdecl("_SuperwallBridge_RespondToCustomerCenterShouldRestore")
public func _SuperwallBridge_RespondToCustomerCenterShouldRestore(_ callbackId: UnsafePointer<CChar>, _ proceed: Bool) {
    unityCustomerCenterRestores.respond(callbackId: String(cString: callbackId), proceed: proceed)
}

@_cdecl("_SuperwallBridge_GetGrantedEntitlements")
public func _SuperwallBridge_GetGrantedEntitlements() -> UnsafePointer<CChar>? {
    guard Superwall.isInitialized else { return toCString("[]") }
    return toCString(toJsonString(serializeEntitlements(Superwall.shared.grantedEntitlements)))
}

@_cdecl("_SuperwallBridge_SetGrantedEntitlements")
public func _SuperwallBridge_SetGrantedEntitlements(_ entitlementsJson: UnsafePointer<CChar>) {
    guard Superwall.isInitialized,
          let entitlements = parseJsonArray(String(cString: entitlementsJson)) as? [[String: Any]] else { return }
    Superwall.shared.grantedEntitlements = parseEntitlementsFromJson(entitlements)
}

// MARK: - Game Controller

/// Passes the input of every connected controller's extended gamepad to SuperwallKit, which hands it to the
/// paywall. A gamepad has a single `valueChangedHandler`, so the one the game set is kept and still called,
/// and put back when forwarding stops. Only used on the main thread.
private final class UnityGameControllerForwarder {
    private var observers: [NSObjectProtocol] = []
    private var attached: [(gamepad: GCExtendedGamepad, previous: GCExtendedGamepadValueChangedHandler?)] = []

    func start() {
        guard observers.isEmpty else { return }
        GCController.controllers().forEach { attach($0) }
        let center = NotificationCenter.default
        observers.append(center.addObserver(forName: .GCControllerDidConnect, object: nil, queue: .main) { [weak self] notification in
            if let controller = notification.object as? GCController {
                self?.attach(controller)
            }
        })
        observers.append(center.addObserver(forName: .GCControllerDidDisconnect, object: nil, queue: .main) { [weak self] notification in
            if let controller = notification.object as? GCController {
                self?.detach(controller)
            }
        })
    }

    func stop() {
        observers.forEach { NotificationCenter.default.removeObserver($0) }
        observers = []
        for entry in attached {
            entry.gamepad.valueChangedHandler = entry.previous
        }
        attached = []
    }

    private func attach(_ controller: GCController) {
        guard let gamepad = controller.extendedGamepad,
              !attached.contains(where: { $0.gamepad === gamepad }) else { return }
        let previous = gamepad.valueChangedHandler
        attached.append((gamepad: gamepad, previous: previous))
        gamepad.valueChangedHandler = { gamepad, element in
            previous?(gamepad, element)
            if Superwall.isInitialized {
                Superwall.shared.gamepadValueChanged(gamepad: gamepad, element: element)
            }
        }
    }

    private func detach(_ controller: GCController) {
        guard let gamepad = controller.extendedGamepad,
              let index = attached.firstIndex(where: { $0.gamepad === gamepad }) else { return }
        gamepad.valueChangedHandler = attached[index].previous
        attached.remove(at: index)
    }
}

private let unityGameControllerForwarder = UnityGameControllerForwarder()

@_cdecl("_SuperwallBridge_SetGameControllerForwardingEnabled")
public func _SuperwallBridge_SetGameControllerForwardingEnabled(_ enabled: Bool) {
    DispatchQueue.main.async {
        if enabled {
            unityGameControllerForwarder.start()
        } else {
            unityGameControllerForwarder.stop()
        }
    }
}

// MARK: - Embedded Paywall

/// Covers the game's view and lays the paywall out in the part of it C# asked for. The rectangle is in fractions of
/// this view's size, because Unity's pixels needn't match UIKit's points, so it stays right through rotation.
/// Touches outside the paywall go through to the game.
private final class UnityEmbeddedPaywallContainer: UIView {
    var normalizedRect: CGRect = .zero {
        didSet { setNeedsLayout() }
    }

    weak var paywallView: UIView?

    private var paywallFrame: CGRect {
        return CGRect(
            x: normalizedRect.minX * bounds.width,
            y: normalizedRect.minY * bounds.height,
            width: normalizedRect.width * bounds.width,
            height: normalizedRect.height * bounds.height
        )
    }

    override func layoutSubviews() {
        super.layoutSubviews()
        paywallView?.frame = paywallFrame
    }

    override func point(inside point: CGPoint, with event: UIEvent?) -> Bool {
        return paywallFrame.contains(point)
    }
}

/// One paywall shown inside the game's view, retrieved with SuperwallKit's `getPaywall`. It owns at most one
/// PaywallViewController, as a child of the window's root view controller, and lets go of it when removed: a
/// paywall view controller must never be shown in two places.
@MainActor
private final class UnityEmbeddedPaywall: PaywallViewControllerDelegate {
    private let viewId: String
    private let container = UnityEmbeddedPaywallContainer()
    private var paywallViewController: PaywallViewController?
    private var loadTask: Task<Void, Never>?
    private var isRemoved = false

    init(viewId: String, normalizedRect: CGRect) {
        self.viewId = viewId
        container.normalizedRect = normalizedRect
        container.clipsToBounds = true
        container.backgroundColor = .clear
    }

    func setNormalizedRect(_ rect: CGRect) {
        container.normalizedRect = rect
    }

    func load(placement: String, params: [String: Any]?) {
        let viewId = self.viewId
        loadTask = Task { @MainActor [weak self] in
            guard let self = self else { return }
            do {
                let paywall = try await Superwall.shared.getPaywall(
                    forPlacement: placement,
                    params: params,
                    delegate: self,
                    onCustomCallback: { callback in
                        return await unityCustomCallbacks.request(handlerId: viewId, callback: callback)
                    }
                )
                guard !Task.isCancelled, !self.isRemoved else { return }
                guard paywall.parent == nil,
                      paywall.presentingViewController == nil,
                      paywall.viewIfLoaded?.superview == nil else {
                    self.reportError("The paywall is already shown elsewhere. Use a different placement for each embedded paywall.")
                    return
                }
                guard self.attach(paywall) else {
                    self.reportError("There is no view controller to show the embedded paywall in.")
                    return
                }
                sendToUnity(method: "embeddedPaywallOnPresent", data: [
                    "viewId": viewId,
                    "paywallInfo": serializePaywallInfo(paywall.info)
                ])
            } catch let reason as PaywallSkippedReason {
                guard !Task.isCancelled, !self.isRemoved else { return }
                var data: [String: Any] = ["viewId": viewId]
                switch reason {
                case .holdout(let experiment):
                    data["reason"] = "holdout"
                    data["experiment"] = serializeExperiment(experiment)
                case .noAudienceMatch: data["reason"] = "noAudienceMatch"
                case .placementNotFound: data["reason"] = "placementNotFound"
                @unknown default: data["reason"] = "noAudienceMatch"
                }
                sendToUnity(method: "embeddedPaywallOnSkip", data: data)
            } catch is CancellationError {
                // Removed while loading.
            } catch {
                guard !Task.isCancelled, !self.isRemoved else { return }
                self.reportError(error.localizedDescription)
            }
        }
    }

    func remove() {
        isRemoved = true
        loadTask?.cancel()
        loadTask = nil
        detach()
    }

    private func reportError(_ message: String) {
        sendToUnity(method: "embeddedPaywallOnError", data: ["viewId": viewId, "error": message])
    }

    private func attach(_ paywall: PaywallViewController) -> Bool {
        let windows = UIApplication.shared.connectedScenes
            .compactMap { $0 as? UIWindowScene }
            .flatMap { $0.windows }
        let window = windows.first(where: { $0.isKeyWindow }) ?? windows.first
        guard let parent = window?.rootViewController, let parentView = parent.view else { return false }

        container.frame = parentView.bounds
        container.autoresizingMask = [.flexibleWidth, .flexibleHeight]
        parentView.addSubview(container)

        parent.addChild(paywall)
        let paywallView: UIView = paywall.view
        paywallView.translatesAutoresizingMaskIntoConstraints = true
        paywallView.autoresizingMask = []
        container.addSubview(paywallView)
        container.paywallView = paywallView
        container.setNeedsLayout()
        paywall.didMove(toParent: parent)
        paywallViewController = paywall
        return true
    }

    private func detach() {
        if let paywall = paywallViewController {
            paywall.willMove(toParent: nil)
            paywall.view.removeFromSuperview()
            paywall.removeFromParent()
        }
        paywallViewController = nil
        container.removeFromSuperview()
    }

    // MARK: PaywallViewControllerDelegate

    // Required of a delegate outside SuperwallKit: its default implementation isn't public. The paywall shows its
    // own loading state, so there's nothing to pass on.
    func paywall(_ paywall: PaywallViewController, loadingStateDidChange loadingState: PaywallLoadingState) {}

    func paywall(_ paywall: PaywallViewController, didFinishWith result: PaywallResult, shouldDismiss: Bool) {
        // A paywall this view has let go of may still report to it.
        guard paywall === paywallViewController else { return }
        let info = serializePaywallInfo(paywall.info)
        if shouldDismiss {
            unityEmbeddedPaywalls[viewId] = nil
            remove()
        }
        sendToUnity(method: "embeddedPaywallOnFinished", data: [
            "viewId": viewId,
            "paywallInfo": info,
            "result": serializePaywallResult(result),
            "shouldDismiss": shouldDismiss
        ])
    }
}

@MainActor private var unityEmbeddedPaywalls: [String: UnityEmbeddedPaywall] = [:]

// x, y, width and height are fractions of the screen, with y measured from the top.
@_cdecl("_SuperwallBridge_ShowEmbeddedPaywall")
public func _SuperwallBridge_ShowEmbeddedPaywall(
    _ viewId: UnsafePointer<CChar>,
    _ placement: UnsafePointer<CChar>,
    _ paramsJson: UnsafePointer<CChar>?,
    _ x: Float,
    _ y: Float,
    _ width: Float,
    _ height: Float
) {
    let id = String(cString: viewId)
    let placementName = String(cString: placement)
    let params = toSwiftString(paramsJson).flatMap { parseJson($0) }
    let rect = CGRect(x: CGFloat(x), y: CGFloat(y), width: CGFloat(width), height: CGFloat(height))
    Task { @MainActor in
        guard Superwall.isInitialized else {
            sendToUnity(method: "embeddedPaywallOnError", data: [
                "viewId": id,
                "error": "Superwall must be configured before an embedded paywall is shown."
            ])
            return
        }
        let embedded = UnityEmbeddedPaywall(viewId: id, normalizedRect: rect)
        unityEmbeddedPaywalls[id] = embedded
        embedded.load(placement: placementName, params: params)
    }
}

@_cdecl("_SuperwallBridge_SetEmbeddedPaywallRect")
public func _SuperwallBridge_SetEmbeddedPaywallRect(
    _ viewId: UnsafePointer<CChar>,
    _ x: Float,
    _ y: Float,
    _ width: Float,
    _ height: Float
) {
    let id = String(cString: viewId)
    let rect = CGRect(x: CGFloat(x), y: CGFloat(y), width: CGFloat(width), height: CGFloat(height))
    Task { @MainActor in
        unityEmbeddedPaywalls[id]?.setNormalizedRect(rect)
    }
}

@_cdecl("_SuperwallBridge_RemoveEmbeddedPaywall")
public func _SuperwallBridge_RemoveEmbeddedPaywall(_ viewId: UnsafePointer<CChar>) {
    let id = String(cString: viewId)
    Task { @MainActor in
        unityEmbeddedPaywalls.removeValue(forKey: id)?.remove()
    }
}
