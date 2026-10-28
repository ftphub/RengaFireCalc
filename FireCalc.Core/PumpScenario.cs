namespace FireCalc.Core;

/// <summary>
/// A declaration of operating assumptions only. No Q-H curve is inferred from BIM geometry.
/// </summary>
public enum PumpMode { Unspecified = 0, DutyStandby = 1, Simultaneous = 2, Manual = 3 }

public static class PumpScenario
{
    public static string Describe(PumpMode mode, IReadOnlyList<int> pumpObjectIds)
    {
        if (pumpObjectIds.Distinct().Count() != pumpObjectIds.Count || pumpObjectIds.Any(id=>id<=0))
            throw new ArgumentException("ID насосов должны быть положительными и не повторяться.");
        return mode switch
        {
            PumpMode.Unspecified => "Режим насосов не определён. Расчёт совместной работы недоступен.",
            PumpMode.DutyStandby when pumpObjectIds.Count == 2 =>
                "Один насос рабочий, второй резервный. Назначение конкретных насосов, автоматику и характеристики подтвердить отдельно.",
            PumpMode.Simultaneous when pumpObjectIds.Count == 2 =>
                "Два рабочих насоса. Точки работы и распределение расходов не определены без кривых Q–H и схемы обвязки.",
            PumpMode.Manual when pumpObjectIds.Count >= 1 =>
                "Ручной сценарий выбора насосов. Используется только как описание исходных условий, не результат гидравлической увязки.",
            _ => throw new ArgumentException("Укажите необходимые ID насосов для выбранного сценария (для двухнасосных режимов ровно 2).")
        };
    }
}
