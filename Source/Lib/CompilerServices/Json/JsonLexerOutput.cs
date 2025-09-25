using Clair.TextEditor.RazorLib.Lexers.Models;

namespace Clair.CompilerServices.Json;

public struct JsonLexerOutput
{
    public JsonLexerOutput(List<TextEditorTextSpan> textSpanList)
    {
        TextSpanList = textSpanList;
    }
    
    public List<TextEditorTextSpan> TextSpanList { get; }
}
