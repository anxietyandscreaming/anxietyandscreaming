using Microsoft.AspNetCore.Components;
using Clair.Common.RazorLib;

namespace Clair.Extensions.DotNet.Outputs.Displays;

public partial class OutputPanelDisplay : ComponentBase
{
    [Inject]
    private CommonService CommonService { get; set; } = null!;
}
