using System.Text;
using Clair.Common.RazorLib.Keys.Models;
using Clair.Common.RazorLib.TreeViews.Models;
using Clair.Common.RazorLib.FileSystems.Models;

namespace Clair.Ide.RazorLib.FileSystems.Models;

public class TreeViewHelperAbsolutePathDirectory
{
    public static async Task<List<TreeViewNoType>> LoadChildrenAsync(TreeViewAbsolutePath directoryTreeView)
    {
        var directoryAbsolutePathString = directoryTreeView.Item.Value;

        var directoryPathStringsList = await directoryTreeView.CommonService.FileSystemProvider.Directory
            .GetDirectoriesAsync(directoryAbsolutePathString)
            .ConfigureAwait(false);

        var tokenBuilder = new StringBuilder();
        var formattedBuilder = new StringBuilder();
        
        var childDirectoryTreeViewModels = directoryPathStringsList
            .OrderBy(pathString => pathString)
            .Select(x =>
            {
                return (TreeViewNoType)new TreeViewAbsolutePath(
                    new AbsolutePath(x, true, directoryTreeView.CommonService.FileSystemProvider, tokenBuilder, formattedBuilder, AbsolutePathNameKind.NameWithExtension),
                    directoryTreeView.CommonService,
                    true,
                    false);
            });

        var filePathStringsList = await directoryTreeView.CommonService.FileSystemProvider.Directory
            .GetFilesAsync(directoryAbsolutePathString)
            .ConfigureAwait(false);

        var childFileTreeViewModels = filePathStringsList
            .OrderBy(pathString => pathString)
            .Select(x =>
            {
                return (TreeViewNoType)new TreeViewAbsolutePath(
                    new AbsolutePath(x, false, directoryTreeView.CommonService.FileSystemProvider, tokenBuilder, formattedBuilder, AbsolutePathNameKind.NameWithExtension),
                    directoryTreeView.CommonService,
                    false,
                    false);
            });

        return childDirectoryTreeViewModels.Union(childFileTreeViewModels).ToList();
    }
}
