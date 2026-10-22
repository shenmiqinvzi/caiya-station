using System.Reflection;

// Offline harness: load the real game assemblies plus CaiYaStation.dll, then invoke the mod's
// own NativeContract.Validate and ModConfig.Load. This catches renamed members without launching
// the game, and does not touch any game file.
var gameArgument = args.FirstOrDefault(candidate => Directory.Exists(Path.Combine(candidate, "Duckov_Data")));
var game = gameArgument != null ? Path.GetFullPath(gameArgument) : @"E:\Steam\steamapps\common\Escape from Duckov";
var managed = Path.Combine(game, "Duckov_Data", "Managed");
var repo = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
var modPath = Path.Combine(repo, "src", "CaiYaStation", "bin", "Release", "netstandard2.1", "CaiYaStation.dll");
if (!File.Exists(modPath)) throw new FileNotFoundException("Build the mod first", modPath);
var searchDirectories = new[]
{
    managed,
    Path.GetDirectoryName(modPath) ?? "",
    Path.Combine(repo, "vendor", "Harmony")
};

AppDomain.CurrentDomain.AssemblyResolve += (sender, eventArgs) =>
{
    var simpleName = new AssemblyName(eventArgs.Name).Name;
    foreach (var directory in searchDirectories)
    {
        var candidate = Path.Combine(directory, simpleName + ".dll");
        if (File.Exists(candidate)) return Assembly.LoadFrom(candidate);
    }
    Console.Error.WriteLine("missing dependency " + eventArgs.Name);
    return null;
};

var assembly = Assembly.LoadFrom(modPath);
var contract = assembly.GetType("CaiYaStation.NativeContract", throwOnError: true)!;
var validate = contract.GetMethod("Validate", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!;
var logs = new List<string>();
validate.Invoke(null, new object[] { new Action<string>(line => { logs.Add(line); Console.WriteLine(line); }) });
var passed = (bool)contract.GetProperty("Passed", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(null)!;
var status = (string)contract.GetProperty("Status", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(null)!;
Console.WriteLine("Contract passed=" + passed + " status=" + status);

var configType = assembly.GetType("CaiYaStation.ModConfig", throwOnError: true)!;
var load = configType.GetMethod("Load", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!;
var config = load.Invoke(null, new object[]
{
    Path.Combine(repo, "mod", "config.json"),
    new Action<string>(line => Console.WriteLine("config: " + line))
})!;
var escorts = configType.GetProperty("Escorts")!.GetValue(config)!;
var none = escorts.GetType().GetProperty("None")!.GetValue(escorts)!;
var scavenger = escorts.GetType().GetProperty("Scavenger")!.GetValue(escorts)!;
var scavengerOption = scavenger.GetType().GetProperty("Option")!.GetValue(scavenger);
var noneFactor = none.GetType().GetProperty("LossFactor")!.GetValue(none);
var noneName = none.GetType().GetProperty("Name")!.GetValue(none);
var scavengerFactor = scavenger.GetType().GetProperty("LossFactor")!.GetValue(scavenger);
var bands = configType.GetProperty("Loss")!.GetValue(config)!.GetType().GetProperty("Bands")!.GetValue(configType.GetProperty("Loss")!.GetValue(config)) as System.Collections.ICollection;
var diagnostics = configType.GetProperty("DiagnosticsKey")!.GetValue(config);
Console.WriteLine($"Config none={noneName}/{noneFactor} scavenger.Option={scavengerOption} scavenger.Factor={scavengerFactor} lossBands={bands?.Count} diagnostics={diagnostics}");

var layoutType = assembly.GetType("CaiYaStation.StationLayout", throwOnError: true)!;
var layoutLoad = layoutType.GetMethod("Load", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!;
var layout = layoutLoad.Invoke(null, new object[]
{
    Path.Combine(repo, "mod", "station-layout.json"),
    new Action<string>(line => Console.WriteLine("layout: " + line))
})!;
var sites = layoutType.GetProperty("Sites")!.GetValue(layout) as System.Collections.ICollection;
Console.WriteLine("Layout sites=" + (sites?.Count ?? -1));

// Reflection-only check of every Harmony patch target. Applying Harmony inside a .NET 10
// console fails in HarmonySharedState (Unity Mono is the real runtime), so the probe verifies
// the members exist instead of invoking the patcher.
var patchOk = true;
try
{
    var patchType = assembly.GetType("CaiYaStation.NativePatches", throwOnError: true)!;
    _ = patchType;
    var core = Assembly.LoadFrom(Path.Combine(managed, "TeamSoda.Duckov.Core.dll"));
    var main = core.GetType("CharacterMainControl");
    var interactable = core.GetType("InteractableBase");
    var gun = core.GetType("ItemAgent_Gun");
    if (main == null || interactable == null || gun == null)
        throw new InvalidOperationException("patch target types not loaded");
    var targets = new (Type Type, string Name)[]
    {
        (main, "Attack"), (main, "StartAction"), (main, "PickupItem"), (main, "UseItem"),
        (main, "DropAllItems"), (main, "Trigger"), (main, "SetAdsInput"), (main, "SetRunInput"),
        (main, "CanRun"), (main, "CanUseHand"), (interactable, "StartInteract"),
        (gun, "TransToFire"), (gun, "ShootOneBullet")
    };
    var missing = targets.Where(target => target.Type.GetMethod(target.Name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance) == null)
        .Select(target => target.Type.Name + "." + target.Name).ToList();
    var interactCount = main.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
        .Count(method => method.Name == "Interact");
    if (interactCount == 0) missing.Add("CharacterMainControl.Interact");
    if (missing.Count > 0)
    {
        patchOk = false;
        Console.WriteLine("Input guard patch targets missing: " + string.Join(", ", missing));
    }
    else
    {
        Console.WriteLine("Input guard patch targets present (interact overloads=" + interactCount + ")");
    }
}
catch (Exception exception)
{
    patchOk = false;
    Console.WriteLine("Input guard target probe FAILED: " + (exception.InnerException ?? exception).Message);
}

var ok = passed
         && scavengerOption?.ToString() == "Scavenger"
         && noneFactor is double factor && Math.Abs(factor - 1.0) < 0.0001
         && bands?.Count == 4
         && sites?.Count == 2
         && patchOk;
Console.WriteLine(ok ? "PROBE PASS" : "PROBE FAIL");
return ok ? 0 : 1;
