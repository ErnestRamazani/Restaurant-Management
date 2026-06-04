using EliteRestaurantPro.Localization;
using EliteRestaurantPro.ViewModels;

namespace EliteRestaurantPro.Views.Components;

public partial class ShiftHistoryOverlayView
{
    public ShiftHistoryOverlayView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => ApplyLocalizedColumnHeaders();
        Loc.LanguageChanged += () => ApplyLocalizedColumnHeaders();
        ApplyLocalizedColumnHeaders();
    }

    private void ApplyLocalizedColumnHeaders()
    {
        if (ShiftHistoryGrid.Columns.Count < 7)
            return;

        if (DataContext is AdminBaseViewModel vm)
        {
            ShiftHistoryGrid.Columns[0].Header = vm.ShiftHistoryColDate;
            ShiftHistoryGrid.Columns[1].Header = vm.ShiftHistoryColShift;
            ShiftHistoryGrid.Columns[2].Header = vm.ShiftHistoryColIn;
            ShiftHistoryGrid.Columns[3].Header = vm.ShiftHistoryColOut;
            ShiftHistoryGrid.Columns[4].Header = vm.ShiftHistoryColStatus;
            ShiftHistoryGrid.Columns[5].Header = vm.ShiftHistoryColJustification;
            ShiftHistoryGrid.Columns[6].Header = vm.ShiftHistoryColNotes;
            return;
        }

        ShiftHistoryGrid.Columns[0].Header = Loc.Admin("empShiftHistoryColDate", "Date");
        ShiftHistoryGrid.Columns[1].Header = Loc.Admin("empShiftHistoryColShift", "Shift");
        ShiftHistoryGrid.Columns[2].Header = Loc.Admin("empShiftHistoryColIn", "In");
        ShiftHistoryGrid.Columns[3].Header = Loc.Admin("empShiftHistoryColOut", "Out");
        ShiftHistoryGrid.Columns[4].Header = Loc.Admin("empShiftHistoryColStatus", "Status");
        ShiftHistoryGrid.Columns[5].Header = Loc.Admin("empShiftHistoryColJustification", "Justification");
        ShiftHistoryGrid.Columns[6].Header = Loc.Admin("empShiftHistoryColNotes", "Notes");
    }
}
