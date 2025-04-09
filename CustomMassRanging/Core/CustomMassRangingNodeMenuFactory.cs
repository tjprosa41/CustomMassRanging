using Cameca.CustomAnalysis.Interface;
using Cameca.CustomAnalysis.Utilities;
using Prism.Events;

namespace CustomMassRanging;

internal class CustomMassRangingMenuFactory : AnalysisMenuFactoryBase
{
    public CustomMassRangingMenuFactory(IEventAggregator eventAggregator)
        : base(eventAggregator)
    {
    }

    protected override INodeDisplayInfo DisplayInfo => CustomMassRanging.DisplayInfo;
    protected override string NodeUniqueId => CustomMassRanging.UniqueId;
    public override AnalysisMenuLocation Location { get; } = AnalysisMenuLocation.Analysis;
}