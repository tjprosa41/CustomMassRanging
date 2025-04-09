using Cameca.CustomAnalysis.Interface;
using Cameca.CustomAnalysis.Utilities;
using Prism.Ioc;
using Prism.Modularity;

namespace CustomMassRanging;

/// <summary>
/// Public <see cref="IModule"/> implementation is the entry point for AP Suite to discover and configure the custom analysis
/// </summary>
public class CustomMassRangingModule : IModule
{
    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
        containerRegistry.AddCustomAnalysisUtilities(options => options.UseStandardBaseClasses = true);

        containerRegistry.Register<object, CustomMassRanging>(CustomMassRanging.UniqueId);
        containerRegistry.RegisterInstance(CustomMassRanging.DisplayInfo, CustomMassRanging.UniqueId);
        containerRegistry.Register<IAnalysisMenuFactory, CustomMassRangingMenuFactory>(nameof(CustomMassRangingMenuFactory));
        containerRegistry.Register<object, CustomMassRangingViewModel>(CustomMassRangingViewModel.UniqueId);
    }

    public void OnInitialized(IContainerProvider containerProvider)
    {
        var extensionRegistry = containerProvider.Resolve<IExtensionRegistry>();

        extensionRegistry.RegisterAnalysisView<CustomMassRangingView, CustomMassRangingViewModel>(AnalysisViewLocation.Default);
    }
}
