using System;
using System.Collections.Generic;
using System.Linq;

namespace FireCalc.Core;

/// <summary>
/// A MANUALLY DEFINED hydraulic route through a piping network.
/// Not a graph solver: the user selects exactly one hydraulic route and supplies flow and Kt per section.
/// Formula is transcribed from user-provided EFER RUP.T pp. 21-22.
/// Formula's MPa units are from the EFER table; density is not dynamically adjusted.
/// </summary>
public sealed record RouteSection(string Name, double LengthM, double FlowLitresPerSecond, double ResistanceKt,
    double? InnerDiameterMm = null);
public sealed record RouteSettings(double LocalLossPercent, double EquipmentLossMeters,
    double RequiredConsumerHeadMeters, double ElevationDifferenceMeters);
public sealed record SectionHydraulicResult(string Name, double LengthM, double FlowLitresPerSecond,
    double ResistanceKt, double LinearLossMpa, double LinearLossMeters,
    double? InnerDiameterMm = null, double? VelocityMs = null);
public sealed record HydraulicRouteResult(
    IReadOnlyList<SectionHydraulicResult> Sections,
    double LengthMeters, double LinearLossMpa, double LinearLossMeters,
    double LocalLossMeters, double EquipmentLossMeters,
    double TotalCircuitLossMeters, double RequiredConsumerHeadMeters,
    double ElevationDifferenceMeters, double RequiredSourceHeadMeters);

public static class HydraulicRouteCalculator
{
    // Equivalent to p / (rho*g), rho=1000 kg/m³, g=9.80665 m/s².
    // This is a transparent constant, NOT an engineering validation of the source methodology.
    public const double MetresPerMpa = 1_000_000d / (1000d * 9.80665d);

    public static HydraulicRouteResult Calculate(IReadOnlyList<RouteSection> route, RouteSettings settings)
    {
        if (route is null || route.Count == 0)
            throw new ArgumentException("Выберите хотя бы один участок диктующей трассы.");
        foreach (var (name, value) in new[] {
            ("Местные потери, %", settings.LocalLossPercent),
            ("Потери оборудования, м", settings.EquipmentLossMeters),
            ("Напор потребителя, м", settings.RequiredConsumerHeadMeters),
            ("Разность отметок, м", settings.ElevationDifferenceMeters) })
            if (!double.IsFinite(value) || (name != "Разность отметок, м" && value < 0))
                throw new ArgumentException($"{name}: недопустимое значение.");
        if (settings.LocalLossPercent > 1000)
            throw new ArgumentException("Местные потери выше 1000% — проверьте ввод.");
        var rows = new List<SectionHydraulicResult>(route.Count);
        foreach (var section in route)
        {
            if (string.IsNullOrWhiteSpace(section.Name))
                throw new ArgumentException("Участок без обозначения.");
            // PipeHydraulics performs strict flow, length, Kt validation.
            var p = PipeHydraulics.LinearLossMpa(section.FlowLitresPerSecond,
                section.LengthM, section.ResistanceKt);
            if (section.InnerDiameterMm.HasValue &&
                (!double.IsFinite(section.InnerDiameterMm.Value) || section.InnerDiameterMm.Value <= 0))
                throw new ArgumentException($"{section.Name}: недопустимый внутренний диаметр.");
            var velocity = section.InnerDiameterMm.HasValue
                ? FlowDiagnostics.VelocityMetersPerSecond(section.FlowLitresPerSecond, section.InnerDiameterMm.Value)
                : (double?)null;
            rows.Add(new SectionHydraulicResult(section.Name, section.LengthM,
                section.FlowLitresPerSecond, section.ResistanceKt, p, p * MetresPerMpa,
                section.InnerDiameterMm, velocity));
        }
        double mpas = rows.Sum(x => x.LinearLossMpa);
        double linear = mpas * MetresPerMpa;
        double local = linear * settings.LocalLossPercent / 100;
        double circuit = linear + local + settings.EquipmentLossMeters;
        return new HydraulicRouteResult(rows, rows.Sum(x=>x.LengthM), mpas, linear,
            local, settings.EquipmentLossMeters, circuit,
            settings.RequiredConsumerHeadMeters, settings.ElevationDifferenceMeters,
            circuit + settings.RequiredConsumerHeadMeters + settings.ElevationDifferenceMeters);
    }
}

public sealed record RouteFixtureLine(string Name, bool Included, double LengthM, double FlowLs, double Kt);
public sealed record RouteFixture(string Name, RouteFixtureLine[] Lines, RouteSettings Settings,
    double PdfCircuitLossMeters, double PdfRequiredHeadMeters);

public static class EferHydraulicFixtures
{
    // Source: RUP.T, hydraulic calculation, printed pp.1-2 (PDF pages 21-22).
    // Excluded branches MUST NOT be added to the dictating route.
    // Printed summary contains discrepancies in the total head; show them, do not hide them.
    public static RouteFixture Painting() => new(
        "ЭФЭР — окрасочный цех / тушение",
        new[] {
            new RouteFixtureLine("1–2", true, 15, 80, 711300),
            new RouteFixtureLine("2–3а", true, 8, 80, 209900),
            new RouteFixtureLine("3а–3б", true, 75, 40, 209900),
            new RouteFixtureLine("3б–4 (соседняя ветвь)", false, 114.8, 42.5, 209900),
            new RouteFixtureLine("3–5", true, 147.2, 37.5, 209900),
            new RouteFixtureLine("4–5", true, 11.6, 2.5, 209900),
            new RouteFixtureLine("5–ПР1.20", true, 1.9, 40, 1429),
        },
        new RouteSettings(20, 5 + .6 + .2 + 5.099 + .25 + 5.3, 61.183, 19.3),
        21.422, 102.2);

    public static RouteFixture Repair() => new(
        "ЭФЭР — ремонтные цеха / тушение",
        new[] {
            new RouteFixtureLine("1–2", true, 15, 160, 711300),
            new RouteFixtureLine("2–3", true, 15, 80, 209900),
            new RouteFixtureLine("3–4 (соседняя ветвь)", false, 247.6, 40.1, 209900),
            new RouteFixtureLine("3–5", true, 251.4, 39.9, 209900),
            new RouteFixtureLine("4–5", true, 12, 0.1, 209900),
            new RouteFixtureLine("5–ПР2.43", true, 2.3, 40, 1429),
        },
        new RouteSettings(20, 5 + .6 + .8 + 5.099 + .25 + 5.3, 61.183, 19.3),
        23.753, 104.5);
}
