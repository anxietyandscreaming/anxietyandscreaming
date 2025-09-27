using Clair.TextEditor.RazorLib.Lexers.Models;
using Clair.Extensions.CompilerServices.Syntax;

namespace Clair.CompilerServices.CSharp.LexerCase;

public ref struct CSharpLexerOutput
{
    public CSharpLexerOutput(ResourceUri resourceUri, List<TextEditorTextSpan> miscTextSpanList)
    {
        ResourceUri = resourceUri;
        MiscTextSpanList = miscTextSpanList;
    }

    public ResourceUri ResourceUri { get; }
    
    /// <summary>
    /// This list is used within TextEditorEditContext and for the lexers to re-use by clearing it prior to starting the lexing.
    /// </summary>
    internal readonly List<SyntaxToken> LEXER_syntaxTokenList = new();
    
    /// <summary>
    /// MiscTextSpanList contains the comments and the escape characters.
    /// </summary>
    public List<TextEditorTextSpan> MiscTextSpanList { get; }
}
