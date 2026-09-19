#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

namespace GameSDK.Editor
{
    internal static class DeviceIosBuildProcessor
    {
        [PostProcessBuild(100)]
        private static void AddFrameworks(BuildTarget target, string buildPath)
        {
            if (target != BuildTarget.iOS)
                return;

            string projectPath = PBXProject.GetPBXProjectPath(buildPath);
            var project = new PBXProject();
            project.ReadFromFile(projectPath);
            string targetGuid = project.GetUnityFrameworkTargetGuid();
            project.AddFrameworkToProject(targetGuid, "Foundation.framework", false);
            project.AddFrameworkToProject(targetGuid, "Security.framework", false);
            project.AddFrameworkToProject(targetGuid, "AdSupport.framework", false);
            project.AddFrameworkToProject(targetGuid, "AppTrackingTransparency.framework", true);
            File.WriteAllText(projectPath, project.WriteToString());
        }
    }
}
#endif
