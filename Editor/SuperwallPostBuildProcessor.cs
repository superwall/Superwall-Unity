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
            string podfilePath = Path.Combine(buildPath, "Podfile");

            if (!File.Exists(podfilePath))
            {
                string podfileContent = @"platform :ios, '16.0'
use_frameworks!

target 'UnityFramework' do
  pod 'SuperwallKit', '~> 4.0'
end

target 'Unity-iPhone' do
end

post_install do |installer|
  installer.pods_project.targets.each do |target|
    target.build_configurations.each do |config|
      config.build_settings['IPHONEOS_DEPLOYMENT_TARGET'] = '16.0'
    end
  end
end
";
                File.WriteAllText(podfilePath, podfileContent);
                Debug.Log("[Superwall] Created Podfile with SuperwallKit dependency");
            }
            else
            {
                string podfileContent = File.ReadAllText(podfilePath);
                if (!podfileContent.Contains("SuperwallKit"))
                {
                    int targetIndex = podfileContent.IndexOf("target 'UnityFramework'");
                    if (targetIndex >= 0)
                    {
                        int doIndex = podfileContent.IndexOf("do", targetIndex);
                        if (doIndex >= 0)
                        {
                            int insertIndex = podfileContent.IndexOf('\n', doIndex) + 1;
                            podfileContent = podfileContent.Insert(insertIndex, "  pod 'SuperwallKit', '~> 4.0'\n");
                        }
                    }
                    else
                    {
                        podfileContent += "\ntarget 'UnityFramework' do\n  pod 'SuperwallKit', '~> 4.0'\nend\n";
                    }

                    File.WriteAllText(podfilePath, podfileContent);
                    Debug.Log("[Superwall] Added SuperwallKit pod to existing Podfile");
                }
            }

            // Modify the Xcode project to enable Swift
            string projPath = PBXProject.GetPBXProjectPath(buildPath);
            var project = new PBXProject();
            project.ReadFromFile(projPath);

            string unityFrameworkGuid = project.GetUnityFrameworkTargetGuid();
            string mainTargetGuid = project.GetUnityMainTargetGuid();

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

            RunPodInstall(buildPath);
        }

        private static void RunPodInstall(string buildPath)
        {
            string podPath = LocatePodExecutable();
            if (podPath == null)
            {
                Debug.LogWarning(
                    "[Superwall] Could not locate the CocoaPods 'pod' executable. " +
                    $"Run manually:\n\n    cd \"{buildPath}\" && pod install\n\n" +
                    "If you don't have CocoaPods installed: 'sudo gem install cocoapods' " +
                    "or 'brew install cocoapods'.");
                return;
            }

            var process = new System.Diagnostics.Process();
            process.StartInfo.FileName = "/bin/bash";
            // Login shell so user PATH from ~/.zshrc / ~/.bash_profile is loaded (rbenv/asdf shims, Homebrew).
            process.StartInfo.Arguments = $"-l -c \"cd '{buildPath}' && '{podPath}' install\"";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            // Some Ruby installs (rbenv/asdf) misbehave when these env vars are inherited from Unity.
            process.StartInfo.EnvironmentVariables.Remove("MONO_PATH");
            process.StartInfo.EnvironmentVariables.Remove("MONO_CFG_DIR");
            process.StartInfo.EnvironmentVariables.Remove("DYLD_FALLBACK_LIBRARY_PATH");
            process.StartInfo.EnvironmentVariables.Remove("DYLD_LIBRARY_PATH");
            // CocoaPods crashes with Encoding::CompatibilityError under non-UTF-8 locales —
            // Unity launched from Finder inherits launchd's empty LANG.
            process.StartInfo.EnvironmentVariables["LANG"] = "en_US.UTF-8";
            process.StartInfo.EnvironmentVariables["LC_ALL"] = "en_US.UTF-8";

            try
            {
                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode == 0)
                {
                    Debug.Log($"[Superwall] pod install completed successfully.\n{output}");
                }
                else
                {
                    Debug.LogWarning(
                        $"[Superwall] pod install failed (exit code {process.ExitCode}). " +
                        $"Run manually:\n\n    cd \"{buildPath}\" && pod install\n\n{error}");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning(
                    $"[Superwall] Could not run pod install automatically ({e.Message}). " +
                    $"Run manually:\n\n    cd \"{buildPath}\" && pod install");
            }
        }

        private static string LocatePodExecutable()
        {
            string home = System.Environment.GetEnvironmentVariable("HOME") ?? "";
            string[] candidates =
            {
                "/usr/local/bin/pod",
                "/opt/homebrew/bin/pod",
                Path.Combine(home, ".rbenv/shims/pod"),
                Path.Combine(home, ".asdf/shims/pod"),
                "/usr/bin/pod",
            };
            foreach (var path in candidates)
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path)) return path;
            }

            // Fall back to `which pod` under a login shell.
            try
            {
                var which = new System.Diagnostics.Process();
                which.StartInfo.FileName = "/bin/bash";
                which.StartInfo.Arguments = "-l -c \"command -v pod\"";
                which.StartInfo.UseShellExecute = false;
                which.StartInfo.RedirectStandardOutput = true;
                which.StartInfo.RedirectStandardError = true;
                which.Start();
                string result = which.StandardOutput.ReadToEnd().Trim();
                which.WaitForExit();
                if (which.ExitCode == 0 && !string.IsNullOrEmpty(result) && File.Exists(result))
                {
                    return result;
                }
            }
            catch { }
            return null;
        }
#endif
    }
}
