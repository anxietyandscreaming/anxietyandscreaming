namespace Clair.Common.RazorLib.TreeViews.Models;

public record TreeViewRenderer(
    Type DynamicComponentType,
    Dictionary<string, object?>? DynamicComponentParameters);
