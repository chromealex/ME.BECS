# ME.BECS source-generator migration: first stage

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

ComponentDestroyGenerator owns complete destroy callback bodies, Burst/MonoPInvokeCallback attributes, and
registration methods for supported public unmanaged non-generic components. Legacy AddMethods omits those
bodies and emits only registration at the original callback-registry position. Null and non-null component
semantics are preserved. Inaccessible/unsupported signatures or missing compiler dependencies use fallback.
The comparison report checks destroy callback registration coverage without invoking callbacks.

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
`Export Graph Topology` writes diagnostic GraphTopology.txt snapshots in Temp. GraphTopology.v1
assigns preorder occurrence IDs to nested graph uses (not asset IDs), preserving repeated uses.
Rows retain local node order, node/system type identities, enabled/group-enabled flags, stored sync
data for each phase, and input/output port edge order including duplicates. Missing sync arrays are
explicitly unknown; the exporter never calls the mutating GetSyncPoint normalization. Cycles,
missing nodes/graphs and missing/cross-graph endpoints fail a snapshot instead of producing a partial
plan. Runtime manifests now carry the snapshot in `graph-topology` records (ordinal, base64 `topology`,
signed root graph ID, base64 GraphTopology.v1 payload). Roslyn parses typed occurrences/nodes/ports
and rejects bad root/parent references, duplicate or missing nodes/phases, noncanonical values and
out-of-range edges. Unknown sync data stays unknown. These inputs are not yet used to emit lifecycle
bodies; topology equality alone does not prove scheduling equivalence. Refreshing sync analysis and
binding generic slot ranges remain required before switching the lifecycle planner.

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
AspectInputs.Construct(ref World). The existing callback registration slot remains unchanged. Empty
aspects are omitted (no storage allocation); nonempty aspects require a compatible generated constructor,
checked for field order and shape before export. The Editor no longer emits pointer assignments or
reflection SetValueDirect fallback bodies. Recompile catalogs and regenerate inputs after updating;
Unity initialization/reload behavior has not yet been verified for this path.

The base StaticTypes component phase now calls manifest-generated ComponentInputs.Register_<hash>
and Aot_<hash> at the same respective initialization/AOT positions. Roslyn emits Validate and the
optional Default assignment; Default is evaluated once per initialization call, never during export.
Closed generic components are resolved structurally, not through nongeneric catalog wrappers.
Shared/static/config-specific registration and AOT methods are also emitted by Roslyn from these flags.
The transitional Editor keeps the original separate loops and call positions, but no longer emits
ValidateShared/ValidateStatic/config Validate or their AOT statements. Config registration deliberately
does not repeat the Default assignment. Custom shared hash selection still uses the existing Editor
interface-map check; migrating that classifier and validating runtime behavior remain outstanding.

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

Compare Editor Catalogs / Compare Runtime Usage now also report ordinary/generic job EarlyInit coverage and
fallback reasons. Diagnostics reuse the actual legacy selection routine in read-only diagnostic mode, skipping
cache access, IL/safety/weight analysis and debug generation. Counts are distinct selected calls, not all declared
job types; cache contents are not validated. Generic metadata accessors may run, but EarlyInit never runs.
Legacy selection failures mark the comparison incomplete rather than reporting successful zero coverage.

EarlyInit now also supports public jobs nested in a generic system with one unmanaged type parameter and
supported public non-generic interface constraints. The emitted wrapper carries the outer constraints.
Before replacement the bridge closes metadata with the chosen concrete argument and verifies the complete
legacy EarlyInit call (job, method, and ordered component/aspect arguments). Metadata access does not initialize
the job. Ambiguous overloads, multiple parameters, jobs with their own generic parameters, or mismatches fall back.

Generated identifiers use the shared SourceGeneratorNames source, compiled into both analyzer and Editor.
Inputs up to 128 UTF-16 characters keep their previous hex encoding; longer names use H + full SHA-256.
EarlyInit method names always use SHA-256. This bounds encoded suffixes to 512 characters, keeping generated
method/catalog names below metadata limits for ordinary namespaces. Naming does not use process hash codes,
timestamps, file paths or culture. Changing helper names does not change simulation type IDs.

JobEarlyInitGenerator emits EarlyInit wrappers for public unmanaged non-generic jobs with supported public
non-generic arguments. It matches the EarlyInit interface constraints; the Editor bridge replaces only an exact
legacy call spelling. Cached content stays legacy and is resolved at emission time, including cache hits.
Generic jobs, unsupported signatures and missing methods fall back. Entity creation counts, operation weights,
max component size, safety/debug metadata and dependency analysis remain with the existing job generator.

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
Runtime usage discovery is unchanged. This compatibility stage does NOT yet eliminate TypeCache work or
claim a performance gain; catalog reads add overhead until the legacy enumeration can safely be retired.
The comparison command explicitly bypasses catalogs for its baseline and checks content/order equality
against catalog-assisted editor discovery for components, aspects, and component groups.

Manual diagnostics after Unity has finished importing/recompiling:
- `ME.BECS > Source Generator > Compare Editor Catalogs`: compare full legacy editor discovery with loaded catalogs.
- `ME.BECS > Source Generator > Compare Runtime Usage`: check coverage of legacy runtime usage; catalog-only
  types are expected here and are counted separately. This inspects loaded Editor assemblies, not player binaries.

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

Config mask callbacks now have complete Roslyn-owned bodies in `ConfigMaskGenerator`, including Burst and
MonoPInvokeCallback attributes. The transitional editor bootstrap selects a per-component registration only
after checking the exported public field order against legacy reflection order. This preserves mask bit
meaning, including partial declarations. Unsupported types/fields retain legacy emission and appear in
`Config mask callbacks` comparison diagnostics. Metadata access does not execute callbacks or registration.
`ConfigCollectionsGenerator` owns collection counts and materialization callbacks for ListAuto/MemArrayAuto,
including dispose-before-replacement and legacy missing-data behavior (new list capacity 1 / empty array).
The bridge compares counts and callback field order independently before selecting them. Custom collection
implementations retain callback fallback until their construction contract is supported. Both phases have
comparison counters; no callback is executed by validation. The inactive `CopyFromCodeGenerator` is retained
for the requested follow-up investigation after the source-generator migration. Both editor and runtime
paths return an empty result unconditionally and its callback bodies are commented out. Do not reactivate
it implicitly. Investigate the recorded Unity Cloud Burst failure, collection cloning/ownership and element
CopyFrom semantics before designing its source-generator replacement.

`MethodSummaryGenerator` exports raw method operation records using assembly metadata with the versioned
key `ME.BECS.MethodSummary.v2`. Downstream compilations can read them without method bodies or executing
any registration. Payload: declaration documentation ID, analysis attributes, unresolved-operation flags,
declared generic environment, then tab-separated operation records (kind, loop context, target assembly
identity, target declaration ID, constructed containing type, method type arguments). Types use a versioned
structural encoding: named definitions + arguments, scoped type parameters, arrays and pointers. Parameter
identity includes its declaring type/method, not just the name T. This replaces ambiguous display/reference
strings. v1 data is deliberately not accepted as v2; dependent assemblies need recompilation.

Ordinary method/constructor summaries now walk reachable Roslyn CFG blocks. Iterative SCC analysis marks
repeating blocks; loop acquisition/initialization outside a cycle is not classified as repeating. Exceptional
finally/filter/catch regions retain gaps until their edges are modeled. Syntax-only fallback is explicitly
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
structural type arguments; all consumers stop type parsing before these annotations. `safety-schema=1`
marks summaries with these contracts so older v2 summaries cannot masquerade as analyzed safety data.
DisableContainerSafetyRestriction takes priority over access terminals, then normal call traversal respects
CodeGeneratorIgnore. Root RO/WO/RW parameter overrides are applied after the transitive union; RO+WO becomes
RW. Conflicting overrides, unknown access modes, generic component output, unresolved definitions/dispatch
remain explicit gaps. No safety fields or registrations are switched to this provisional output yet.

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

Runtime initialization attributes and module initializers are intentionally absent: compiler or assembly
load order must not decide simulation IDs. Unity assets still require an editor-produced manifest;
Roslyn cannot inspect method bodies from referenced assemblies, so cross-assembly dependency analysis
requires exported summaries or the existing IL analyzer.
