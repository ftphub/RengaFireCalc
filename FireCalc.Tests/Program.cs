using FireCalc.Core;

static void Check(string name, double actual, double expected, double tol = 1e-9)
{
    if (Math.Abs(actual - expected) > tol) throw new Exception($"FAIL: {name}; {actual} != {expected}");
    Console.WriteLine($"OK: {name} = {actual:F6}");
}

var painting = ReserveCalculator.Calculate(new(15, 80, 1, 0, 0, 0.14, 13.84, 2.5));
Check("Painting water", painting.SuppressionWaterM3, 71.28);
Check("Painting foam", painting.WorkingFoamM3, 0.72);
Check("Painting min level Excel", painting.MinimumLevelFoamM3, 0.388);
Check("Painting foam total Excel", painting.TotalFoamM3, 2.328);
Check("Painting water with dry pipe", painting.WaterWithDryPipeM3, 85.12);
if (painting.TankSufficient != true) throw new Exception("Tank test failed");

var repair = ReserveCalculator.Calculate(new(10, 80, 1, 60, 80, 0.25, 24.13, 2.5));
Check("Repair water fire", repair.SuppressionWaterM3, 47.52);
Check("Repair water cooling", repair.CoolingWaterM3, 288);
Check("Repair water with dry pipe Excel", repair.WaterWithDryPipeM3, 359.65);
Check("Repair foam total Excel", repair.TotalFoamM3, 1.74);
Check("EFER hydraulic 1.9 m", PipeHydraulics.LinearLossMpa(40, 1.9, 1429), 0.02127362, 1e-5);
Check("Pipe geometric volume", new PipeSegment(1, "sample", 10, 100).InternalVolumeM3!.Value, Math.PI * .1 * .1 / 4 * 10);
try { ReserveCalculator.Calculate(new(15,80,150,0,0,0)); throw new Exception("Validation failed"); }
catch (ArgumentException) { Console.WriteLine("OK: invalid concentration rejected"); }
// Independent fixture extracted from the user's Renga 9.3 IFC (10 pipe segments).
// 6 x PPR PN20: Dinside=13.2 mm; 4 x ductile-iron pipes: Dinside=50 mm.
double[] ifcLengthsM = {0.1147075263057568, 9.837769275690325, 0.931091375404427,
    0.15078503636690834, 0.07523072430967386, 1.5265839380770924,
    0.70362353233248, 0.29174876273159384, 1.1255645030765626, 2.0211710113291297};
double ifcTotalVolume = 0;
for (int i = 0; i < ifcLengthsM.Length; i++)
    ifcTotalVolume += new PipeSegment(i, "IFC", ifcLengthsM[i], i < 6 ? 13.2 : 50).InternalVolumeM3!.Value;
Check("IFC 10-pipe volume m3", ifcTotalVolume, 0.00986224113627213, 1e-12);
// v0.3: user's Excel has 11 segments after adding PPR PN20 110x18.3 (ID 100042).
var added = new PipeSegment(100042, "PPR PN20 110x18.3", 29.823012399617628, 73.4);
Check("IFC added pipe m3", added.InternalVolumeM3!.Value, 0.12619249013123016, 1e-12);
Check("IFC 11-pipe total m3", ifcTotalVolume + added.InternalVolumeM3!.Value,
    0.13605473126750228, 1e-11);
Check("Pipe litres display", added.InternalVolumeLitres!.Value, 126.19249013123016, 1e-9);

// v0.2 route: adjacent branches are NOT summed.
foreach (var fx in new[] { EferHydraulicFixtures.Painting(), EferHydraulicFixtures.Repair() })
{
    var selected = fx.Lines.Where(x => x.Included)
        .Select(x => new RouteSection(x.Name, x.LengthM, x.FlowLs, x.Kt)).ToList();
    var r = HydraulicRouteCalculator.Calculate(selected, fx.Settings);
    Check($"{fx.Name} line loss", r.LinearLossMpa,
        fx.Name.Contains("окрасочный") ? 0.04064481730080879 : 0.05479216638363795, 1e-10);
    Check($"{fx.Name} circuit loss (vs PDF rounded)", r.TotalCircuitLossMeters,
        fx.PdfCircuitLossMeters, .002);
    var all = HydraulicRouteCalculator.Calculate(fx.Lines
        .Select(x => new RouteSection(x.Name, x.LengthM, x.FlowLs, x.Kt)).ToList(), fx.Settings);
    if (all.TotalCircuitLossMeters <= r.TotalCircuitLossMeters)
        throw new Exception("FAIL: selecting extra branch must increase losses");
    Console.WriteLine($"OK: {fx.Name}, derived total head={r.RequiredSourceHeadMeters:F3} m, printed PDF={fx.PdfRequiredHeadMeters:F3} m; discrepancy reported");
}
try { HydraulicRouteCalculator.Calculate(new[] {new RouteSection("bad", 12, 80, 0)},new RouteSettings(20,0,0,0)); throw new Exception("Validation must fail"); }
catch (ArgumentException) { Console.WriteLine("OK: missing Kt rejected"); }

Console.WriteLine("Reserve and hydraulic fixture tests completed. Differences vs PDF are documented.");

// v0.4: isolated graph tests, no COM/Renga required.
var tree = new NetworkTopology(new[] {
    new RouteLink(101, 1, 2, new[] { 1001, 1002 }, 1),
    new RouteLink(102, 2, 3, new[] { 1003 }, 1),
    new RouteLink(103, 2, 4, new[] { 1004 }, 1),
});
if (tree.Components().Count != 1 || tree.Components()[0].BranchNodeCount != 1 ||
    tree.Components()[0].HasCycle) throw new Exception("Tree topology failed");
var routePath = tree.FindUniquePath(1, 3);
if (!routePath.RouteIds.SequenceEqual(new[] {101,102}) ||
    !routePath.PipeObjectIds.SequenceEqual(new[] {1001,1002,1003}))
    throw new Exception("Unique path failed");
var reversedPath = tree.FindUniquePath(3, 1);
if (!reversedPath.PipeObjectIds.SequenceEqual(new[] {1003,1002,1001}))
    throw new Exception("Reverse orientation failed");
var flowsTree = tree.CalculateTreeFlows(1, new Dictionary<int,double> { [3]=40, [4]=20 });
Check("tree source Q", flowsTree.TotalFlowLs, 60);
Check("route 101 Q", flowsTree.RouteFlowLs[101], 60);
Check("route 102 Q", flowsTree.RouteFlowLs[102], 40);
Check("route 103 Q", flowsTree.RouteFlowLs[103], 20);
try { tree.CalculateTreeFlows(1, new Dictionary<int,double>{{3,40}}); throw new Exception("Missing terminal Q must fail"); }
catch (InvalidOperationException) { Console.WriteLine("OK: incomplete consumer demands rejected"); }
var ring = new NetworkTopology(new[] {
    new RouteLink(1, 1, 2, new[] {101},1),
    new RouteLink(2, 2, 3, new[] {102},1),
    new RouteLink(3, 3, 1, new[] {103},1)
});
if (!ring.Components()[0].HasCycle) throw new Exception("Cycle detection failed");
try { ring.FindUniquePath(1, 3); throw new Exception("Ambiguous ring path must fail"); }
catch (InvalidOperationException) { Console.WriteLine("OK: ring path rejected"); }
try { ring.CalculateTreeFlows(1, new Dictionary<int,double>{{2,40},{3,30}}); throw new Exception("Ring demands must fail"); }
catch (InvalidOperationException) { Console.WriteLine("OK: ring flow solving rejected"); }
var disconnected = new NetworkTopology(new[] {
    new RouteLink(1, 1, 2, new[] {101}),
    new RouteLink(2, 10, 11, new[] {102})
});
if (disconnected.Components().Count != 2) throw new Exception("Disconnected group detection failed");
try { disconnected.FindUniquePath(1, 11); throw new Exception("Disconnected path must fail"); }
catch (InvalidOperationException) { Console.WriteLine("OK: disconnected path rejected"); }
try { new NetworkTopology(new[] {new RouteLink(1,1,2,new int[0])}).FindUniquePath(1,2); throw new Exception("Route missing pipes must fail"); }
catch (InvalidOperationException) { Console.WriteLine("OK: empty route rejected"); }
try { new NetworkTopology(new[] {new RouteLink(1,1,2,new int[] {1},5), new RouteLink(2,2,3,new int[] {2},6)}).FindUniquePath(1,3); throw new Exception("Mixed system styles must fail"); }
catch (InvalidOperationException) { Console.WriteLine("OK: mixed systems rejected"); }
Check("velocity v on 13.2 mm PPR pipe at 1 L/s", FlowDiagnostics.VelocityMetersPerSecond(1,13.2),
    .001/(Math.PI*Math.Pow(.0132,2)/4), 1e-12);
var velocityFixture = HydraulicRouteCalculator.Calculate(
    new[] { new RouteSection("PPR", .115, 1, 5, 13.2) }, new RouteSettings(0,0,0,0));
Check("XLSX hydraulic velocity basis", velocityFixture.Sections[0].VelocityMs!.Value,
    FlowDiagnostics.VelocityMetersPerSecond(1,13.2));
try { FlowDiagnostics.VelocityMetersPerSecond(1,0); throw new Exception("Invalid diameter must fail"); }
catch (ArgumentException) { Console.WriteLine("OK: velocity with invalid D rejected"); }
Console.WriteLine("ALL v0.4 TOPOLOGY TESTS PASSED.");

// v0.5: pump modes are declarations, NOT simulations from BIM geometry.
if (!PumpScenario.Describe(PumpMode.Unspecified, Array.Empty<int>()).Contains("не определён"))
    throw new Exception("Pump mode defaults must remain unspecified");
if (!PumpScenario.Describe(PumpMode.DutyStandby, new [] { 101, 102 }).Contains("резервный"))
    throw new Exception("Duty/standby description failed");
if (!PumpScenario.Describe(PumpMode.Simultaneous, new [] { 101, 102 }).Contains("Q–H"))
    throw new Exception("Parallel operation must warn that curves are required");
try { PumpScenario.Describe(PumpMode.Simultaneous, new[] {101}); throw new Exception("Missing pump rejected"); }
catch (ArgumentException) { Console.WriteLine("OK: incomplete pump scenario rejected"); }
try { PumpScenario.Describe(PumpMode.DutyStandby, new[] {101,101}); throw new Exception("Duplicate pump IDs rejected"); }
catch (ArgumentException) { Console.WriteLine("OK: duplicate pumps rejected"); }
Console.WriteLine("ALL v0.5 CORE TESTS PASSED.");
