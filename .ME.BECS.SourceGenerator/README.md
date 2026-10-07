# ME.BECS source-generator migration

## Revised architecture: IL analysis + compiler-owned emission

The migration target is now a hybrid pipeline. Fresh compiled IL is the intended
authority for behavioral analysis; Roslyn owns emitted C#, registration wrappers,
generic specialization, graph/injection bodies and AOT. The Editor still exports
asset data and analysis snapshots. Removing IL is no longer a migration requirement.
Source behavior summaries are retained as a diagnostic/regression oracle during
the transition, not a second long-term production analyzer.

Current project-specific C# emission belongs to the compiler, including bootstrap entry points/AOT,
graph lifecycle and injection, config callbacks, addon registrations and all 72
template job backends. The Editor exports ordered discovery/IL/asset data and
consumer assembly settings; it no longer writes aggregate `.cs` retirement
comments, executable bodies or compilation stamps. IL analysis is retained intentionally. The disabled
CopyFrom implementation is deferred separately, as requested.

Removing `Assets/ME.BECS.Gen` entirely is also part of the final target. Current
publication owners and input catalogs are independent of the retired aggregate;
publication tests reject references back to its assemblies. Project-owned native additional files carry the input data,
without per-consumer response arguments or dummy C# stamps. The first
decoupling step moves the project-independent initialization lifecycle into
`ME.BECS.BootstrapRuntime`. Generated entry points now supply ordered type/method
callbacks to that runtime API. It retains the same reset, type/lock, callback, config
and module-pass sequence and has no knowledge of consumer names or locations.
System/component AOT roots now belong to their selected publication owners (see
"Owner-local AOT preservation" below). Core composition now has a project-owned,
type-free publisher; Views dependency selection has its own feature-scoped project
owner. Compiled input evidence and readiness checks now use independent profile
catalogs plus each typed owner's receipt. Readers discover those catalogs by their
profile markers rather than a fixed aggregate assembly name. Startup no longer
comes from the aggregate. Compatibility exclusions and migration diagnostics still
recognize its old names; these are not executable generation paths.

Unity verification of this lifecycle extraction (2026-10-05, 14:49–14:51 MSK):
`Tests_SourceGeneratorBootstrapOwnership` passed 31/31, including IL checks of both
generated entry points, the ordered type plan and the framework lifecycle phases.
`Tests_JobEntityLimits` passed 6 with 0 failures; 2 optional source-summary comparison
tests were ignored because `BECS_SOURCE_ANALYSIS_DIAGNOSTICS` is disabled. The passed
tests include real Editor bootstrap initialization and deterministic entity IDs across
parallel batch sizes. Runtime Play reached the main menu and Stop returned to EditMode
without a BECS exception or stale-input warning. This is not player/AOT execution proof.
The analyzer DLL build completed with zero warnings/errors. Unity's script compile,
automatic Runtime/Editor input export and consumer compile all completed successfully;
the cold exports after the analyzer/core change took 95.46 s and 60.01 s respectively.

Selected registrations, graph bodies, AOT references and core composition now have explicit owners,
including downstream bridges for closed generic specializations. Remaining structural
work includes auditing remaining legacy export/upgrade paths. The clean-import and build-machine path
must recreate this state, not depend on a pre-existing Library cache. Moving or
renaming the generated folder does not satisfy this requirement.

Unity EditMode migration smoke verification (2026-10-06, 21:20:29–21:27:08 MSK):
430 selected, 428 passed, zero failures, two ignored optional source-analysis
comparisons (`BECS_SOURCE_ANALYSIS_DIAGNOSTICS` disabled). This includes publication
ownership, lifecycle plans, bootstrap/config/injection contracts, executable linker
roots, deterministic entity limits, isolated IL worker-cache merges and build-machine
analysis reconstruction without input writes. The latest run also covers the four
Player-build guard call sites and removal of the obsolete retirement-stub constant.
Guard call-site tests are structural, not a real concurrent export/build test.
The report is project-local at
`Temp/ME.BECS.SourceGenerator/MigrationSmoke.Tests.xml`. It does not establish a
clean-Library import or actual Player/Burst AOT execution and stripping correctness.

Local Player build verification (2026-10-06, 20:53:04–21:11:46 MSK):
Unity reported `Succeeded` for the Dev macOS IL2CPP build in 1122 seconds.
Both the Player executable and `GameAssembly.dylib` contain x86_64 and arm64
slices. The project-local artifact is `Builds/Dronefall-Dev-macOS-20261006.app`
(approximately 1.6 GiB). This establishes Player/native compilation, not execution
of generated registrations, Burst callbacks or stripping-sensitive paths. The
application was not launched as part of this verification. An earlier attempt
failed because source input export was still running; the successful retry does
not establish that export/build overlap is handled correctly.

### Non-modal input export progress

Clean-project relocation verification (2026-10-06): copied the framework to
`Assets/RenamedFramework` in an isolated project without existing Library or source
inputs. Unity 6000.2.14f1 compiled it successfully after configuring its package
dependencies, Animation/Particle System modules and project-level `Assets/csc.rsp`
(required by existing framework response files). Ordinary Editor startup published
337 input files; a subsequent export changed zero files. A project-local probe
validated readiness, executed the Editor bootstrap, entered Play Mode and required
the Runtime bootstrap plan successfully; the Editor exited with code 0.
Evidence is in `/private/tmp/becs-clean-import.JDTrJD/clean-import-result.txt` and
`probe-play.log`. This verifies relocation and initialization, not gameplay,
IL2CPP execution or a package-manager-only installation. Shutdown reported two
Persistent allocations; leak investigation remains deferred by request.

Input export uses scoped `UnityEditor.Progress` tasks in Unity's background-task UI,
not `EditorUtility.DisplayProgressBar`/`DisplayCancelableProgressBar`. Stage names,
current subject and elapsed time are still reported at most every 150 ms. Managed
tasks finish with the export outcome and never clear unrelated Unity/Burst progress.
Cancellation is cooperative during analysis and disabled before publishing any input
files or assembly settings. Batch mode creates no UI task. The migration smoke suite
includes task isolation/cleanup and cancellation-before-publication tests (passed
in the 2026-10-06 smoke run recorded above).

Automatic refresh, Rebuild and both graph Compile buttons now use `RequestExport`:
capture Unity data -> background IL analysis -> validate/compose on the Editor
thread -> write files on a worker -> import/validate on the Editor thread.
Its return value means accepted, not completed; graph UI clears compile-dirty
state only in the successful publication callback. Synchronous `TryExport`/`TryRebuild`
remain for compatibility, not for these UI paths. No Player build is started.

`CaptureRuntimeDiscovery` reads graph, config and module roots into a type-only snapshot;
`AnalyzeRuntimeDiscovery` consumes it without retaining asset instances.
`ILAnalysisEnvironment.Capture` copies compilation inventory, target and cache path.
The coordinator worker prepares discovery and exact selected job
safety/entity-count/weight summaries. Editor system dependencies run concurrently
on a second worker using the same production analyzers. The coordinator joins that
worker before transferring results; the Editor never waits synchronously for it.
Both workers own isolated memo/persistent-cache dictionaries. Child cache changes
merge once, preserving coordinator results for duplicate keys; only the coordinator
writes the merged analysis cache. An isolated
`ILAnalysisSession` transfers its memo once, only after its Task completes; it never
borrows the Editor's retained dictionary. Code-identity dictionaries use short locks;
file hashing runs outside those locks. User `IRefOp` constructors/getters are serviced
by a small Editor-update queue, with cancellation releasing the waiting worker.

Editor Update polls without waiting. Native Progress stays on the Editor thread;
analysis supports cooperative cancellation, including declaration/body fingerprinting.
Compilation or a new input request supersedes the task. Before publication both the
request version and full fingerprint must still match. Reload is locked while the
worker owns reflection objects and unlocked on cleanup, including cancellation/error.
No cancelled/stale task publishes files or a successful receipt. Cancelling an unfinished
declaration index preserves the previous persistent cache.

Asset capture, final input composition/addon work, asset imports and receipt validation
still run on the Editor thread, as does Unity's own reload. Planned input-file writes
run on a worker, with asset editing/reload protection retained until completion.
This does not claim an entirely stall-free
Editor: compare the remaining main-thread timings in Unity after these changes.
`Tests_ILAnalysisSnapshot`, `Tests_ILExportSession` and graph-refresh tests cover ordered
worker/main-thread discovery parity (including generic systems), memo transfer/isolation,
cancellation and stale-result gates. These fixtures passed in the 2026-10-06 smoke
run recorded above.

### Independent compiled input catalogs and readiness

`InputCatalogPublicationGenerator` emits data-only `InputCatalog_Editor` and
`InputCatalog_Runtime` markers into project-owned bridges selected without gameplay
or addon references. A checksummed native additional file contains the exact exported
manifest, including its profile and asset snapshot. The marker has no methods,
initializers, world state or registration side effects. Retired selections emit no
marker; duplicate inputs withhold the entire affected profile rather than publishing
the first candidate. No compiler arguments or framework-local input assets are added.

`SourceGeneratorInputCatalog` discovers the compiled owner by its marker and metadata,
not an aggregate assembly name. Raw input rows, content hash and graph snapshot remain
available to Editor consumers without referencing gameplay types. Parsed metadata is
cached per immutable assembly, but candidate uniqueness is checked on each discovery.
Public raw-row access returns a copy so diagnostic callers cannot corrupt readiness.

Analysis receipts and graph readiness use logical `runtime`/`editor` profiles. They
require matching current analysis, compiled input content and asset snapshot, followed
by exact typed publication receipts for every runtime-required selection. An input
catalog alone cannot certify world readiness. Each readiness pass shares its assembly
inventory, catalog lookups and owner metadata between fragment kinds. Advisory system
dependency tables remain excluded. Transitional compiler recovery guards are still
detected by their actual type and metadata, not by the old consumer name.

`Tests_SourceGeneratorInputCatalog` covers exact compiled evidence, caller isolation,
missing/ambiguous profiles, envelope round trips and invalid/retired selections. Its
integration case removes aggregate assemblies from the candidate set, checks both
analysis and typed publication readiness, then removes a real typed owner while keeping
the input catalogs to ensure readiness fails. These tests are added to migration smoke;
Unity execution of this stage is still pending. Existing aggregate diagnostic emission,
compatibility adapters and the old assembly exporter have not yet been removed.

Config/count/mask and destroy availability readers now consume these input catalogs
and validate the selected owner-local callback/registration methods. Their obsolete
aggregate fallback paths and returned C# `Initialize()` call strings were removed.
They remain read-only diagnostics and never execute registration callbacks. Config
and destroy publication tests obtain selection from the independent input catalogs;
global composition/phase ordering is tested by the bootstrap publication/phase fixtures,
not by asserting that legacy forwarding facades still exist. Component flag/AOT readers
were the next detachment step, now described below; other older diagnostic consumers
and compatibility adapters remain.

Analyzer DLL built and installed on 2026-10-06 in 21.51 s, zero warnings/errors.
Installed/output SHA-256 both:
`61c0b34b30341de6f930ba6a4d8546b368d5881a12c2d00dc7a9923039202002`.
This confirms analyzer compilation only, not Unity compilation or execution of the
new input-catalog and updated callback tests.

### Owner-local component flags and AOT diagnostics

Each selected `TypeFragment_Editor`/`TypeFragment_Runtime` now exposes a literal
`Flags_<ordinal>` beside its registration/AOT/size methods. The value is the exact
compiler classification used to emit that closed component's registration, including
generic/local fallbacks. It introduces neither a runtime initializer nor simulation
state. Component availability diagnostics read the constant after validating the
selected owner receipt and phase methods. Aggregate flag metadata/fallback bodies
are no longer the authority for these readers.

System AOT diagnostics read the existing profile-specific `SystemAotPublication`
metadata from the selected owners. Masks are matched by exact system identity and
assembled by explicit global ordinal, never assembly-load or attribute enumeration
order. The raw input catalog still provides the independent global system selection.
The distributed aggregate path no longer repeats AOT analysis or emits duplicate
`SystemAotPlan`/`ComponentFlags` metadata; legacy-format emission is transitional only.

Ownership/contract tests now use the independent input catalog and selected owners.
AOT expectations come from reflection interface maps and WithoutBurst/Burst attributes,
including explicit and closed generic lifecycle implementations. Additional diagnostic
tests exclude aggregate assemblies, reverse input/assembly enumeration and omit a typed
owner to check missing coverage. No AOT, Default or registration method is executed by
these diagnostics. Unity/Burst/Player execution of this stage remains unverified.

Analyzer DLL built/installed on 2026-10-06 in 21.59 s with zero warnings/errors;
installed/output SHA-256:
`23fb959dac858eed8c583f2b5c4f182fea927e9b7dbcd194c7722eed02a6680a`.

### Independent Editor system dependency diagnostics

`SystemDependencyPublicationGenerator` consumes the Editor-only dependency input
slice in a dedicated project bridge. It reuses `SystemDependencyInputEmitter`'s
existing IL operation selection, writer edges, canonical ordering, generic-definition
unions, private-type indirection and advisory messages. The current aggregate path
no longer analyzes or emits these tables or their selected/origin metadata.

The universal Editor `SystemDependencyCatalog` discovers the compiled owner and
binds typed delegates once. Graph layout and node UI use this API instead of looking
up `ME.BECS.Editor.StaticMethods` in a named generated assembly. New systems whose
diagnostics have not compiled yet do not break node drawing; explicit table requests
still report unavailable data rather than silently proving an empty dependency set.
Late bridge loads invalidate the discovery cache. Diagnostic tables are initialized
only when queried, with no Editor/runtime startup hook or simulation State changes.

Missing/invalid dependency diagnostics produce a generator warning, not a runtime
preflight rejection. No synchronization hint changes scheduling or requires the user
to add/remove `Complete()`. These diagnostic fragments are intentionally excluded
from the world/bootstrap publication-completeness gate. Raw global input metadata,
analysis receipts and graph freshness were the next removal step; the independent
input catalog section above describes that subsequent transition.

On 2026-10-06 the analyzer DLL built/installed with zero warnings/errors (21.42 s),
SHA-256 `0783206988c20d37b2ba1b86b4bb4ceb8a6b11d8d0c019a2a5282362f30245ac`.
Two new smoke cases cover detached ownership, API forwarding, selected typed plans,
unknown/null systems, receipts, unrelated-input stability and retired envelopes.
Existing generic-union/private-type diagnostics tests read the new owner. Unity
compilation/tests and Player builds were not run for this change.

### Project-owned Views dependency selection

`ViewSelectionPublicationGenerator` consumes only the canonical Views input slice
in a project-owned bridge. It reuses the existing tracker/type selectors unchanged:
IL callback snapshots, `IViewIgnoreTracker`, `IViewTrackIgnore<T>`, explicit opt-in
after exclusions, imported private aspect filters, dual view/module role union and
type callback flags retain their semantics. Its compiler view imports all metadata
but still applies normal C# accessibility checks. The bridge references Views and
its selected type dependencies, not the complete systems/jobs/graphs inventory.

The selection emits a preserved Editor/Runtime publisher plus the ordered component
ordinal dependency table. Typed registration callbacks still belong to the existing
per-type owners. The table is static startup data and the runtime takes its own copy;
no simulation State layout or component access behavior changes. Empty/retired owners
emit receipts without publishers. Runtime owners remain linker roots.

Current aggregate inputs validate the transported selection envelope without redoing
Views symbol analysis or emitting its selected catalogs/table. Views diagnostic menus
and tests resolve the actual compiler owner; they no longer read selected Views
metadata from `ME.BECS.Gen`. `StaticMethods.PublishBootstrapPlan` and its automatic
initializer are absent in the fully distributed path. The transitional callable
`Load` is now only a forwarding call to `BootstrapRuntime.LoadInstalled`.

On 2026-10-06 the analyzer DLL built/installed with zero warnings/errors (21.24 s),
SHA-256 `60384047e65ada034d0c930615f89a29ecb62f78ea38f0438b5eab092d91f2a9`.
Two additional Editor/Runtime cases check independent ownership, exact dependency
ordinals/role unions, transport receipts, startup attributes, unrelated-input stability
and retired envelopes. Existing tracker, callback-flag and repeated-bootstrap tests
read the new owner. Unity tests/builds were not run for this change; analyzer build
success alone is not generated Unity-code or Player/AOT verification.

### Project-owned bootstrap composition

`BootstrapPublicationGenerator` emits the core composition in a minimal project
bridge under `Assets/ME.BECS.SourceInputs`. Its reference surface contains the
framework, Unity startup attributes and installed Network/Views addons, not the
concrete gameplay systems/components/jobs. `BootstrapFragment.v1` carries only
ordered plan hashes/counts, feeder kinds and the job setup ordinal map. Editor
and Runtime have separate owners. The framework remains universal and receives
no project cache files or response-file paths.

The owner publishes expected system/type/entity/aspect/destroy/config/network/job
selections before installing the immutable core phase data. Runtime also publishes
the graph selection and registers its first-pass callback at `BeforeSplashScreen`.
Neither publication runs registrations, creates a world or assigns IDs. Existing
global ordinals and repeated EarlyInit slots are unchanged. Runtime owners have
`AlwaysLinkAssembly`; the composition publication has `Preserve` and the same
Editor/Runtime initialization stage as before.

The aggregate no longer publishes core phase data or the graph first-pass hook.
The initial composition extraction left `BootstrapViewsSelection` there; the feature
selection step above removes that last startup publication. Retiring remaining
diagnostic/freshness/compatibility consumers is still necessary before the aggregate
assemblies/folder can be removed. The old manifest branch is transitional, not an
alternative completed architecture.

On 2026-10-06 the analyzer DLL built/installed with zero warnings/errors (21.52 s),
SHA-256 `9d90418e243467c7cd5154ada162eee6a680f7e5f14e19f8d678734e588252d1`.
Ten added composition cases cover exact publication arguments/receipts and phase
order, the graph hook, canonical round trips, unrelated snapshot changes, retired
owners, invalid plans and empty profiles without optional addons. Existing graph
and phase tests follow the new owner. These Unity cases have not been run; this
build is not Unity compilation, Player/AOT execution or clean-import evidence.

### Framework-owned bootstrap entry point

The current distributed path publishes `BootstrapPhaseInputs`: ordered
`Action<bool>` phase arrays, addon preflight delegates and the original EarlyInit
slot-to-statistics-ordinal mapping. `BootstrapPhases` in the core runtime copies
this data and owns both execution loops. Normal system/component registration
still precedes the ordered feeder initializers; config masks still precede config
collections inside their registration feeder. None/no-op feeder slots and repeated
job slots remain explicit. Each job setup runs immediately before its corresponding
EarlyInit/stat-only slot, independent of publication-owner arrival order.

The runtime checks complete job-map coverage and optional addon preflight before
shared resets. Identical publication reuses the same core plan; a conflicting
phase order, callback, map or debug flag poisons that profile. The debug flag is
compiled in the publishing profile rather than inferred from core assembly defines.
All of this is managed startup data, outside simulation State.

For this path the compiler no longer emits `RegisterTypePlan`,
`RegisterAdditionalTypes`, `RegisterGeneratedMethods`, `ValidateGeneratedInputs`
or per-feeder executable hooks. `StaticTypesInitializer.Load` and
`JobBootstrapInputs.Initialize` temporarily remain as callable forwarding adapters;
the installed plan contains only framework-owned execution delegates. The initial
extraction still placed expected selections and phase data in the aggregate; the
project-owned composition step above removes that core publication dependency.
Views selection is now extracted as described above; other diagnostic tables remain.

On 2026-10-06 the analyzer DLL built/installed with zero warnings/errors (21.51 s),
SHA-256 `f6c8a20d7836ff0eb2e65ffcae2d6bf2d6a6b49bc16c1611742c112956d91dc2`.
Thirteen phase-plan cases cover ordered data, repeated-slot execution, independent
owner arrival, copied arrays, invalid coverage and conflict handling. Existing
ownership/feeder/EarlyInit checks now inspect data plus core execution instead of
requiring the retired generated bodies. These Unity tests have not been run for
this change; no Unity/Player build was initiated.

Automatic startup now belongs to `BootstrapRuntime.LoadRuntime` in the core runtime
assembly, at `BeforeSceneLoad`. Generated Runtime code publishes typed delegates at
`AfterAssembliesLoaded`; generated Editor code publishes its separate plan through
`InitializeOnLoadMethod`. Publication does not execute registrations, allocate a
world, assign IDs, or reset shared state. The framework later executes the same
ordered type/method callbacks through its existing lifecycle.

`AllTests.Start` requires the installed Editor plan and calls `LoadInstalled` directly.
It no longer discovers `ME.BECS.Gen.Editor` or invokes a named generated method through
reflection. The callable generated `StaticMethods.Load` remains a compatibility
adapter forwarding to the typed runtime API, not another automatic startup hook.
The managed plan registry is outside simulation State; main-thread startup owns it.
Repeated identical publication is idempotent, and conflicting owners/callbacks poison
only their profile so initializer ordering cannot select a silent winner.

This removes ownership of automatic execution and test startup from the aggregate
assembly, not the aggregate registration plan itself. The next structural step is
replacing each full-profile callback plan with explicitly ordered owner fragments;
global IDs, feeder phases, graph calls and AOT reachability must survive that split.

The core type portion now executes through ordered `BootstrapTypeRegistry` instances
in the runtime. Systems have a separate first phase, followed by groups, ordinary
components, shared, static, then config. Compiler-selected callbacks carry explicit
ordinals within their phase; both phases must be complete before either executes.
Supported component/system entries point directly to the declaring-assembly typed
methods, including closed generic methods. Publication is side-effect free and may
arrive in any fragment order; only the explicit ordinals control execution. Arrays
are copied on acceptance; identical replay is harmless. Missing slots and conflicting
selections/owners are rejected before `LoadInstalled` resets any shared state.

System publication is now distributed: each selected script owner emits its own
`SystemFragment_Editor` / `SystemFragment_Runtime` publisher. The selecting consumer
only declares the expected system plan identity/count; it contains no system callback
arrays. Components/groups now use the equivalent owner-local `TypeFragment_Editor`
and `TypeFragment_Runtime` publishers. Additional type/method feeders, graph wiring,
component size/AOT facades and the selected AOT root still have aggregate ownership
and must be moved next. Merely grouping
by a declaring assembly does not prove that compilation can name a closed generic
specialization, so actual reference-based placement remains mandatory.

System fragment placement is now an explicit input contract. The Editor exports
one `system-registration-owner` for each selected system, in exactly the registration
order. It chooses from Unity's actual compiler references, including nested generic
arguments and their generic constraint assemblies, instead of assuming a definition
assembly can name and bind its closed forms.
Runtime placement additionally requires the candidate to exist in the active player
compilation inventory and uses that inventory's references, not its Editor-only
references. Definition owners are preferred when eligible, then argument owners,
then smaller existing reference surfaces; all ties use ordinal names. Generated
aggregate assemblies are never candidates and existing asmdef dependencies are not
modified. When no existing owner has the required reference surface, the exporter
creates a project-owned publication bridge (see below).

The compiler validates exact ordered coverage; registration targets/ordinals remain
unchanged. All compiler inputs belong to the consuming project, under
`Assets/ME.BECS.SourceInputs`, regardless of the framework folder name/location.
They use Unity's native `Filename.ME.BECS.SourceGenerator.additionalfile` transport.
No publisher writes project data beside the analyzer DLL, changes an owner's
`csc.rsp`, or injects content hashes/compiler paths. IL caches remain in
`Library/ME.BECS.SourceGenerator`; diagnostic reports remain in `Temp`.

System fragment assets carry only selected identities, owners and ordinals, never
graph/config payloads or the code fingerprint. Unchanged contents are not rewritten,
and retired selections become data-only tombstones without initialization hooks.
No executable C# source is written to Assets. A one-way migration moves the former framework
inputs with their existing GUIDs and removes only the old managed response blocks;
unrelated user response options are preserved.

### Project-owned publication bridges

Cross-assembly generic specializations sometimes have no ordinary owner: for
example, a framework test generic job closed over a gameplay component, where
neither assembly references the other. The exporter places these publications in
`Assets/ME.BECS.SourceInputs/Bridges`, never inside the framework. A bridge name is
stable for its profile and exact required assembly set; existing eligible ordinary
owners are still preferred. Old bridges are not used opportunistically as owners
of unrelated selections, so their creation order cannot change registration IDs.

Each bridge contains an asmdef, a fixed comment-only `AssemblyMarker.cs` (Unity's
compilation anchor), and an ownership receipt. Roslyn emits all executable code.
The asmdef is not auto-referenced, explicitly references the dependency closure,
keeps platform/test/define constraints and precompiled references, and gives
dependency-local version defines unique names before combining constraints.
No framework asmdef, response file or machine-specific path is modified. Existing
bridge files with user modifications are preserved and reported, not overwritten.

Planning and build-machine validation are read-only. Only normal input publication
creates or updates bridge assets, and unchanged files keep their timestamps and
GUIDs. Cached feeders recover the required assembly set from the project receipt.
Bridge compiler output is excluded from behavioral IL/code fingerprints to avoid
an export/compile feedback loop. Empty retired publications retain their inert
hosts; no automatic destructive assembly cleanup is performed.

`Tests_SourceGeneratorPublicationBridges` covers deterministic placement, platform
intersection, test constraints, local version defines, consumer/predefined-assembly
rejection and project-only fixed anchors. Exact callback targets and ordinals are
checked by the existing publication fixtures, including Job EarlyInit.

Verified in Unity 6000.2.14f1 on 2026-10-05: 13 Editor/test bridge assemblies
compiled; migration smoke tests passed 324/326 with 0 failures and 2 expected
source-diagnostics skips (all 10 bridge tests and 5 EarlyInit publication tests
passed). Repeated export and the tests preserved all 227 project input/host files'
contents and timestamps. Ordered EarlyInit/statistics/entity input records retained
their pre-migration hash. Play reached the main menu and was stopped successfully;
no Player build or IL2CPP stripping validation was performed.

### Owner-local job statistics

Job entity reservations, IL operation weights and native component size setup now
use project-owned `JobSetupFragment` inputs and compiler-emitted owner callbacks.
Only the used entity groups are included in each job entry, with their unchanged
global IDs and total group count. Placement includes the job's closed generic
arguments, all selected layout dependencies, entity groups and required native APIs.
Ordinary owners are preferred; the same project bridge policy covers missing refs.

The aggregate retains diagnostic selection metadata but no typed statistics bodies.
It dispatches the setup callback before **every** original EarlyInit slot, including
repeated jobs and stat-only slots; deduplicating emission never deduplicates execution.
Each callback keeps entity counts -> weight -> native size order. Both profiles
require a complete setup plan before shared-state reset. The registry lives outside
simulation State. Old input snapshots retain their aggregate setup only until the
next normal export, allowing the updated Editor to load without a compile deadlock.

`Tests_SourceGeneratorJobSetupPublications` checks owner bodies, group bindings,
profile metadata and exact setup/EarlyInit interleaving without executing jobs.
Debug wrappers and graph/AOT aggregate ownership are separate remaining steps.

Unity verification on 2026-10-06: DLL build completed with zero warnings/errors;
Unity compiled the owner fragments successfully. Migration smoke tests passed
330/332 (0 failures, 2 expected optional source-diagnostic skips), including all
6 setup-publication tests and deterministic entity IDs across batch sizes.
The original ordered EarlyInit/statistics/entity input records retained their
pre-migration hash. The new test's repeated per-job metadata scan was subsequently
changed to a single indexed pass; that test-only optimization awaits the next run.
No Player build or IL2CPP stripping validation was performed.

### Owner-local debug job wrappers

`JobDebugFragment` carries each original job/contract/IL-safety plan with its global
ordinal. The compiler emits safe/unsafe layouts, a private SharedStatic cache and
the Burst factory in the selected owner assembly, including closed generic jobs.
Public wrapper owner names include the profile and an assembly hash to avoid
collisions between referenced owners. The aggregate `DebugJobs` is now only a
compatibility dispatcher, without typed wrappers, factories or cache fields.

Publications do not compile Burst pointers or initialize caches. The framework
executes the complete ordered debug registry at the same point before job statistics
and EarlyInit. Multiple contracts for a job retain their original SetFunction order.
Checked and unchecked builds publish the same slot coverage; unchecked callbacks
are empty and omit debug types/Burst factories entirely. Both profile registries
remain outside simulation State and are required before shared-state reset.

A single `PublicationRootsGenerator` emits `AlwaysLinkAssembly` for assemblies with
nonempty selected runtime fragments, unless the assembly already declares it. This
also covers other publication kinds and project bridges with no scene references.
Editor-only and retired empty inputs do not request a new root. Existing Preserve
and RuntimeInitializeOnLoadMethod attributes identify the actual entry points;
AlwaysLinkAssembly requests linker processing, not blanket preservation of every type.
See [Unity's attribute contract](https://docs.unity3d.com/ScriptReference/Scripting.AlwaysLinkAssemblyAttribute.html).

The debug publication fixture checks exact slot metadata, Burst factory references,
layout fields/attributes, preflight and the last registered contract of each job.
Compiled metadata checks are not a substitute for build-machine Burst/IL2CPP
stripping validation. Graph/injection and AOT aggregate roots remain to be moved.

On 2026-10-06 the analyzer DLL built with zero warnings/errors and Unity compiled
the new owner fragments successfully (38 debug fragment inputs: 9 Runtime and
29 Editor). The original EarlyInit/statistics/entity record hash stayed unchanged.
The debug fixture and the indexed statistics-test update have not yet been run:
the user was inspecting a paused gameplay session, which was left untouched.

### Project-owned graph publications

`GraphFragment` transports each complete graph's original registration, slot table,
topology and fresh IL job selections. It does not transport injection fields or C#.
The compiler emits storage, all five lifecycle phases, system/job injection and
delta-time patches together in a downstream project bridge. The bridge reference
surface includes the actual graph/node/system/job types and generic constraints;
private setters are consumed from their already-compiled declaring assemblies.
Bridges and inputs live in `Assets/ME.BECS.SourceInputs`, never in the framework or
response-file paths. Graphs with the same required reference surface share a host.

Publication only installs typed registration delegates with global graph ordinals.
The original BeforeSplashScreen first-pass hook now dispatches that complete ordered
registry; storage allocation and repeated patch registration still occur inside the
original graph Initialize callbacks. Slot indices, serialized system values, generic
parallel/sequential groups, phase dependency handles and batch boundaries are retained.
The registry is outside simulation State and its Runtime preflight precedes shared reset.

`GraphInputEmitter` is shared by the publication path and old-snapshot upgrade path.
Fresh aggregate inputs emit only the first-pass adapter, not graph storage, lifecycle
or delta bodies. Aggregate graph metadata remains temporarily as a diagnostic oracle;
owner metadata uses the `PublishedGraph` prefix so existing reports do not double-count
plans. Tests compare both exact plans and emitted registration/injection call order.
Final selection/bootstrap/AOT ownership and aggregate removal are still outstanding.

On 2026-10-06 the analyzer DLL built without warnings/errors. Unity compiled both
new Runtime bridges successfully (25.39 s): compiled DLLs contain the LogicGraph
and VisualGraph storage/lifecycle owners, graph publishers and linker root attributes.
The aggregate DLL retains the selection/first-pass adapter but no typed graph or
delta owner. The original EarlyInit/statistics/entity input hash is unchanged.
Cold input exports took 103.81 s Runtime / 79.29 s Editor. The five new graph tests,
six debug tests and indexed statistics test still need a completed fresh smoke run;
intervening Features.Editor/Tests recompilations triggered further input refreshes.
The later UI test-launch attempt was not confirmed: the Unity window became
unavailable/timeouts persisted after reload. The last smoke XML is still the earlier
01:22 result, not a result for these changes. Play verification is also outstanding;
the running Editor was not restarted or its user session stopped.
No Player build or stripping validation was performed.

### Owner-local AOT preservation

`SystemFragment_Editor/Runtime.PreserveReferences` retains the selected registration,
Burst/NoBurst/direct lifecycle and factory references in the owner's original slot
order. Masks are still compiler-derived from the exact closed system/interface map,
including explicit/private implementations and `[WithoutBurst]`. Public declaring-
assembly contracts are preferred; precompiled definitions use typed local references.

`TypeFragment_Editor/Runtime.PreserveReferences` retains normal/shared/static/config
component AOT calls through global-ordinal wrappers. Native size helpers live in the
same owner. Group slots do not introduce AOT calls. Registration delegates and global
phase-major ID assignment are unchanged. These preservation-only methods have no
runtime/Editor initialization attribute and are never put in registration arrays.
The existing `AlwaysLinkAssembly` publication marker makes the linker process each
nonempty Runtime owner; it does not preserve unrelated systems or lifecycle phases.

Distributed inputs no longer emit `SystemInputs`, `ComponentInputs`, `SystemAotInputs`,
`CoreTypeInputs.AotComponents`, or the aggregate `AOTBurstHelper`. The old-snapshot
upgrade branch temporarily retains its former roots independently per domain.
Diagnostic comparison resolves the current owner receipts and masks; it never invokes
an AOT/registration/Default method. The aggregate AOT metadata remains only an oracle
until aggregate diagnostic consumers are retired.

Ownership tests now compare exact owner calls and closed generic arguments, complete
phase-major coverage, preserved non-initializer roots, linker marking and absence of
aggregate typed AOT bodies. Unity tests and Player/Burst/IL2CPP stripping for this
change have not been run; an analyzer DLL build alone does not verify them.
On 2026-10-06 the analyzer DLL built and installed with zero warnings/errors
(21.53 s). Installed SHA-256: `a86358b15150826ff41b81f471661ac9288e80f65f562d86ffdc7a54d0254673`.

Type fragment assets use the same checked envelope and native transport. Their
entries are globally ordered group
applications followed by ordinary/shared/static/config component registrations.
Group type identities (including open generic groups and named-argument overrides)
are selection data; a changed group changes the plan even when component names stay
the same. The compiler validates attributes and phase interfaces against the current
definition and computes layout/Default flags itself. Supported registration methods
remain in their declaring assembly; precompiled-component fallback bodies are emitted
in the selected publication owner. The aggregate no longer publishes component/group
callback arrays or executes an aggregate group-registration body.

Entity fragment assets now use the same project-owned transport under
`EntityFragments`. Each callback is emitted in an eligible existing owner assembly
and invokes `EntityTypes.Register<T>` with its exact global ordinal as the ushort
group ID. Definition/argument/reference placement follows the existing deterministic
owner rules; no asmdef references or response files are modified. The common
selection publishes only identity/count, and `EntityInputs` retains diagnostic ID
constants plus an untyped call into the framework, not typed registration bodies.
At the original entity-feeder position, the framework checks complete coverage,
clears entity registration, executes callbacks in ordinal order and sets groupsCount.
The complete entity plan is also required before any world/shared-state reset.
Empty plans are explicit; retired owner fragments contain no initializer.

Aspect fragment assets use `AspectFragments` in the same project-owned input root.
Each selected ordinal publishes a pair: a typed ID/query initializer and a typed
`ref World` constructor (explicitly null for an aspect with no data-pointer fields).
The owner generator uses the catalog's own eligibility rules for same-compilation
targets, including private fields in partial aspects; imported targets require the
actual public catalog methods. It never relies on seeing another generator's output
in the current compilation. Both phases share one complete ordered runtime plan,
independent of owner load order. The original feeder positions remain unchanged:
registration first, then one world dispatcher after the callback reset. Every world
gets its own aspect data pointers; neither pointers nor publication data enter State.
The aggregate retains only selection identity/count and untyped feeder facades.

Destroy callback fragments use `DestroyFragments` under that same project root.
Owner selection additionally requires unsafe compilation and the actual Burst/AOT
references for the target profile; asmdefs are never modified to force eligibility.
Each owner emits closed, non-generic Burst/MonoPInvokeCallback entry points and
registers them in global selection order at the original destroy feeder position.
The constrained generic body dispatches the interface implementation, including
explicit implementations, and retains the null-data path for tag components.
Burst callback classes have no managed publication fields/type initializer;
publication arrays live in a separate class and execute no Burst compilation or
registry mutation at assembly-load time. Full coverage is required before reset.

Config publications use project-owned `ConfigFragments` with a phase-major ordered
selection: collection counts, field masks, then collection materialization. Counts
are still derived from compiler-validated collection fields, not exported numbers.
The three phases execute at their original bootstrap feeder positions; publishing
does not resize count tables or register pointers. A missing owner blocks all phases
before any shared state is reset. Each owner must support unsafe compilation and
the actual field-type/Burst/AOT references. Mask bits retain declaration order;
collection fields retain stable reflection-type ordering. Managed publication and
diagnostic getters are separate from closed, preserved Burst callback bodies.
The aggregate contains only phase counts/identity and untyped initialization facades.

Network method publications use project-owned `NetworkFragments`. Typed method
delegates live in eligible existing owner assemblies; the common selection retains
only the identity/count and an untyped feeder facade. Publication never creates a
method storage or assigns wire IDs. The original module callback registers methods
in global ordinal order, with wire ID equal to ordinal + 1, for each storage.

Views publications use `ViewsFragments` under the same project-owned input root.
The ordered phases are component tracking, tracker owners, then view types. Typed
callbacks live in existing eligible owner assemblies. A type selected as both a
view and a module receives one tracker ID and the ordered union of both roles'
dependencies. The common compiler plan supplies component ordinals; the runtime
maps them to tracker indices and passes `ViewInfo` to the owner callback. Owners
therefore need no new assembly references to the components used by their views.
View flags still come from the current compiler contracts, and type registration
remains deferred to the original `ViewsModuleData` callback. Complete Network and
Views coverage is checked before shared-state reset, without core-to-addon references.
The publication registries are startup-only managed state, not simulation State.

Job EarlyInit calls now use project-owned `JobInitFragments`. Every original slot
has a global ordinal, including repeated jobs and stat-only no-ops. The common
initializer still interleaves entity reservations, weight and native layout setup
before dispatching that slot; those three setup bodies and debug wrappers are not
distributed yet. Owners emit preserved calls to the exact selected ordinary or
closed generic wrapper, without changing its reflection-data initialization.
Local wrappers are validated through `JobEarlyInitGenerator`'s own selection logic:
one generator cannot resolve another generator's output in the same compilation.
Imported wrappers retain normal symbol/signature binding. Publication does not
invoke EarlyInit, and the complete plan is required before shared-state reset.
Old snapshots retain a temporary common publisher until the normal input refresh;
new snapshots leave no typed EarlyInit calls or delegate tables in the aggregate.

Live entity-publication verification (2026-10-05, 20:21–20:24 MSK): the complete
pre-migration entity-registration rows remained byte-identical after migration:
34 Runtime entities across nine owners and 58 Editor entities across 12 owners.
All 21 new input assets are project-owned. The expanded smoke suite passed 221
tests, failed zero and ignored two optional comparisons (223 total, 42.24 s).
All 14 entity tests passed, including real compiled owner callbacks and their
literal IDs, absence of aggregate typed registration bodies, native-file delivery,
complete coverage before clearing groups, ushort bounds and repeated Editor
bootstrap. The analyzer DLL built with zero warnings/errors. Play reached the main
menu and was stopped with zero console errors, no input export and no compilation.
Entity fragment contents/timestamps stayed unchanged across the final re-analysis,
tests and Play cycle. This is Editor verification, not a Player/IL2CPP or full
gameplay validation. Aspects, other feeder callbacks, graph wiring and aggregate
selection/consumer retirement remain unfinished.

The runtime installs typed callbacks without assigning IDs, then executes systems
in the selected global order before groups/components. Complete coverage and plan
identity are required before world/shared-state reset. The Editor freshness gate
also checks each compiled owner's input hash against the consumer's exact selection;
missing/stale fragments cannot pass merely because the aggregate graph snapshot is
current. Malformed owner inputs permit Editor recovery with BECSG107 (systems),
BECSG109 (components/groups), BECSG111 (entities), BECSG113 (aspects), BECSG115
(destroy callbacks), BECSG117 (configs), BECSG119 (Network) or BECSG121 (Views)
and no fragment; Player compilation reports BECSG106, BECSG108, BECSG110,
BECSG112, BECSG114, BECSG116, BECSG118 or BECSG120 respectively.
The selected AOT root/calls are unchanged.

During upgrade, old snapshots without the corresponding `system-publication-schema`
or `type-publication-schema` / `entity-publication-schema` / `aspect-publication-schema` retain temporary common publishers until automatic
input refresh. Publication no longer requires a writable asmdef directory; placement
still requires the actual compiler references and active Player inventory. No asmdef
references are added to manufacture an eligible owner. Clean-import/package checks,
component AOT/size facades, graph/addon placement and removal of the aggregate consumers
remain unfinished.

Unity routes native additional files to every assembly using the analyzer. Generators
filter by exact assembly/profile filename before reading contents, but that does not
make Unity's compilation invalidation assembly-local. This is an explicit current
performance tradeoff, not a claimed optimization. The earlier response-file experiment
reduced compilation breadth but was rejected because project state must not live in
or modify the reusable framework. Further optimization must preserve that boundary.

Analysis freshness no longer changes compiler input merely because an assembly's
code fingerprint changed. `graph-input-snapshot` contains the effective asset/target
snapshot; the manifest's actual IL-derived plans still change whenever analysis
produces different semantics. The complete code/asset fingerprint lives only in
`Library/ME.BECS.SourceGenerator/AnalysisReceipt.v1.txt`, together with the two exact
analyzed manifest hashes and a checksum. Export invalidates the old receipt before
publishing anything and commits a new one only after both profiles succeed and
inputs remain stable. A failed/interrupted export cannot reuse the old receipt.

Play/build readiness requires the receipt to match current code/assets and current
project inputs, both consumers' compiled `InputContentHash.v1` and graph snapshots
to match that analysis, and all registration owner fragments to be current. A new
code fingerprint alone requires analysis, not a new compiler manifest or compiler
define. If that analysis produces identical data, existing compiled inputs remain
usable. A missing Library receipt on a batch build is reconstructed by read-only
IL analysis of both profiles: it must reproduce the entire published manifests,
does not register graph references or modify Assets, and still requires matching
compiled evidence. Different data fails preflight instead of exporting/recompiling
inside a build. This does not remove the remaining cost of re-analysis or Unity's
broad invalidation when semantic inputs really change.

Live verification of this separation (2026-10-05, 19:24–19:30 MSK): changing only
an assertion message in the new non-job test changed the compiled code identity.
Unity compiled 10 items in 3.52 s; automatic Runtime/Editor analysis took 73.27 s /
44.53 s and updated the Library receipt's analysis fingerprint. All 58 native
input assets kept identical contents and timestamps; no second script compilation
occurred. The previous schema upgrade had required 23.93 s / 239 items, so this
check proves suppression of an unnecessary second compilation, not cheap analysis.
The expanded smoke suite passed 182 tests, failed zero and ignored two optional
diagnostic comparisons (184 total, 30.12 s). This includes replaying the actual
read-only build-preflight analysis and checking all native files remained untouched,
plus real compiled freshness, malformed/partial receipts, changed semantic data,
and missing/stale/ambiguous compiled consumers. Play reached the main menu without
errors or another export/compile; the game was stopped. A real Player build or a
fresh-Library batch Editor run was not performed.

The test-only `ME.BECS/Source Generator/Run Migration Smoke Tests (EditMode)` menu
uses Unity Test Runner's API to run the placement/publication, bootstrap, ownership,
transport and entity-limit fixtures together. It does not run automatically, export
inputs or build a Player. Its single NUnit report is
`Temp/ME.BECS.SourceGenerator/MigrationSmoke.Tests.xml`.

Project-owned native transport and component-publication verification in Unity
(2026-10-05, 19:02–19:10 MSK): migration moved 58 input assets out of the framework,
preserving GUIDs, and removed only the managed blocks from 21 response files.
The framework's `SourceGenerator` directory contains no project input snapshots.
The fresh migration smoke suite passed 144 tests, failed zero, and ignored two
optional source-analysis comparisons (146 total, 20.80 s). The compiled type plans
contain 801 Editor and 590 Runtime group/component phase calls, published by
18 Editor and 14 Runtime owners. Tests cover exact phase order, group attributes,
typed callbacks, compiled content hashes and Unity's actual native input delivery.

Two subsequent Play → main menu → Stop cycles with domain/scene reload disabled
completed with zero console errors and no stale-input rejection, export or script
compilation during either cycle. All 56 system/type fragment assets retained their
contents and timestamps across the final exports, test run and Play cycles.
The analyzer build succeeded with zero warnings/errors. Native-input compilation
took 20.52 s / 239 updated items before the test run, and 18.25 s / 239 items after
a later code refresh, versus roughly 5–6 s / 19 items with the superseded scoped
response transport. The latter refresh still spent 76.89 s / 47.94 s exporting
Runtime/Editor inputs: incremental export and broad native-input invalidation are
remaining performance work, not solved by this migration. No clean-import,
read-only-package, full gameplay or Player IL2CPP/Burst/stripping validation is
claimed; no Player build was run.

Earlier distributed system-publication verification in Unity (2026-10-05, 18:09 MSK): the
fresh migration smoke run passed 132 tests, failed zero, and ignored two optional
source-analysis comparisons (134 total, 11.32 s). The compiled selection contains
513 Editor and 124 Runtime systems, published by 15 Editor and 9 Runtime owners;
41 cross-assembly generic placements are covered in each profile. Tests inspect
the actual owner-local callbacks, their global ordinals and compiled input hashes.
The common system publisher contains only the expected selection identity/count.

Two consecutive Play → main menu → Stop cycles with domain/scene reload disabled
completed with zero console errors, no stale-input rejection, export or script
compilation. All 24 system fragment files retained their contents and timestamps
through the final exports and these cycles. The final analyzer build had zero
warnings/errors; the last Runtime/Editor exports took 80.12 s / 52.34 s and the
subsequent consumer compilation took 5.91 s. This validates the distributed system
phase in the current Unity project, not clean-import/package portability, a full
gameplay session or Player IL2CPP/Burst/stripping behavior. No Player build was run.

Earlier owner-placement verification in Unity (2026-10-05, 17:24–17:29 MSK): all 16 placement
tests passed against the final player-inventory/reference and generic-constraint
changes. The current selection contains 513 Editor and 124 Runtime systems, with
41 cross-assembly generic placements in each profile. The tests check exact compiled
owner records and unchanged fragment ordinals, not merely the planner in isolation.
Bootstrap passed 62/62; ownership passed 50 with 8 optional comparisons ignored;
entity limits passed 6 with 2 optional comparisons ignored. Across these groups,
103 distinct tests passed, zero failed, and 10 were ignored. Results are saved in
`Temp/ME.BECS.SourceGenerator/RegistrationOwners*.Tests.xml`.

The final Unity script/export/consumer compilation completed successfully; the
Runtime/Editor exports took 75.30 s / 47.53 s and the consumer compile took 5.51 s.
Play reached the main menu and Stop returned to EditMode with no console errors
or another input export. This confirms the current aggregate publisher remains
usable with the new placement contract; it does not claim owner-local publication
has been implemented. No local player build or IL2CPP/stripping validation was run.

Ordered-fragment verification in Unity (2026-10-05, 16:46–16:50 MSK): bootstrap
tests passed 62/62 (including 18 fragment tests), component ownership 11/11, system
ownership 8/8, and entity limits 6 passed. That is 87 distinct passed tests, zero
failures, and 10 ignored optional source-analysis comparisons. Compiled delegate
targets were checked against the exact manifest-selected global sequence, including
the installed Editor registry; malformed/missing fragments cannot partially execute.
Two consecutive Play → main menu → Stop cycles with domain/scene reload disabled
completed without an exception, stale-input rejection or another input export.
Results: `Temp/ME.BECS.SourceGenerator/TypeFragments.*.Tests.xml`. DLL compilation
had zero warnings/errors; Unity script compilation succeeded, exports took
85.16 s / 53.90 s, and the subsequent consumer compile took 5.58 s. No player build
or full gameplay/network session was run; AOT checks here inspect reachability,
not actual IL2CPP stripping or Burst execution.

Earlier entry-point-only verification (2026-10-05, 16:30–16:34 MSK): bootstrap ownership/registry
passed 44/44, component ownership 11/11, system ownership 8/8, and entity limits
6 passed (69 distinct passed tests, zero failures). Ten optional source-analysis
comparisons were ignored. The registry tests cover side-effect-free publication,
profile independence, idempotent replay, conflicts in either publisher order and
the live installed Editor plan. Entity-limit tests execute the new typed startup
path, including deterministic IDs across parallel batch sizes. XML snapshots are
`Temp/ME.BECS.SourceGenerator/BootstrapRegistry*.Tests.xml` in the Unity project.

Two consecutive Play → main menu → Stop cycles succeeded with the existing domain
and scene reload options both disabled. No exception, stale-input rejection or new
input export occurred during those cycles. This verifies Editor startup and replay,
not player stripping/AOT execution or a full gameplay session. The analyzer build
and Unity compilation succeeded; after the runtime/analyzer change, the one-time
Runtime/Editor exports took 87.33 s / 57.89 s and the consumer compile took 5.78 s.

The remaining integration gate is live Unity verification of automatic input refresh
after graph/config/target changes (including nested graphs and restart), plus player
Burst/IL2CPP/stripping validation. Local source/metadata checks do not prove those
runtime outcomes. Explicit recovery remains available as `Source Generator/Rebuild
Inputs (Full Analysis)`; graph Compile remains a cached export retry. Historical notes later in this document
are a migration log, not the current production selection policy.

### Declaring-assembly component registrations

`ComponentRegistrationGenerator` emits typed size, registration and AOT methods in
each component's declaring assembly. Public unmanaged definitions are supported,
including nested generic definitions whose constraints prove unmanaged storage.
Generic wrappers retain the declaring-type constraints and rename type parameters
by symbol, so nested scopes do not depend on parameter-name uniqueness. The generator
does not evaluate `Default`, assign IDs or install runtime initialization callbacks.

The selected `ComponentInputs` methods now forward to these owner methods when the
compiler can verify the complete contract and matching flags. `CoreTypeInputs` keeps
the original global phase-major sequence: all ordinary registrations, then shared,
static and config passes, with the same within-pass order. Native sizes use the
existing `TSize<T>.size` API (backed by `UnsafeUtility.SizeOf<T>`), so component
asmdefs do not acquire a direct Unity.Collections dependency. The selected AOT root directly reaches the typed owner
methods; unselected owner methods are not new preservation/runtime-init roots.

Precompiled components without generated owner contracts, and open definitions that
cannot prove unmanaged storage, still have compiler-emitted local bodies. Both paths
use one emitter to keep flags, Default handling and phase semantics identical. This
fallback is not Editor C# generation; moving its concrete specializations into a
referencing script assembly remains part of removing the aggregate consumers.
The aggregate selection/diagnostic facade and input transport still remain.

### Declaring-assembly system registration and AOT

`GenericSystemGenerator` owns ordinary and generic system registration bodies as
well as typed lifecycle/pointer AOT wrappers. `SystemInputs` now forwards selected
registrations to those methods; the duplicate ordinary-system bodies were removed
from the discovery catalogs. The Editor registration bridge reads the same owner
contract for ordinary and closed generic types, without invoking it.

The preserved system AOT root forwards only the selected Burst/NoBurst/direct/factory
phases to the owner assembly. It retains the original masks, ordering and root
attributes. Explicit interface lifecycle implementations use constrained calls,
and nested generic type parameters are renamed by symbol (including shadowed names).
Owner methods do not run during module loading, assign IDs speculatively or become
new preservation roots. Definitions without a compatible generated owner contract
still use a compiler-emitted body in the selecting assembly. The global selection
and graph ownership/input transport remain work needed to retire the aggregate folder.

Unity verification (2026-10-05, 15:20–15:23 MSK): system ownership passed 8/8,
component ownership 11/11, bootstrap ownership 31/31, and entity limits 6 passed
with 2 optional source-analysis comparisons ignored (0 failures). The system test
observed 513 selected Editor registrations and 124 Runtime registrations forwarding
to owner assemblies, with zero local fallbacks in this project. Ordered AOT calls
were inspected without executing the preservation-only methods. Nested generic
and explicit-interface cases passed; component size checks include native bool and
explicitly sized empty layouts through `TSize<T>`.

The ARM64 SDK analyzer build completed with zero warnings/errors (the earlier x64
Rosetta compiler invocation crashed and was cancelled). Unity script compilation,
automatic Runtime/Editor export and consumer compilation all completed; the exports
took 83.98 s and 52.58 s after the generator change. Play reached the main menu and
Stop returned to EditMode with zero console errors, without another export during
the tests/Play cycle. No player build, IL2CPP stripping check or full gameplay/network
session was run. Test XML snapshots are in the project's
`Temp/ME.BECS.SourceGenerator/SystemOwnership*.Tests.xml` files.

### Automatic refresh, recovery and compiled-input caching

Normal code/graph/config/target changes use the coalesced core Editor refresh service.
The old Run/Run Automatically menus no longer control freshness. The manual Rebuild
Inputs command bypasses the analysis caches; it is for recovery, not a required step
after every edit. The legacy `IsEnabledAuto` API returns true for compatibility.

Play/build entry uses a shared read-only compiled-snapshot check. A queued startup
comparison is not itself a failed/in-progress export: current compiled inputs may
clear that request immediately, without waiting for an Editor update tick (which
never exports in batch mode). Failed/active exports, stale/missing snapshots and
ongoing compilation/import still reject entry. A newer request raised during the
check is preserved and requires a retry; neither entry gate generates code or
starts compilation. This covers the startup gate in code, not player/Burst execution.

Invalid or missing consumer inputs report BECSG105 during Editor compilation only
(the actual `UNITY_EDITOR` parse symbol, not the manifest's profile). The consumer
emits a pending-recovery marker and no bootstrap or valid freshness snapshot. This
allows a repaired, independent Editor exporter to load and regenerate stale inputs.
Play Mode and build freshness gates continue to reject this state, and the incomplete
consumer also emits a runtime initialization error guard. Player compilation keeps
BECSG100 errors. Recovery never supplies empty entity reservations/registrations as
a usable bootstrap. Automatic recovery is attempted once per code/asset fingerprint;
an unchanged failure needs a code/input change or explicit retry, avoiding reload loops.

IL results are retained in memory across exports with an identical compiled-code
fingerprint. Default diagnostic/test sessions remain isolated. Jobs EarlyInit,
system dependencies and view tracker feeders additionally cache their complete data
records and referenced type identities under `Library/ME.BECS.SourceGenerator/CompiledInputs`.
Keys include ordered type selections, assembly selection, target, defines and compiled
assembly identities (including dependencies, excluding generated consumers). Changed code
invalidates these whole-feeder shortcuts, but no longer requires repeating all IL
analyses: the incremental summary layer below reuses unaffected work. Graph/config/theme data is not
cached there and is still exported from current assets. Invalid checksums/unresolvable
types cause a cache miss, not an empty/partial plan. Full Rebuild bypasses all analysis caches;
explicit diagnostic reports do not use the persistent cache. This reduces repeated
analysis, not the cost of an uncached analysis; live Unity timings
and automatic recovery still need verification in the project.

Consumer rebuilds may also recompile `Assembly-CSharp` and other transitive
dependants without user edits. These assemblies use their own declaration/IL and
source-content identity, not the cascading MVID, in both freshness and IL-cache
contexts. The compiler reference graph determines this set (even unused assembly
references can trigger recompilation). Other script assemblies retain the MVID fast path.
Precompiled inputs come from Unity's compiler reference paths and use cached file-content
hashes, including references not yet loaded into the domain. The live AppDomain inventory
is not an input set: Play can lazily load `System.Buffers`, and compilation can load Bee
tooling without changing project code. These loads previously invalidated both freshness
and every persistent IL summary. Only compiler-listed scripts are selected from the domain;
generated consumers are excluded from both script and library identities. File hashes are
cached for the loaded domain and refreshed after Unity's assembly reload. Identity-change
logs list the changed entries (capped at 20) to distinguish real input changes from reloads.
An unchanged automatic export attempt is not repeated after asset imports or a
consumer reload, including failed/pending exports. Changed inputs or explicit
Compile/Rebuild allow another attempt; this does not bypass the compiled freshness gate.

### Incremental IL summaries across recompilations

Production export additionally persists symbolic summaries in
`Library/ME.BECS.SourceGenerator/IncrementalIL.v2.json`. Discovery uses independent
single-body summaries (selected types plus outgoing methods); a fresh reachability
walk merges them from the current graph/config/module roots. Safety, entity counts,
weights, scheduled jobs and system dependencies persist their analyzed results with
the exact IL bodies read during analysis. Nested in-memory memo hits propagate those
dependencies as well, so a previously cached helper is not forgotten by its caller.

The v2 format stores each method/body version once in a shared table; summaries
contain integer indices instead of repeating full method identities and hashes.
Different bodies of the same method and closed generic arguments remain distinct.
Dependency validation is memoized per version within an export, and restored
summaries share those version objects. The file is loaded lazily, only when the
in-memory cache actually needs a persistent summary. The oversized v1 file is not
read or migrated; a cold v2 pass reconstructs results from the current assemblies.
Load/save logs expose summary count, unique method versions and output size.

On recompilation an unchanged method MVID is a fast path. For a changed MVID the
validator compares normalized IL operands, local types and exception regions, not
raw metadata-token numbers. A changed helper invalidates summaries that read it;
unchanged bodies in the same rebuilt assembly can still be reused. Generic method
and declaring-type arguments remain part of each key. No entity ID/group index or
world state is persisted, and reservation multiplicity/loop semantics are unchanged.

Declaration fingerprints are indexed once per assembly MVID/format and reused across
reloads. Changed types, interfaces, fields, attributes, constraints or type catalogs
currently invalidate the declaration context more broadly. Precompiled libraries
and executable metadata (custom attribute/IRefOp implementations) keep conservative
assembly boundaries. Unsupported reflection or
method identities reduce reuse instead of accepting unverifiable results. This is
incremental for ordinary body edits, not a claim of minimal invalidation for every
metadata change. Cold indexing still costs time; `Incremental IL declaration index`
and per-kind `reused` / `analyzed` counters expose the actual reuse in Editor.log.
Explicit diagnostic reports remain independent; Full Analysis bypasses all analysis
caches. Cached payloads have a checksum and are resolved against current types and
methods before selection. Missing/corrupt entries are reanalyzed.

Roslyn's embedded `EmbeddedAttribute`, `IsUnmanagedAttribute`, nullable/read-only/
by-ref-like/scoped/ref-safety markers are no longer mistaken for arbitrary executable
user attributes. A local constructor-content stamp is used only for an internal,
sealed, CompilerGenerated type with one of the exact known identities, directly
derived from `System.Attribute`. Every declared method must be an instance
constructor whose IL is limited to argument/constants, primitive field storage,
byte-array initialization and the exact base Attribute constructor. Helpers, static
field reads/initializers, virtual calls and other instructions retain the conservative
assembly stamp; the name and attributes alone cannot authorize reuse. The constructors
are inspected, never invoked. Their bodies and declaration metadata remain covered,
while an unrelated method edit no longer invalidates the global context just because
the compiler embedded these storage-only attributes. Persisted declaration format 2
forces a one-time re-index instead of mixing old MVID-based stamps with the new
algorithm. Cache reports now also distinguish context, implementation, dependency,
decode/resolve and missing-entry misses.

Live warm-cache verification (2026-10-05, 19:54–19:58 MSK): changing only an
assertion message changed the test assembly MVID while its declaration hash and
the global IL context stayed identical. Unity compiled 10 items in 3.42 s. Automatic
export took 13.46 s Runtime + 9.14 s Editor, versus 73.27 s + 44.53 s for the earlier
equivalent edit before this fix. The declaration index took 1.04 s and cache loading
4.12 s. Every requested persistent summary was reused: discovery 17,476, entity
counts 1,658, safety 1,707, scheduled jobs 159, system dependencies 567, weights
1,658; all analyzed counts were zero. These counts describe requested summaries,
not the total number of transitive entries in the cache. All 58 native input files
retained their contents and timestamps, the Library analysis receipt advanced,
and no second script compilation occurred. This is a measured warm body-only case,
not a cold-indexing or arbitrary-declaration-change guarantee.

The expanded migration smoke suite passed 207 tests, failed zero and ignored two
optional comparisons (209 total, 30.43 s), including all 25 incremental IL tests.
The new cases cover actual compiler-embedded attributes and reject same-named
fixtures with helper calls, static field reads or static initializers. Play reached
the main menu and was stopped, with zero console errors and no export/compilation
during the cycle. No Player build or fresh-Library batch run was performed.

The Editor analyzer assembly is indexed from its loaded declarations, normalized
method bodies, resources and RVA data, not from newer source files on disk. The
`ILScheduledJobs` traversal and its nested helpers have a separate implementation
stamp: body-only changes there invalidate scheduled-job summaries, not discovery,
safety, counts, weights or system dependencies. Shared scheduling contracts and all
other Editor implementation bodies remain in the common context. Declaration/layout
changes still invalidate broadly; unknown future summary kinds conservatively depend
on both partitions. Unsupported metadata falls back to the assembly MVID and logs
that reuse is conservative. These fingerprints never invoke analyzed jobs or field
initializers. The optional implementation stamp is persisted in v2 records; older
records without it are misses. The first export after this change rebuilds summaries.
This is one isolated analyzer partition, not minimal invalidation for every analyzer.
Unity EditMode evidence (2026-10-05, 13:26 MSK): `Tests_ILIncrementalAnalysis`
passed 16/16, including partition isolation, nested helpers, compact persistence and
dependency propagation. The first export with the new implementation stamp completed
in 70.91 s Runtime + 42.85 s Editor; declaration/analyzer indexing took 2.97 s and
the v2 cache contained 35,114 summaries / 25,850 method versions (75,610,700 bytes).
This was the expected cold rebuild, not evidence of warm reuse after an isolated
analyzer edit. No conservative-fingerprint warning or repeated export appeared
between that consumer reload and the test run. Warm reuse, live graph/config/target
refresh including restart, and player Burst/IL2CPP/stripping still need verification.

Live Editor verification later on 2026-10-05 confirmed that saving a disabled node in
the nested SpawnUnits graph, without Compile/Run, changed root 1011's compiled Update
plan from `invoke` to `pass`; the test edit was then restored. Play reached the main menu
and Map_Zones_Square simulation (entities, timer and respawn), and exited without BECS
exceptions. A network timeout switched that session to local simulation, so this is not
network synchronization evidence. Repeated Play exposed the lazy-library fingerprint bug
described above. Player builds remain the build machine's responsibility.
After the compiler-reference identity fix, Unity compiled successfully and two successive
Play/Stop cycles reached the main menu (14:21–14:24 MSK), with no new stale-input warning,
identity change, export or BECS exception. Domain/scene reload were disabled in the
project's existing Play settings; these settings were not changed. Two regression tests
cover lazy-loaded assembly exclusion and canonical precompiled reference selection.
Both subsequently passed as part of `Tests_SourceGeneratorGraphRefresh` (24/24,
2026-10-05, 14:49 MSK) after UI control recovered.
Config/target changes, Editor restart and player execution are not claimed by this check.

### Historical assembly-scoped compiler input experiment (superseded)

The following measurements describe the earlier implementation, not the current
storage contract. Project data beside the framework DLL and project-derived owner
response blocks have since been removed. Production now uses project-owned native
additional files as described above; native input takes precedence over stale scoped
snapshots during migration. Do not restore framework-local paths to reproduce these
timings. The compilation breadth remains an optimization task.

The Editor publishes `RuntimeInputs.becs-inputs` and `EditorInputs.becs-inputs` beside the analyzer when it is
installed under Assets. For a read-only package installation, these project-owned
data files instead live under `Assets/ME.BECS.SourceInputs`. They contain ordered
asset/IL data, not C# or asmdefs; relocating the analyzer does not rebuild its DLL.
Each consumer's response file selects its input and carries an unused
`ME_BECS_INPUT_<SHA256>` define derived from the full contents. Thus changing only a
graph changes actual compiler arguments, even when Unity does not track a custom
additional file as a dependency. Unchanged exports do not rewrite inputs or response
files. No `.Dummy.cs` update or accumulating hash-named data files are needed.
Compiler metadata separately records that the current data and its fingerprint
argument reached Roslyn together; the input schema and deterministic ordering are unchanged.

The native `.additionalfile` experiment was rejected after live Unity measurement:
39 transport/refresh tests passed, but a graph-only change caused 239 build steps
and 19.43 s compilation, plus 12.68 s domain reload. Native files reach all assemblies
using the analyzer, even when generators filter them before reading. The local
experiment's files were moved to scoped names, preserving their data and .meta GUIDs.
Unity retained old compiler paths after MoveAsset (CS2001); a temporary marker import,
successful domain reload, and marker deletion/import cleared them. The experimental
native publisher/mover is not part of the production export path.
Scoped inputs take precedence during transition, including on invalid input (no
silent fallback to an old native snapshot). Legacy input reading remains for reports.

The aggregate asmdefs/bootstrap/graph bodies still exist. This decouples data storage
and invalidation from generated C#, but is not completion of folder retirement.

Unity verification (2026-10-05, 16:07–16:12 MSK): toggling one SpatialQuerySystem
node in the nested TreesGraph, without changing C# or the analyzer, automatically
exported and compiled both profiles. Compilation took 5.53 s / 19 build steps;
restoring the node took 5.64 s / 19 steps (native experiment: 19.43 s / 239 steps).
This compares compilation only: cached exports still took about 14–15 s combined,
and domain reload remains separate. Both restored input files matched their original
SHA-256 exactly, and TreesGraph matched the pre-check backup byte-for-byte.
No repeated export loop occurred after compilation or tests.

`Tests_SourceGeneratorInputTransport` passed 16/16 on both the modified and restored
graphs, including compiled metadata proving that current data and its matching
compiler fingerprint were consumed. `Tests_SourceGeneratorGraphRefresh` passed 24/24;
bootstrap/component/system ownership passed 31/11/8; entity limits passed 6.
Ten optional source-analysis comparisons were ignored (8 selected by the ownership
filter and 2 entity-count tests). Thus the final restored-state checks contain
96 passes, 10 ignored, zero failures. XML results are in
`Temp/ME.BECS.SourceGenerator/ScopedTransport*.Tests.xml`. Play reached the main menu
and Stop returned to EditMode without errors or stale-input warnings. This does not
prove player AOT/IL2CPP/stripping or a complete gameplay session.

Analyzer build: ARM64 dotnet Release, zero warnings/errors, installed DLL updated.
The initial export after a code/tool change is still expensive (latest small Editor
change: Runtime 82.08 s, Editor 49.34 s). Scoped delivery removes the extra global
recompile; it does not solve that separate analysis/export invalidation cost.

### Job execution backends

`JobBackendGenerator` emits all 72 formerly template-generated implementations:
components and aspects (1–9 arguments), both deprecated parallel-for families
(1–9 arguments), and the 36 mixed aspect/component combinations (1–9 aspects,
1–4 components). Checked-in files under `Runtime/Jobs/Generated/` retain the
interfaces and public partial method declarations: other generators
must bind Schedule/EarlyInit signatures in the same compilation, before generated
implementations become visible. Defaults, constraints and declaration attributes
remain on that public API. The compiled API and scheduling/execution bodies are unchanged.

The five implementation templates `Runtime/Jobs/Generated/*.Tpl.txt` are
embedded in the analyzer DLL. Editing them requires rebuilding/installing that DLL;
normal Unity compilation needs no template files or Editor code-generation run.
Output is restricted to the assembly declaring the partial runtime owner, respects
its flat-query/collection-check defines, and is not repeated in consumer assemblies.
Ownership is checked per arity, so an old full implementation is not emitted twice.
The internal Generate Jobs menu and its Editor C# file writer were removed.
The hand-written zero-component implementation is unchanged; it was never emitted
by that tool. Public partial declarations are intentional compiler inputs, not an
Editor-generation dependency. Manual input recovery and graph input refresh
remain separate from this retirement.

### Optional behavioral source diagnostics

Normal compilation no longer walks method bodies or constructor initializers to
build source behavior summaries. `MethodSummaryGenerator`, the destroy-registry
syntax audit, and manifest-level destroy callback analysis run only when
`BECS_SOURCE_ANALYSIS_DIAGNOSTICS` is defined. The syntax predicates reject work
before requesting semantic operations/CFGs; disabled output also skips transitive
summary and callback analysis. Type catalogs, registration, generic specialization,
graph/injection emission, callback wrappers and AOT remain enabled.

The legacy graph C# emitter (`SystemsCodeGenerator.AddGraph/GetSystemGraph` and
its code-string helpers) has been removed. It was reachable only from the old
lifecycle comparison report, not active export. `SystemsCodeGenerator` now supplies
retirement filenames and a compatibility systems-count query only. Compiler graph
planning/emission, IDs, generic ordering and parallel-mode handling are unchanged.
`Inspect Graph Lifecycle Calls` replaces the legacy comparison menu: it displays
compiled call groups, symbolic dependency events and the final handle, with current
topology availability. Cached sync hints do not make that topology stale. The report
keeps the historical `GraphLifecycleComparison.txt` filename but explicitly says it
is inspection, not legacy equivalence or runtime/Burst/stripping verification. It
never gates export; `Export Compiled Lifecycle Plans` remains available as well.

For explicit source-vs-IL investigations, enable this scripting define across all
participating BECS/project assemblies, not just the test asmdef. Source-oracle test
readers explicitly skip when it is off; IL, bootstrap and registration tests still
run. With the define on, missing/malformed summaries continue to fail those tests
normally. Source comparison menu commands explain the disabled mode rather than
presenting absent diagnostics as an export failure. The production IL hints remain
available without this define.

Upgrade note: active inputs should be exported by the current IL-only Editor
exporter. Editor-only recovery permits it to load when old inputs still demand
removed source behavior catalogs; no temporary diagnostics define is needed. Do not
clear working manifests to recover from stale inputs. The compiler still reads old
formats for this transition.

System exports now carry `S\toperations\til` and `S\tsynchronization\til`.
The existing IL pass supplies both component operations and advisory messages;
neither the Editor selector nor the compiler reads source lifecycle catalogs for
these snapshots. An empty warning list remains empty even if old source metadata
exists. Generic dependency aliases still aggregate all selected closed members;
warning messages never affect those edges. The IL Complete() heuristic remains a
review hint, not a proof or a requirement to change user code. Explicit source
comparison reports and old source/legacy manifest formats are still supported.

The legacy script import index is no longer maintained. `ScriptsImporter` is a
read-only compatibility reader for the deferred CopyFrom investigation, not an
AssetPostprocessor: importing scripts no longer regex-parses their declarations,
rewrites `ImportCache.cache`, or queues asset paths in EditorPrefs. Normal input
export never uses this index or persistent legacy code caches. The import-cache
toggle and Run and Clean menu items were retired; existing cache files/preferences
are left untouched. The public `cleanCache` argument now forces recomputation of the
new compiled-input cache, without deleting legacy files. The unused old `Destroy`
helper was removed as well. Manual Rebuild and graph Compile provide explicit retries.

Job weights now export as `job-weight ... il <uint>`. The Editor computes the
static IL heuristic once per selected job in the export pass; the compiler validates
the job/value and emits a direct `JobStaticInfo<T>.opsWeight` assignment. Source
weight catalogs cannot override or veto an explicit IL value. Old source/value
manifest records remain readable for migration and diagnostics only.

The heuristic keeps the existing API price table and once-per-closed-helper-body
policy (`CodeGeneratorIgnoreVisited` expands a wrapper per call, with an active-cycle
guard). Roots bind exact Execute interface slots; constructors contribute and ldftn
addresses do not. Work is bounded and arithmetic checked. This is not a dynamic cost
bound or a proof about arbitrary runtime callback targets; entity reservation and
safety analysis remain independent. The old weight analyzer/catalog readers are
retained only for comparison diagnostics.

The first switched domain is job debug safety (including its native-size layout
inputs): the Editor exports `job-debug` v2 with `S\til` and concrete dependency rows.
The compiler validates those types/modes without reading or overriding them with
source safety metadata. Missing, incomplete and corrupt source catalogs cannot block
this IL path; failures in the IL analyzer itself still abort export. Old source/legacy
payloads remain readable until inputs are regenerated. Execute roots use interface
maps; safety traversal includes constructor bodies and has a bounded method walk.
The unused Editor C# writer for DebugJobs/cache/native wrappers has been removed;
only the Roslyn emitter owns those bodies. Old output retirement names are retained
for upgrades, along with the independent IL analysis and diagnostic name contract.
The IL safety walk includes reachable static initializers on field access/calls and
exact closed `new T()` constructors. Each closed initializer is visited once without
running it; constructor overloads and type-identity-only arguments are not invoked.
Typed Burst SharedStatic factories remain audited leaves and do not initialize their
context types. CodeGeneratorIgnore suppresses bodies, not explicit SafetyCheck rows.
The shallow inspection mode remains shallow; system analysis separately preserves
initializer/body writes when projecting readonly query arguments.
`SharedStatic<Component>.Data` retains a conservative RW access even though the
getter implementation is a no-callback leaf; its raw void-pointer getter and
type-identity factories do not invent typed component accesses.
Disabled component/aspect argument roles use a null filter, not the real `TNull`
component type. The compiler narrowly normalizes the old duplicated C/A `TNull`
record shape so already exported inputs cannot prevent the fixed Editor from loading;
all remaining interface arguments and their order are still validated.

Entity counts now have an explicit `job-entity-il` v1 transport for fully covered
compiled bodies. Each helper call contributes independently; per-method CFG cycles
retain loop/filter/finally context without mutating cached instructions. Closed generic
methods and constrained value-type interface calls resolve to concrete bodies. Rows
carry reservation, inline sites and repeating sites separately. The compiler validates
limits, sums and all registered groups (including unbounded loop-only groups), emits
the initializer itself, and records `il` authority without consulting source catalogs.
Group IDs still come from the unchanged global entity-registration order.

The IL invocation graph is condensed into strongly connected components before
counting: allocation-free recursion contributes zero; creation reachable from a
recursive component is repeatable and uses the same per-Execute maximum as loops.
Every call site entering that component still contributes independently. Initializer
edges are solved separately for possible creation/unknown effects, so a .cctor calling
its own type's helper does not invent ordinary invocation recursion. Generic-instance
depth, method count, instruction count and CFG work remain bounded. Audited typed
SharedStatic storage factories do not execute context-type constructors; their
System.Type overloads are not treated as leaves because Type can be user-overridden.

Production entity-count selection is IL-only, including closed generic jobs.
Fully covered bodies use `job-entity-il`; otherwise the known-site compatibility
projection below uses `job-entity-fallback`. Fresh exports no longer select
`job-entity-initializer` or read source-count catalogs/initializer methods. Source
counts remain an explicit comparison oracle, not a gate or fallback for export.
The compiler still accepts the old source input formats so previously exported
manifests can load while the Editor upgrades.

This is not a claim of complete IL coverage: unsupported dispatch and unproven
type initialization remain gaps, not a proven per-Execute zero. The compatibility
snapshot is deliberately distinguished from covered IL. Traversal truncation or
arithmetic overflow cannot publish an arbitrary partial reservation. Improving
diagnostic coverage is independent of the generator migration.

Once the Editor selects `job-entity-fallback`, its compatibility IL snapshot is
authoritative too: the compiler validates the job, current maximum, entity types and
registered groups, then preserves its counts, allocation flag and group order. It does
not retry or replace that selection with a source catalog, even if one is complete or
duplicated. The existing v1 transport is retained to unblock already exported inputs
without requiring an Editor reload first. Selection metadata remains `legacy`, not
`il`, to distinguish this transitional analyzer from fully covered new IL analysis.

The compatibility projection now shares the IL call-site/SCC counter instead of
flattening and globally deduplicating helper bodies. Repeated helper calls,
constructors, closed methods and inline/loop contexts keep their contributions;
cached `Instruction.loopInfo` is never modified. Group indices still follow the
existing registration list, with checked count/loop conversion and no silent drop
of an unregistered group. In unresolved virtual calls it retains the declared
target's known body for compatibility, not a claim about the runtime override.
Unknown callbacks/type initialization remain coverage gaps: this path is still
labelled `legacy`, not complete `il`. Traversal truncation or arithmetic overflow
cannot publish an arbitrary partial reservation. Runtime reservation/maximum
guards are unchanged; advisory analysis never disables those execution checks.

Scheduled-job IL inventory now traverses constructor/initializer bodies, closed
`new T()` constructors, exception regions and constrained value-type interface
targets. Actual Unity scheduling APIs are terminal calls identified by exact types,
not by scanning all loaded assemblies. Address intrinsics and typed Burst storage
accesses have narrow no-callback contracts. Traversal is bounded and a failed root
cannot publish a partial job set. Loaded delegate addresses remain conservative
potential targets, not proof of invocation. Concrete formatting callbacks now use
bounded IL value flow (boxing, locals/branches, local object arrays and actual
ToString/IFormattable dispatch slots). Unknown runtime receivers remain unresolved;
this inventory is not a whole-program proof of arbitrary virtual dispatch.

Production graph job selection exports an explicit `v2\nil\n<ordered jobs>` snapshot.
The compiler validates closed unmanaged jobs and canonical order, then
generates injection code without consulting source scheduled-job catalogs. Empty
snapshots remain empty; corrupt, missing or different diagnostic source catalogs do
not replace them. Old source/legacy payloads stay readable for migration. Each
system's roots are collected transactionally before publishing the snapshot. The
catalog comparison is diagnostic only and does not assert invocation ordering.
Private helper jobs without injected fields remain in this inventory but emit no
type reference or patch. Jobs requiring injection must still be accessible; that
check belongs to patch planning, not discovery (including `QueryBuilder.DisposeJob`).

Production runtime type usage now also comes from compiled IL, alongside job
dependencies/counts/weights, graph job selection and view tracker dependencies.
Source behavior catalogs remain explicit comparison oracles, not production gates.
Synchronization analysis is advisory, not a migration completion gate; do not
expand it merely to eliminate coverage gaps. Do not switch entity reservations
back to the old globally-deduplicated call walk: that can underallocate deterministic IDs.
System operation unions and synchronization proofs must migrate independently.
Production system operations now export `S\toperations\til` with a complete
typed IL snapshot in the existing v2 transport. Roslyn validates its types/modes,
then derives graph edges and generic alias unions from that snapshot. Missing,
corrupt or different diagnostic operation catalogs cannot veto or replace it;
an empty IL snapshot stays empty. Older source/legacy inputs remain readable.
Editor dependency tables preserve resolvable inaccessible component types, including
closed generics with internal arguments. Accessible types use `typeof`; inaccessible
ones use their validated assembly-qualified identity with `Type.GetType(..., true)`.
No dependency is dropped, and this does not change runtime registrations or AOT.
This also applies when old source/legacy inputs select a current source catalog:
its component symbols need not be accessible from the consumer assembly, and the
selected table still uses indirect identities. Otherwise an old loaded exporter
could block compilation/domain reload before it can regenerate the new IL inputs.
Synchronization selection is unchanged and independent: source proof when covered,
compatibility IL diagnostics otherwise. An IL operation failure never falls back
to a source union or publishes a partial plan.
Missing, incomplete or invalid synchronization catalogs cannot block bootstrap
generation. If source warnings are unavailable, their emitted origin is
`unavailable` (not a successful proof); explicit compatibility warnings are retained.
Warnings never alter component operations, graph edges, job scheduling or entity
reservations. Messages invite review rather than requiring users to add/remove
`Complete()`, and the graph presents them as advisory warnings.
The latest isolated operation audit compared 267 complete compiled source roots
without executing lifecycle bodies: zero missing accesses, differences or failures
(230 roots lacked a complete source oracle). The previously found 13 access gaps
are covered by SharedStatic refs, exception-filter mutations and exact typed native
memory/list contracts. Native contracts add effects without suppressing allocator,
key callback or argument traversal; layout/scalar calls and local copies add no
component access. Unknown runtime dispatch and arbitrary raw pointer provenance
remain limitations, not a whole-program completeness certificate. Neither source
parity nor a successful traversal proves JobHandle synchronization.
This step was checked with 252 isolated IL/query/selection regressions, 57 IL
safety/count/weight checks and 355 compiler dependency transport checks. Current
Editor exporter code also fed seven real compiled fixture systems (including a
closed generic specialization and its alias) into the compiler with every operation
snapshot preserved. These are metadata/in-memory checks, not Unity integration or
Burst/player validation; the analyzer DLL alone was rebuilt and installed.

The independent `ILSynchronization` analyzer is now available through
`ME.BECS/Source Generator/Compare IL Synchronization`. This explicit diagnostic
writes `Temp/ME.BECS.SourceGenerator/ILSynchronization.txt`; it is not invoked by
normal export and does not change production synchronization selection. It binds
closed lifecycle roots (including generic specializations), reports source/IL
coverage separately, and retains gaps and unproven access sites. Cancellation
publishes a clearly partial report, never a complete coverage claim.

The initial IL interpreter tracks incoming work, handle copies, helper calls,
ref/out aliases, SystemContext dependency updates, Query/WaitForAllJobs, audited
CombineDependencies/CompleteAll overloads and repeated scheduling generations.
A handle from an earlier loop iteration cannot complete a later submission at
the same IL site. Conditional joins retain must-coverage only on paths where the
work is outstanding. IsCompleted and ScheduleBatchedJobs do not complete work.
User SafetyCheck annotations do not suppress traversal of their bodies.

Constructor bodies now use the same ordered IL flow: newobj, value-type/base/this
constructor calls and closed Activator.CreateInstance<T> preserve already-evaluated
arguments, ref aliases and scheduled work. No constructor is executed. Only the
actual parameterless System.Object constructor is an empty base contract; a user
constructor is never treated as empty by spelling or signature. Ordinary default
value types have no implicit user body, while a resolved user constructor is
expanded. A throwing constructor has no normal continuation. Normal branch/loop
flow retains repeated constructor calls and field/event/property initializer order.
Primitive fields on ordinary managed objects cannot carry component/handle
storage; this does not bless borrowed component fields or mixed ref aliases.

Finally/fault routes now have separate continuation identities for normal leaves,
returns and exceptional exits. Inner cleanup runs before outer cleanup; a cleanup
exception cancels the saved normal continuation. Helper/constructor exceptional
outcomes retain preceding handle/ref mutations and newly scheduled work. Native
terminal calls expose both pre-effect and post-effect failure states, so a failing
Complete cannot certify an outer cleanup's access, nor can a failed Schedule hide
work before its handle is stored. Generic using/Dispose, nested cleanup, return
snapshots and break/continue are exercised without executing those bodies. Fault
is tested using emitted CLI metadata and is never run on a normal leave.

Ordinary catches now receive exceptional flow after inner finally/fault cleanup.
Handlers are selected in stable EH-table order; sibling catches do not share
effects and throws/rethrows in a handler search its enclosing regions. Matching
is conservative, retaining an escaping alternative unless an object catch covers
every possible CLI throw. Catch entry receives the exception stack value. Active
cleanup parents are distinct from final transfer destinations: a local catch inside
finally preserves its suspended return/exception, including when an inner cleanup
throws during a route that would otherwise escape that finally. An escaping throw
cancels that continuation.

Filters use a two-pass protocol, including across helper/constructor calls. The
first pass evaluates candidate filters before any original-exception cleanup and
carries their local/ref mutations to subsequent candidates. The chosen destination
then travels through finally/fault unwinding without rerunning the filter; a new
exception during cleanup starts a new search. Exceptions from a filter's helper
unwind that helper to a filter-rejection boundary, retaining partial effects rather
than replacing the original search. Filters inside active cleanup preserve its
suspended return/exception. Separate scalar filter alternatives retain true/false
outcomes across IL join temporaries; unknown type matches remain conservative.
Search/copy work and filter depth have deterministic bounds as well as normal IL
flow. Exhaustion or an opaque callback yields incomplete coverage, never a proof.

ScheduleByRef reads its job argument synchronously: a component-backed or otherwise
unknown ref is an access at scheduling time, even when a dependency is supplied.
Known local storage propagated through ref helpers is distinguished from borrowed
addresses. Root ref parameters remain external; copying an opaque address into a
local does not make its target local. This concerns direct-access ordering, not
correct publication of dependencies or all conflicts between scheduled jobs.

Unknown dispatch/delegates, tracked fields,
arrays, unknown pointer/ref storage, replaceable destroy callbacks and non-inert static initializers currently
invalidate the proof. Metadata inspection never executes those initializers.
An ldobj remains a memory read even when its value is discarded; returning an
unknown ref does not certify a safe access. Instruction/call/storage and total
flow-state work limits are deterministic and produce incomplete coverage, not
a partial proof. Retained completed-handle certificates cannot cover future
generations. Arbitrary memory provenance and remaining call/storage contracts still
need coverage before production authority can migrate.

IL synchronization now recognizes the exact audited Burst SharedStatic factories
and generic type-hash overloads without expanding native allocation or context
initializers. Reflective overloads require a runtime type-identity fact originating
at `ldtoken`/`Type.GetTypeFromHandle`; local/helper transport and joins of known
identities preserve it, but an unknown Type path loses it. User method names are
not contracts, and all call arguments retain their preceding effects.
`SharedStatic<Component>.Data` carries borrowed storage through ref helpers and
field addresses. A subsequent read/write after newly scheduled work is still an
access, even when the getter ran after an earlier Complete. Possible component
storage is a MAY fact at ref joins; fully known storage is a separate MUST fact.
Copying the data does not copy that address provenance. Tracked handles from
shared storage, raw pointer dereferences and unresolved scalar storage remain
incomplete. Scalar indirect opcodes consult storage provenance but discard
unconverted scalar constants/type/handle certificates; truncation/sign extension
must not select a false completion branch. Memory-failure paths still participate
in filter search and cleanup. These contracts only extend the diagnostic IL proof;
production synchronization selection has not changed.

The diagnostic IL interpreter also recognizes exact typed UnsafeUtility reference
and copy contracts. Known same-type local aliases preserve handle coverage;
unknown addresses, reinterpretation and raw memory operations do not certify it.
The isolated IL/query suite passed 500 checks after these contracts. This remains
an optional diagnostic; improving its coverage is not required for migration.

The compiled-root audit after filter and shared-storage support examined 497 lifecycle implementations from the
test/gameplay assemblies and their published generic specializations: 165 had
complete IL analysis, 332 remained incomplete. Of 161 roots complete in both
analyzers, one differed: SchedulingFinallySystem passes a proven caller-local job
by ref to ScheduleByRef inside finally. Its compiled IL has no direct component
access; the source oracle conservatively treats that generic ref as unknown
component storage. The difference remains visible in the report and has an IL
regression; production selection was not switched. The initial run exposed false proofs for
opaque ref returns, discarded pointer loads and hidden destroy dispatch; all now
have regressions and fail closed. SetOneShot/SetTag forwarding cannot hide a
destroy callback either. The four IL-only covered roots were reviewed separately:
deferred scheduling (including filter/catch/finally) does not execute the job body
inline, FinallyFiltersSystem accesses a component before completing its input, and
a readonly context receiver does not update the original context's dependency. These counts are
coverage evidence, not a whole-project correctness
claim. The standalone suite passed 465 IL/query/selection checks, including 213
synchronization/report cases. It checks report ordering, duplicate inputs,
cancellation and the exact lifecycle comparison without running Unity
jobs or lifecycle methods. Unity/Burst/player execution remains a separate gate.

Constrained IL calls share one metadata-only slot binder across safety, entity
counts, weights, scheduled-job discovery and query-mode flow. Closed value types
and sealed reference receivers bind exact interface/virtual implementations,
including inherited and generic slots; hidden `new` methods cannot replace an
override. Query-mode binding retains that receiver proof even when the method is
declared in a non-sealed base class. Open or non-sealed receivers and covariant
arrays are not devirtualized. Unknown dispatch still prevents a complete new IL
entity-count result; operation-dispatch reports are not completeness certificates.
Local single-cast delegate construction/invocation now has a bounded IL target
flow shared by counts, weights, scheduled-job inventory and operation diagnostics.
It follows local copies/ref aliases, branches, loops, casts and closed method
addresses. Each Invoke site contributes separately to entity reservations; possible
alternative targets form a conservative union. Merely loading an address, or later
overwriting the delegate, does not invoke it in counts/weights. Exception edges
include changes before throwing, and finally effects reach leave continuations.
Escaped local storage, unknown parameters/fields/returns, virtual addresses and
multicast lists remain incomplete, never a proven zero. This binds target methods,
not receiver/argument values: query modes still use unbound callback contexts, and
the broader safety/operation inventory still retains possible method-address roots.
The IL dependency fallback now binds AsReadonly to actual query storage, tracking
copies, ref aliases, defensive copies, branches/loops and conservative exception
edges. A scheduler is terminal; its selected job supplies the access contract.
Filter effects also reach later sibling/outer handler search and protected cleanup:
first-pass filter execution can mutate a query before finally/fault runs, and a
false or throwing filter does not roll back those mutations. This is a conservative
mode union, not an exception-order or JobHandle completion proof.
Unknown ref/pointer writes invalidate readonly facts, and a possibly-readonly
WriteOnly argument contributes both read and write access. The fallback now collects
the closed method inventory before projecting schedules, then binds helper parameters,
scalar boolean flags, return values and ref/out storage across direct calls. Shared
addresses preserve aliases; reassigning a callee's ref parameter does not rebind the
caller's variable. Normal exits and conservative exceptional mutation effects are
separate, so a helper that resets a query before throwing cannot leave a readonly-only
fact in the caller's catch. Method-address/unknown virtual entries receive unbound
contexts even when another direct call was already analyzed. All call contexts merge
before component modes are consumed; closed generic methods remain distinct.

Expansion is bounded; recursion/unsupported flow invalidates precise facts for the
whole analyzed inventory instead of publishing a partial readonly result. Captured
fields, unresolved dispatch and arbitrary memory remain conservative. Independent job
body accesses are unioned after query argument projection: compatibility safety merging
can otherwise erase their origin when the same component is also an Execute argument.
Presence now has an explicit run-local IL contract too. Dynamic/static builder and
compose filters bind exact owner/signature/arity, including WithAll and every WithAny
argument (but not the TNull sentinel). Aspect filters inspect private imported instance
QueryWith fields, deduplicate components and exclude optional/static fields. An empty
aspect is a valid terminal, not a reason to traverse query internals. Static query APIs
are discovered from the referenced engine assembly only when available in the profile.

Scheduling adds presence reads from the actual scheduler's closed work-interface
constraint rather than all interfaces implemented by its job. This preserves reads
for unused component/aspect arguments and combines ordinary WO with presence RO into
RW. A known low-level scheduler's CodeGeneratorIgnore marks its internals, not an
absence of scheduled job effects. Direct SafetyCheck calls are terminals consistent
with the job IL analyzer, so allocator/validation internals do not pollute query mode
analysis. Typed caller/body effects and query presence are combined only at the final
operation union; the job's original WO/RO contract remains unchanged.

Scheduled body access now uses the same exact work-interface binding as presence.
The scheduler's generic constraints are closed with its actual type arguments, and
the interface map selects one Execute slot (including explicit implementations and
the imported in/modreq adapter). Access caches are keyed by job plus closed contract,
so a job scheduled through two different interfaces cannot reuse the first one's
body or argument annotations. Global job safety, statistics and registration still
use all implemented work contracts. This does not expand the runtime's existing
one-debug-wrapper-per-job-family registration support; deliberately multi-contract
IL fixtures stay private and are never scheduled or registered.

Unity IJob/IJobFor/parallel/batch/defer and BECS query/command-buffer/single-job
contracts are bound without executing their producers. ScheduleSingle/ByRef and
the compatibility ScheduleParallelFor/Batch aliases are now scheduling terminals
as well. An open, unsupported or ambiguous binding fails explicitly, not as empty
dependencies. Metadata-only regressions cover dual Unity/BECS jobs, distinct closed
component interfaces, nested generic jobs, RO/WO forwarders, contract-cache ordering,
and unchanged global safety selection. Binding coverage includes the closed generic
systems from current non-generated assembly catalogs; this is not Unity/Burst or
whole-program call-graph validation.

The system IL body inventory now uses MethodBase, so constructor chains, instance
field initializers, static initializers and closed `new T()` construction can
contribute direct accesses and scheduled jobs. Initializers are possible effects,
not an assertion that they run on every call. The inventory is bounded by method,
depth and instruction limits; it does not publish a partial result on failure.
Known typed Burst storage and Unity address intrinsics stay terminals.

Constrained calls bind the actual closed value-type receiver from the IL prefix,
including explicit and generic interface slots. System query-mode flow and job
safety traversal share this binding; job safety no longer guesses the receiver
from the first generic argument of the containing type. Constructor value/ref
arguments are bound by the same flow engine as ordinary helpers. Ref mutations
affect caller storage, including mutations before exceptions; value copies do not.

Unbound inventory entries with no schedules propagate unknown contexts to their
callees rather than interpreting unrelated static setup. Already-bound callees
must also receive those additional contexts, so a static initializer or escaped
helper cannot leave only the first caller's readonly facts. This is conservative:
an initializer-only helper can lose precision, but cannot silently disappear.
Any failure while actually interpreting a schedule-bearing context still drops
precise modes for the whole inventory. Regression checks cover constructor/ref/
exception flow, initializer/shared-helper unions and different closed generic
receiver/constructor instances. An IL-only inventory audit of 126 lifecycle roots
from 103 current gameplay/registered generic systems completed without traversal
errors; it does not establish dependency parity or complete dispatch coverage.

The existing positive formatting-callback inventory is now consumed by system
dependency and job safety analysis too, not only scheduled-job discovery. Known
boxed/local/array values select the actual ToString or IFormattable slot; direct
callback accesses and schedules inside callbacks contribute to the union. Every
system body collects its own direct accesses, including callbacks with no literal
incoming call instruction. Independent callback writes remain writes even when the
enclosing job's query uses AsReadonly. Formatting calls are query-flow terminals
with conservative caller-storage invalidation, not a claim that user callbacks are
pure; callback bodies receive separate unbound contexts.

The dependency section of Compare Scheduled Jobs now lists unresolved formatting,
virtual, delegate, missing-target and indirect call sites, even when the known
dependency sets agree or the source union is unavailable. Diagnostics include the
closed body identity and IL offset when available, with stable ordering. Collecting
a local delegate address is only a possible target and does not certify its runtime
invocation list. Conversely, a proven constrained value-type slot is not reported as
an unknown virtual target. These diagnostics are counterexamples, NOT a coverage
certificate: zero issues still does not prove all native/library/aliasing effects.
The report does not change production authority or replace synchronization proof.

These changes support the production IL operation snapshot, not a proof of full
system operation coverage or synchronization. Keep auditing callback dispatch,
unknown virtual targets and unsupported paths against independent source reports.
Job-body initialization/construction, native typed access and filter mutations have
focused regressions; operation and synchronization authority remain separate.
Finding Complete near the start of a root still does not prove that the right
handle was completed on every path before access. Do not switch production away
from the current source flow analysis until the IL replacement also preserves handle
copies/rescheduling and exception/branch ordering for synchronization.
The recent module/config/destroy source-usage work is retained, not the next expansion
target. Keep existing graph freshness guards and compile only the analyzer DLL here;
Unity integration and Burst/player validation are separate. CopyFrom and the unified
Schedule API remain follow-ups. Historical source-only retirement notes below are
superseded by this architecture and do not describe the current migration target.

If Editor manifest generation fails, the exporter preserves the previous consumer
asmdef/response file rather than publishing partial references. A subsequent missing
manifest diagnostic points back to the first export exception; it does not emit a
dummy bootstrap or treat failed analysis as an empty registration set.
Missing Editor inputs use warning BECSG104 so the repaired Editor exporter can reload
and recover. No bootstrap or graph snapshot is emitted in this state; play/build
freshness guards still reject it. Malformed supplied inputs and missing Runtime
inputs remain BECSG100 errors. Re-export before running worlds/integration tests.

## Graph-editing integration gate

Graph editing must continue to regenerate graph code without the legacy Run menu.
Source generators do not observe Unity graph assets by themselves: the Editor must
export refreshed graph inputs and let Unity recompile the consumer assembly.
Automatic refresh and the replacement Rebuild Inputs recovery menu are implemented;
the graph window's Compile action is retained. Their live integration verification
below is still required before claiming migration completion.

The replacement must cover saved topology/system changes, system configuration,
enable/disable and parallel-mode changes, nested graph edits (including affected
parents), Undo/Redo followed by save, and asset import/move/delete. It must retain
graph IDs, coalesce repeated edits, write only changed inputs, defer work during
compilation/import, and avoid a refresh/recompile loop. Cosmetic changes should
not rebuild an unchanged semantic plan. Play/player builds must not silently use
a stale graph plan after failed input generation.

Current integration points: the core Editor `SourceGeneratorInputRefresh` service
owns reload, asset save/import/move/delete and build-target-change requests. It no
longer lives in the FeaturesGraph window/assembly. The window's Save requests a
coalesced refresh; Compile uses its explicit cached retry entry point and Rebuild
Inputs requests full analysis.
`OnGraphChanged` marks topology changes dirty. `SystemsGraphPostProcessor` maintains graph IDs.
Preserve a visible retry/error path when replacing these hooks. Verification must
include changing an inner graph and seeing the owning root's generated lifecycle
and injection plans change, without invoking the removed legacy Run command.

### Compiled graph freshness (Unity verification still required)

`graph-input-snapshot` exports a canonical SHA-256 stamp of semantic root/nested graph
content, the component/aspect type set from EntityConfig assets, the exported theme
selection, active target, and loaded script assembly identities/MVIDs. Config values,
graph layout, and cached sync analysis remain excluded. Generated consumer assemblies are excluded from
the script fingerprint, so compiling an input cannot invalidate its own snapshot.
This stamp is diagnostic metadata, not an entity/component ID or simulation state.
The compiler emits `ME.BECS.GraphInputSnapshot.v1` only with valid executable inputs;
old manifests remain readable, but lack the evidence required by the new launch guard.

Play/player guards require matching compiled Runtime **and** Editor snapshots. An export
success flag, stale assembly from before a failed compile, or cleared SessionState after
restart does not prove freshness. Missing/duplicate/mismatched metadata fails closed.
Player preprocessing no longer exports/imports files during the build, including batch
mode: generate and compile inputs beforehand. No player/Burst execution is proven by
these metadata checks. This tracks graph/code/config-type/theme freshness, not every
unrelated addon asset.

The core `Builder` owns both pending/failed-export and compiled-snapshot preflight;
the duplicate Features editor build guard was removed. Existing SessionState keys
are preserved across the ownership move. Semantic refresh remains independent of
the old auto-codegen preference, and the manual retry menu remains available.

Reload requests are deferred until imports/compilation settle and are coalesced with
asset saves/imports/deletions/moves. Config and stylesheet changes, including moved
theme folders, request a comparison; generated asset paths are excluded. An unchanged
effective snapshot (for example, editing only config values) produces no export.
Already-current compiled inputs produce no writes. A successful export
records its captured snapshot, not a new fingerprint taken afterwards, and retains
requests raised during export. Both input preparation and the paired Runtime/Editor
export reject relevant assets/scripts changing midway. Failed export is retried on a new request
or manual Compile, not every frame. The replacement recovery menu remains available;
the old auto-codegen preference does not disable graph safety. Implemented hooks and
metadata-only tests do not substitute for verification of these workflows in Unity.

The historical status notes below describe earlier migration stages, not proof
that the complete migration or this graph-editing gate has passed.

Status: prototype DLL built in Release and copied to `../SourceGenerator` with RoslynAnalyzer import metadata.
Unity loading has not been verified. The existing generator remains authoritative for discovery and ordering.
The editor bootstrap emitter now uses individual generated registration methods when available and compatible,
otherwise it emits the legacy calls. It never invokes registration during discovery.
The library build succeeded with zero warnings and errors using .NET SDK 7.0.401.
No Unity compilation, generated-output comparison, or performance measurement has been run.

Build from the repository root:
```sh
dotnet build ExtClient/Assets/ME.BECS/.ME.BECS.SourceGenerator/ME.BECS.SourceGenerator.csproj -c Release --nologo -p:InstallUnityAnalyzer=true
```
Output: `bin/Release/netstandard2.0/ME.BECS.SourceGenerator.dll` relative to this directory.
The opt-in install target copies only this DLL to the Unity analyzer directory, without Roslyn dependencies.
That directory has no enclosing asmdef, so analyzer scope is not restricted to the ME.BECS runtime asmdef.

The project targets netstandard2.0 and Microsoft.CodeAnalysis.CSharp 4.3.1, matching the compiler
dependency version in Unity 6000.2.14f1's local csc.deps.json. Unity analyzer loading must still be verified.

## Current scope

DestroyInputEmitter owns destroy callback bodies, Burst/MonoPInvokeCallback attributes and ordered
registration in the target input assembly. The Editor selector and manifest exporter share one type list,
ordered ordinally by full type name and assembly identity (not the machine's culture). Declarative feeder
dispatch calls DestroyInputs.Initialize at the original callback-registry position.
The old body/cache fallback and redundant per-component-assembly ComponentDestroyGenerator were removed.
Every selected type is checked for a closed accessible unmanaged IComponentDestroy implementation before
emission; invalid selection is an explicit diagnostic rather than missing registration. The mandatory
destroy-schema/v1 marker distinguishes a valid empty selection from stale manifests: regenerate inputs
after this upgrade. Component source assemblies no longer need allowUnsafe merely to own a callback.
The comparison report now checks target-manifest records and callback signatures without invoking them;
unavailable callbacks are issues, not claimed legacy fallbacks. Unity/Burst execution remains unverified.
Destroy callbacks dispatch through a private constrained generic helper, so explicit
IComponentDestroy.Destroy implementations are supported. A non-null component is passed by ref from
its pointer (no boxing or live-value copy); a null pointer uses a local default value as before.
The actual AOT/Burst callback remains non-generic and names a concrete component specialization.

## Full migration target

Compiler transport is started by `InputManifestGenerator`: an AdditionalText ending in `.becs-inputs`
has header `ME.BECS.TypeInputs.v3<TAB>base64(target assembly name)<TAB>editor|runtime` and records
`kind<TAB>ordinal<TAB>base64(assembly-qualified type name)`. Kinds are system, system-registration, component,
component-group, job, entity, entity-registration, aspect, aspect-registration and aspect-construction. Ordinals are contiguous per kind, starting at zero;
the original supplied order is preserved, not re-sorted into new runtime IDs. Duplicate kinds/types,
noncanonical encodings, malformed records and multiple inputs for one target are errors.
The final `end<TAB>record count<TAB>SHA256` line is mandatory, followed by a newline. The hash covers
the header and all records with LF line endings (including their final newline); CRLF input is normalized
before validation. Truncation, missing footer and payload/hash mismatch fail compilation instead of
emitting an incomplete initialization phase. Regenerate older v1/v2 manifests with the current exporter.
The additional `component-registration` kind has a fourth field: canonical decimal flags (1=tag,
2=static, 4=Default property, 8=shared, 16=custom shared hash, 32=config initializer).
Tag+Default and custom hash without shared are invalid. Static/shared/config flags must match the
resolved symbol interfaces, otherwise the manifest is rejected. Tag classification temporarily uses the existing
Editor Marshal.SizeOf/field test; Roslyn does not guess runtime layout from source fields.
`group-registration` also has a fourth field: the base64 assembly-qualified group type. Editor exports
the actual ComponentGroupAttribute value; Roslyn resolves both identities and emits ordered ApplyGroup
calls in GroupInputs.Initialize at the original pre-component registration phase. Group IDs still come
from first occurrence in this explicit order, not Roslyn symbol enumeration. Open generic group Type
values are emitted as unbound typeof expressions; component types must remain closed unmanaged types.
Runtime-only `graph-registration` records contain ordinal, base64 qualified callback-class prefix,
signed graph ID and nonnegative pointer-slot capacity. They preserve AssetDatabase discovery order and omit inner graphs. Roslyn emits the
seven existing SystemsStatic registration calls in GraphInputs.Register and owns its runtime load hook:
BeforeSplashScreen registers that preserved method with CustomModules.RegisterFirstPass. Editor-profile
manifests do not emit a runtime hook. SystemsCodeGenerator no longer emits SystemsLoad/Initialize;
regenerate its output together with the manifest when updating to avoid retaining an old hook.
Duplicate IDs/prefixes and Int32.MinValue callback IDs
are rejected. Roslyn also emits a partial initialization class containing the SharedStatic pointer array,
its ref accessor, dispose/create helper and GetSystem callback. The key class and member names remain
unchanged. Capacity still uses Editor's existing generic expansion and counts repeated nested graph
occurrences separately; recursion cycles, missing nested graphs and count overflow now fail export.
`graph-system` records contain ordinal, base64 closed system type, signed root graph ID, slot index and
default-mode (0/1), registry source ID (uint) and source node index (nonnegative int). Default slots
require both source fields to be zero; ordinary slots require a nonzero source ID.
Slots must reference a preceding graph, be contiguous and exactly fill its capacity;
types must resolve to accessible closed unmanaged ISystem implementations. Roslyn now emits each
allocation, value assignment, TSystemGraph registration and pointer-slot assignment in GraphInitialize.
Ordinary values are read after allocation directly by Roslyn-generated code using the registry source
ID and source node index; generic variants remain default-initialized. Normal codegen registers graph
references while preparing the manifest. The Temp export menu only looks up existing references and
fails if a required graph is not registered. Neither path invokes system constructors. Injections,
lifecycle bodies and dependency/generic-parallel scheduling still need migration.
Graph capacity and transitional creation now share GetGraphSystems: an ordered list of closed system
type, original graph/node and default-initialization mode. It preserves repeated nested occurrences
and rejects cycles. This removes independent count/creation generic expansion. Roslyn now owns the
GraphInitialize callback, its MonoPInvokeCallback attribute, allocator selection and ResetStorage call.
After all slots are initialized it calls the remaining Editor-generated ApplyInjections helper. The
transitional Editor no longer emits creation calls. Regenerate inputs and its partial class together.
Public writable InjectSystem<T> fields now use typed Roslyn injection helpers. `system-injection`
records carry owner assembly-qualified type and base64 field name; multiple fields on one owner are
distinct records. Closed generic owners/targets are resolved by compiler symbols. Editor retains the
graph's first-instance target lookup, field iteration order and existing bool-containing-system guard,
then calls the generated helper. Non-public writable InjectSystem<T> fields in public
partial systems (with a fully partial containing-type chain and unsafe enabled) now use generated
in-type setters instead of SetValueDirect. The bridge verifies the setter signature and
CompilerGenerated marker without invoking it; field order, first target selection and bool guard
remain unchanged. Generic partial systems and generic containing types share these setters; the
bridge resolves the exact ref-owner signature on the closed specialization and formats nested generic
arguments without collapsing them to the first argument. Constraints remain on the original partial
declaration. Non-partial private fields, custom/readonly injection paths and mixed
injected job patching still need migration. The Features.Editor consumer uses a public core Editor naming API, avoiding
a reverse assembly dependency or access to internal naming helpers.
The same in-type setters now cover writable InjectSystem<T> fields in public partial jobs, including
generic owners. Graph-specific patch callbacks call them in their existing field order. The public
InjectSystem<T>.FromPointer factory avoids relying on internal constructors from arbitrary user
assemblies; it creates a non-owning link without allocating or changing the pointer. Graph job lookup,
callback registration, delta-time patching and the bool guard remain on the transitional Editor path.
Partial jobs also expose per-field InjectDeltaTime setters for uint, float and sfloat, including private
fields and generic jobs. Mixed graph callbacks select these setters by exact closed signature and
CompilerGenerated marker. Each call retains its original world delta-time read and SystemContext
conversion at the original position in field order. Full callback/AOT registration remains transitional;
these setters are ordinary static calls, not function-pointer callbacks on generic types.
Runtime manifests now include `job-delta-registration` records for closed generic jobs whose only
injections are public writable InjectDeltaTime fields. Records contain ordinal, base64 job AQN and
base64 comma-separated field names in reflection order. Graph lifecycle discovery supplies the jobs;
each exact system type is scanned once per export. Roslyn validates the complete field set and emits
non-generic static AOT/Burst callbacks plus registration methods in GenericJobDeltaInputs. Transitional
graph code calls those registration methods at the original positions and no longer emits their patch
bodies. No generic callback is passed to Burst. Non-public delta-time fields are also supported when
their partial job exposes the generated setter: both reflection export and compiler validation require
the exact closed ref-owner/ushort signature and CompilerGenerated marker. Callback emission invokes
that setter at the corresponding field position; unavailable setters fail selection/validation rather
than emitting illegal private access. Mixed generic callbacks still need migration. Regenerate runtime
inputs and graph glue together with the updated DLL.
`graph-job` records move graph-dependent job callbacks to Roslyn. They contain ordinal, base64 closed
job AQN, signed graph ID and base64 ordered comma-separated `field:slot` / `field:d` operations.
Currently selected jobs must have public writable standard InjectSystem<T>/InjectDeltaTime fields,
no bool field and every injected target present in the graph. Generic jobs are supported. Roslyn
validates complete field coverage, exact target types and first-occurrence slot selection, then emits
non-generic AOT/Burst patch methods and registration helpers inside the graph's partial initialization
class. The patch reads its graph's SharedStatic storage; no additional mutable pointer cache is added.
Editor calls registration in the existing per-system/job order, including repeated registrations.
Non-public standard injection fields are also supported when the partial job provides the generated
setter. Export and Roslyn validate its exact closed ref-owner signature, void return, non-generic
public static form and CompilerGenerated marker (void* for system links, ushort for delta time).
The callback invokes it in manifest field order. Non-partial private/custom fields remain transitional.
Discovery caches exact system-to-job sets per export and
reuses them across graphs without dropping a repeated graph's registrations. JobInject<T>'s existing
global last-registration behavior is unchanged; this is not a redesign of graph/world ownership.
`graph-system-injection` uses the graph-job field-plan format for system-to-system links. Roslyn
requires the owner in the graph, chooses its first slot, rejects delta-time operations and emits a
typed InjectSystem_<hash> method in the graph's partial initialization class. Target slot/type and
private setter validation are shared with job patches. Editor calls this method at the original
per-system position before that system's job registrations. Unsupported fields retain the transitional
path; this does not yet move the outer ApplyInjections ordering or discovery into Roslyn.

Current exports supersede that transitional field path with `graph-injection-schema`
v1 (the prior system-only schema remains readable). Roslyn derives every unique owner directly from graph-system slots in first graph occurrence order;
no redundant owner records or field/target choices are exported. Roslyn selects its instance injection
fields, binds closed generic targets to their first matching graph slots, and validates writable standard
InjectSystem fields and private partial setters. The existing bool-layout guard remains for systems
with injections; bool-only systems produce empty plans. System InjectDeltaTime fields are rejected
explicitly instead of being silently ignored (delta-time patching belongs to jobs).
Graph apply transport keeps an owner marker before that owner's job registrations. Empty system
markers emit neither a call nor a patch method, preserving actual runtime call order and avoiding no-op
initialization calls. Missing, duplicate, foreign or reordered system markers are rejected, including for
empty plans; schema mode cannot mix explicit per-owner plans. ME.BECS.GraphSystemInjectionPlan.v1 exposes the effective owner slot and ordered
field/target slots for read-only validation; it allocates nothing and executes no injection.
The unused graph-independent `system-injection` exporter/field walker and SystemInjectionInputs bodies
are removed. Old such records remain readable as inert transport metadata; stale field names cannot
affect initialization. Old explicit graph-system-injection plans remain validated for input compatibility.
For each first-occurrence graph owner, `graph-job-selection` transports ordinal, base64 owner AQN,
graph ID and a base64 selection payload. New `v2\nsource` payloads carry no captured job identities;
only explicitly incomplete selections use `v2\nlegacy\n` followed by newline-separated job AQNs.
Old unversioned job lists remain readable. Roslyn selects current scheduled jobs and system-link,
delta-time or empty job plans, validates partial setters and closed generic fields, and derives the
entire apply sequence. Graph callbacks are unique per graph/job; pure delta callbacks are shared across
graphs, while repeated registrations across owners and graphs remain in the original positions.
Missing/duplicate/reordered owners, duplicate or unresolved jobs, unsupported fields and mixed old/new
selection records are errors. Inaccessible jobs without injections emit no reference; inaccessible jobs
with injected fields are errors, not silently skipped. Empty/bool-only jobs need no patch. The bool
layout guard remains for patched jobs. GraphJobInjectionPlan.v1 and GraphInjectionActions.v1 assembly
metadata expose effective fields/kinds and the ordered registration sequence without executing it.
The exporter no longer emits graph-job, job-delta-registration or graph-apply decisions, and the loaded-DLL
injection preflight is removed. Independent reflection field checks remain only in Injection Coverage.
Complete scheduled-job discovery is compiler-owned. `CompilerScheduledJobs` reads all five current
lifecycle catalogs, including DrawGizmos and explicit implementations, from
`ME.BECS.SystemScheduledJobs.v1`. Ordinary owners use their defining assembly; closed generic
owners use their exact constructed identity across publishing assemblies. Root binding uses the
same source interface maps as compiler-owned system dependencies, including imported private or
Unity `in`/`modreq` methods. Wrong/duplicate lifecycle roots, malformed complete records and invalid
typed helper signatures fail closed. GetJobs/GetRoot are validated as symbols, never invoked.
Missing helpers (which older zero-gap summaries can legitimately omit) remain incomplete.
Diagnostic examples may repeat for distinct missing generic instantiations; their count, not
unique text count, must match the v1 truncated gap header.

A current complete catalog supersedes old or explicit fallback job lists. Source-only inputs
require complete coverage; incomplete domains retain only explicitly exported fallback. Partial
source sets never leak into injection plans. Effective jobs are deduplicated across lifecycle
phases and sorted ordinally by reflection FullName then assembly identity, preserving the old
per-owner registration order. `ME.BECS.GraphJobSelection.v1` exports graph ID, owner, source/legacy
origin and the effective ordered jobs. Injection/action tests consume this effective metadata,
not obsolete captured arrays. Graph owner order, repeated registrations, first target slots,
SystemGenericParallelMode and runtime lifecycle scheduling are unchanged.

The Editor no longer serializes complete job sets or makes their current injection decisions.
It still discovers graph owners/references and decides whether loaded catalogs have complete
coverage; missing/incomplete systems still use the transitional IL collector. Removing that
remaining fallback requires covering those roots, not certifying a partial set as empty.
All selected pure delta-time jobs now use job-delta-registration, including non-generic jobs. The
transitional registration selector no longer invokes per-assembly GetFields metadata getters or
chooses a separate ordinary-job callback path. The historical GenericJobDeltaInputs class name is
retained for continuity; its non-generic methods cover both ordinary and closed generic jobs.
Fully supported graphs now export `graph-apply` records: ordinal, base64 `apply`, signed graph ID,
and base64 ordered comma-separated `s:<hash>`, `j:<hash>` or `d:<hash>` actions. Empty sequences are
valid. The compiler verifies every reference against preceding system-injection, graph-job or delta
registration plans, then emits ApplyInjections itself. Editor suppresses its method only when the same
complete-plan selector succeeds. Any unsupported injection keeps the entire outer method transitional;
no action is silently dropped. Owner order follows first graph occurrence; jobs retain FullName then
assembly-name ordering and repeated registrations across owners. Asset/IL discovery still runs in
Editor; this is migration of sequence emission, not removal of the remaining discovery pipeline.
Complete (zero-gap) system lifecycle summaries now also emit ScheduledJobs_<hash>.GetJobs() Type[]
catalogs, keyed by exact system identity and root method ID. Emission requires every structural job
type to resolve to an accessible unmanaged symbol; private/unresolvable types do not produce an
incomplete catalog. Compare Scheduled Jobs checks the typed catalog against the encoded summary,
including cardinality, before reporting it available. Generic system catalogs remain owned by the
assembly exporting that specialization. Production discovery still uses the IL collector pending
coverage validation; the catalog getter only returns typeof values and performs no registration.
Scheduled job catalogs also expose GetRoot(), binding the exact closed declaring type, CLR method
name and parameter types/ref modes to MethodInfo. The generator resolves the original method using
its assembly and documentation ID and substitutes generic root arguments before emitting the lookup.
Compare Scheduled Jobs rejects missing/duplicate root bindings or bindings outside the system's actual
interface-map lifecycle methods. Matching summary counts alone no longer makes a typed catalog valid.
GetRoot only returns metadata; it does not invoke the lifecycle method.
Graph lifecycle AOT entry points (Awake, Start, Update, Destroy, DrawGizmos) are now emitted from
graph-registration by Roslyn. Their original public names, delegate signatures and registration order
are retained. Transitional Editor phase classes are partial and contain private ExecuteGraphOn...
bodies called by these entry points. Dependency traversal, generic parallel scheduling, ref-world/ref-
JobHandle propagation and inner Burst methods are unchanged. Regenerate phase files together with
the DLL; this moves callback ownership, not yet lifecycle-body generation.
Per-slot lifecycle invocation is now compiler-owned as well. Each phase emits InvokeSystem_<slot>
only for slots implementing that lifecycle interface. The helper uses SourceGeneratorSystemCalls'
constrained ref-T dispatch, supporting explicit interface implementations without boxing or copying
system state. Transitional ordinary/sequential-generic/parallel-generic bodies call these helpers at
their original positions; SystemContext creation, dependsOn propagation and batch placement remain
unchanged. The runtime bridge is separate from the AOT-only null-node reachability helpers.
SystemLifecycleGenerator exports per-definition ME.BECS.SystemLifecycle.v1 metadata, including open
generic and nested system definitions. Three five-bit masks describe phase availability, method-level
BurstCompile, and WithoutBurst. Roslyn binds actual interface implementations (including explicit
implementations); derived attribute types are recognized. No lifecycle code or generic specialization
is instantiated by this catalog. SourceGeneratorSystemLifecycle reads and validates the entire assembly
catalog once, normalizes closed generic types to their definitions, and provides graph method selection
and Burst eligibility. Existing type-level Burst selection and graph Burst boundaries are unchanged.
Missing/invalid catalogs retain reflection fallback. Compare System Lifecycle independently compares
all flags with reflection interface maps for definitions and selected closed variants, without invoking
methods; passing this check is not Burst execution/stripping validation. Full graph planning remains open.
Sequential generic groups now also execute through Roslyn-generated InvokeSequential_<start>_<count>
helpers derived from bound topology ranges. Variants retain manifest slot order; every call receives
the preceding handle and optionally applies batches before the next variant. Empty groups return the
incoming handle. Editor supplies the existing node/ancestor/flat-query apply decision via a shared
RequiresApply predicate and retains the final group apply. The returned handle is stored separately:
the input can be a combined-dependency expression and must not be assigned to or overwritten for siblings.
Parallel generic groups execute through InvokeParallel_<start>_<count> helpers from the same bound
topology. Every variant receives the original input handle, results are combined, and temporary handle
storage is disposed after combination. Empty groups return the input without allocating. The outer
group apply remains Editor-owned. The old IL safety query only fed an unused forceWithoutSync argument;
both the query and argument have been removed for all lifecycle nodes. Outer graph traversal, sync
decisions and Burst grouping remain transitional; these changes do not complete graph migration.
Production graph discovery now uses SourceGeneratorScheduledJobs: complete zero-gap summaries must
cover the exact full interface-map lifecycle set, each typed root must be unique, and Type[] must
round-trip to the encoded job set. Only then can all jobs or a selected lifecycle phase be collected
without IL traversal. Failure leaves the caller's set untouched and uses fresh legacy IL collection.
The manifest exporter, injection discovery and sync-requirement query share this selector. Catalogs
are cached under a lock and invalidated when the loaded non-dynamic assembly set changes. Comparison
still performs fresh IL independently and reports source selection, fallback reasons and differences;
source-vs-IL equality is not assumed merely from zero gaps. Runtime/Burst validation remains separate.
Use `ME.BECS/Source Generator/Compare Injection Coverage` to report source-selected versus
fallback/blocked system fields and graph/job pairs into Temp/ME.BECS.SourceGenerator/InjectionCoverage.txt.
It uses fresh graph/IL discovery and the production selectors, reads no generated files or manifests,
does not modify the registry and never invokes patch/registration methods. Counts describe selection
only, not successful generated compilation, Burst execution or runtime equivalence. Graph errors are
reported separately and must not be treated as zero fallback coverage.
`Export Graph Topology` writes diagnostic GraphTopology.txt snapshots in Temp. GraphTopology.v3
assigns preorder occurrence IDs to nested graph uses (not asset IDs), preserving repeated uses.
Rows retain local node order, node/system type identities, enabled/group-enabled flags, stored sync
data for each phase, and input/output port edge order including duplicates. Missing sync arrays are
explicitly unknown; the exporter never calls the mutating GetSyncPoint normalization. Cycles,
missing nodes/graphs and missing/cross-graph endpoints fail a snapshot instead of producing a partial
plan. Runtime manifests now carry the snapshot in `graph-topology` records (ordinal, base64 `topology`,
signed root graph ID, base64 GraphTopology.v3 payload). Each occurrence includes the first start and
exit node indices, or -1 when missing, matching GetStartNode(0)/GetEndNode without mutating assets.
Roslyn validates their ranges and, when node types resolve, their identities and asset order.
Roslyn parses typed occurrences/nodes/ports
and rejects bad root/parent references, duplicate or missing nodes/phases, noncanonical values and
out-of-range edges. Unknown sync data stays unknown. These inputs are not yet used to emit lifecycle
bodies; topology equality alone does not prove scheduling equivalence. Each node now carries an
absolute slot start/count and generic-parallel flag. Binding checks contiguous ranges, parent/child
range agreement, complete single ownership of allocated slots, original system definitions, unique
closed variants within each node and SystemGenericParallelMode. Repeated graph occurrences own
distinct ranges. Refreshing sync analysis and compiling dependencies remain required before switching
the lifecycle planner. Old v1/v2 topology snapshots must be regenerated.
Roslyn now exports ME.BECS.GraphDependencyOrder.v2 metadata for each topology. This independent
structural pass checks reciprocal edges, deduplicates dependencies in first-edge order and uses a
stable node-index topological traversal. Cycles/blocked dependents are explicit and never produce a
partial success order. All nested occurrences are analyzed separately. This is diagnostic planning
data, not the runtime scheduling order. It also records entry-reachable and disconnected nodes,
missing boundaries and unreachable exits using a bounded visited-set traversal over all ports.
Phase filtering, nested execution, sync/batch decisions and generic group calls still need to be
incorporated into the runtime planner before replacing lifecycle bodies. Stored
sync accumulators are signed integers, matching the existing in-degree/out-degree calculation.

GraphLifecyclePlan builds typed per-phase operations from topology: stable queued traversal, all-port
dependency order, other-entry filtering, nested occurrence entry/exit forwarding, generic slot ranges,
pre/post batch decisions and symbol-bound lifecycle/Burst flags. Each handle refers to a preceding
operation; -1 means caller input, while an empty list means default handle. Repeated graph assets use
independent occurrence keys. A full stalled queue reports a cycle/unreachable dependency instead of
emitting a partial plan. Unknown node kinds are explicit unavailable plans. Phase plans
use flat queries and invariant numeric serialization.
InputManifestGenerator publishes diagnostic ME.BECS.GraphLifecyclePlan.v1 metadata for all five phases.
`Export Compiled Lifecycle Plans` saves it to Temp/ME.BECS.SourceGenerator/GraphLifecyclePlans.txt,
without reading generated C# or invoking systems. This is not yet the production emitter: fresh sync
decisions, ordering and nested scheduling need comparison before switching callback bodies to this IR.

GraphSyncAnalysis now computes candidate sync decisions from topology and compiler lifecycle symbols,
not stored asset sync arrays. Nested occurrences are processed bottom-up; inactive phase nodes are
bypassed, reachable cycles rejected before projection, and unique ancestors collected with a visited
set. The structural branch accumulator and redundant direct-exit treatment remain, but inactive nodes
no longer retain phantom edges and one phase cannot reset another phase's start-node state. Arithmetic
uses a wide accumulator and checked final conversion. Disabled systems/groups do not keep a phase
node active. These are deliberate differences requiring review rather than blind legacy equivalence.
GraphLifecyclePlan uses these fresh decisions for runtime bodies. ME.BECS.GraphSyncComparison.v1
records differences against stored sync/count values; Export Compiled Lifecycle Plans includes them.
No asset is modified and no sync array normalized. Registered runtime callbacks now execute the source plan.
Before bottom-up sync calculation, a top-down reachability pass selects active occurrences. A disabled
or disconnected GraphNode does not activate its nested occurrence, so internal cycles or missing
execution boundaries there cannot reject an otherwise valid phase. Slot/type input validation still
covers storage for all graph occurrences; this change concerns execution planning only.

GraphLifecycleEmitter emits runtime bodies for available phase plans, inside a private nested
PlannedLifecycle class. Plans up to 64 steps keep temporary handles in a bounded stack buffer;
larger plans use a Temp NativeArray whose scratch memory is reclaimed by Allocator.Temp, without
per-call Dispose/try-finally. Parallel generic groups follow the same policy. Completion validation rejects forward
references, so each stack slot is assigned before use; pointers are consumed synchronously by the
generated groups and never stored in jobs. This avoids a per-call allocation for small candidate plans
without changing scheduling order. Unity/Burst execution of this path remains unverified.
The registered callback calls PlannedLifecycle.Execute. Missing/invalid plans reject generation;
there is no silent lifecycle fallback. ME.BECS.GraphLifecycleExecution.v1 records each selected phase
as source-plan. Old Editor bodies remain temporarily for comparison until Unity/Burst execution is
verified; they are no longer the registered execution target. Generated
groups preserve IR order, use the existing per-slot and generic helpers, and split on the next invoked
system's Burst flag. Only the candidate container/groups receive Burst attributes; the transitional
phase class is unchanged. Handles live in invocation-local native storage, released in finally after
scheduling (not after job completion: jobs do not retain this handle array). No world/State fields or
shared scheduling buffers are added. Every handle reference must point backward or to caller input;
invalid result references are rejected before emission. Empty phases return without allocation.
This compiles the candidate C# when Unity recompiles, but does not prove runtime/Burst equivalence.
The IR completion validator rejects forward/out-of-range handle references and requires every invoke
or batch operation to be an ancestor of the returned handle. An unjoined branch is an unavailable plan,
not silently scheduled work omitted from completion. The emitter repeats this validation before writing
its candidate body. This proves structural joins only, not that each system implementation preserves
its input dependency. All input ports participate in saved port/edge order, with duplicate dependency
nodes removed at their first occurrence, matching BaseNode.GetInputNodes rather than the legacy
first-port limit. Pre-apply detects multiple incoming edges across all ports. The snapshot parser
verifies reciprocal input/output endpoint sets, so sync projection and handle dependencies cannot
use different edge sets.

Compare Graph Lifecycle Calls instruments the actual transitional AddGraph traversal with an optional
invocation trace. Normal generation does not allocate a trace. The diagnostic compares ordered slot
ranges, generic mode, pre/post batches and Burst flags against compiled IR invoke operations. It refuses
missing/duplicate/stale topology snapshots and unknown sync arrays before running the Editor traversal,
which only constructs strings in memory: no generated files or registration/lifecycle methods are used.
The result is written to Temp/ME.BECS.SourceGenerator/GraphLifecycleComparison.txt.
LifecycleDependencyTrace additionally models handles at the actual Editor emission sites and evaluates
the compiled IR independently. It compares ordered invoke/apply events (including pass-through batches),
their symbolic inputs and the final handle. Pure joins flatten and deduplicate their leaves; batches
and invocations remain distinct ordered operations. Uninitialized symbolic reads are errors, not default
handles. Generic sequential/parallel helpers are expanded symbolically using their actual mode.
This verifies static dependency wiring, not system implementations, Burst execution or stripping and
must not alone authorize switching runtime callbacks. Legacy differences must be investigated, not
automatically copied into the source planner. Traces allocate only during the diagnostic command.

The obsolete per-assembly `JobDeltaTimeGenerator` was removed after production selection moved to
manifest-owned callbacks. Its partial-scope construction is retained as PartialTypeScope and used by
SystemPrivateInjectionGenerator; delta setters and callbacks remain available through the unified
path described above. Old generated graph glue must be regenerated together with the new DLL.
Unity Burst execution and stripping remain unverified. Transitional dependency traversal now throws
on its iteration limit instead of silently emitting a truncated lifecycle body.
Transitional lifecycle Burst selection now resolves the implementation through the interface map,
including explicit methods and closed generic owners. WithoutBurst takes precedence on that actual
method. Graph method-level BurstCompile is inspected as a method attribute rather than compared to
the collection of Burst-decorated types. Main AOT selection uses the same implementation lookup for
WithoutBurst; graph dependency traversal itself is not changed by this correction.

System lifecycle implementations now receive method-summary roots separately from job Execute roots.
`ME.BECS.SystemScheduledJobs.v1` exports system metadata name, lifecycle method ID, unresolved count,
J rows with structurally encoded scheduled types and bounded G diagnostics. Recognized scheduling calls
return JobHandle, have a job-constrained first generic argument and match the existing three scheduling
names. Generic substitutions flow through transitive summaries; explicit lifecycle implementations are
identified by interface maps. Older summaries lacking schedule-schema are incomplete, not empty.
Open generic system roots, missing bodies and unresolved dispatch remain gaps. These summaries are
analysis infrastructure only; injection discovery still uses IL until coverage is compared/validated.
Single-parameter generic lifecycle roots are additionally instantiated for public unmanaged components
in the producing assembly, using interface constraints and IGenericWithout exclusions. Closed roots
carry assembly-qualified system identities and retain their definition assembly and substitution
environment; they do not change SystemGenericParallelMode or graph scheduling. Multi-parameter roots
remain unsupported by this specialization step.
`ME.BECS/Source Generator/Compare Scheduled Jobs` compares closed visible systems against fresh IL,
using interface maps for all lifecycle roots, including explicit implementations. The report is saved
to Temp/ME.BECS.SourceGenerator/ScheduledJobs.txt. Equal sets, incomplete traversal, missing summaries
and unresolved open definitions are separate counts. Comparison expands system definitions through
the production Editor filter and locates closed-root summaries across component assemblies. The IL collector is shared with graph
injection discovery rather than duplicated, and comparisons invoke no patches or registrations.
This is not a runtime/Burst check and does not yet switch production discovery to source summaries.
Graph injection discovery and the per-lifecycle synchronization decision now share the same interface-map
root selection as this comparison. Explicit implementations are no longer omitted by name-only reflection;
unrelated overloads are not selected. Lifecycle order remains Awake/Start/Update/Destroy/DrawGizmos.
System scheduled-job traversal does not execute method-reference edges: unresolved/deferred delegates
produce a gap, and resolved delegate calls contribute through their separate invocation edge. The
transitional IL collector fails explicitly above 10000 method instances, rather than hanging on recursive
generic expansion or returning a truncated set to the graph synchronization decision.
The target compilation exports type-input metadata and an ordered entity registration initializer.
The menu actions `Export Editor Type Inputs`
and `Export Runtime Type Inputs` write discovery snapshots under `Temp/ME.BECS.SourceGenerator`,
outside Assets, preserving current discovery order. They do not import files or trigger compilation.
The existing Editor Build source now writes a profile-specific manifest beside its output and adds
`-additionalfile` to the response file independently of asmdef changes. Only changed inputs are written
and imported. This happens when the user runs the existing codegen, not when exporting Temp snapshots.
Unity delivery to Roslyn still needs verification. Symbol resolution supports closed generic and nested
types with exact assembly identities. Graph topology/config payload transport and the remaining global
bootstrap phases are not yet implemented. Entity registration now uses `EntityInputs.Initialize()` at
the existing initialization slot: the Editor selects types with the existing GetAllTypes filter and
passes their order; Roslyn emits Init, Register calls with contiguous IDs and groupsCount. Unresolved,
open, inaccessible, non-unmanaged/non-IEntityType registrations and IDs exceeding UInt16 fail compilation.
The Editor no longer emits individual entity registration statements. Regenerate inputs using the
existing codegen after updating the DLL; v1/v2 snapshots are deliberately rejected, not treated as an
empty registration list. Runtime/reload verification in Unity remains outstanding.

System registration statements are also compiler-owned. The manifest producer expands a copy of the
system list using the existing constraint/exclusion selection and assembly/visibility filters, then
exports closed `system-registration` entries. Roslyn emits `SystemInputs.Register_<SHA256>()` methods;
the transitional bootstrap calls them at each original registration position in initialization and AOT.
They are intentionally not batched before lifecycle AOT calls. Generic scheduling, dependencies and
SystemGenericParallelMode remain unchanged. Missing or invalid registration inputs fail compilation;
the bootstrap no longer falls back to emitting StaticSystemTypes.Validate statements itself.

Aspect initialization now calls the manifest-generated `AspectInputs.Initialize()` at the existing
phase position. It validates each selected aspect then calls its per-assembly generated query initializer.
Catalogs emit an explicit no-op query initializer for empty queries (no Resize(0)). Nonempty queries
are checked against reflection field order by the manifest producer; absent or incompatible catalogs
stop generation rather than silently dropping filters. The Editor aspect initializer retains reference
collection only, not cached C# registration/query statements. Aspect construction bodies now also come
from Roslyn: ordered aspect-construction entries call existing per-assembly constructors through
AspectInputs.Construct(ref World). Source-owned RegisterConstruction registers that method directly
as the managed World callback; the redundant Editor AspectsConstruct wrapper is removed. The existing
callback registration slot and default subId remain unchanged; registration is not moved into the
earlier aspect-ID/query phase. This callback was not Burst-compiled or a native function pointer and
retains its managed delegate semantics. Empty
aspects are omitted (no storage allocation); nonempty aspects require a compatible generated constructor,
checked for field order and shape before export. The Editor no longer emits pointer assignments or
reflection SetValueDirect fallback bodies. Recompile catalogs and regenerate inputs after updating;
Unity initialization/reload behavior has not yet been verified for this path.

The base StaticTypes component phase now calls manifest-generated ComponentInputs.Register_<hash>
and Aot_<hash> at the same respective initialization/AOT positions. Roslyn emits Validate and the
optional Default assignment; Default is evaluated once per initialization call, never during export.
Closed generic components are resolved structurally, not through nongeneric catalog wrappers.
Shared/static/config-specific registration and AOT methods are also emitted by Roslyn from compiler
classification, not Editor-supplied flags. The current feeder exports only ordered type identities.
Old four-column component records remain readable for reload compatibility, but their flags cannot
override tag/default/static/shared/config decisions. A mismatch emits BECSG103 and uses the compiler
result, allowing the Editor to reload and replace the stale manifest; malformed flag syntax remains
an error. Raw input metadata is retained separately from effective ComponentFlags.v1 metadata.
CoreTypeInputs preserves separate registration passes and their call order; config registration
does not repeat Default assignment. Custom shared hash selection follows the actual IComponentShared
GetHash slot, including explicit implementations supplied by derived/constructed generic interfaces.
Only the original throwing interface fallback counts as no custom hash; unrelated overloads and
new interface slots do not. Unused Editor tag/static/hash classifiers have been removed.
Per-assembly Catalog_* no longer emits duplicate component Register/Shared/Static/Config or Aot*
methods. The old Editor string-call bridges for those methods are removed too. Catalog comparison
now checks the selected global ComponentInputs signatures and ComponentFlags.v1 records, without
invoking them. Discovery metadata and aspect/group/system helpers are unaffected. The sole component
registration and component AOT bodies live in the compiler-owned global plan, with the existing
bootstrap preservation roots; signature availability is not runtime/stripping proof.
Compiler checks do not establish Burst/player support for every default-interface implementation;
that execution/stripping verification remains separate.

Component tag/layout classification is exported independently of closed-type discovery. ComponentTags
records describe original definitions, including open generic structs, nested generic owners, private
fields and synthesized auto-property storage. Multiple partial declarations emit one ordinal-sorted
record, and closed specializations reuse their definition's classification without introducing IDs or
adding open types to GetComponents(). Explicit StructLayout.Size is captured before metadata import.
The consumer rejects malformed/duplicate matching classification records rather than guessing a tag.
For old catalogs/precompiled component libraries without these records, ComponentLayoutReader reads
instance-field presence and CLI ClassLayout directly from Roslyn's PortableExecutableReference images.
This also works with in-memory references, uses per-compilation module caches, and does not load a
runtime assembly, invoke user code, inspect IL method bodies, or scan/read project paths. If neither
classification nor a compiler reference image is available, registration fails with a layout diagnostic.

The aggregate InputManifestGenerator, BootstrapGenerator and InputRecoveryOutput have been
removed. No analyzer emits StaticMethods/StaticTypesInitializer compatibility entry points from
old aggregate manifests or exported hooks. Project-owned composition is emitted by
BootstrapPublicationGenerator, and each typed publication validates its own owner/profile and
input fragment. The input catalog retains the full exported evidence for diagnostics; it is not
an executable aggregate. Missing publications remain subject to the installed-plan preflight.
GraphDeltaSetterContract retains the shared private-field injection contract independently of
the retired manifest reader. Normal export does not modify old Assets/ME.BECS.Gen files.
The current project removed that directory explicitly, with a recoverable backup outside Assets;
clean-project Unity verification remains required.

View-tracker manifests keep distinct `(owner, role)` records for an EntityView that also implements
IViewModule. The runtime has ONE tracker ID per Type; emission registers that owner once using the
union of both roles' component dependencies, preserving first occurrence order. Duplicate inputs
within the same role remain invalid. Inaccessible component diagnostics name the offending type;
the private-component safety test fixture opts out of runtime tracking via IViewIgnoreTracker.
The compiler also applies the current IViewIgnoreTracker contract to stale owner records.
It removes an inaccessible global component only when that component is referenced by ignored
owners and no active owner needs it. Orphaned/private active dependencies still fail validation.
BECSG102 requests regeneration after reload without forcing a circular dependency on an Editor
exporter that still has the old assemblies loaded. Raw input metadata remains available for
comparison; effective emitted tracker dependencies follow the current compiled ignore contract.

The compatibility bootstrap is temporary, not the final architecture. Remaining retirement gates:
1. Move remaining config callbacks and job metadata generation; delete legacy emitters after coverage checks.
2. Replace reflection/IL dependency analysis with semantic analysis plus exported cross-assembly summaries.
3. Export Unity graph/config asset inputs and explicit type selections to compiler-readable manifests; Roslyn
   must not access AssetDatabase. Preserve graph initialization, dependency wiring, generic parallel mode and IDs.
4. Generate one ordered global bootstrap from manifests and per-assembly catalogs, replacing Editor Build output.
5. Validate runtime behavior, Burst/IL2CPP and reload before removing the old generator and compatibility paths.
Only a minimal asset-manifest importer should remain in Editor; registration/code emission belongs to Roslyn.

Public unmanaged non-generic IEntityType structs now have catalog entries and registration wrappers accepting
the ushort ID chosen by the existing EntityTypeCodeGenerator. Init, selection/order, groupsCount and fallback
are unchanged. Comparison checks catalog coverage and wrapper availability for the actual selected (type, ID)
pairs without registering them. Entity type discovery remains legacy; no IDs are assigned by Roslyn.

Each normal bootstrap Build now logs Codegen timings separately for Editor/Runtime: discovery, generic expansion,
registration/AOT, each custom generator phase and cache I/O, formatting/import, and asmdef work. Measurements
include synchronous asset operations but not subsequent Unity compilation. Compare commands do not run Build.
EarlyInit method enumeration is reused per generator instance; interface TypeCache results are snapshotted per
lookup scope. Legacy discovery fallback remains enabled until timing data identifies the next useful removal.
Job initialization no longer reads or writes the script-only persistent cache: entity counts, component
sizes and generated initializer selection depend on transitive methods, group IDs and referenced catalogs.
The count/weight/size statements are memoized only inside one Build instance, shared across job categories;
Editor and Runtime builds get separate instances. EarlyInit calls are selected afresh per category.
Counts and component-size analysis still use IL pending source coverage verification. This removes stale
cached decisions, not that remaining analyzer. Existing disk cache files are left untouched.

Compare Editor Catalogs / Compare Runtime Usage now also report ordinary/generic job EarlyInit coverage and
unavailable-wrapper reasons. Diagnostics reuse the actual legacy selection routine in read-only diagnostic mode, skipping
cache access, IL/safety/weight analysis and debug generation. Counts are distinct selected calls, not all declared
job types; cache contents are not validated. Generic metadata accessors may run, but EarlyInit never runs.
Legacy selection failures mark the comparison incomplete rather than reporting successful zero coverage.
Each source EarlyInit wrapper now has a v2 Selection_<hash> constant describing its job
definition, selected method, wrapper, ordered-argument metadata accessor, generic arity and explicit
bootstrap phase (0..6). Phase selection does not depend on Roslyn member enumeration order.
Comparison reads this catalog independently of legacy call spelling and closes generic argument
accessors with the selected system component. It compares distinct call sets per job and reports
source-only/legacy-only calls as validation issues. Only Type[] getters execute, never initialization.
A second comparison reconstructs the full source call sequence using those phases and the same
discovery/generic-expansion order as the existing bootstrap. It retains duplicates, rejects ambiguous
per-job/per-phase selections, and compares every position against actual legacy selection. Missing
metadata marks the sequence INCOMPLETE, never equal. This proves only call selection/order for that
snapshot, not independent discovery correctness or runtime initialization. Production now reads source
phase selections directly with SourceGeneratorJobEarlyInit, without the legacy comparison, string-call
reconstruction, or executing Args_* metadata getters. It validates each constant's hash/schema/phase,
unique wrapper/getter signatures and closed generic constraints, and rejects missing or ambiguous
selections before emission. CollectLegacyEarlyInit and the spelling-based bridge are migration diagnostics
only, reachable through Compare rather than export. Counter/weight/size
initialization stays at each original category/job slot, even slots without an EarlyInit call;
duplicate slots are not collapsed. Assemblies must be recompiled for v2 records. Missing records
are not empty coverage. Runtime/Burst/IL2CPP behavior remains unverified; remove the oracle only after
project coverage and runtime validation, not simply after building the generator DLL.
The source selector returns the exact resolved initialization snapshot consumed by emission, including
stat-only slots. It keeps discovery/generic-expansion order and repeated slots unchanged. Source method
successes/failures are memoized per closed job for that exporter instance, with defensive array copies;
there is no cross-reload cache. Diagnostic call-spelling plans retain their separate lookup-scope cache.

EarlyInit now also supports public jobs nested in a generic system with one unmanaged type parameter and
supported public non-generic interface constraints. The emitted wrapper carries the outer constraints.
The production reader closes wrapper/getter signatures with the selected concrete argument but invokes
neither. Diagnostics additionally invoke the typed getter to compare the complete legacy call (job, method,
ordered component/aspect arguments). Ambiguous overloads, multiple parameters or jobs with their own
generic parameters remain unsupported and block export, not a legacy fallback.

Generated identifiers use the shared SourceGeneratorNames source, compiled into both analyzer and Editor.
Inputs up to 128 UTF-16 characters keep their previous hex encoding; longer names use H + full SHA-256.
EarlyInit method names always use SHA-256. This bounds encoded suffixes to 512 characters, keeping generated
method/catalog names below metadata limits for ordinary namespaces. Naming does not use process hash codes,
timestamps, file paths or culture. Changing helper names does not change simulation type IDs.

JobEarlyInitGenerator emits EarlyInit wrappers for public unmanaged non-generic jobs with supported public
non-generic arguments. It matches the EarlyInit interface constraints; the diagnostic Editor bridge checks
exact legacy spelling, while production requires the wrapper named by the source selection catalog,
including closed generic jobs supported by the generic wrappers described above. Missing catalogs,
unsupported signatures and ambiguous/missing wrappers stop export with the job identity and reason;
there is no direct legacy EarlyInit fallback. Missing legacy method selection fails comparison instead of
logging a warning and omitting initialization. Comparison remains nonthrowing per missing wrapper and
counts it as a validation issue; no initialization executes. Reflection-based selection remains only
in the explicit migration comparison. Entity creation counts, max component size, safety/debug metadata and
dependency analysis remain with the existing job generator; weights use complete source summaries
where available. Persistent initialization-cache entries are no longer consumed.

Generic component candidate snapshots are memoized by (open system definition, interface constraint) in the
existing per-Build/per-comparison lookup scope. Registration, graph sizing, allocation and execution reuse the
same filtered snapshot, including empty results. Public array results are copied so callers cannot corrupt it.
Outside a scope discovery remains uncached; nested scopes and other threads do not share mutable snapshots.

Single-parameter generic system expansion uses the same EditorUtils constraint/exclusion filter for registration
and graph construction/counting/execution. It discovers public closed unmanaged IComponentBase implementations
of the interface constraint across loaded assemblies, validates every CLR generic constraint, and applies
IGenericWithout. Ordering uses ordinal full names with assembly tie-breaks (graph retains BECS-first grouping).
SystemGenericParallelMode retains common input dependency + combined outputs; without it variants are chained.
Zero-variant parallel nodes preserve their input dependency. These filter/order corrections can change previous
specialization IDs: clients must use matching regenerated builds. Multi-parameter automatic expansion is rejected
explicitly. Discovery stays in Editor because a system's compilation cannot see components in downstream asmdefs;
the resulting closed calls use the existing generic source-generated registration/AOT methods.

Editor bridge lookups (catalog types, encoded names, method signatures including absent methods) are cached
within each bootstrap Build or comparison run. Scopes are thread-local, nestable, synchronous, and disposed on
exit including exceptions. No cache survives the run or enters simulation State; runtime/editor runs stay separate.
This avoids repeated reflection/name encoding across registration and AOT phases; speedup has not been measured.

Burst/NoBurst/factory system AOT wrappers now call the narrow public SourceGeneratorSystemAot runtime bridge.
Internal Node and Burst implementation types remain internal. Each wrapper preserves the previous null/default
call at the same AOT position; bootstrap Burst/WithoutBurst selection remains unchanged. These are reachability
stubs, NOT executable initialization APIs: null-node calls must never run. Diagnostics inspect signatures only.
IL2CPP stripping and Burst compilation still require Unity validation. Open generic definitions are reported
separately from actual unsupported closed specializations.

Direct lifecycle AOT calls for public OnAwake/OnStart/OnUpdate/OnDestroy/OnDrawGizmos methods now use generated
wrappers for ordinary and supported generic systems. Each wrapper preserves `new System().OnX(ref context)`;
the bootstrap retains phase order, the same context, and fallback. Explicit interface implementations remain
legacy. Burst pointer/MakeMethod generation stays legacy because those runtime implementation types are internal.

Generic system registration is emitted by GenericSystemGenerator as constrained generic methods, without
enumerating project-specific specializations. The bridge closes these methods for the concrete systems already
selected by PatchSystemsList and preserves their order. Public unmanaged generic structs and public generic
containing types are supported when parameter names are unique and constraints are publicly accessible.
Unsupported cases keep legacy registration. Existing PatchSystemsList specialization limits are unchanged;
this does not add automatic multi-parameter specialization discovery. System lifecycle/Burst AOT stays legacy.
Diagnostics additionally check generated registration coverage for the expanded closed generic systems.

Public unmanaged non-generic ISystem structs now have GetSystems catalog entries and individual RegisterSystem
methods. The bootstrap uses these at the previous Validate positions in initialization and AOT, preserving its
existing system list and generic specialization handling. System discovery is not replaced. Comparison includes
systems and reports unsupported/generic systems separately. Burst pointers and lifecycle AOT calls remain legacy.

Editor discovery now consults validated component/aspect catalogs, retaining the legacy TypeCache enumeration
as the migration safety boundary and as fallback for every unlisted type. This preserves the exact original
insertion order (including equal FullName sort keys), full test-only discovery, and unsupported type coverage.
Runtime usage now uses compiled IL (the source-assisted comparison reader is described below).
This editor compatibility stage does NOT yet eliminate TypeCache work or
claim a performance gain; catalog reads add overhead until the legacy enumeration can safely be retired.
The comparison command explicitly bypasses catalogs for its baseline and checks content/order equality
against catalog-assisted editor discovery for components, aspects, and component groups.

Manual diagnostics after Unity has finished importing/recompiling:
- `ME.BECS > Source Generator > Compare Editor Catalogs`: compare full legacy editor discovery with loaded catalogs.
- `ME.BECS > Source Generator > Compare Runtime Usage`: compare the independently discovered IL baseline
  with source-assisted runtime components/aspects/groups/entity types/jobs/systems, including final order.
  Source-only and legacy-only types are listed for review; an IL difference is not by itself proof that
  the source result is wrong. The separate full-catalog coverage can include unused types.
  This inspects loaded Editor assemblies, not player binaries.

### Runtime lifecycle type usage: IL production, source comparison

The normal `GetUsedObjects(false, ...)` path selects compiled IL. Editor discovery
still includes all declared types, including unused test types; its declaration
catalog path is unchanged. Only explicit diagnostic callers can request the
source-assisted runtime lookup. Missing/corrupt source usage catalogs therefore
cannot block runtime input export or change registration selection.

The IL walk follows the exact lifecycle/module/config/destroy slots and the
Execute contract selected by each scheduler. It does not use a name-only Execute
lookup or collect unrelated job interfaces. It records signature components and
all selected aspect storage fields, and does not prune user helpers merely
because they carry SafetyCheck. The fixed-point config/destroy callback closure,
per-run caches and final discovery sorting rules remain unchanged. Types that
were previously missed can now enter the registration set; this is not a claim
that IDs remain identical after correcting discovery.

Ten isolated IL regression cases cover deferred and closed-generic jobs, explicit
and overloaded Execute, user annotations, optional aspect storage, module slots
and cyclic callback discovery. No lifecycle/job bodies or Unity native APIs were
executed. This Editor-only switch requires no analyzer DLL rebuild.

`ME.BECS.RuntimeTypeUsage.v1` now describes the registration-relevant component, aspect,
entity-type and job sets for each concrete system lifecycle root. This is independent of
scheduled-job discovery: knowing which job is scheduled does not prove which types its
Execute body uses. The traversal binds the exact interface implementation and follows
scheduled bodies, generic substitutions, helper methods and certified exceptional effect
unions. It does not infer execution counts, ordering or synchronization from those unions.

`type-usage-schema=1` marks exact engine entity-creation/aspect-storage boundaries and
component-storage terminals. A user SafetyCheck annotation alone does not stop usage
traversal; helpers can still introduce other types. Query factories remain traversable
despite their independent no-submission contract. Unknown callbacks, unavailable schemas,
unbound/unsupported types, work-limit exhaustion and the open destroy registry leave the
plan incomplete. No partially discovered set is silently certified or merged as complete.

`ME.BECS.AspectStorageTypes.v1` includes **all** instance IAspectData component fields,
including private fields without QueryWith. Query-presence catalogs alone are insufficient
for aspect storage registration. Imported aspects require their unique own-assembly
storage catalog even when reference metadata omits private fields. Missing/conflicting
catalogs withhold the usage plan; internal/private types and hidden generic arguments
cannot produce a selectable public registration list.

Complete plans expose canonical ordinal-AQN Type[] getters and the exact lifecycle
MethodInfo binding. The run-local Editor reader validates identities, signatures,
categories, uniqueness, order and getter results, and returns copied arrays. It invokes
only generated metadata getters, never lifecycle, Execute, constructors or registrations.
Malformed complete plans fail explicitly; absent/incomplete plans retain the transitional
IL lookup. Closed generic plans may be published by the component argument's assembly.

The explicit source-assisted comparison uses those complete plans for system lifecycle roots. Both source
selection and production IL use exact lifecycle interface mapping
(including explicit implementations) and the same PatchSystemsList generic expansion as
graph registration. SystemGenericParallelMode and runtime scheduling remain untouched.
Asset/TypeCache enumeration is retained as the Editor input feeder. The existing final discovery sorter is unchanged; this is
not a proof of player registration parity or equal-name ordering across different assemblies.
Isolated checks cover deferred/generic jobs, hidden aspect fields, annotation wrappers,
metadata damage and source selection with a throwing IL stub. Unity/player/Burst execution
and full-project discovery comparison still require validation after script recompilation.

Reports go to the Unity Console, sorted ordinally. They distinguish unsupported types from missing catalogs/types,
detect duplicate/invalid entries, compare registration/tag/default metadata, and identify bridge fallbacks.
Only catalog/metadata accessors are invoked: no registration methods or component Default getters are executed.
Diagnostics are opt-in and do not add work to normal generation. They do not prove equality of runtime IDs/default
values or replace player/domain-reload validation. Types from precompiled assemblies may legitimately lack catalogs.

- Incremental syntax/semantic discovery of public unmanaged non-generic components and aspects in each compilation.
- Partial declarations are deduplicated. Output is sorted ordinally and contains no timestamps or file paths.
- Per-assembly catalogs use collision-free encoded assembly names.
- Ordinary components get individual Validate/SetDefaultValue methods. None are called automatically.
- Shared/static components have separate generated registration methods, called at their existing bootstrap
  positions. Legacy isTag/hasCustomHash values are passed as arguments; AOT and base registration stay unchanged.
  Catalog component discovery now covers IComponentBase, including static-only components.
- Group assignment and aspect ID validation have individual generated methods, called in the original order.
  The bootstrap still supplies the group Type from the attribute. Aspect initialization caches keep legacy registration text and resolve
  the current generated method only when emitting, so analyzer availability does not invalidate cached calls.
- Nonempty supported aspect QueryWith lists now have generated metadata and an initializer. The bridge compares
  the exact component sequence with reflection before using it, including duplicates and private fields.
  Partial-declaration ordering differences, unavailable methods, and unsupported/generic component types fall back.
  Empty queries retain the old no-resize behavior. Generated query calls are not persisted in the legacy cache;
  required component/aspect references are added on every generated-path run. Metadata reads do not register IDs.
- World construction is generated for aspects whose data fields are all public writable AspectDataPtr<T> with
  supported public non-generic components, when the compilation allows unsafe code. A metadata accessor exposes
  field order; the bridge checks it against the exact legacy ordering before emitting the constructor call.
  Empty aspects, private/readonly/custom data fields, unsupported components and order differences retain legacy
  construction. No world or state is stored in the catalog: the current world is passed by ref to each call.
  Diagnostic checks read metadata only and never construct aspect data.
- Partial aspects can initialize private/internal writable AspectDataPtr<T> fields directly. Every containing
  type must also be partial; non-generic class/struct nesting is supported (records remain fallback).
  The generator emits a uniquely named static helper inside the aspect and a catalog forwarding method.
  It adds no fields and does not change layout or world/state ownership. Existing source declarations are not
  rewritten automatically: opt in with `public partial struct MyAspect : IAspect` (and partial containing types).
  Readonly fields, implicit backing fields, unsupported component types and helper name collisions retain fallback.
- Component/shared/static/config AOT calls now have separate generated wrappers, referenced from their original
  bootstrap AOT positions. IConfigInitialize registration has its own Validate-only method with legacy flags;
  it does not repeat Default initialization. Unsupported types and unavailable wrappers use the old emission.
  User OnInitialize execution is not moved. System/job AOT remains legacy. IL2CPP/stripping equivalence is unverified.
- System/job AOT, remaining aspect storage cases, jobs, asset discovery,
  closed generic instantiations and dependency analysis remain with the existing generator.
- Catalog ordering is NOT a replacement for the existing global ordering or a source of numeric type IDs.

The generated source lives in Roslyn's compilation output, not in Assets/ME.BECS.Gen.
The implementation is in the hidden `.ME.BECS.SourceGenerator` directory, which Unity ignores during asset import.

## Integration stages

Config mask callbacks now have complete Roslyn-owned bodies in `ConfigMaskInputEmitter`, including Burst and
MonoPInvokeCallback attributes. The manifest carries every selected component's public instance fields
in reflection order; the compiler verifies exact field-set coverage and uses that explicit order for
mask bits, including partial declarations. The Editor body-emission fallback and redundant per-assembly
ConfigMaskGenerator are removed. Unsupported/nonwritable fields now produce a diagnostic. The shared
selector uses ordinal type ordering, while field order is never sorted. Editor initialization calls
ConfigMaskInputs.Initialize in the original mask-registration position before collection callbacks.
The mandatory config-mask-schema/v1 marker requires regenerating older manifests and distinguishes
empty selection from stale input. Validation compares manifest field order and concrete callback
signatures without invoking callbacks or metadata getter methods; input records are cached per lookup
scope. Unity/Burst execution is still unverified.
Collection counts are now manifest-owned via config-collection-count records and a mandatory
config-collection-count-schema/v1 marker. Roslyn verifies each number against public instance
IUnmanagedList fields and emits ConfigCollectionCounts.Initialize, including the original resize.
Editor AddInitialization only collects references and calls it in the original count phase. The selector
retains ordinary/static/shared category order, uses ordinal type ordering and deduplicates identical
types (repeated SetCollectionsCount writes had no extra semantics). Per-assembly count getters and
registrations are removed. Validation reads target-assembly records, not executable count getters.
Older manifests must be regenerated. `ConfigCollectionsInputEmitter` now owns materialization
callbacks through config-collection-callback records and a mandatory config-collection-callback-schema/v1
marker. Callback and count selections must contain exactly the same types in the same order.
The Editor exports collection field names in ordinal field-type order (preserving reflection order
for ties); the compiler verifies exact coverage and uses this explicit order, including partial types.
Editor AddMethods only collects references and calls ConfigCollectionsInputs.Initialize after masks.
The old per-assembly ConfigCollectionsGenerator and Editor callback body fallback are removed.
Materialization covers ListAuto/MemArrayAuto,
including dispose-before-replacement and legacy missing-data behavior (new list capacity 1 / empty array).
The bridge compares counts and callback field order independently before selecting them. Custom collection
implementations now use source callbacks when their public construction contract is verified: in-Ent
data/length constructor, GetConfigId, IsCreated, Dispose, and the appropriate list capacity constructor
or array Empty field/property. The data argument must have an implicit unmanaged conversion; boxing
constructors are rejected. Non-generic custom collection fields are no longer excluded by bridge metadata
selection. Collections implementing neither IMemList nor IMemArray preserve the old no-replacement
missing-data branch. Unverified contracts now produce a compiler diagnostic rather than fallback.
Validation checks target-assembly manifest records and concrete callback signatures without invoking
metadata getters. Unity/Burst execution remains unverified. Both phases have
comparison counters; no callback is executed by validation. The inactive `CopyFromCodeGenerator` is retained
for the requested follow-up investigation after the source-generator migration. Both editor and runtime
paths return an empty result unconditionally and its callback bodies are commented out. Do not reactivate
it implicitly. Investigate the recorded Unity Cloud Burst failure, collection cloning/ownership and element
CopyFrom semantics before designing its source-generator replacement.

Query jobs can declare `[EntitiesJobMaxCount(32)]` to bound total entity creation per Execute invocation,
including calls outside loops and across all entity groups. Pass `in jobInfo` to Ent.New. The limit must
be positive; it is not a per-worker or whole-query budget. A per-invocation counter shared by JobInfo
copies resets before Execute, while different workers allocate independent counters. Both sequential
and parallel query execution check the limit; overflow throws E.JOB_ENTITIES_MAX_COUNT unconditionally.
Parallel creation in detected loops, recursive calls or potentially repeating exception filters without the attribute
throws an error explaining how to add it. A filter may repeat during exception unwinding even when
there is no explicit loop in Execute. The current bootstrap reserves the declared maximum for each
group with repeatable creation, preserving
index-based deterministic allocation and returning unused entities after the job. This can reserve
maximum * query size * affected group count slots; oversized per-group allocations are rejected.
Loop detection in the transitional IL analyzer includes comparison back-branches (blt/bge/etc.), not
just br/brtrue/brfalse. No State fields, global counters or atomic allocation-order IDs are introduced.
Source method summaries carry entity-limit-schema=1 and entity-max-count, including generic job roots.
Zero-gap entity summaries emit a source-owned JobEntityCounts initializer, with v3 initializer metadata
and explicit group-ID parameters in reserved C-row order. Bootstrap selects validated v3 initializers
without running legacy count analysis. v3 counts each call site and preserves separate inline/loop
contexts; legacy visited-method deduplication can undercount both and is not a correctness oracle.
Independent legacy comparison remains diagnostic; incomplete/missing source coverage still uses the
legacy body pending migration. IL analysis is therefore not yet removed. The initializer executes at
the original count-initialization slot; group IDs remain owned by ordered global discovery.
Raw C rows remain analysis counts; L records the attribute limit.
The entity-count comparison additionally validates each source initializer without invoking it:
exact ordinary/closed-generic job identity, zero gaps, canonical ordered count records, selected group
IDs and bounds, current attribute limit, v3 initializer identity and closed signature. Unknown records,
duplicate groups/initializers and ambiguous assemblies are unavailable, not silently accepted. This
availability check is reported separately from full parity and does not execute either initializer.
Recompile catalogs and regenerate
bootstrap inputs to apply attribute changes. Tests cover bounds/reset/copy/worker isolation and
bounded-loop ID equality across batch sizes, but have not been run in Unity.

Production job dependency consumers select complete typed source D records without reading IL.
This applies to debug safety containers and job component-size selection; complete system unions
are selected independently as described below. Closed generic jobs use their specialization catalog.
Missing/incomplete metadata still requires legacy analysis; malformed or conflicting catalogs stop
export instead of silently falling back. Selection caches live for one export pass and return copies,
because callers merge/mutate dependency sets. The legacy IL oracle remains in the explicit comparison
report and uncovered paths; this stage does not claim its full removal or runtime/Burst validation.
Complete accessible summaries export an A/v1 JobSafetyTypes catalog containing ordered typeof
references for all D records. The Editor validates catalog identity, assembly uniqueness, getter
signature, component types and exact D-row order. Legacy no longer supplies the Type bindings;
it is used only by explicit comparison/fallback. GetTypes does not initialize statics or run jobs. Catalog
availability is reported separately and is not proof of runtime safety or IL2CPP preservation.
The Safety report uses the same source reader as production, counts complete selection independently
of IL success/parity, and distinguishes fallback from corrupt metadata that blocks export. ReadSource
does not call the legacy analyzer. Tests inject a failing legacy callback for ordinary, explicit and
closed-generic jobs; cover missing/incomplete fallback caching; reject duplicate/conflicting headers,
unreported gaps, invalid modes and absent typed catalogs; and check caller mutation isolation.

Zero-gap safety analysis additionally emits a candidate JobMaxStructSize initializer
for accessible unmanaged IComponent dependencies, including closed generic job roots. It calculates the
maximum using UnsafeUtility.SizeOf<T>, not managed marshalling size, and exports ordered GetComponents /
GetSizes metadata getters plus an S/v1 initializer record. The getters do not initialize job statics.
Production maxStructSize remains legacy until component-set and size differences (notably bool layout)
are reviewed; this value affects automatic batch size. Candidate generation is not runtime verification.
Compare Job Safety now validates the size initializer identity/signature, invokes only GetComponents
and GetSizes, and compares the component set and maximum against independent legacy Marshal.SizeOf.
Per-component layout differences are listed even when the maximum coincides. Size counters cover jobs
with readable safety summaries and successful legacy analysis; missing summaries remain in the broader
safety unavailable count. No Apply, jobs or registration methods execute, and production is unchanged.

`MethodSummaryGenerator` exports raw method operation records using assembly metadata with the versioned
key `ME.BECS.MethodSummary.v2`. Downstream compilations can read them without method bodies or executing
any registration. Payload: declaration documentation ID, analysis attributes, unresolved-operation flags,
declared generic environment, then tab-separated operation records (kind, loop context, target assembly
identity, target declaration ID, constructed containing type, method type arguments). Types use a versioned
structural encoding: named definitions + arguments, scoped type parameters, arrays and pointers. Parameter
identity includes its declaring type/method, not just the name T. This replaces ambiguous display/reference
strings. v1 data is deliberately not accepted as v2; dependent assemblies need recompilation.

Production maxStructSize emission now selects the source Apply initializer for complete safety
summaries only after validating its identity/signature, exact component set and maximum size against
the independent legacy calculation. Closed generic jobs use their specialization catalog. Incomplete
or unavailable metadata keeps the legacy assignment; a complete differing result stops export.
This does not remove the IL safety analyzer or switch runtime safety dependencies.

External `System.Runtime.CompilerServices.Unsafe.AsPointer<T>(ref T)` and
`AsRef<T>(void*)` / `AsRef<T>(in T)` have a signature-checked `ecs-leaf` contract:
they only convert addresses and do not execute callbacks, schedule jobs or create entities.
Their argument expressions remain traversed. No other Unsafe methods are implicitly covered;
source-defined lookalikes are excluded. Counts, safety, weights and scheduled-job discovery
consume the same marker rather than treating the entire external assembly as safe.
The metadata-defined parameterless System.Object constructor also has this contract;
user constructors and other base constructors do not inherit it. Constructor metadata tests
check both the positive Object case and the negative user-constructor case.

Entity-count traversal treats each closed generic instantiation and loop context as a separate
analysis node. It caches transitive count contributions and adds them at every call site, rather
than skipping later calls to an already visited helper. Recursion and integer overflow remain gaps.
The cache is bounded to 200,000 group contributions per root (`EntityCountCacheLimit`), and the
sum of loop counts must fit the runtime uint field (`EntityLoopCountOverflow`) before emission.
This intentionally corrects legacy under-reservation for repeated helper calls; parity checks
remain in place so new discrepancies cannot silently switch production metadata.
`LegacyGenericVisitIdentity` is no longer an incompleteness condition: a fully resolved generic
graph can be complete even when the old IL visitor conflates specializations. Actual count/loop
differences still fail the independent production parity check and require investigation.

Manifest-driven destroy registration additionally exports `ME.BECS.DestroyCallbackTargets.v1`
assembly metadata: selected component count, unresolved count and ordered target rows with the
component token, implementation assembly/documentation ID and constructed receiver. Per-target
`S` rows distinguish available, missing, ambiguous, malformed and locally incomplete method
summaries. Available means body metadata exists, NOT that transitive analysis is complete. Explicit
interface implementations are resolved through Roslyn. Source method summaries export a `destroy-owner`
binding from the source interface map. When referenced metadata exposes an interface forwarding method
without a summary, only a unique matching binding may select the source body; the catalog records
that analysis target in an `A` row. A method name alone is never sufficient.
`ME.BECS.DestroyCallbackSafety.v1` additionally
exports transitive per-target safety analysis using the same summary loader and analyzer as jobs,
with runtime initializer emission disabled. The callback's writable component receiver is explicitly
included as an RW argument dependency, even if its body only mutates fields through `this`.
`ME.BECS.DestroyCallbackCounts.v1` exports per-invocation entity counts and internal loop sites using
the shared count analyzer, also without initializer emission. These counts do not establish a bound
on how many callbacks a runtime registry iteration invokes; callers still need that context.
Missing/conflicting summaries and generic binding errors
remain explicit. Consumers do not yet close runtime-registry calls from it, and such calls remain incomplete. It does not
assign component IDs or invoke registrations/callbacks.

Constructor summaries carry `constructor-schema=2`. Entity counts, safety and weights traverse
these calls rather than treating disagreement with legacy constructor traversal as an intrinsic gap.
Missing or old constructor summaries remain incomplete. Explicit constructors also export instance
field/property/event initializer expressions in compilation/declaration order before the constructor CFG.
A `this(...)` delegating constructor does not repeat them. Each expression uses its own source file's
semantic model and initializer CFG, so unreachable branches are not counted. Older producers
without event-initializer coverage cannot certify constructor calls (including `this`/`base` calls).
Unavailable initializer CFGs remain explicit `InitializerControlFlowUnavailable` gaps;
unknown expressions remain gaps, and a missing operation is reported as
`ConstructorInitializerOperationUnavailable`. Implicit parameterless constructors of ordinary source
classes now export the same initializer summaries and a transitive call to the parameterless base
constructor (except the no-op System.Object constructor). Partial declarations export once. Record
synthesis and base constructors requiring optional-argument binding remain unsupported rather than
being assumed empty; missing external base summaries still prevent complete coverage.
Existing production
selection/parity checks remain in force; constructor support alone does not establish full coverage.

Ordinary method/constructor summaries now walk reachable Roslyn CFG blocks. Iterative SCC analysis marks
repeating blocks; loop acquisition/initialization outside a cycle is not classified as repeating. Exceptional
regions carry a marker consumed only by the independently versioned effect/count/synchronization
contracts described below; unmodeled edges remain gaps. Syntax-only fallback is explicitly
marked when no CFG can be obtained. Local-function and lambda CFGs are resolved through the enclosing
graph, so their declarations are not executed as part of the caller. A loop-context flag is
still NOT an execution count; branch/control-flow parity needs comparison with the legacy analyzer.
Getter/setter calls are exported for reads, assignments, compound updates and increments, including
expression-bodied properties/indexers and user-defined compound-assignment conversions. Setter targets
evaluate their receiver/indices once; ref-return assignments call the getter, not a setter. Coalesce writes
retain a conditional-write flag. Foreach exports compiler-selected GetEnumerator/MoveNext/Current/Dispose
and user-defined element conversions. Acquisition/disposal stay outside the repeating region; async and
deconstructing foreach retain explicit gaps. Events export add/remove calls; available auto-property backing
fields are exported. Local functions/lambdas have separate bodies with enclosing-method + lexical-ordinal
IDs (independent of paths/line numbers). Local function declarations add no calls; lambdas/method groups
add `method-ref` edges and retain DelegateBinding gaps, since a reference is not execution.
Deconstruction, implicit using/lock calls, unavailable backing fields and virtual/delegate/dynamic dispatch
still need further resolution. This layer
does not yet replace IL analysis or prove complete dependency coverage.
`MethodSummaryGraph` now consumes summaries from referenced assemblies and the current compilation.
Roots are actual implementations of job-interface Execute methods, not unrelated Execute overloads.
Instantiated reachability is exported as `ME.BECS.JobGraph.v2`: root ID, visited count, missing count,
unresolved count and up to 12 deterministic examples. Full assembly identity and declaration ID identify
definitions; instantiated argument lists distinguish graph nodes. Duplicate summaries are conflicts. Ignore
boundaries stop traversal. Generic receiver/method arguments are substituted through calls across assemblies,
including nested types and enclosing-method parameters. Open generic roots/unbound parameters remain gaps.
Expanding generic recursion has explicit node, depth and type-expression-size limits; reaching a limit is
never reported as complete coverage.

`JobEntitySummary` independently collects creation sites at the exact Ent.NewEnt_INTERNAL<T> contract.
It expands IgnoreVisited call sites, respects Ignore boundaries and carries caller loop context. Recursive,
unresolved or unsupported paths remain gaps. Legacy generic visited-identity differences are explicit gaps,
not assumed equivalent. `Compare Job Entity Counts` compares these provisional counts with fresh legacy IL
analysis, mapped through the existing entity-group IDs. It performs no cache writes or runtime registration.
Matching incomplete summaries do NOT prove coverage. Source counts are NOT used for runtime initialization.

`JobWeightSummary` computes provisional opsWeight from the same instantiated graph, starting with the
legacy interface-argument base weight. Call-site weight is counted before Ignore/visited body filtering;
IgnoreVisited expansion, recursive/generic identity differences and unresolved paths are tracked explicitly.
The producer emits `!weight` contracts for the exact BECS API families used by the legacy weight table and
marks summaries with `weight-schema=1`. `Compare Job Entity Counts and Weights` compares both metrics
against fresh legacy IL analysis. Neither provisional metric changes runtime initialization yet.

`JobSafetySummary` independently collects component access through the instantiated call graph. Operation
records may now end in `!` annotations carrying SafetyCheck, IRefOp and ignore/disable contracts, after the
structural type arguments; all consumers stop type parsing before these annotations. `safety-schema=3`
requires implicit component parameter contracts marked `!implicit`, including generic `T` resolved through its
constraint graph (Roslyn's `T.AllInterfaces` is empty). Older method summaries remain incomplete
instead of silently losing the argument access or its `isArg` marker when instantiated elsewhere.
Regression tests inspect the open summary and compare closed generic RW/explicit-RO jobs with the
transitional IL analysis without invoking jobs or bootstrap initialization. The explicit Safety report
includes sorted source-only/legacy-only records to expose the exact mode/argument mismatch; differences
do not veto a complete compiler-owned job contract.
Implicit parameter accesses are unioned with the body before explicit overrides: an `in T` parameter
cannot narrow a `Set<T>` access to ReadOnly. Repeated implicit parameters merge rather than conflict.
DisableContainerSafetyRestriction takes priority over access terminals, then normal call traversal respects
CodeGeneratorIgnore. Root RO/WO/RW parameter overrides are applied after the transitive union; RO+WO becomes
RW. Conflicting overrides, unknown access modes, generic component output, unresolved definitions/dispatch
remain explicit gaps. Production selects complete typed source catalogs; unavailable/incomplete
catalogs still require transitional IL analysis. Invalid/ambiguous metadata remains an export blocker.

`effect-union-schema=1` certifies enumeration of all Roslyn-reachable CFG blocks, including
catch/filter/finally regions without normal predecessor edges. The unordered safety union may
consume `ExceptionControlFlow` without a gap when this marker is present on that exact method summary.
Old imported summaries without the marker remain incomplete. Constructor coverage requires both the
body and instance initializer CFGs; called helpers and generic substitutions are still resolved normally.
Unknown callbacks, missing constructors/summaries and other dispatch gaps are not relaxed. Static job
weights also accept this marker: they sum reachable call-site costs, not dynamic execution counts.
Existing visited-method and IgnoreVisited behavior is unchanged. Scheduling modes and synchronization
need their own control-flow contracts; a complete effect set does not prove correct `Complete()` placement.

`exception-count-schema=1` separately certifies entity-reservation multiplicity in methods with
ordinary catches and finally regions. Every reachable protected block is conservatively connected to
all enclosing catch entries; exception types do not remove possible targets. A catch can throw to an
outer handler, not back into its sibling handlers. SCC analysis includes these exceptional transfers,
so retries through a catch are repeating while a catch surrounding a loop can remain one-shot.
Roslyn's `ControlFlowBranch.FinallyRegions` identifies bodies executed on normal
leave edges. A finally body repeats only when that edge connects blocks in the same cyclic SCC;
marking every leave from a looping block would incorrectly classify a finally surrounding the loop.
Exceptional edges also record finally regions unwound on their path to a handler. Thus a finally
throwing during a return can repeat when an outer catch retries the protected body.
Repeating outer finally bodies propagate their context to nested regions with a linear interval pass.
Normal loops inside a one-shot finally keep their own loop context. The analysis reserves an upper
bound per lexical call site, including repeated helper calls, not an exact path-dependent count.
Unknown callees still block source initializers. Filter multiplicity requires the separate proof below.
Exceptional graph construction has a bounded work budget; exhausting it produces no count proof.
The older `finally-count-schema=1` is accepted for imported producer summaries that certified only
methods without local catches/filters; new producers emit the extended marker. Missing/unsupported
count contracts are not silently trusted.
Constructor initialization graphs and bodies must both support the proof. The marker is exported
through method summaries and honored after cross-assembly/generic substitution. It does not certify
synchronization. Entity IDs, reservation layout, EntitiesJobMaxCount and runtime limit checks are unchanged.

`filter-count-schema=1` extends reservation multiplicity to exception filters without certifying
their execution order. Exceptional SCC edges include accepted handlers and rejected/throwing filters
continuing to later sibling or outer handlers. A filter surrounding a loop is not automatically a
loop site; handler retries and filters inside repeating finally bodies retain their actual CFG context.
First-pass search may evaluate the same filter again when cleanup throws, including cleanup in an
imported callee. A one-shot filter proof therefore also requires that its protected try has no reachable
finally or operation that can call other code. Explicit calls, properties, constructors, user operators,
formatting and static-field access invalidate that local proof. This is intentionally conservative:
even a helper that happens not to throw during cleanup requires a bound until cross-frame multiplicity
is proved. Unknown/deferred dispatch still produces a gap; the bound does not hide missing effects.
Potentially replayed filter creation uses the existing repeatable-site count and
`[EntitiesJobMaxCount(number)]` reservation. Catch bodies and outer cleanup are not marked repeatable
merely because their filter may repeat. The limit covers all creation in one Execute, not each filter
evaluation separately. Missing bounds remain visible in the source plan and the parallel-job guard
rejects them. No State fields, schedule-order counters, ID ordering or runtime reservation layout change.
Imported filter proofs must have exactly one supported marker and cannot also claim the older
catch/finally count contracts. Missing, future, duplicate or conflicting markers withhold the source
initializer. Weight/effect proofs remain independent. Metadata tests cover ordinary and generic jobs,
repeated/shared helpers, constructors, implicit calls and private-trimmed producer references.
The bounded graph work limit discards the entire proof on exhaustion. Ordered filter synchronization
still needs a separate cross-frame first-pass model; entity count coverage does not relax that gap.

`schedule-schema=4` recognizes job type parameters through their constraint graph as well. A generic
wrapper calling Unity's exact `Schedule<TJob>` carries a symbolic `!scheduled-job` and deferred-call
contract, which are substituted at each closed call site. User methods merely named Schedule are still
traversed and do not become scheduling terminals. Tests compare the selected job set for direct and
generic-wrapped calls and inspect the raw generic contract without executing any scheduled jobs.
The Unity scheduling terminal set includes ScheduleByRef, ScheduleParallel, ScheduleParallelByRef,
ScheduleBatch and ScheduleBatchByRef on the exact IJob/IJobParallelFor/IJobFor/Batch/Defer extension
types. Both deferred-list (two type arguments) and pointer-count overloads are supported. These calls
record the user job and stop the scheduled-job traversal at the Unity boundary; Run/RunByRef remain
synchronous calls and are not marked deferred. User methods with the same names remain ordinary call
edges. The independent reflection oracle uses the same terminal names. Tests compile against the
installed Unity API and validate all 18 scheduling overloads without scheduling or running any jobs.
Scheduled-job reachability also accepts certified effect-union-schema=1 exception summaries: it unions
jobs from try, filter, catch and finally, including closed generic helpers. This does not certify entity
counts, readonly-mode flow or synchronization on exceptional paths. Old/missing effect-union
schemas and unknown callbacks in exception regions still make discovery incomplete.

`query-scheduling-schema=1` adds a separate `!query-no-schedule` call-site contract for
the eight exact engine API/APIExt.Query overloads and audited builder configuration
operations (readonly, unsafe, parallel batch, steps, sorting and fence waiting).
Scheduled-job discovery can stop at those calls instead of inspecting query storage,
allocators and guard implementations. This is NOT `!ecs-leaf`: allocation, component
presence and completion/access effects still require their own domain analysis.
Dispose(handle), Schedule, ForEach, same-name user APIs and unknown factory overloads
are not covered. Argument/receiver calls, constructors and user wrappers remain edges;
jobs submitted there are retained, including closed generic nested jobs.
Imported proofs need exactly one current producer schema and one call-site marker;
missing, unsupported or duplicate proofs make discovery incomplete. Old catalogs
without the marker remain traversable rather than gaining a guessed exemption.
The query scheduling metadata fixtures never execute queries or submit jobs. Isolated
checks use the actual Unity API metadata and validate imported typed job selections,
generic substitutions and rejection of corrupted cross-assembly contracts.

`RefOpContractGenerator` exports compile-time-constant IRefOp.Op results as `ME.BECS.RefOp.v1`, including
custom implementations and references across asmdefs. Unknown getters are not executed or guessed by name.
Field-access analysis caches the contract per type within a method summary. The `Compare Job Safety` menu
compares normalized access modes and isArg against fresh legacy analysis without cache writes or running
jobs/registrations. Unlike source analysis, the legacy analyzer invokes IRefOp.Op getters; the menu requires
explicit confirmation and reports that distinction. Matching incomplete summaries does not establish parity.

Closed generic job analysis now instantiates one type parameter against public, closed unmanaged
components implementing its interface constraints, including jobs nested in generic systems.
The component assembly exports these specializations using imported method summaries; component-only
assemblies need no local method bodies. All constraints must pass implicit conversion checks.
Nested generic constraints use structural type reconstruction; multiple parameters remain unavailable, not assumed safe.
Closed metadata uses assembly-qualified job identities, and comparison searches across asmdefs;
conflicting duplicates are unavailable rather than selected by assembly load order.
Root RO/WO/RW overrides receive the same concrete type substitution as transitive calls.
Legacy job selection, system parallel mode, runtime allocation and safety are unchanged.
The bootstrap weight consumer selects source metadata only for an unambiguous closed job with zero
analysis gaps; incomplete/missing metadata uses legacy IL weights. Cached initialization has its opsWeight
line refreshed through the same selector: the old cache hashes only job scripts, not transitive methods
or source metadata. Selected weights are memoized per job during a generator run, never across reloads.
Comparisons always calculate the independent legacy baseline and do not call the selecting consumer.
The existing count/weight and safety comparison menus include these closed jobs; no parity is assumed.

`destroy-dispatch-schema=1` prevents SafetyCheck/CodeGeneratorIgnore terminals from hiding
runtime destroy callbacks. Exact engine `EntExt.Set/Remove<T>` and `Batches.Set/Remove<T>`
call records retain a symbolic `!destroy-component` before generic substitution;
`EntExt.Destroy` and the ignored type-ID batch variants carry `!destroy-registry`.
Safety diagnostics traverse the default IComponentDestroy interface implementation, including
explicit methods and closed generic jobs, and union writable component storage plus transitive
accesses. Count/weight diagnostics traverse the default body at ignored batch boundaries only,
without counting both the public wrapper and the batch callback; entity counts preserve call-site
multiplicity and loop context. Old summaries lacking the schema remain incomplete.

This is NOT a closed registry guarantee: RegisterAutoDestroyCallback is public and permits
replacement callbacks, and an entity-wide destroy can target components outside the job's asmdef.
These dispatches retain UnclosedDestroyRegistry gaps in safety, counts, weights, scheduled-job
discovery/modes and synchronization. Partial source catalogs/initializers must not be published as
complete, even if the default callback is empty or the input handle was completed. The transitional
IL fallback is still in place and is not evidence that it models those callbacks correctly. Closing
the manifest-selected registry, including custom registration provenance, remains migration work.

`DestroyRegistryAccessGenerator` exports potential typed registry references as
`ME.BECS.DestroyRegistryAccess.v1` and one `ME.BECS.DestroyRegistryCoverage.v1` marker per
analyzed assembly. Records bind the declaring member and lexical site to a component type,
access kind, status and (for a directly bound static callback) an encoded semantic call row.
Generic component/target bindings remain symbolic. Delegate parameters, escaped registration
method groups (including static field initializers), instance targets and arbitrary registry
storage accesses remain unresolved. `nameof` and same-name user APIs are excluded. This is
potential source reachability, not proof that a registration ran or that reflection cannot mutate it.

The manifest-level `DestroyRegistryAudit` combines these records with default registrations in
their manifest order. It flags missing coverage in typed engine-dependent assemblies, scans the
bootstrap's own input source (other generators' current outputs are not visible to it), and exports
`ME.BECS.DestroyRegistryAudit.v1`. Audited engine storage accesses are distinguished from custom
ones. `RuntimeRegistryNotSealed` deliberately keeps the result unclosed; neither this inventory nor
an empty custom-call list can enable production dispatch specialization. The existing DestroyCallbacks
report includes the raw access, coverage and audit records without invoking registrations or callbacks.

`ME.BECS.DestroyRegistrationEffects.v1` now binds each potential custom registration site to
independent target safety, entity-count and weight summaries. Exact static method-group targets
use the actual callback body, not the registered component's default Destroy implementation.
Both constructed declaring-type arguments and method arguments are retained, including across
asmdefs. Writable pointer storage contributes RW access to the registered component even for
an empty callback. The target summaries preserve transitive accesses, entity creation call-site
multiplicity, loop contexts and unresolved nested dispatches. They emit no runtime initializer.

Each record contains `v1`, the Base64 access record, binding status (`analyzed`/`unavailable`),
binding gap count, and Base64 `S`/`C`/`W` target summaries or `G` binding gaps. `analyzed` describes
target binding only: each nested summary has its own coverage count. Open registration contexts,
delegate parameters, escaped APIs, direct storage access, missing and ambiguous bodies remain
unavailable. A known callback target is not proof that its registration executed or remains active;
multiple sites are retained separately, never treated as runtime order or combined call counts.
DestroyCallbacks expands these payloads for inspection. Registry closure is still required before
using this information to replace the runtime-dispatch fallback.

Exact metadata-only external value contracts cover `Unity.Burst.Intrinsics.Common.Pause()`,
`SharedStatic<T>.Data` / `UnsafeDataPointer`, and the `string` constructors/implicit conversions
of `FixedString32/64/128/512/4096Bytes`. Audited against Burst `6bb9aca3ef38` and Collections
`aea9d3bd5e19`: CPU hints, address access and UTF16-to-inline-UTF8 copying do not invoke user
callbacks. The producer validates assembly, type and full method shape; source implementations
are traversed normally. Receiver/argument/conversion effects remain in the graph. A tracked
handle returned by an opaque leaf is still a synchronization gap, not a proven dependency.

FixedString32/64/128/512/4096Bytes also have exact contracts for their inline-buffer API:
IsEmpty, Length/Capacity getters and setters, the byte indexer, UTF8MaxLengthInBytes,
ElementAt, Clear, Add(in byte), GetUnsafePtr and TryResize(int, NativeArrayOptions).
The installed Collections implementation touches only the length/inline bytes; bounds checks
format built-in scalar values, and resizing clears the inline buffer without allocator dispatch.
Public visibility, method kind, static/ref/parameter/return shapes and assembly/type identity
are checked; source bodies and varargs do not receive the external contract. Other formatters,
constructors and generic extension bodies are not implicitly whitelisted. Receiver, index,
value and option expressions retain component accesses, entity counts and scheduled work.
Constrained IUTF8Bytes calls become leaves only after binding to the concrete audited method;
user implementations, including explicit ones, keep their own transitive effects. These contracts
are shared by source safety/counts/weights and synchronization, including imported summaries.
They neither complete a job nor change reservation/ID ordering or runtime string behavior.

Unity instrumentation has 37 exact external contracts, audited against Unity 6000.2.14f1's
CoreModule: six ProfilerMarker constructors (string or char* + length, with optional category
and category + flags), Begin/Begin(Object)/End, Auto, Handle and AutoScope.Dispose; two
ProfilerCategory constructors, its ushort conversion and 19 fixed built-in category getters;
JobsUtility.ThreadIndex, ThreadIndexCount and IsExecutingJob getters. The profiler IL only
stores native handles/category IDs and forwards to native profiling APIs. AutoScope's internal
constructor and Dispose start/end a sample when the handle is nonzero; they invoke no user
Dispose implementation. These contracts do not claim pure return values or deterministic
thread indices and do not alter runtime scheduling, simulation, instrumentation or entity IDs.

Assembly/type identity, exact signatures, accessibility, static/ref passing modes and source
versus metadata declarations are checked. Generic profiler payloads, arbitrary IDisposable
implementations, category Name/Color, worker configuration and other native profiling APIs are
not whitelisted. All receiver, argument and conversion effects stay in the method graph,
including transitive component writes, entity creation and scheduled work. Conditional
Begin/End calls and their arguments are omitted only when ENABLE_PROFILER is absent at the
call site. Concrete constrained Dispose dispatch still analyzes user implementations normally.
Source/imported summaries retain exact typed dependency unions, counts and weights, including
closed generic jobs. Instrumentation and job-state reads never prove handle completion.
The unordered effect/count analyses and ordered synchronization both support using/AutoScope
cleanup. The ordered proof follows normal and exceptional unwinding instead of dropping Dispose.
Metadata-only regressions cover both ENABLE_PROFILER profiles without executing native APIs.

Standard BCL exception construction has a separate, exact metadata contract. It covers 44
public constructor signatures on Exception, SystemException, ArgumentException,
ArgumentNullException, ArgumentOutOfRangeException, InvalidOperationException,
IndexOutOfRangeException, NotSupportedException, NotImplementedException,
NullReferenceException, ArithmeticException, OverflowException and ObjectDisposedException.
The Unity 6000.2 JIT/AOT core-library implementations and their transitive initializers only
store error fields (including Exception's static lock initialization); they do not call Message,
ToString or serialization on the supplied inner exception/actual value. Core assembly and exact
type/member/signature identities are required. Protected serialization constructors, unknown
overloads, virtual properties, formatting and user subclasses are not leaves. Argument evaluation,
user constructor field initializers and bodies retain their own effects, including through new T().
Tests include the actual BECS NotCreatedException, QueryBuilderException and TypeNotFoundException
helper bodies, plus closed generic jobs and imported summaries, without invoking exception/job code.
This does not exempt diagnostic branches from safety or certify arbitrary formatting callbacks.
`FunctionPointer.Invoke`, other fixed-string constructors/formatters
and unknown callbacks are not exempted. Runtime execution and registration are unchanged.

`BurstStorageContracts` covers the five exact SharedStatic factory signatures in
Burst `6bb9aca3ef38` (generic contexts, partial hash contexts and raw hashes), plus
generic BurstRuntime GetHashCode32/64. Context types are metadata, not constructed
objects: their constructors, static initializers, GetHashCode and ToString are not
invoked. The two reflective SharedStatic overloads and two reflective hash overloads
are covered only when every Type argument is directly proved to be typeof(...),
including built-in casts. Arbitrary Type values, user-returned Type instances and
function-pointer dispatch remain incomplete. Actual arguments, including alignment
helpers that create entities or schedule work, keep their ordinary effects and order.

SharedStatic.Data exports a conservative RW dependency when its escaped writable
reference is a component, including after generic substitution and metadata import.
Older/malformed access rows without that typed contract cannot certify safety.
The pointer getter and storage acquisition alone do not invent a T-value access.
Shared JobHandle reads remain opaque; storage acquisition and type hashing are not
completion primitives. Metadata-only regression tests exercise source selection,
exact typed unions, entity multiplicity, existing unique-helper weight semantics,
cross-assembly contracts and synchronization without invoking native storage APIs.

Native memory contracts cover 20 exact public static `UnsafeUtility` signatures in
Unity 6000.2.14f1's `UnityEngine.CoreModule`: eight byte-memory operations, generic
SizeOf/AlignOf, AddressOf/As/AsRef/ArrayElementAsRef, four typed array reads/writes,
and CopyPtrToStructure/CopyStructureToPtr. Their installed IL/XML contracts were
audited: pointer/value copies and native byte operations do not dispatch user code.
Assembly, owner, signature, passing modes, pointer types and relevant value-type
constraints are checked; source declarations and varargs are not exempted.
Malloc/Free, reflection-based SizeOf(Type)/GetFieldOffset, GC pinning and other
UnsafeUtility members remain outside this contract.

Argument expressions remain ordinary calls, including entity creation and weights.
`!native-memory-access` records the data access after all arguments have run, so a
later argument scheduling work cannot be hidden by an earlier address acquisition.
Typed native operations additionally export `!native-component-read`/`write` type
expressions. Once generic arguments are bound, actual components enter the safety
union (RO for loads, conservative RW for writes/copies/escaping references, including
both sides of As); scalar and other non-component values do not create dependencies.
Pure layout queries do not imply a component access. Tracked handles read/copied
through native storage remain opaque rather than inventing completed fences. These
contracts do not validate arbitrary pointer lifetimes, bounds or aliasing, and do
not change runtime memory operations. Exact contract tests use installed metadata;
source/imported lookalikes and full consumer tests never execute native memory calls.

`UnityContainerContracts` covers audited non-allocating operations in Collections
`aea9d3bd5e19`: NativeList/UnsafeList index reads/writes, ElementAt, AddNoResize,
AddRangeNoResize, removals, header getters and Clear; NativeHashMap/UnsafeHashMap
header getters and Clear; and UnsafeList's borrowed-pointer constructor. Signatures
and exact metadata owners are checked, not method names alone. Typed element accesses
reuse the native component read/write contracts; header access never invents an element
dependency. Pointer-backed header operations and element loads/stores retain their
ordered memory access, while inline headers and a borrowed buffer constructor do not
dereference element storage. Handles read from container storage remain opaque.

Allocating constructors of those four containers additionally require a call-site
proof of a built-in allocator: a constant named Allocator value passed through the
exact implicit conversion, or the exact readonly AllocatorManager Temp/TempJob/
Persistent/AudioKernel field. `BuiltinAllocatorProof` also follows static factory/getter
chains with a single returned expression and publishes `allocator-result=builtin-v1`
in their method summaries. This carries e.g. Cuts.ALLOCATOR's constant Domain value
across assembly boundaries in the Unity 2023+ profile. It is a value fact, NOT a purity
annotation: every getter/factory body, receiver, argument and conversion is still
analyzed normally. Duplicate/future result contracts, recursive chains, arbitrary
readonly fields, mutable storage and unproved runtime choices are not accepted.
Per-compilation caches avoid rescanning factory metadata, without retaining compilations.
These values dispatch through TryLegacy rather than the mutable user allocator
registry. A general handle/enum parameter, mutable field, unproved factory result or
user allocator ID is not a proof. The constructor contract survives
source-summary import and generic substitution; all capacity/allocator/initializer
expressions are still analyzed normally and in order. Construction does not invoke
key Equals/GetHashCode. Construction alone does NOT establish allocator provenance
for later operations: local lists require the separate whole-local proof below;
fields, aliases and map growth still need additional coverage. Dispose(JobHandle) is not a completion
primitive. No runtime collection code, allocation, deterministic ordering or Temp
disposal behavior is changed. Metadata tests cover current Unity signatures, source
selection, exact typed unions, counts/weights and ordered synchronization without
executing any native code.

The same call-site proof covers the exact by-value AllocatorManager Allocate/Free
overloads (typed and untyped) and UnsafeUtility Malloc/Free. Generic ref-IAllocator
dispatch remains ordinary callback analysis, not a built-in allocation leaf. Free
retains ordered native-memory access after its arguments and RW component dependencies
for typed pointers. Allocation alone neither reads a T component nor establishes a
completed JobHandle. Allocator-value facts are not propagated through mutable local
or field storage; public/custom handles remain separate work.

`LocalContainerAllocatorProof` certifies a whole-local allocator invariant for
NativeList/UnsafeList. Every assignment must directly construct a list with a
proved built-in allocator and every use must preserve that allocator (or reset it
to Invalid on disposal). Supported growing calls are Add, AddReplicate, AddRange,
Resize, ResizeUninitialized, SetCapacity and TrimExcess, plus Length/Capacity setters
and synchronous Dispose. Exact package signatures are checked. These operations
copy plain element bytes without calling T; no hash-map callback is waived.

The proof rejects header/Allocator access, copies, returns, ref aliases, field or
pointer escapes, captures and unknown methods for the entire local, including calls
before an eventual escape. Fields, parameters, returned containers and borrowed
buffers are not certified. All assignments are examined across branches, loops and
exception regions, not just whichever constructor was visited last. Analysis limits
invalidate the whole proof. Lowered receiver captures retain the same local identity;
implicit using cleanup resolves the exact concrete Dispose implementation. This is
an allocator provenance proof, not validation of bounds, alias lifetime, disposal
timing or other unsafe code. Dispose(JobHandle) still requires separate scheduling
and lifetime analysis. No runtime calls or Temp disposal behavior are changed.

The call-site proof is exported in both effect rows and ordered synchronization.
Arguments retain their ECS effects, inline/repeated entity counts and existing
unique-helper weight semantics. Growing/freeing and setters retain memory access
AFTER argument evaluation and RW element-component dependencies after generic
substitution. Late argument scheduling cannot be erased by an earlier Complete.
Tests use actual package metadata, imported generic summaries and production readers
without running collection/job bodies or native code.

`NativeHashMapMethodSummaries` projects non-allocating ContainsKey, TryGetValue,
Remove and indexer reads for NativeHashMap/UnsafeHashMap, including the applicable
ReadOnly APIs. These are call graphs, not container leaves. GetHashCode resolves the
concrete override of the object virtual slot; a same-named hiding method is not that
override. Equals resolves the actual IEquatable<TKey> implementation, including
private explicit implementations imported through source summaries and nested generic
key types. Source-exported package methods take precedence over these projections.
The default ValueType hash implementation remains unresolved; it is not assumed pure.

Collision traversal contributes repeated Equals calls, preserving entity-creation
loop context and requiring the ordinary EntitiesJobMaxCount bound when appropriate.
Hashing contributes one possible call per lookup. Checked indexer failures may format
the key once; this follows the real formatting contract, not an arbitrary ToString
method. UnsafeHashMap.ReadOnly's getter returns default without formatting. Missing
formatting coverage (including unsupported span formatting) stays an explicit gap.
Package-check branches are independent of the consuming asmdef's defines.

The ordered synchronization projection includes header/bucket/key/value accesses,
callback scheduling, collision retries and the formatted throwing path separately
from the unchecked default-return path. It never assumes callback-created work is
complete. Handles loaded from values remain opaque. Typed key/value loads contribute
RO dependencies; callback bodies retain their own accesses and scheduled jobs.
Unknown delegate callbacks, mutable allocator dispatch and allocating/growing map
operations are not waived by the lookup contracts. Metadata-only tests cover source and private-trimmed
cross-assembly consumers without executing collections, callbacks or jobs.

`NativeHashMapGrowthSummaries` extends the whole-local allocator proof to exact
NativeHashMap/UnsafeHashMap TryAdd, Add, indexer setters, Capacity setters,
TrimExcess and synchronous Dispose. A versioned call-site fact selects a separate
built-in-allocator projection; it never marks growing map calls as ECS leaves or
globally certifies fields/default maps/custom allocators. Real package summaries
still take precedence. Missing/future/duplicate facts and mismatched signatures
cannot publish complete safety/count/weight initializers.
Boolean key/value arguments can carry independent schedule-value facts. The map
contract validates their ordinals, actual bool parameter types and value grammar
without confusing them with allocator provenance or removing callback analysis.

The projection follows the actual object.GetHashCode and IEquatable<TKey>.Equals
targets for both incoming and stored keys. ResizeExact reinserts old keys via
TryAdd, so both its hashing phases and collision comparisons contribute loop
creation counts. A setter includes its preliminary Find in addition to insertion;
Add includes checked duplicate-key formatting. These are possible call-site
reservation counts, not a prediction of the number of runtime collisions. Existing
EntitiesJobMaxCount and unique-helper weight semantics remain unchanged. Dispose
does not invoke key callbacks. RW key/value component memory effects survive closed
generic substitution, including imported and private explicit callback bodies.

Synchronization models optional/repeated callback phases, native access before and
after them, and Add's formatted throwing branch. It conservatively overapproximates
the package's conditional traversal; a callback or argument that schedules work
cannot be erased by an earlier completion. Using/finally cleanup also observes work
submitted on a callback's exceptional path before Complete. An out discard gets
callee output storage without reading a nonexistent caller value. Unknown callback
targets and tracked native handles remain gaps. Formatter coverage is independent:
e.g. scalar Add/indexer reads can still require span-formatting support in a BCL
that implements ISpanFormattable. No formatter is assumed pure to make a map pass.

`NativeMapEnumerationContracts` covers the exact NativeHashMap/UnsafeHashMap
enumerators (including ReadOnly views), MoveNext/Reset/Dispose/Current and KVPair
Key/Value/GetKeyValue. Bucket traversal does not invoke key hashing/equality or
formatters, and obtaining a pair does not read its elements. Key/GetKeyValue loads
retain typed RO dependencies; Value returns a mutable reference and retains RW.
Native accesses remain ordered synchronization events. Handles read from native
values remain opaque; this contract cannot prove their completion or ownership.
Interface GetEnumerator overloads and allocating key/value array APIs are not
covered merely because they share a name with an audited operation.

`ContainerBorrowProof` exports `native-container-borrow=v1:<parameter ordinals>`
for helpers that preserve a container header's allocator provenance. This is NOT
an ECS leaf or a claim that the helper only reads elements. Helper bodies, key
callbacks, entity creation and scheduled work are analyzed independently. Source
and imported helper chains support by-value/in/ref parameters and generic maps
or lists without erasing their effects. Rebinding a parameter, retaining it in a
field/return/closure/iterator, copying its header, passing it to an unknown target
or exposing it through an unaudited API prevents that proof. A foreach is accepted
only while its KVPair local is used through the audited element accessors; copying,
capturing or leaking the pair would expose its retained header pointer.

The proof walks a bounded graph of method/parameter uses. Every reachable node
must preserve provenance, including cycles; effect/count recursion is a separate
analysis. A compilation-local locked cache stores structural nodes, not partial
successes. Traversal charges cached nodes too, so a previously warmed helper suffix
cannot change a limit result. Missing/future/duplicate/malformed or contradictory
imported facts fail closed. Cancellation cannot install a partially visited node.
The local allocator invariant now survives proven helper borrowing and non-escaping
foreach loops, including calls after native allocation/growth and before Dispose.
Tests cover the current InventoryAspect.HasEnoughResources body structurally, not
its runtime behavior or a complete safety result for all of its transitive calls.

`container-allocator-schema=1` adds call-specific interprocedural allocator facts.
`!container-allocator-arguments=v1:ordinal=b|ordinal=pN` binds a proven local builtin
allocator or forwards a header-preserving caller parameter. Growing/freeing native
calls on such parameters carry `!container-allocator-parameter=v1:N`, not a leaf.
The importing analysis validates the exact helper, borrowing contract, parameter
ordinals and forwarded generic types before producing run-local method instances.
Unknown and builtin calls never share a coverage cache entry. Both ordinary effect
rows and ordered synchronization symbols are instantiated identically; CLR generic
environments remain unchanged, and synthetic instances are not exported as roots.

Native list allocations use the existing typed memory contracts; native map growth
still traverses hash/equality/rehash/formatting callbacks. Entity counts retain each
call and loop context. Static helper-body weights are charged once per actual CLR
generic context, not again per allocator instance, while each instance is checked
for missing effects. Unsupported/malformed facts and bounded-instance exhaustion
remain explicit gaps. Container fields, copied/escaped headers, custom/unknown
allocators and deferred Dispose(JobHandle) still require further provenance/lifetime
work. This does not change runtime maps, iteration order, Temp cleanup or simulation.

Allocator value contracts cover `AllocatorHelper<T>.Allocator` (stored pointer to ref),
`AllocatorHandle.Value`, `ToAllocator`, `IsCustomAllocator`, the Handle getter/setter,
the implicit Allocator-to-handle conversion and `AllocatorManager.ConvertToAllocatorHandle`.
The two conversions retain their different version semantics; neither proves a built-in
allocator. These exact Unity.Collections members only expose/copy fields, not invoke T.
Caller expressions and user-defined getters remain visible. Allocation/free, Try, helper
construction and disposal are NOT value contracts: the pinned package can invoke a custom
`IAllocator.Handle`, `Try`, Dispose or a registered function pointer/delegate. Such paths
keep coverage incomplete until their actual target effects can be resolved. Ref access
also does not prove synchronization; before/after-completion tests exercise that boundary.

Metadata-only `AllocatorManager.Allocate<T>(ref T,int,int,int)` now has an audited
semantic call projection (Collections `aea9d3bd5e19`), not a leaf exemption. It visits
the concrete allocator's Handle getter and Try(ref Block) through the interface map,
including explicit implementations, closed/nested generic substitution and imported
source summaries. Existing package source summaries take precedence. Safety and
scheduled-job discovery retain the union of both callbacks' effects; unrelated
same-named methods and Dispose are not invoked by this entry point.

Try contributes one entity-creation call per allocation; caller multiplicity and
loop contexts survive traversal caching. Unity's conditional CheckValid can invoke
Handle an additional time, so a getter that creates entities retains an explicit
`AllocatorGetterCreationMultiplicity` gap and receives no count initializer. A
weight-bearing getter likewise cannot certify its uncertain multiplicity. Numeric
AllocatorHandle and typed-memory Allocate overloads forward to the handle's Try;
its mutable function/delegate registry remains `UnclosedAllocatorRegistry`, never
an assumed built-in or historically registered target. Unknown callback bodies
also remain incomplete. Ordered synchronization projections explore both one-getter
and two-getter paths before Try, preserve borrowed allocator accesses and retain
work scheduled by either callback. Completing the input before allocation does not
complete new work launched inside the allocator. Actual callback summaries supply
their own completion/ordering effects; imported explicit/generic callbacks use the
same interface binding as safety. The three public Free overloads now lead to the
same registry dispatch, with the null-pointer bypass represented in synchronization.
They are not leaves, and an opaque handle still cannot certify callback coverage.
Helper construction/disposal and registry target closure require further migration.

`generic-construction-schema=1` records explicit handling of `new T()`. Its symbolic
construction row is resolved after substitution: implicit value-type construction
is zero initialization (including default JobHandle), while public parameterless
user constructors retain body/initializer effects, call counts, weights and scheduled
jobs. User-defined parameterless struct constructors are not default initialization.
Older/missing schema producers cannot certify effect catalogs. Instance-constructor synchronization
now composes initializer CFGs, the base/this call and the body in C# evaluation order. Each CFG
has separate capture/local slots while constructor parameters preserve caller aliases. Initializers
run only in the terminal constructor of a `this(...)` chain; arguments to `this` run before them,
but arguments to `base` run after them. Implicit class constructors compose the same initializer
graphs and the real parameterless base call. Get-only auto-property initializers write their
backing field without inventing a setter invocation. Branches and pending jobs survive composition
and imported generic summaries. A late Complete cannot retroactively protect an earlier access.
Exception flow, tracked field storage and unresolved constructor bodies remain explicit gaps.
Types with static constructors/initializers retain `ConstructorTypeInitializer` in effects and
synchronization: the once-only runtime state is not inferred from an instance constructor CFG.
Static-constructor synchronization itself remains unsupported.

Auto-property accessors export synchronization programs for their backing storage.
The typed `O` event defers classification until generic arguments are bound: ordinary
data is modeled, but JobHandle/SystemContext/query storage remains `FieldStorage`,
never an invented empty/completed handle. Component-owner and IRefOp storage accesses
are retained. This supports ordinary/AllocatorHandle auto-properties without hiding
tracked handles behind `GenericProperty<T>` in another asmdef.

The five exact public `CombineDependencies` overloads in Unity 6000.2.14f1's
`UnityEngine.CoreModule` also have leaf contracts: two/three `JobHandle` values,
`NativeArray<JobHandle>`, `NativeSlice<JobHandle>`, and the unsafe utility's
`JobHandle*` plus `int` overload. They combine fences, not user job bodies. Argument
effects and jobs scheduled while evaluating arguments remain in source summaries.
Only the by-value handle overloads propagate a known token union for synchronization;
arrays/slices/pointers remain opaque and cannot prove input completion. A combine
without `Complete()` never clears pending work. `Complete`, `IsCompleted`,
`ScheduleBatchedJobs`, user lookalikes and other overload shapes are not leaf contracts.

`job-control-schema=1` gives those control operations a separate **system-scope**
contract, not `!ecs-leaf`: instance `Complete()`, the two/three-ref and NativeArray
`CompleteAll` overloads, `IsCompleted`, and `ScheduleBatchedJobs`. Exact metadata
assembly/type/member shapes are required. System discovery lists jobs submitted by
the lifecycle; direct-access summaries exclude deferred Execute bodies, regardless
of whether Unity runs them on a worker or during a wait. The system dependency union
still requires the safety summary of every submitted job. Incoming work remains the
dependency of its submitting system; waiting does not transfer its access ownership.
Receiver/argument effects, user wrappers and unknown delegates are still traversed.
Missing/newer control schema versions keep imported effect catalogs incomplete.

Synchronization is independent: Complete consumes only the known receiver's tokens;
CompleteAll(ref ...) unions inputs before resetting their cells (including aliasing).
Polling/flushing never proves completion. NativeArray completion retains an explicit
opaque-input gap. Job-root safety, entity counts and weights do not consume this
system-scope exemption. Tests use actual Unity handle metadata, not an empty Complete
implementation, and check generic helper imports and stale producer contracts.

Direct `typeof(T).Name`, `FullName` and `AssemblyQualifiedName` reads have a call-site
`!typeof-metadata` leaf contract in both effect and synchronization summaries. The exact
core-library string getter and direct `typeof` receiver are required; built-in receiver
conversions are allowed. This does not construct T or execute its type initializer.
The contract survives generic substitution and cross-assembly imports. It does NOT apply
to arbitrary `Type` values, including dictionary entries, locals, factories, `object.GetType`,
user conversions or other reflection APIs: a user Type subclass can override virtual getters.
The CLR metadata path was inspected on host Mono, not validated by Unity runtime execution.

Synchronization analysis evaluates a discard assignment's RHS without allocating a storage
slot. Discarding a scheduled JobHandle does not discard its pending work; completing only
the old input handle remains unproven. An actual local named `_` is still tracked normally.

`implicit-formatting-schema=1` records that implicit formatting was checked. Interpolation,
built-in string concatenation and string compound assignment can invoke user `ToString` or
`IFormattable` implementations without an explicit invocation operation. A versioned symbolic
`implicit-formatting=1:concat|format` call now retains the receiver through generic substitution
and cross-assembly imports. Closed structs/nullable values and sealed classes resolve the exact
virtual Object.ToString slot for concatenation (never a `new` hiding method). Eager String.Format
interpolation instead prefers the exact BCL IFormattable interface map, including private explicit
implementations exported in source metadata. Arguments and user conversions keep their own effects;
numeric conversions are not unwrapped as boxing. Repeated and loop calls contribute independent
entity reservations, and a formatter can contribute scheduled jobs as well as component accesses.

FormattableString/IFormattable-targeted interpolation stores arguments without invoking formatting;
their argument expressions still execute. Plain strings and constant null require no user formatter.
The exact String/Object/ValueType default ToString bodies are metadata/value leaves only AFTER concrete
virtual dispatch. Unknown object/interface/non-sealed receivers remain incomplete. Numeric/enum library
formatting is not a blanket leaf; unmodeled span formatting and interpolated-string handlers remain explicit gaps.
The lowering and core formatter paths were inspected using Roslyn and Unity JIT/AOT core metadata,
not Unity runtime execution. Unordered formatting edges do NOT certify synchronization order. The separate
ordered program emits a `T` formatting event referencing the same versioned receiver contract. Concat
evaluates and formats each operand before evaluating the next; eager interpolation evaluates all argument
expressions first, then invokes formatters in their order. Compound assignment preserves getter/receiver/
index evaluation before the RHS, and the setter/store after formatting. Receiver values are snapshots,
not borrowed aliases to generic handle storage. Deferred interpolation has argument effects only.
Nullable and reference receivers explore both skipped and executed formatting paths. Equivalent handle-
coverage states are merged between formatting events to avoid 2^n growth inside a single CFG block;
traversal size and cancellation guards still apply. Future formatting contracts remain incomplete, and
older ordered-program parsers reject the unknown `T` event instead of silently dropping it. Unknown
unordered summary versions and ordered-flow versions are validated independently. Legacy fallback is
not proof of these callbacks or their evaluation order.

`BclFormattingContracts` covers exact core-library Boolean/Char/Guid ToString
overloads and numeric ToString calls with a direct CultureInfo.InvariantCulture or
NumberFormatInfo.InvariantInfo provider. Only the provider fact is emitted at the
call site; purity is selected after resolving the exact numeric target, including
constrained IFormattable calls. A user IFormattable implementation retains its body
even with the same provider. Receiver, format and provider argument expressions
still contribute component access, entity creation, weights and scheduled work.

Unity 6000.2.14f1 JIT/AOT core paths were inspected: Boolean/Char/Guid do not invoke
user format providers. Guid's internal ISpanFormattable path and IFormattable
fallback are both callback-free, so that exact type is not blocked by the generic
span-formatting gap. This does not cover arbitrary span formatters. Numeric
formatting with a null/omitted provider can invoke virtual CultureInfo.GetFormat
through CurrentCulture, so ambient interpolation/ToString remains incomplete.
Mutable locals, fields, provider factories and unknown providers are not promoted
to invariant facts. Missing/future/duplicate facts and incorrect target signatures
cannot publish complete catalogs. Tests include private-trimmed cross-assembly
imports, custom constrained formatters, bool-key maps and ordered argument effects;
no formatter, provider callback, native container or Unity job is executed.

Strongly typed instance Equals/CompareTo on built-in Boolean, Char, integer, Single and Double
types are explicit scalar-comparison terminals. Their receiver and argument expressions are still
analyzed; only the scalar method body has no ECS dependencies/creation/API weight. Object overloads,
strings, enums, arbitrary IEquatable implementations and generic comparers are not whitelisted.
The same contract applies after exact constrained-interface resolution to one of these methods.

Immediately invoked delegate-creation expressions resolve their lambda/method-group target directly.
The bound receiver and invocation arguments are analyzed before the target body; virtual targets
retain a dispatch gap. Only built-in conversions are unwrapped. CFG temporary captures also resolve
when creation precedes invocation in the same basic block; capture bindings are cleared between blocks,
and the bound receiver is not reevaluated. Other uses/escapes retain binding diagnostics.
Delegates read from fields, locals, parameters, other blocks or callback registries still require
separate binding/dataflow analysis. Counts and weights never execute a method-reference row; resolved
invocations have their own call row. Unresolved callbacks remain incomplete rather than zero cost.

`ME.BECS/Source Generator/Inspect Semantic Job Graphs` displays root-specific gaps without invoking code;
the normal comparison includes totals. Missing external/BCL summaries are not silently considered safe.
Comparison reports metadata volume/format issues separately from legacy coverage. Next: resolve implicit
calls, resolve delegate bindings and compare computed job metadata before switching
its consumers. No raw operation sequence is currently treated as a complete execution graph.

1. DLL and RoslynAnalyzer metadata are installed, with normal plugin compatibility disabled.
   Verify analyzer loading for all relevant asmdefs and both editor/player compilations in Unity.
2. Compare the catalog with GetUsedObjects separately for editor and runtime. Keep the full editor type
   discovery and runtime asset/usage filtering, including explicit includes and generic specializations.
3. Implemented: the editor bootstrap emitter calls an individual generated registration method at the exact
   position where it previously emitted Validate/SetDefaultValue. Group setup, AOT and all other phases remain.
   Unsupported types fall back to legacy emission. Never register an entire assembly in one step:
   global type order can interleave multiple assemblies.
4. Verify equality of component/group/aspect/system IDs and default values with the previous bootstrap,
   including domain reload, player builds, fixed-point configuration and test-only components.
5. Only then remove redundant legacy emission and migrate the remaining registration phases.

## System lifecycle access analysis

System lifecycle summaries now also export `ME.BECS.SystemDirectAccess.v1`:
system identity, lifecycle method identity, gap count, component access rows,
system-pointer dependencies (`Y`, exact closed assembly-qualified type), query-presence
dependencies (`Q`, filter kind and closed component type), root
binding (`R`, a generated MethodInfo getter), and explicit gaps. Closed generic
lifecycle roots retain their specialization identity. The Scheduled Jobs report
checks these bindings against interface maps without executing lifecycle methods.
`query-filter-schema=1` identifies exact instance APIs on QueryBuilder
and QueryCompose. With/WithAll, Without and every WithAny argument are retained separately
from component data accesses; TNull in an odd-sized WithAny group is a sentinel, not a dependency.
Source-only helpers are traversed and generic component arguments are substituted per call.
An unrelated method with the same name is not a query contract.

WithAspect expands only QueryWith fields, including non-public fields. The declaring compilation
exports `ME.BECS.AspectQuery.v1` so consumers of a referenced assembly do not mistake an incomplete
Roslyn private-field view for an empty filter. Missing/conflicting catalogs remain explicit gaps.
Tests cover local private fields, imported TransformAspect, static/dynamic queries, generic systems,
malformed report rows, and all WithAny arguments against the corrected transitional IL analysis.

This describes direct call-graph accesses and query presence, not the union of scheduled jobs,
or a proof of `dependsOn.Complete()` placement.
It does not initialize job metadata and does not yet replace the
Editor system dependency analyzer. The next integration must combine these
distinct contracts and validate coverage before switching consumers.

`schedule-mode-schema=1` records the query value at each schedule call, using a CFG fixed point
for locals, copies, assignments, branches, loops, catch/filter and try/finally (including lowered using).
Normal leave/return continuations and exceptional unwinds have distinct fixed-point inputs:
an exception entering cleanup never flows into code following the try/finally. Nested cleanup
resumes its parent continuation; a new throw cancels the pending leave/return and visits only
lexical outer handlers. Potential call exceptions observe argument effects and both pre/post
receiver or ref mutations; unknown aliases remain unknown. A call site's annotations union
all converged incoming continuations. No partial annotations are published on traversal limits.
Methods without exception handlers keep the array-based fixed point, without handler/continuation tables.
Nested function CFGs stop handler lookup at their own root rather than importing the caller's
block ordinals. The same boundary is enforced by the ordered synchronization producer.
`AsReadonly()` mutates its actual struct
receiver; calling it on a fluent-return temporary or an `in` parameter does not mutate the original.
Factory and fluent contracts match exact BECS symbols, not user method names. Omitted Conditional
calls have no effects. Alias escapes and unsupported factories remain
unknown rather than silently proving read-only. Symbolic `pN` values and per-call argument modes
allow generic helpers to be visited separately for each incoming mode.

`ME.BECS.SystemScheduleModes.v1` exports these instantiated modes separately from discovery and
direct accesses: `S` rows contain mode and closed assembly-qualified job identity. `0` is normal,
`1` is read-only, `0|1` is a union requiring normal write dependencies, and `?` requires an explicit
gap. The Scheduled Jobs report validates and shows these rows without invoking lifecycle methods.
Tests cover independent queries, repeated schedules, copies/resets, fluent temporaries, in parameters,
conditional/loop joins, ref escapes, helper call contexts and closed generic jobs. Finally tests
also cover return/break/continue, throwing nested cleanup, normal versus exceptional values,
argument-before-call ordering, symbolic modes, nested local functions and exact typed selection
without IL. Full dependency
selection combines this coverage with direct access, query filters and job safety. Synchronization
is a separate contract: a complete dependency union alone does not prove Complete() control flow.

For catch dispatch, `ExceptionalFlowRoutes` exports lexical handler alternatives and their
intervening finally regions. The query interpreter propagates the state at each potentially
throwing operation through cleanup before entering a catch. Handler type matching is conservative:
all enclosing catches and the escaping alternative are considered, not guessed from a method name
or one observed exception type. `throw`/`rethrow` inside a handler cannot enter sibling catches.
Catches inside an already running finally preserve that finally's pending return/leave. Pending
continuations are discarded only on actually leaving their region: another exception raised while
unwinding can still be caught within the enclosing cleanup. Nested function graphs never reuse
caller ordinals; reservation-bound catch edges enforce the same root boundary.
Tests cover typed sibling catches, loops/reentry, ref escapes, symbolic helper values, nested cleanup,
rethrows and source/imported generic jobs. Catch synchronization uses the separately versioned
handler protocol described below. Complete dependency unions remain selectable even when another
synchronization gap requires its IL fallback; fallback dependencies cannot overwrite the source
union.

For query values, filter-enabled `ExceptionalFlowRoutes` adds an ordered first-pass search.
Each filter executes before intervening finally regions; false or throwing filters pass their
modified state to later candidates. Type mismatches also retain a separate skip alternative.
Only selecting a catch or exhausting the search starts cleanup. A new exception during cleanup
starts a new search, so a filter call site can observe both pre-cleanup and post-mutation values.
Search identity includes its origin, candidate index and pending cleanup continuation; loops
merge states only for the same continuation. A filter inside an active finally does not discard
that finally's return/leave prematurely. Ref/delegate escapes still yield unknown modes.
Source and trimmed-reference tests cover filter/cleanup order, rejected and throwing filters,
siblings, rethrows, repeated search after throwing cleanup, nested functions and generic jobs.
The first-pass rules follow [C# exception handling](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/language-specification/statements#1311-the-try-statement).
The traversal budget includes handler candidate visits, so rejecting a long chain cannot cause
unbounded quadratic search work. Exhaustion discards all annotations, including earlier call sites.
This extends the existing value-annotation contract: old producers without filter analysis retain
their unknown modes; consumers never infer a new proof from the presence of a catch/filter alone.
The retained source JobHandle oracle still rejects filters; the independent IL synchronization
analysis now models cross-frame first-pass callbacks as described at the top of this document.
Entity counts use the independent one-shot/runtime-bound proof above. The default
route-builder mode therefore still rejects filters for consumers without first-pass support.

`ME.BECS.SystemDependencies.v1` is the compiler-owned union per lifecycle root. It combines
direct component accesses and presence filters with each closed job's safety summary,
instantiated through the actual Execute interface implementation. Readonly scheduling narrows
only component-argument contracts: independent body accesses (including writes to the same
component type) remain in the union. Mixed normal/readonly schedules include both access sets.
For imported jobs, compiler-created in/modreq forwarders or stripped private explicit Execute
methods are resolved through the source-exported exact interface map, including closed generic
owners. A same-named overload is never a substitute; missing/conflicting maps or multiple distinct
Execute targets retain incomplete coverage. Source and imported readonly argument analyses use
the same bound body and preserve independent component writes.
The generated Schedule adapters' implicit With/WithAspect presence filters are included too;
an unused aspect still requires its QueryWith fields, while optional fields are not filters.
The six sequential/parallel job markers are resolved from `ME.BECS`, not `ME.BECS.Jobs`.
Missing marker contracts make coverage incomplete instead of silently omitting presence reads.
Job safety analyses are cached per closed job and argument mode within one compilation.

Complete dependency unions emit a `SystemDependencyPlan_*` typeof/byte catalog and the exact
lifecycle root binding. The run-local Editor reader validates every component, mode, explicit
system dependency and binding against the metadata, rejects incomplete/corrupt/duplicate plans,
and returns independent sets under a lock. It performs no IL analysis and invokes no lifecycle
or registration methods. The Scheduled Jobs report compares this union to the separate legacy IL oracle
without treating legacy output as authoritative; AsReadonly/body-write differences are expected
to require semantic review. Tests cover body writes, presence filters, generic and explicit
lifecycle roots, malformed catalogs and caller isolation. Production now selects this union whenever
the source contract is complete, independently of synchronization coverage. A legacy difference is
diagnostic information, not an automatic veto of compiler-owned contracts.

Raw method summaries now include `sync-flow-schema=3` and a Base64 `sync-flow` program.
This preserves the reachable CFG and evaluation order across assembly boundaries; a flat
transitive call set is insufficient for proving that the correct handle was completed on
every path. The program is analysis input, **not a successful synchronization proof**.
Production consumes the validated synchronization result below, not the raw program directly.

The `v3` program contains `M` return information; `I`/`P` receiver/parameter declarations;
`V` local/temporary slots; interned `S` call/field contracts (the same identities, generic
arguments and safety contracts as raw method summaries); explicit `G` gaps; and `B` blocks
with kind, condition kind and both successor IDs/semantics. Unreachable blocks retain inert
ordinal anchors, never their operations: region endpoints still resolve, and a leave targeting
unreachable code still executes its intervening finally. Events are `=` copies, `Z` zero
initialization, `?` unknown values, `C` calls, `F` field accesses, `A` borrowed-reference
accesses, and `R` return values. A store through a ref-return/borrowed lvalue is also recorded
after its RHS: acquiring Get<T>() before a schedule does not make a later write safe.
Call operands bind parameter ordinals independently of evaluation order and distinguish
value snapshots from ref/in/out storage. Readonly struct receivers use defensive copies;
in/ref extension arguments retain their storage. Roles H/C/Q represent JobHandle, SystemContext
and query builders; generic role T retains its portable type expression for call-site
substitution. Untracked U values have no handle slot. Type declarations must be instantiated
before an interpreter decides whether a generic value carries a dependency.

Unavailable bodies, exception filters,
ambiguous CFG location captures, ref locals, tracked ref returns/field storage, unresolved dispatch
and other unsupported operations remain explicit gaps. Conditional calls omitted by compilation
also omit their argument effects. Tests cover default versus entry-handle copies, one-sided
Complete branches, new schedules followed by old-handle completion, ref/in/out bindings,
named-argument order, generic handle forwarding, omission and explicit unsupported-flow records.
`ME.BECS.SystemSynchronization.v3` now interprets these programs across source helper methods
and referenced-assembly summaries. A strict parser checks declarations, contracts, call bindings,
operands and CFG edges before interpretation. Handle values carry covered-work tokens; outstanding
work is tracked separately. Completing a default or old handle cannot erase work added by a later
Schedule. CombineDependencies, context dependency setters/adders, query factories, WaitForAllJobs
and exact scheduling entry points have audited symbol-bound effects. Helpers retain value copies,
ref/in/out aliases, return values and closed generic type substitutions. CodeGeneratorIgnore is
not treated as a promise that a helper cannot mutate handles: unaudited helpers still need bodies.

`sync-exception-schema=1` extends the v3 flow with `Y` cleanup-region declarations,
`H` block-local lexical exception handlers, `X` possible built-in throw points and optional
finally-entry lists on successor edges. Older program readers reject these records. Current
readers require this contract from every helper traversed with exception capture enabled;
a pre-contract imported body cannot silently certify cleanup ordering. Unsupported protocol
versions, duplicate/unknown regions and invalid continuations remain gaps.

The additional `sync-catch-schema=1` contract declares `K first last` catch regions and
block-local `J target unwind` routes, with `-` for no intervening finally. Handlers are
resolved from the compiler CFG, not reconstructed from the flat effect union. A catch receives
an independent copy of the state at each modeled throw point, including changes made by
partially executed helpers. All lexical catch alternatives and the escaping route are explored;
exception-type matching never narrows the result. Throws inside one catch do not enter siblings.
Missing/future/duplicate catch protocol markers, unknown handler targets, self-handler edges,
duplicate routes and invalid cleanup lists cannot produce a complete synchronization result.
Old catch-free helpers remain valid without this additional marker.

The interpreter executes finally regions on normal leaves, return, break, continue and
exceptional exits. A continuation records the remaining cleanups and original destination;
an exception cancels that continuation only when it actually leaves the running cleanup.
A nested catch within the cleanup retains the pending return/leave, including when a second
exception interrupts an inner unwind. Exception routes visit only enclosing protected
regions, never the currently running finally again. Callee exception results retain pending
jobs and partial ref/out changes separately from normal return results. Possible exceptions
are conservatively modeled before/after calls and memory accesses, plus built-in throw points
such as conversions/arithmetic. A Schedule that may throw after creating work but before its
handle is assigned leaves uncovered pending work. Complete inside a try does not by itself
certify a component access in finally: it can throw before completion. No native callback is
executed by this analysis and no result is a guarantee about arbitrary asynchronous exceptions.

Return values are snapshots in real storage cells throughout cleanup, so nested helper state
normalization preserves their token identity even if finally changes the original ref parameter.
Generic constrained using/Dispose keeps its actual receiver instead of treating Roslyn's implicit
interface conversion as an escaping boxed handle. Unknown Dispose implementations remain gaps.
Unwinding participates in fixed-point state identity; limits cover traversal, state size and
cleanup nesting. Exception filters remain explicitly unsupported: they execute before unwinding,
so treating them as ordinary catch bodies would silently reorder their side effects.
Schedule-mode discovery has its own CFG value analysis and version contract; successful
synchronization does not substitute for readonly query-mode coverage or vice versa.

Each root exports `S` proven/unproven/incomplete, `A` distinct direct-access sites, `C` completion
sites, `U` unproven sites, up to twelve `E`/`G` examples, and the typed lifecycle-root binding.
Proven refers to completion of context-derived and locally scheduled outstanding work before
modeled synchronous accesses, not a general runtime safety theorem or a check that all scheduled
handles are returned to the caller. Ref parameters can alias fields inside components even when
their own type is int/Ent/etc.; accesses to such borrowed storage conservatively require completion
too. This may leave a stack-only reference unproven until its provenance is modeled. Handle/context/
query reference storage uses the distinct handle transfer model. Older v1/v2 summaries are rejected:
v1 omitted borrowed-reference accesses, while v2 could miss writes through captured untracked lvalues.

CFG captures are classified using all reachable definitions and uses. Conditional/switch values (including
nested conditionals), value-returning calls and ordinary by-value argument captures keep snapshots
at capture time. Fixed mutable local/parameter/this addresses alias the original storage cell, so
later argument effects and writes by the callee remain visible. Location aliases require the same
source expression at definition and use, not just equal symbols across value-producing branches.
A conditional value used as a receiver
is still a temporary, never an alias of the selected source variable. Borrowed scalar captures record
their accesses/stores at the point of use, including after an RHS schedules work. Captured fields,
ref-returning getters/helpers and conditional choices of data addresses retain acquisition effects
once and conservatively require completion at later uses/stores. Their exact address identity is not
needed when the addressed type is untracked U; this can leave stack-only data accesses unproven.
Helpers returning ref/ref readonly data are analyzed across method/assembly boundaries. Generic
return types are substituted first: ref T is supported only when the closed T is untracked data,
not a JobHandle/context/query alias. Reading a ref-return result also records an access after the
helper's own effects. Dynamic tracked-address choices, tracked field locations and readonly tracked
receivers with uncertain defensive-copy timing remain explicit gaps. Open-T capture locations are
not presumed untracked; their support still requires a typed address model.
Primitive constant/discard patterns preserve their input effects and explore CFG edges. The exact
parameterless BCL SwitchExpressionException constructor is an audited leaf (Roslyn includes the
failure path even for exhaustive switches); user exception constructors and other overloads are not.

Branches are explored independently; a feasible overapproximation can leave a correlated conditional
unproven. Cycles iterate to a fixed point over canonical coverage states. Two pending jobs are merged
only when every tracked storage cell (including caller frames and a return value) covers both or neither.
Copies, combinations and completion cannot distinguish jobs within such a class. Uncovered/lost jobs
retain a pending class; completing the most recent independent job cannot erase earlier iterations.
Completed tokens are removed. Canonical renaming makes fresh equivalent iterations converge without
reusing schedule-site identities or imposing an assumed iteration count. Recursion, exception filters,
unresolved storage and bounded traversal exhaustion still make coverage incomplete. No partial proof is accepted.
Programs and type roles are cached within a compilation, and linear edges do not clone the heap.

Compare Scheduled Jobs validates these records and their exact typed root bindings and reports the
three outcomes separately. Tests cover copied/default/stale handles, combined independent schedules,
ref versus value context mutation, out-handle and generic helper returns, explicit lifecycle methods,
closed generic systems, late Complete, query waits, chained versus independent loop schedules,
zero-iteration paths, do/while, helper returns inside loops and borrowed writes after RHS scheduling.
Capture tests distinguish ref versus value arguments across conditional side effects, mutable versus
temporary/conditional receivers, conditional default handles, nested selections, borrowed scalar
stores, field/ref-return address evaluation order, generic data-ref helpers, conditional data addresses,
rejection of generic handle-ref aliases and unsupported readonly tracked addresses.
Unsupported exceptional flow remains explicit. Unity tests remain the integration gate.

The production Editor selector validates dependency and synchronization contracts independently.
With both complete, it reads only typed catalogs and never calls the legacy IL analyzer. If either
contract is unavailable, IL is evaluated once and supplies only the uncovered part; complete source
results are retained. A complete `unproven` synchronization plan emits MethodCallRequired with its
source access sites. A `proven` plan has no such error; it does not claim Complete is unnecessary
when no component accesses were found (completion may synchronize other side effects). Incomplete,
malformed or incorrectly bound records cannot suppress fallback diagnostics. Compiler bootstrap
diagnostics retain their serialized text without requiring a live caller MethodInfo.

The Scheduled Jobs report counts source-only roots and the two kinds of remaining IL fallback.
The comparison endpoint stays on legacy IL even when production selects source. Selector tests
make the legacy callback fail for fully covered ordinary, explicit and closed-generic roots;
partial-coverage tests check that only the missing result is replaced and fallback runs once.
The unused legacy C# table emitter, its worker/cache state and string-initialization helpers have
been removed. Table emission remains in SystemDependencyInputEmitter; the Editor feeder exports
typed records and retains the IL oracle only for coverage gaps and explicit comparisons.
Continue expanding coverage and remove the remaining IL fallback only after project coverage and
Unity integration tests confirm it is no longer needed.

## View callback safety selection

`ME.BECS.ViewSafety.v2` binds each concrete owner and callback phase to the method actually
dispatched by Views: the EntityView virtual slot or the IViewApplyState/IViewApplyStateParallel
interface implementation, followed to its most-derived override. Explicit implementations,
inherited closed generic bases and nested generic type contexts are supported. Same-named
overloads, hidden new slots and methods on a marker-only IViewModule are not runtime callbacks.
If a type is both an EntityView and an IViewModule, `module:ApplyState`/`module:ApplyStateParallel`
records keep its module interface targets separate from its view virtual targets; the feeder,
fallback resolver and comparison all select the corresponding role.
The v1 name-based records are intentionally not selectable; a stale assembly requires IL fallback
until it is recompiled, rather than certifying the wrong callback body.

The Editor tracker feeder selects freshly compiled IL dependencies independently of source catalogs.
Both phases and view/module roles have independent cached selections; returned sets are copies,
and the cache transaction is protected if an export shares the reader between threads. The
diagnostic source-first reader has a separate cache and cannot supply or veto production sets.
An IL analysis failure does not fall back to a source set or publish a partial snapshot. This adds
no runtime locks. With tracker input v3, compiler selection owns IViewIgnoreTracker,
IViewTrackIgnore and explicit IViewTrack filtering and the deterministic tracker type order.
If a dependency is inaccessible to the generated typeof helper, the shared safety analyzer now
emits `InaccessibleSafetyComponent` rather than a zero-gap header without its required typed catalog.
That is incomplete source coverage, not corrupt metadata, and keeps the explicit fallback available.

Compare View Safety retains an independent source oracle, not a parity gate for production.
Compare View Tracker Inputs uses the same IL selection as export. Metadata tests cover
actual callback binding, phase isolation, generic inheritance, complete source with forbidden IL,
incomplete fallback, invalid metadata and independent comparison. They do not execute view callbacks,
instantiate Unity objects or validate player/Burst behavior. IL behavior analysis and Editor
type discovery remain intentional input feeders; source generators own emitted C#.

### Compiler-owned view tracker selection

The v3 `view-tracker` payload contains only its version and capacity. It no longer captures a
global component list in the Editor. Each selected `view-tracker-view` / `view-tracker-module`
record starts with the owner's assembly-qualified identity and has independent
`S\tApplyState\til` and `S\tApplyStateParallel\til` rows. Each phase carries its raw
`C\tphase\tcomponentIdentity` snapshot before tracking filters, deduplicated and sorted by
ordinal assembly-qualified identity. An absent callback has an explicit empty IL snapshot.
An ignored owner instead has the single `ignored` row, without dependencies.

The compiler validates these IL snapshots but never consults source safety/dispatch catalogs
to replace or veto them. An empty snapshot stays empty. Previous `source|legacy|none` selectors
remain readable for upgrades through the compatibility path described below.

`CompilerViewSafety` binds each phase to current own-assembly `ME.BECS.ViewSafety.v2` and
`ME.BECS.ViewCallbackDispatch.v1` catalogs. The latter records the actual source callback's
declaring type (including closed generic base arguments), declaring assembly and method ID.
It preserves the source dispatch before Unity introduces `in`/`modreq` forwarding methods;
the consumer does not infer it from a same-named metadata method. View and module roles stay
separate. Complete safety rows must agree with that dispatch and with the typed helper's
identity/signature. Validation reads Roslyn symbols only, not callbacks or typeof getters.

Current complete source catalogs supersede captured fallback arrays. Missing/incomplete coverage
requires explicit fallback for that phase; malformed present contracts fail compilation even
when a fallback was exported. A module without a phase interface has no callback for that phase.
Older typed catalogs without the new dispatch proof remain transitional: the Editor can export
their validated arrays without rereading IL, but labels them captured (`legacy`) until the
owning assembly recompiles. This permits upgrading without a circular bootstrap dependency.

The current ignore-all contract clears both callback and explicit tracking dependencies.
Per-component exclusions apply to callback dependencies first, then explicit `IViewTrack<T>`
opt-ins can add them back. For `IViewTrack<TAspect>`, compiler-owned `ME.BECS.AspectQuery.v1`
records provide QueryWith components, including private fields omitted by metadata imports.
Missing or invalid aspect contracts fail closed. Active tracked types must be closed,
accessible unmanaged components. The global set is the distinct union, sorted by ordinal
assembly-qualified identity; owner order, separate role records and capacity are retained.
Dual-role owners still have one runtime tracker registration with the union of their roles.

`ME.BECS.ViewTrackerInputs.v1` retains the raw transport for fresh-export comparisons.
`ME.BECS.ViewTrackerSelection.v1` records the effective component lists, while
`ME.BECS.ViewTrackerOrigin.v1` identifies `il` or `ignored` per role/phase (and the
`source`, `legacy`, `none` origins of older compatibility inputs).
Old v1/v2 inputs remain readable, including recovery from stale ignored private dependencies.
The IL switch was checked with nine actual-IL callback regression cases, 37 isolated
source/selection cases and 277 compiler tracker transport checks, including missing/corrupt
source metadata, generic inheritance, dual roles and explicit tracking precedence. The
analyzer DLL was rebuilt; Unity/Burst/player execution was not run. Runtime callback
scheduling and simulation state are unchanged.

## View type registration and callback flags

ViewsCodeGenerator no longer reads IL or emits ViewsLoad. Its input feeder exports only a
canonical list of concrete EntityView types (`view-type-schema` / `view-type`). The compiler
binds the eight runtime virtual slots, including inherited overrides from closed generic and
nested generic bases. Hidden `new` methods, unrelated overloads and an EntityView's separate
IViewModule implementation do not replace those slots. The same override resolver is shared
with view safety analysis. TypeFlags values come from the current runtime enum symbols.

ViewCallbackBodyGenerator exports `ME.BECS.ViewCallbackBody.v1` per owning assembly. It proves
only narrow no-op bodies (empty blocks, empty statements, expressionless returns, uninvoked
local-function declarations and constant-leaf conditionals selecting only those operations).
Compiler-omitted Conditional/partial calls are ignored together with receiver/argument
evaluation, including expression bodies and file-local define/undef directives. This matters
for callbacks containing only production-stripped logs. Async callbacks, remaining calls
including base calls, assignments,
allocations and any unrecognized source operations stay enabled. This is not an IL-length
heuristic or a transitive purity optimizer. Missing external body metadata and corrupt/duplicate
contracts are errors, never permission to enable or suppress a callback. Even an extra empty
ApplyState phase can consume changes in the provider's shared GroupChangedTracker before
another phase runs; an unknown imported body therefore cannot default to an active flag.
The bootstrap can read local source directly and imported descriptions without IL fallback.

ViewTypeInputs registers the original ViewsModuleData callback at the feeder's existing
position; it fills ViewTypeInfo flags and reuses the existing per-type ViewsTracker entry.
Every selected type must have a view-role tracker record. IViewIgnoreTracker still has that
record with empty dependencies. Missing counts/owners, duplicate inputs, an invalid runtime
contract or a missing tracker fail with BECSG100 before bootstrap emission. No views or
modules are instantiated while generating. Callback bodies, scheduling, culling and tracker
ID assignment remain unchanged. `ME.BECS.ViewTypeInputs.v1` records ordered types, flags and
each selected callback's method identity/body status for inspection. The Views addon remains
optional; no input records means no references to its runtime in this emitter's output.

Tests inspect all eight flags, empty/inherited/hidden/overloaded/generic/dual-role/async cases,
Editor/runtime type selection and tracker correspondence, without running real callbacks.
Assemblies defining callbacks (including generic bases) must be compiled with the current
body-contract generator. Missing contracts name the owning method/assembly in BECSG100 and
request recompilation; no production IL fallback or guessed callback flag remains here.

## Network method registration

The built-in NetworkCodeGenerator is now a discovery-only input feeder. It exports a
`network-method-schema` count and ordered `(declaring type identity, method name)` records,
and adds declaring assemblies to the bootstrap references. It no longer emits NetworkLoad,
delegate registration statements or method bodies. InputManifestGenerator validates the
current exact NetworkMethodAttribute and NetworkMethodDelegate signature, accessibility,
closed owners, non-generic static methods, record counts, ordinals and duplicates. Invalid
or ambiguous attributed overloads stop export with BECSG100 instead of generating an
incorrect method-group call. Ordinary unrelated overloads do not participate in selection.

Network publications use project-owned `NetworkFragments` beside the other native inputs.
Each eligible owner emits typed delegates to the original methods, not wrappers, retaining
pointer identity and existing AOT attributes. The aggregate retains only an untyped feeder
facade and expected selection. `BootstrapNetworkMethods` installs one ordered callback at
the existing network feeder position; owner load order cannot affect the wire mapping.
Runtime and Editor publications are isolated. Partial/conflicting selections fail the
optional bootstrap preflight before shared-state reset. Core has no Network reference.
No callback runs while compiling, publishing or inspecting metadata. With the addon absent,
the generated selection/preflight is empty and mentions no Network symbols.

Wire method IDs are one-based, following name **ordinal** order and declaring assembly-qualified
type identity as a tie-breaker. This removes dependence on process culture and TypeCache tie
order. Unlike old culture-based sorting, this is deterministic across clients; a project with
same-name or culture-sensitive names may get a changed mapping after regenerating, so mixed
old/new builds are not wire-compatible. The compiler checks rather than silently reorders
the manifest. `ME.BECS.NetworkMethodInputs.v1` records the precise ID mapping for comparison.
The count is bounded by UInt16's nonzero ID range. Empty plans still register the original
empty callback; omitted, truncated or duplicate plans cannot masquerade as empty success.
Tests inspect Editor/runtime mappings and owning fragments, and verify real MethodsStorage
IDs across independent worlds and repeated Editor bootstrap without calling network methods
or opening a transport. Player stripping validation remains separate.

## Manifest-owned bootstrap without C# markers

### Compiler-selected debug safety

`job-debug` payload v2 transports the selected job/interfaces/typed arguments and an
`S\tsource` or `S\tlegacy` selector. Complete source exports no longer contain a frozen
component list. The compiler resolves the current ordinary/closed-generic JobSafety
catalogs from its assembly references, validates the Execute root, producer/getter
identity and typed component records, and selects the dependency modes itself. No
generated getter, runtime initializer, job body or IL analyzer is invoked by this reader.
The D records are the dependency authority; the A getter's identity/signature is checked,
not its IL body. Catalogs are local to one compilation, with cancellation during traversal.
Unity's metadata-only `in`/modreq forwarding stubs are bound through the exported source
interface map; a same-named public Execute is never guessed to be the implementation.

Current complete source also supersedes v1 or explicit legacy lists. Missing/incomplete
source-only plans and malformed/conflicting complete catalogs fail with BECSG100; they
cannot silently become empty wrappers. Explicit fallback remains only for genuine
missing/incomplete coverage and must be retired as that coverage is completed. The Editor
still uses the source selector to gather assembly references, not to emit complete lists.
Closed nested job identities are exact; identical repeated closed records use their named
producer, while ordinary duplicates and conflicting closed records are invalid.

`ME.BECS.DebugJobSafety.v1` records the effective ordered selection for each job/contract,
including source/legacy origin. Both debug wrapper variants and native-size initializers
use that selection. Dependencies retain ordinal reflection FullName/assembly ordering;
safe/unsafe layouts remain paired. Tests inspect these effective records, rather than
assuming the old input transport determines field layouts. Runtime execution and Unity
Burst/player validation remain separate.

### Compiler-selected job statistics

Complete entity-count inputs now contain only `job-entity-initializer` payload
`v2\n<job identity>`; complete weights use `job-weight ... catalog v1`. The Editor still
collects assembly references and decides whether its snapshot has source coverage, but no
longer chooses the initializer identity or exports its entity group argument list. The
compiler selects the current typed initializer from JobEntityCounts/JobWeights metadata.
The shared CompilerJobCatalogs reader owns ordinary/closed-generic conflict handling,
canonical completeness headers and exact source Execute/interface-map binding for all
three job analyses; assembly metadata is scanned once per compilation, not once per job.

Entity selection requires the v3 multiplicity contract, exact initializer identity and
signature, a limit record matching the current EntitiesJobMaxCount attribute, unique
ordinal C rows, valid entity types and a non-overflowing total loop count. Every referenced
group must exist in the selected global entity catalog, including a loop-only group that
does not reserve slots without an explicit limit. Reserved arguments retain C-row order;
actual IDs still come from the global manifest. The existing source initializer is called,
so allocation, per-group reservations, total runtime limit and deterministic ID assignment
are unchanged. No initializer or entity/job code runs during selection.

Weights validate canonical scalar/contribution records and the exact typed Apply adapter.
Complete current catalogs also supersede old weight source/value transport. Entity
`job-entity-fallback` snapshots now retain the Editor-selected compatibility IL counts
without consulting source catalogs, as described in the revised architecture above. Source-only
plans cannot silently downgrade when coverage disappears; malformed or conflicting complete
catalogs stop with BECSG100 instead of selecting Editor IL output. Missing/incomplete
catalogs (including unavailable versioned initializers) still require explicit transitional
fallback. This is not full retirement of the remaining IL analyzers.

`ME.BECS.JobEntitySelection.v1` and `ME.BECS.JobWeightSelection.v1` publish effective plans,
not copies of the input transport. Bootstrap tests compare group argument order, limits,
initializer identities and weight values against those selections and current catalogs,
without invoking runtime initialization. Editor/runtime regeneration and Burst/player
execution remain separate integration checks.

### Compiler-selected system dependencies and synchronization

`system-dependencies-schema v2` transports a selected closed system and two independent
origins: operations and synchronization. Complete domains contain no copied component
or diagnostic rows. Roslyn reads the current `SystemDependencies.v1` and
`SystemSynchronization.v3` catalogs, binds the exact lifecycle implementations (including
explicit implementations and imported source interface maps), and validates their typed
root/plan contracts without invoking reflection getters or lifecycle methods. Update,
Awake, Start and Destroy contribute; DrawGizmos is validated as a separate known root,
but does not enter these simulation diagnostic tables.

Each domain may still carry explicit transitional fallback when its coverage is incomplete.
A complete current catalog supersedes that fallback independently; complete operations
do not prove synchronization, and complete synchronization does not prove operations.
Required source operation coverage cannot silently downgrade. Malformed operation
records stop compilation rather than select stale IL output. Synchronization
catalogs only supply optional warnings: unavailable/invalid coverage is marked
`unavailable` when source was selected, without stopping compilation or changing
operations/dependencies. v1 inputs remain readable
unchanged for upgrade compatibility.

The compiler derives cross-system reader/writer edges AFTER selecting effective operations;
v2 inputs cannot transport stale D rows. Explicit system dependencies remain separate from
component writers, and a WO-only operation does not create a read edge. Generic definition
aliases select every chosen closed member exactly once and union their effective operations,
errors and dependencies in canonical order. Generic parallel scheduling itself is unchanged.
`ME.BECS.SystemDependencySelection.v1` records the effective C/D/E diagnostic tables;
`ME.BECS.SystemDependencyOrigin.v1` records the two origins and alias members. Tests compare
these metadata and Editor dictionary getters, without initializing worlds or executing jobs.
Asset/type selection, incomplete-domain fallback and other legacy analyzers remain retirement
work; this step does not claim that all legacy IL has been removed.

BootstrapPublicationGenerator emits profile composition and its phase selection.
BootstrapRuntime owns execution; typed registration and AOT callbacks belong to independent
publication owners. StaticTypesInitializer/StaticMethods and the aggregate emitter are retired.
Editor dependency tables live in EditorSystemDependencies. Job debug wrappers remain available
with checks disabled, while their safety bodies retain the checks guards. Registration ordering
is explicit in the exported global ordinals and phase plans, not assembly discovery order.

Normal export publishes project-owned additional files and independent owner bridges.
It no longer creates an aggregate asmdef, writes a compiler response, or reads/writes
retirement stubs in the former `Assets/ME.BECS.Gen` directory. Existing old outputs
are left untouched by the exporter; this project removed them explicitly after inspecting their
stubs and checking incoming assembly references. The old header target identity is retained only for
snapshot compatibility, not as a requirement to create a compilation host.
Export does not execute C# hooks, format C# templates, load/store their file caches,
or recursively delete the output folder.
Old `AddInitialization`, `AddMethods`, `AddPublicContent` and `AddFileContent` overrides
are rejected before constructing a feeder; extensions must use data inputs/references and
a compiler emitter instead. These base APIs remain only for actionable migration errors.
The new v2 schema rejects `legacy` dispatch. Old aggregate manifests and marker hooks no longer
emit an executable bootstrap. The disabled CopyFrom body is retained privately for the
deferred investigation, not invoked or enabled by this change. Graph initialization now
owns its class-level BurstCompile attribute in Roslyn as well, without duplicating an old stub's attribute.
Publication generators validate their own fragments before emitting callbacks. Runtime plan
preflight and the Editor freshness gate reject missing/incomplete publications. The aggregate
BECSG100–BECSG105 diagnostics were retired with their emitter, not reused as publication IDs.

### Remaining verification after aggregate retirement

The analyzer DLL builds successfully; this does not prove Unity compilation or runtime behavior.
Required evidence still includes fresh Editor compilation without the aggregate directory, source
generator contract and integration tests, independent-world deterministic entity creation and
generic scheduling, graph-edit re-export, config/View/Network callbacks, and clean import with the
framework relocated. Local macOS IL2CPP compilation succeeded on 2026-10-06 (see
the evidence above); Player/Burst execution and stripping-sensitive behavior remain
unverified. A clean build-machine import is separate from that cached local build.
Do not treat older test results, published receipts alone, or an analyzer-only build as proof of
these checks. CopyFrom remains a separately deferred investigation, not a silently enabled feature.

## Compiler-owned Editor theme menus

Theme menus are published independently into the assembly that declares `Themes`.
`ThemeMenuPublicationGenerator` consumes only that owner's Editor additional file,
stored under the project's `Assets/ME.BECS.SourceInputs/ThemeMenuFragments`, not
inside the framework. The aggregate generator skips menu emission when the
`thememenu-publication-schema` selection is present. Older manifests retain the
compatibility emission until the next export. Invalid owner-local menu inputs
produce warning BECSG137; menu receipts are not world/bootstrap readiness gates.
The menu tests resolve the actual Editor owner and independent input catalog,
without loading the former aggregate assemblies or invoking preference setters.

`ThemesCodeGenerator` discovers built-in and custom stylesheet assets and exports
`theme-menu-schema` / `theme-menu` data only. The source generator owns `ThemesMenu`,
its selection methods and validation callback, with fully qualified references to
`ME.BECS.Editor.Themes.CurrentTheme`. The preference key and stylesheet refresh path
are unchanged. Built-in order and the priority gap before custom themes are preserved;
custom themes use ordinal name order. Overlapping custom theme folders are deduplicated
by asset path, and conflicting menu names fail explicitly with both stylesheet paths.
Names and paths are escaped string literals, never C# identifiers or executable input.
Method names use bounded ordinals, including for long or non-identifier asset names.

The compiler validates schema counts/order, duplicate names and accessible Editor
contracts. These records are rejected in Runtime inputs; an absent plan emits no
Editor references. `ME.BECS.ThemeMenuInputs.v1` records names, paths and priorities
without reading preferences or invoking menu actions. Older migration versions retired
the old `MenuThemes` body with a comment-only stub; current export does not access
the old output directory. Asset discovery remains
an Editor responsibility. Unity menu execution is distinct from compiler/metadata tests.

## Follow-up: unified job API

After the source-generator migration, provide one job interface instead of the
`IJobForComponents<...>` / `IJobFor2Aspects3Components<...>` family and allow
`Schedule()` without explicitly listing all job/component/aspect type arguments.
The generator should infer the required types and access contracts from the job's
`Execute` signature and emit the scheduling adapters. The exact interface name
and API shape remain to be designed. Preserve deterministic entity reservations,
parallel execution semantics, safety checks and Burst/AOT support. This is a
separate follow-up, not a change to the current public scheduling API.

Runtime initialization attributes and module initializers are intentionally absent: compiler or assembly
load order must not decide simulation IDs. Unity assets still require an editor-produced manifest;
Roslyn cannot inspect method bodies from referenced assemblies, so cross-assembly dependency analysis
requires exported summaries or the existing IL analyzer.
