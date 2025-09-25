using Clair.TextEditor.RazorLib.Lexers.Models;

namespace Clair.CompilerServices.Xml;

public struct XmlLexerOutput
{
    public XmlLexerOutput(List<TextEditorTextSpan> textSpanList)
    {
        TextSpanList = textSpanList;
    }
    
    public List<TextEditorTextSpan> TextSpanList { get; }
}
