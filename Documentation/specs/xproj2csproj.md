# xproj -> csproj

## Scenario: Add csproj refernece to xproj through UI

1. There are __xproj__ project __A__, and __csproj__ project __B__. The dependency relationship is not established now.
2. User add project __B__ as a reference to __A__ through Visual Studio UI.
3. __WTE__ add project B reference to project A's __xproj file__.
4. __WTE__ walk through the csproj graph and build a __dg__ file.
5. __dotnet restore__ restores projects. Relies on the __dg__ file generated in previous steps it manages to walk through all csproj files. It generated __project.lock.json__ for project A. The lock file has project references to project __B__ as well as all csproj it references. The lock file only provide the path to the csproj files.
6. __WTE__ walk down the entire msbuild project graph. For each csproj file it reachs it generates a __fragment lock file__.
7. After all the __fragment lock files__ are generated, these files are aggregrated into one __project.fragments.lock.json__ file by __WTE__. It placed at the same place as project A's __project.json__.

## Scenario: dotnet Build

1. CLI looks for a fragment file whenever the lock file is read. If one is there, it does the merge in memory.
2. Upstream code should not care about merging anymore.
2. __dotnet build --no-dependencies__ works as usual.
 
## Proposed data formats

**A fragment export object will:**
- be unique in terms of name / version
- have the same format as a lock file target library (representable by a LockFileTargetLibrary)
- will never contain dependencies. Those are already in the lock file libraries
- only has asset elements
- all paths are relative to the csproj location

project.json
```json
﻿{
    "version": "1.0.0-*",
    "compilationOptions": {
        "emitEntryPoint": true
    },

    "dependencies": {

    },

    "frameworks": {
      "net46": {
        "dependencies": {
          "ClassLibrary1": {
            "target": "project"
          },
          "ClassLibrary2": {
            "target": "project"
          },
          "ClassLibrary3": {
            "target": "project"
          }
        }
      }
    }
}

```

Fragment file:
```json
{
	"version": 2,
	"exports": {
		"ClassLibrary1/1.0.0": {
			"type": "msbuildProject",
			"framework": ".NETFramework,Version=v4.5.2",
			"compile": {
				"../bin/Debug/ClassLibrary1.dll": {}
			},
			"runtime": {
				"../bin/Debug/ClassLibrary1.dll": {}
			}
		},
		"ClassLibrary2/1.0.0": {
			"type": "msbuildProject",
			"framework": ".NETFramework,Version=v4.6",
			"compile": {
				"../../bin/Debug/ClassLibrary2.dll": {}
			},
			"runtime": {
				"../bin/Debug/ClassLibrary2.dll": {}
			}
		},
		"ClassLibrary3/1.0.0": {
			"type": "msbuildProject",
			"framework": ".NETFramework,Version=v4.6",
			"compile": {
				"../bin/Debug/ClassLibrary3.dll": {}
			},
			"runtime": {
				"../bin/Debug/ClassLibrary3.dll": {}
			}
		}
	}
}
```

Main lock file:

```json
{
	"locked": false,
	"version": 2,
	"targets": {
		".NETFramework,Version=v4.6": {
			"ClassLibrary1/1.0.0": {
				"type": "msbuildProject"
			},
			"ClassLibrary2/1.0.0": {
				"type": "msbuildProject"
			},
			"ClassLibrary3/1.0.0": {
				"type": "msbuildProject"
			}
		},
		".NETFramework,Version=v4.6/win7-x64": {
			"ClassLibrary1/1.0.0": {
				"type": "msbuildProject"
			},
			"ClassLibrary2/1.0.0": {
				"type": "msbuildProject"
			},
			"ClassLibrary3/1.0.0": {
				"type": "msbuildProject"
			}
		},
		".NETFramework,Version=v4.6/win7-x86": {
			"ClassLibrary1/1.0.0": {
				"type": "msbuildProject"
			},
			"ClassLibrary2/1.0.0": {
				"type": "msbuildProject"
			},
			"ClassLibrary3/1.0.0": {
				"type": "msbuildProject"
			}
		}
	},
	"libraries": {
		"ClassLibrary1/1.0.0": {
			"type": "msbuildProject",
			"msbuildProject": "../../ClassLibrary1/ClassLibrary1.csproj"
		},
		"ClassLibrary2/1.0.0": {
			"type": "msbuildProject",
			"msbuildProject": "../../ClassLibrary2/ClassLibrary2.csproj"
		},
		"ClassLibrary3/1.0.0": {
			"type": "msbuildProject",
			"msbuildProject": "../../ClassLibrary3/ClassLibrary3.csproj"
		}
	},
	"projectFileDependencyGroups": {
		"": [],
		".NETFramework,Version=v4.6": [
			"ClassLibrary1",
			"ClassLibrary2",
			"ClassLibrary3"
		],
		".NETStandardApp,Version=v1.5": [
			"NETStandard.Library >= 1.0.0-rc2-23826"
		]
	}
}
```

## Merging logic

- for each export in the fragment, add its assets elements to all the library occurences in the lock file (key = name + version).
- Compatibility checks:
    - exit with error if
        - type does not match
        - tfm in fragment export is null (this means that restore could not resolve the dependency due to framework / runtime missmatches)
        - there are csproj libraries in lock file that do not have a corresponding export

## CLI project model changes
The msbuild projects cannot be represeted as ProjectDescription CLI objects. ProjectDescription leaks a Project object which wraps over project.json. The whole CLI codebase took a dependency on the assumption that ProjectDescription.Package is project.json.

Solution: introduce a new library type, "msbuildProject". Update all code that switches on LibraryDescription and / or does casting. This new type behaves very similar to a package. 

## Discussion topics:

```
David's view:
- The fragment file is never a true lock file, it has no targets.
- The lock file has holes that need to be filed.
- Compatibility needs worked out by nuget not by the merging
- These types of projects need to be identified separately, "legacyProject" is a bad name. "msbuildProject" is better.

```
