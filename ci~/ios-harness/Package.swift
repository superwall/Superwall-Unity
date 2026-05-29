// swift-tools-version:5.7
import PackageDescription

// Standalone SPM harness so CI can type-check Plugins/iOS/SuperwallUnityBridge.swift
// against the real SuperwallKit API without spinning up Unity. The Sources/SuperwallBridge
// directory is a symlink to ../../Plugins/iOS.
let package = Package(
    name: "SuperwallBridgeHarness",
    platforms: [.iOS(.v16)],
    products: [
        .library(name: "SuperwallBridge", targets: ["SuperwallBridge"])
    ],
    dependencies: [
        .package(url: "https://github.com/superwall/Superwall-iOS", from: "4.0.0")
    ],
    targets: [
        .target(
            name: "SuperwallBridge",
            dependencies: [
                .product(name: "SuperwallKit", package: "Superwall-iOS")
            ],
            exclude: [
                "SuperwallUnityBridge-Bridging-Header.h",
                "SuperwallUnityBridge-Bridging-Header.h.meta",
                "SuperwallUnityBridge.swift.meta",
            ]
        )
    ]
)
