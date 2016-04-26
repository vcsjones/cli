using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Frameworks;

namespace Microsoft.DotNet.ProjectModel
{
    public class FrameworkNameHelper
    {
        private static Dictionary<NuGetFramework, string> _wellKnownFrameworksShortNames = new Dictionary<NuGetFramework, string>() {
            { FrameworkConstants.CommonFrameworks.NetStandard10, "netstandard1.0" }
        };

        private static Dictionary<NuGetFramework, string> _wellKnownFrameworksToString = new Dictionary<NuGetFramework, string>() {
            { FrameworkConstants.CommonFrameworks.NetStandard10, ".NETStandard,Version=v1.0" }
        };

        private static Dictionary<string, NuGetFramework> _wellKnownFrameworks = new Dictionary<string, NuGetFramework>() {
            {"netstandard1.0", FrameworkConstants.CommonFrameworks.NetStandard10 },
            {".NETStandard,Version=v1.0", FrameworkConstants.CommonFrameworks.NetStandard10 }
        };

        private static Dictionary<NuGetFramework, string> _wellKnownDefines = new Dictionary<NuGetFramework, string> {
            { FrameworkConstants.CommonFrameworks.NetStandard10, "NETSTANDARD1_0" }
        };

        public static string ToString(NuGetFramework framework)
        {
            string toString;
            if (!_wellKnownFrameworksToString.TryGetValue(framework, out toString))
            {
                toString = framework.ToString();
                Console.WriteLine($"Haven't cached ToString({framework.DotNetFrameworkName} {toString}");
            }
            return toString;
        }

        public static string GetShortFolderName(NuGetFramework framework)
        {
            string shortName;
            if (!_wellKnownFrameworksShortNames.TryGetValue(framework, out shortName))
            {
                shortName = framework.GetShortFolderName();
                Console.WriteLine($"Haven't cached GetShortFolderName({framework}) = {shortName}");
            }
            return shortName;
        }

        public static NuGetFramework Parse(string framework)
        {
            NuGetFramework frameworkName;
            if (!_wellKnownFrameworks.TryGetValue(framework, out frameworkName))
            {
                frameworkName = NuGetFramework.Parse(framework);
                Console.WriteLine($"Haven't cached Parse({framework}) = {frameworkName.DotNetFrameworkName}");
            }
            return frameworkName;
        }

        public static string GenerateFrameworkNameDefine(NuGetFramework framework)
        {
            string define;
            if (!_wellKnownDefines.TryGetValue(framework, out define))
            {
                define = MakeDefaultTargetFrameworkDefine(framework);
                Console.WriteLine($"Haven't cached GenDefine({framework}) = {define}");
            }
            return define;
        }

        private static string MakeDefaultTargetFrameworkDefine(NuGetFramework targetFramework)
        {
            var shortName = targetFramework.GetTwoDigitShortFolderName();

            if (targetFramework.IsPCL)
            {
                return null;
            }

            var candidateName = shortName.ToUpperInvariant();

            // Replace '-', '.', and '+' in the candidate name with '_' because TFMs with profiles use those (like "net40-client")
            // and we want them representable as defines (i.e. "NET40_CLIENT")
            candidateName = candidateName.Replace('-', '_').Replace('+', '_').Replace('.', '_');

            // We require the following from our Target Framework Define names
            // Starts with A-Z or _
            // Contains only A-Z, 0-9 and _
            if (!string.IsNullOrEmpty(candidateName) &&
                (char.IsLetter(candidateName[0]) || candidateName[0] == '_') &&
                candidateName.All(c => Char.IsLetterOrDigit(c) || c == '_'))
            {
                return candidateName;
            }

            return null;
        }

    }
}
