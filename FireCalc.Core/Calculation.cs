using System.Globalization;

namespace FireCalc.Core;

/// <summary>
/// Calculation transcription from the provided EFER Excel workbook.
/// This is NOT an independently verified fire protection design standard.
/// Volumes in m3; flows in l/s; periods in minutes; concentration in percent.
/// </summary>
public sealed record ReserveInputs(
    double SuppressionMinutes,
    double SolutionFlowLitresPerSecond,
    double ConcentrationPercent,
    double CoolingMinutes,
    double CoolingFlowLitresPerSecond,
    double FoamPipeCapacityCubicMetres,
    double DryPipeCapacityCubicMetres = 0,
    double? DoserTankCapacityCubicMetres = null);

public sealed record ReserveResults(
    double SuppressionWaterM3,
    double CoolingWaterM3,
    double TotalWaterM3,
    double WaterWithDryPipeM3,
    double WorkingFoamM3,
    double ReserveFoamM3,
    double DosingCorrectionFoamM3,
    double TestFoamM3,
    double FoamPipesM3,
    double MinimumLevelFoamM3,
    double TotalFoamM3,
    double? TankCapacityM3,
    bool? TankSufficient);

public static class ReserveCalculator
{
    public static ReserveResults Calculate(ReserveInputs p)
    {
        Validate(p);
        double conc = p.ConcentrationPercent / 100.0;
        double waterFire = 60 * p.SuppressionMinutes * p.SolutionFlowLitresPerSecond * (1 - conc) / 1000.0;
        double waterCooling = 60 * p.CoolingMinutes * p.CoolingFlowLitresPerSecond / 1000.0;
        double waterTotal = waterFire + waterCooling;
        double working = 60 * p.SuppressionMinutes * p.SolutionFlowLitresPerSecond * conc / 1000.0;
        double reserve = working;
        double dosing = 0.20 * (working + reserve);
        double testing = 0.10 * working;
        // Excel L26: =ROUND((L16+L18+L20+L22+L23)*0.2,3)
        // Excel ROUND for positive values uses MidpointRounding.AwayFromZero.
        double minLevel = Math.Round((working + reserve + dosing + testing + p.FoamPipeCapacityCubicMetres) * 0.20,
                                     3, MidpointRounding.AwayFromZero);
        double foamTotal = working + reserve + dosing + testing + p.FoamPipeCapacityCubicMetres + minLevel;
        return new ReserveResults(waterFire, waterCooling, waterTotal,
            waterTotal + p.DryPipeCapacityCubicMetres,
            working, reserve, dosing, testing, p.FoamPipeCapacityCubicMetres,
            minLevel, foamTotal, p.DoserTankCapacityCubicMetres,
            p.DoserTankCapacityCubicMetres.HasValue ? p.DoserTankCapacityCubicMetres.Value >= foamTotal : null);
    }

    private static void Validate(ReserveInputs p)
    {
        foreach (var (key,value) in new[] {
            ("Время тушения", p.SuppressionMinutes), ("Расход раствора",p.SolutionFlowLitresPerSecond),
            ("Концентрация",p.ConcentrationPercent), ("Время охлаждения",p.CoolingMinutes),
            ("Расход охлаждения",p.CoolingFlowLitresPerSecond), ("Объём Wтр",p.FoamPipeCapacityCubicMetres),
            ("Объём сухотруба",p.DryPipeCapacityCubicMetres) })
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0)
                throw new ArgumentException($"{key}: требуется конечное число ≥ 0");
        }
        if (p.ConcentrationPercent > 100) throw new ArgumentException("Концентрация: максимум 100 %");
        if (p.DoserTankCapacityCubicMetres is double c && (!double.IsFinite(c) || c <= 0))
            throw new ArgumentException("Объём бака: требуется положительное число");
    }
}

public sealed record PipeSegment(int RengaObjectId, string Name, double? LengthM, double? InnerDiameterMm, string DiameterSource = "")
{
    public double? InternalVolumeM3 =>
        LengthM is double len && len >= 0 && InnerDiameterMm is double d && d > 0
            ? Math.PI * Math.Pow(d / 1000.0, 2) * len / 4.0 : null;
    public double? InternalVolumeLitres => InternalVolumeM3 * 1000.0;
    public string State =>
        LengthM is null ? "Нет длины" : InnerDiameterMm is null ? "Нет внутреннего диаметра" : "Готово";
}

public static class PipeHydraulics
{
    // According to EFER RUP.T hydraulic calculation (linear loss in MPa).
    // This alone does not constitute a complete hydraulic network analysis.
    public static double LinearLossMpa(double flowLitresPerSecond, double lengthM, double resistanceK)
    {
        if (!double.IsFinite(flowLitresPerSecond) || !double.IsFinite(lengthM) ||
            !double.IsFinite(resistanceK) || flowLitresPerSecond < 0 || lengthM < 0 || resistanceK <= 0)
            throw new ArgumentException("Неверные данные линейных потерь");
        return flowLitresPerSecond * flowLitresPerSecond * lengthM / (100.0 * resistanceK);
    }
}
