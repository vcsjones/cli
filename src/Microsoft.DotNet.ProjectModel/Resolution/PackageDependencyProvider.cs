// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.ProjectModel.Graph;
using NuGet.Frameworks;
using NuGet.Packaging;

namespace Microsoft.DotNet.ProjectModel.Resolution
{
    public class PackageDependencyProvider
    {
        private readonly VersionFolderPathResolver _packagePathResolver;
        private readonly FrameworkReferenceResolver _frameworkReferenceResolver;

        public PackageDependencyProvider(string packagesPath, FrameworkReferenceResolver frameworkReferenceResolver)
        {
            _packagePathResolver = new VersionFolderPathResolver(packagesPath);
            _frameworkReferenceResolver = frameworkReferenceResolver;
        }

        public PackageDescription GetDescription(NuGetFramework targetFramework, LockFilePackageLibrary package, LockFileTargetLibrary targetLibrary)
        {
            // If a NuGet dependency is supposed to provide assemblies but there is no assembly compatible with
            // current target framework, we should mark this dependency as unresolved
            var containsAssembly = package.Files
                .Any(x => x.StartsWith($"ref{Path.DirectorySeparatorChar}") ||
                    x.StartsWith($"lib{Path.DirectorySeparatorChar}"));

            var compatible = targetLibrary.FrameworkAssemblies.Any() ||
                targetLibrary.CompileTimeAssemblies.Any() ||
                targetLibrary.RuntimeAssemblies.Any() ||
                !containsAssembly;

            var dependencies = new List<LibraryRange>(targetLibrary.Dependencies.Count + targetLibrary.FrameworkAssemblies.Count);
            PopulateDependencies(dependencies, targetLibrary, targetFramework);

            var path = _packagePathResolver.GetInstallPath(package.Name, package.Version);
            var exists = Directory.Exists(path);

            var packageDescription = new PackageDescription(
                path,
                package,
                targetLibrary,
                dependencies,
                compatible,
                resolved: compatible && exists);

            return packageDescription;
        }

        private void PopulateDependencies(
            List<LibraryRange> dependencies,
            LockFileTargetLibrary targetLibrary,
            NuGetFramework targetFramework)
        {
            foreach (var dependency in targetLibrary.Dependencies)
            {
                dependencies.Add(new LibraryRange(
                    dependency.Id,
                    dependency.VersionRange,
                    LibraryType.Unspecified,
                    LibraryDependencyType.Default));
            }
        }

        public static bool IsPlaceholderFile(string path)
        {
            return string.Equals(Path.GetFileName(path), "_._", StringComparison.Ordinal);
        }

        public static string ResolvePackagesPath(string rootDirectory, GlobalSettings settings)
        {
            // Order
            // 1. global.json { "packages": "..." }
            // 2. EnvironmentNames.PackagesStore environment variable
            // 3. NuGet.config repositoryPath (maybe)?
            // 4. {DefaultLocalRuntimeHomeDir}\packages

            if (!string.IsNullOrEmpty(settings?.PackagesPath))
            {
                return Path.Combine(rootDirectory, settings.PackagesPath);
            }

            var runtimePackages = Environment.GetEnvironmentVariable(EnvironmentNames.PackagesStore);

            if (!string.IsNullOrEmpty(runtimePackages))
            {
                return runtimePackages;
            }

            var profileDirectory = Environment.GetEnvironmentVariable("USERPROFILE");

            if (string.IsNullOrEmpty(profileDirectory))
            {
                profileDirectory = Environment.GetEnvironmentVariable("HOME");
            }

            return Path.Combine(profileDirectory, ".nuget", "packages");
        }
    }
}
