# ME.BECS source-generator migration: first stage

Status: source-only prototype, not installed into Unity. The existing generator remains authoritative.
No build, Unity compilation, generated-output comparison, or performance measurement has been run.

The project targets netstandard2.0 and Microsoft.CodeAnalysis.CSharp 4.3.1, matching the compiler
dependency version in Unity 6000.2.14f1's local csc.deps.json. Unity analyzer loading must still be verified.

## Current scope

- Incremental syntax/semantic discovery of public unmanaged non-generic components and aspects in each compilation.
- Partial declarations are deduplicated. Output is sorted ordinally and contains no timestamps or file paths.
- Per-assembly catalogs use collision-free encoded assembly names.
- Ordinary components get individual Validate/SetDefaultValue methods. None are called automatically.
- Shared/static registrations, groups, AOT, config initialization, aspect storage, jobs, asset discovery,
  closed generic instantiations and dependency analysis remain with the existing generator.
- Catalog ordering is NOT a replacement for the existing global ordering or a source of numeric type IDs.

The generated source lives in Roslyn's compilation output, not in Assets/ME.BECS.Gen.
The Tools directory is outside Assets so Unity will not compile the generator implementation as game code.

## Integration stages

1. When builds are allowed, build this project and import only ME.BECS.SourceGenerator.dll as a Unity
   RoslynAnalyzer, with normal plugin platform compatibility disabled. Do not import Roslyn dependency DLLs.
   Verify analyzer scope for all relevant asmdefs and both editor/player compilations.
2. Compare the catalog with GetUsedObjects separately for editor and runtime. Keep the full editor type
   discovery and runtime asset/usage filtering, including explicit includes and generic specializations.
3. Teach the editor bootstrap emitter to call an individual generated registration method at the exact
   position where it currently emits Validate/SetDefaultValue. Keep group setup, AOT and all other phases.
   Fall back to legacy emission for unsupported types. Never register an entire assembly in one step:
   global type order can interleave multiple assemblies.
4. Verify equality of component/group/aspect/system IDs and default values with the previous bootstrap,
   including domain reload, player builds, fixed-point configuration and test-only components.
5. Only then remove redundant legacy emission and migrate the remaining registration phases.

Runtime initialization attributes and module initializers are intentionally absent: compiler or assembly
load order must not decide simulation IDs. Unity assets still require an editor-produced manifest;
Roslyn cannot inspect method bodies from referenced assemblies, so cross-assembly dependency analysis
requires exported summaries or the existing IL analyzer.
