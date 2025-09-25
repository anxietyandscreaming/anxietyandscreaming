using Clair.TextEditor.RazorLib.Lexers.Models;

namespace Clair.CompilerServices.Css;

public struct CssLexerOutput
{
    public CssLexerOutput(List<TextEditorTextSpan> textSpanList)
    {
        TextSpanList = textSpanList;
    }
    
    public List<TextEditorTextSpan> TextSpanList { get; }
}
