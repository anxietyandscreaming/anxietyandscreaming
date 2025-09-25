using Clair.TextEditor.RazorLib.Lexers.Models;

namespace Clair.CompilerServices.Razor;

public struct RazorLexerOutput
{
    public RazorLexerOutput(List<TextEditorTextSpan> textSpan)
    {
        TextSpanList = textSpan;
    }
    
    public List<TextEditorTextSpan> TextSpanList { get; }
}
