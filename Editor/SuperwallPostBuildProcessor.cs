using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
#if UNITY_IOS
using UnityEditor.iOS.Xcode;
#endif
using System.IO;

namespace Superwall.Editor
{
    public class SuperwallPostBuildProcessor : IPostprocessBuildWithReport
    {
        public int callbackOrder => 99;

#if UNITY_IOS
        private const string PackageRepositoryURL = "https://github.com/superwall/Superwall-iOS";
        private const string PackageMinimumVersion = "4.0.0";
        private const string PackageProductName = "SuperwallKit";
#endif

        public void OnPostprocessBuild(BuildReport report)
        {
#if UNITY_IOS
            if (report.summary.platform == BuildTarget.iOS)
            {
                PostProcessIOS(report.summary.outputPath);
            }
#endif
        }

#if UNITY_IOS
        private static void PostProcessIOS(string buildPath)
        {
            RemoveLegacyPodDependency(buildPath);

            string projPath = PBXProject.GetPBXProjectPath(buildPath);
            var project = new PBXProject();
            project.ReadFromFile(projPath);

            string unityFrameworkGuid = project.GetUnityFrameworkTargetGuid();
            string mainTargetGuid = project.GetUnityMainTargetGuid();

            // Append builds reuse the exported project, so the package reference from a
            // previous export may already be present — adding it again would duplicate it.
            if (!File.ReadAllText(projPath).Contains(PackageRepositoryURL))
            {
                string packageGuid = project.AddRemotePackageReferenceAtVersionUpToNextMajor(
                    PackageRepositoryURL, PackageMinimumVersion);
                project.AddRemotePackageFrameworkToProject(
                    unityFrameworkGuid, PackageProductName, packageGuid, false);
                Debug.Log($"[Superwall] Added {PackageProductName} Swift Package dependency " +
                    $"({PackageRepositoryURL}, {PackageMinimumVersion} up to next major).");
            }

            // Enable modules and Swift for the framework target
            project.SetBuildProperty(unityFrameworkGuid, "CLANG_ENABLE_MODULES", "YES");
            project.SetBuildProperty(unityFrameworkGuid, "SWIFT_VERSION", "5.0");

            // Set supported platforms to include simulator
            foreach (var targetGuid in new[] { mainTargetGuid, unityFrameworkGuid })
            {
                project.SetBuildProperty(targetGuid, "SUPPORTED_PLATFORMS", "iphonesimulator iphoneos");
                project.SetBuildProperty(targetGuid, "SUPPORTS_MACCATALYST", "NO");
            }

            project.WriteToFile(projPath);
        }

        // Older releases of this package integrated SuperwallKit via CocoaPods. On append
        // builds the previously generated Podfile survives, and leaving the pod in place
        // alongside the Swift Package would link SuperwallKit twice.
        private static void RemoveLegacyPodDependency(string buildPath)
        {
            string podfilePath = Path.Combine(buildPath, "Podfile");
            if (!File.Exists(podfilePath)) return;

            string podfileContent = File.ReadAllText(podfilePath);
            if (!podfileContent.Contains("SuperwallKit")) return;

            var kept = new System.Collections.Generic.List<string>();
            foreach (var line in podfileContent.Split('\n'))
            {
                if (line.Contains("pod") && line.Contains("SuperwallKit")) continue;
                kept.Add(line);
            }
            File.WriteAllText(podfilePath, string.Join("\n", kept));

            Debug.LogWarning(
                "[Superwall] Removed the SuperwallKit pod from the Podfile — SuperwallKit is now " +
                "integrated via Swift Package Manager. Run 'pod install' in the build folder to " +
                "prune it from your Pods (or 'pod deintegrate' if SuperwallKit was your only pod).");
        }
#endif
    }
}
