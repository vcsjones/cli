// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.DotNet.ProjectModel;
using NuGet.Frameworks;
using System.Threading.Tasks;
using System.Collections.Concurrent;

namespace Microsoft.DotNet.Tools.Build
{
    internal abstract class ProjectBuilder
    {
        private ConcurrentDictionary<ProjectContextIdentity, Task<CompilationResult>> _compilationResults =
            new ConcurrentDictionary<ProjectContextIdentity, Task<CompilationResult>>();

        public IEnumerable<CompilationResult> Build(IEnumerable<ProjectGraphNode> roots)
        {
            return Task.WhenAll(roots.Select(node => Build(node))).GetAwaiter().GetResult();
        }

        protected CompilationResult? GetCompilationResult(ProjectGraphNode projectNode)
        {
            Task<CompilationResult> result;
            if (_compilationResults.TryGetValue(projectNode.ProjectContext.Identity, out result))
            {
                return result.GetAwaiter().GetResult();
            }
            return null;
        }

        protected virtual bool NeedsRebuilding(ProjectGraphNode projectNode)
        {
            return true;
        }

        protected virtual void ProjectSkiped(ProjectGraphNode projectNode)
        {
        }

        protected abstract Task<CompilationResult> RunCompile(ProjectGraphNode projectNode);

        private Task<CompilationResult> Build(ProjectGraphNode projectNode)
        {
            return _compilationResults.GetOrAdd(projectNode.ProjectContext.Identity, i => CompileWithDependencies(projectNode));
        }

        private async Task<CompilationResult> CompileWithDependencies(ProjectGraphNode projectNode)
        {
            var results = await Task.WhenAll(projectNode.Dependencies.Select(d => Build(d)));
            if (results.Contains(CompilationResult.Failure))
            {
                return CompilationResult.Failure;
            }

            var context = projectNode.ProjectContext;
            if (!context.ProjectFile.Files.SourceFiles.Any())
            {
                return CompilationResult.IncrementalSkip;
            }

            if (NeedsRebuilding(projectNode))
            {
                return await RunCompile(projectNode);
            }
            else
            {
                ProjectSkiped(projectNode);
                return CompilationResult.IncrementalSkip;
            }
        }
    }
}