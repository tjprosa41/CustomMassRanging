using Cameca.CustomAnalysis.Utilities;

namespace CustomMassRanging;

internal class CustomMassRangingViewModel : AnalysisViewModelBase<CustomMassRanging>
{
    public const string UniqueId = "CustomMassRanging.CustomMassRangingViewModel";

    public CustomMassRangingViewModel(IAnalysisViewModelBaseServices services)
        : base(services)
    {
    }
}