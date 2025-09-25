using Clair.Common.RazorLib;
using Clair.Common.RazorLib.FileSystems.Models;
using Clair.Common.RazorLib.TreeViews.Models;
using Clair.Common.RazorLib.TreeViews.Models.Utils;
using Clair.Common.RazorLib.Keys.Models;

namespace Clair.Ide.RazorLib.FileSystems.Models;

public class TreeViewAbsolutePath : TreeViewWithType<AbsolutePath>
{
    public TreeViewAbsolutePath(
            AbsolutePath absolutePath,
            CommonService commonService,
            bool isExpandable,
            bool isExpanded)
        : base(absolutePath, isExpandable, isExpanded)
    {
        CommonService = commonService;
    }

    public CommonService CommonService { get; }

    public override bool Equals(object? obj)
    {
        if (obj is not TreeViewAbsolutePath treeViewAbsolutePath)
            return false;

        return treeViewAbsolutePath.Item.Value == Item.Value;
    }

    public override int GetHashCode() => Item.Value.GetHashCode();
    
    public override string GetDisplayText() => Item.Name;

    public override async Task LoadChildListAsync()
    {
        try
        {
            var previousChildren = new List<TreeViewNoType>(ChildList);

            var newChildList = new List<TreeViewNoType>();

            if (Item.IsDirectory)
                newChildList = await TreeViewHelperAbsolutePathDirectory.LoadChildrenAsync(this).ConfigureAwait(false);

            ChildList = newChildList;
            LinkChildren(previousChildren, ChildList);
        }
        catch (Exception exception)
        {
            ChildList = new List<TreeViewNoType>
            {
                new TreeViewException(exception, false, false)
                {
                    Parent = this,
                    IndexAmongSiblings = 0,
                }
            };
        }
    }

    public override void RemoveRelatedFilesFromParent(List<TreeViewNoType> siblingsAndSelfTreeViews)
    {
        // This method is meant to do nothing in this case.
    }
}
