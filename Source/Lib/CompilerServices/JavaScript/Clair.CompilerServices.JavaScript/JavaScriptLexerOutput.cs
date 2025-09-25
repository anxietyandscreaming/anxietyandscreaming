using Clair.TextEditor.RazorLib.Lexers.Models;

namespace Clair.CompilerServices.JavaScript;

public struct JavaScriptLexerOutput
{
    public JavaScriptLexerOutput(List<TextEditorTextSpan> textSpanList)
    {
        TextSpanList = textSpanList;
    }
    
    public List<TextEditorTextSpan> TextSpanList { get; }
}
