using Clair.TextEditor.RazorLib.Exceptions;
using Clair.TextEditor.RazorLib.Lexers.Models;
using Clair.TextEditor.RazorLib.Decorations.Models;
using Clair.Extensions.CompilerServices.Syntax;
using Clair.Extensions.CompilerServices.Syntax.Utility;
using Clair.CompilerServices.CSharp.LexerCase;
using Clair.CompilerServices.CSharp.ParserCase;
using Clair.CompilerServices.CSharp.BinderCase;

namespace Clair.CompilerServices.CSharp.CompilerServiceCase;

// Worries:
// - String interpolation
// - Deferred parsing

// Order of interactions:
// - Is the parser asking the TokenWalkerPooledBufferWrap
// - or is the parser asking the Lexer.
//
// The parser could ask the Lexer if things were written such that the Lexer could statefully handle the buffer
// But with the current static Lexer, it seems more sensible to ask the TokenWalkerPooledBufferWrap
//
// All the states could more or less get merged.
// Having the states separate is not the "end of the world".
// It would just mean the passing of more arguments between each static method invocation
// ...but it can certainly add up in extreme cases.
// 


/*
Parser
    TokenWalkerWrap
        Lexer
            StreamReaderWrap

Parser... CurrentToken
    TokenWalkerWrap...
    
    the return is at the Lexer switch statement.

*/

// It isn't a wrap of the existing TokenWalker, it is a new TokenWalker, "from scratch" that maintains a buffer.
public class TokenWalkerBuffer
{
    public TokenWalkerBuffer()
    {
    }
    
    public List<TextEditorTextSpan> MiscTextSpanList { get; private set; }

    // It needs an in
    public void ReInitialize(
        CSharpBinder binder,
        ResourceUri resourceUri,
        List<TextEditorTextSpan> miscTextSpanList,
        TokenWalkerBuffer tokenWalkerBuffer,
        StreamReaderPooledBufferWrap streamReaderWrap,
        bool shouldUseSharedStringWalker)
    {
        MiscTextSpanList = miscTextSpanList;
        
        var previousEscapeCharacterTextSpan = new TextEditorTextSpan(
            0,
            0,
            (byte)GenericDecorationKind.None);
            
        var interpolatedExpressionUnmatchedBraceCount = -1;
        
        StreamReaderWrap = streamReaderWrap;
        
        // Loop patterns when parsing / etc...
        //
        // - IEnumerable is (I think) the most common one.
        // - I have been using a 'foreach variable' perspective... i.e.: the initial entry is available without me having to invoke any methods.
        //
        // Changing to IEnumerable is not of the highest concern at the moment, because I still have a lot to learn and I don't want to spend time
        // on something like that at the moment.
        
        // Each 'Lex_Frame' would presumably Lex the next SyntaxToken.
        //
        // How this will interact with string interpolation I'm not quite sure yet.
        // - I could lex the entirety of any string interpolation scenarios, probably in the worst case scenario.
        //   ... these lists wouldn't be guaranteed to a smaller capacity than the current LOH scenario, but it certainly would be expected
        //   ... that a capacity decrease overall would occur.
        // 
        // 'Lex_Frame' invocations will therefore move me forward in the StreamReader as well.
        // If the StreamReader is at the 'End of File' then I can probably return an 'End of File' token with respect to the TokenWalkerBuffer.
        
        // CSharpLexer.Lex_Frame(binder, miscTextSpanList, streamReaderWrap, ref previousEscapeCharacterTextSpan, ref interpolatedExpressionUnmatchedBraceCount);
        
        // You probably don't have to set these to default because they just get overwritten when the time comes.
        // But I'm unsure, and there is far more valuable changes to be made so I'm just gonna set them to default.
        _peekBuffer[0] = default;
        _peekBuffer[1] = default;
        _peekBuffer[2] = default;

        _peekIndex = -1;
        _peekSize = 0;

        _backtrackTuple = default;
    }
    
    private StreamReaderPooledBufferWrap StreamReaderWrap { get; set; }

    // With respect to the StreamReaderPooledBufferWrap, this relates to reading 1 character at a time.
    // In terms of connecting each part of the StreamReaderPooledBuffer to its respective wording in TokenWalkerPooledBuffer.
    // Thus this reads in 1 SyntaxToken at a time. I will continue with that mindset for now. I am aware that it isn't "great".
    private SyntaxToken[] _syntaxTokenBuffer = new SyntaxToken[1];

    /// <summary>
    /// I'm pretty sure the PositionIndex is '_positionIndex - PeekSize;'
    /// But I'm adding it here cause I'm tired and don't want to end up in a possible rabbit hole over this right now.
    /// </summary>
    private (SyntaxToken Character, int PositionIndex, int ByteIndex)[] _peekBuffer = new (SyntaxToken Character, int PositionIndex, int ByteIndex)[3]; // largest Peek is 2
    private int _peekIndex = -1;
    private int _peekSize = 0;

    private (SyntaxToken Character, int PositionIndex, int ByteIndex) _backtrackTuple;

    public TokenWalker TokenWalker { get; private set; }

    /// <summary>
    /// The count is unreliable (only accurate when the most recent ReadCharacter() came from the StreamReader.
    /// The main purpose is to check if it is non-zero to indicate you are NOT at the end of the file.
    /// </summary>
    public int LastReadCharacterCount { get; set; } = 1;

    private int _streamPositionIndex;
    public int PositionIndex
    {
        get
        {
            if (_peekIndex == -1)
            {
                return _streamPositionIndex;
            }
            else
            {
                return _peekBuffer[_peekIndex].PositionIndex;
            }
        }
        set
        {
            _streamPositionIndex = value;
        }
    }

    private int _streamByteIndex;
    public int ByteIndex
    {
        get
        {
            if (_peekIndex == -1)
            {
                return _streamByteIndex;
            }
            else
            {
                return _peekBuffer[_peekIndex].ByteIndex;
            }
        }
        set
        {
            _streamByteIndex = value;
        }
    }

    public bool IsEof
    {
        get
        {
            if (_peekIndex == -1)
            {
                return StreamReaderWrap.IsEof;
            }
            else
            {
                // peak immediate end of stream bad
                // peak overlap end of stream bad

                return false;
            }
        }
    }

    public SyntaxToken Current
    {
        get
        {
            if (_peekIndex == -1)
            {
                return _syntaxTokenBuffer[0];
            }
            else
            {
                return _peekBuffer[_peekIndex].Character;
            }
        }
    }

    public SyntaxToken Next
    {
        get
        {
            if (_peekIndex == -1)
            {
                return Peek(1);
            }
            else
            {
                if (_peekIndex + 1 < _peekSize)
                {
                    return _peekBuffer[_peekIndex + 1].Character;
                }
                else
                {
                    return _syntaxTokenBuffer[0];
                }
            }
        }
    }

    public SyntaxToken Consume()
    {
        
        return default;
    }

    public SyntaxToken Peek(int offset)
    {
        return default;
    }

    /// <summary>
    /// Backtrack is somewhat a sub-case of Peek(int)
    /// </summary>
    public void BacktrackSyntaxTokenNoReturnValue()
    {
        if (_peekIndex != -1)
        {
            // This means a Peek() was performed,
            // then before the PeekBuffer was fully traversed
            // another peek occurred.
            //
            // I'm hoping that this case just doesn't occur in the Lexer at the moment
            // because I'm quite tired.
            throw new NotImplementedException();
        }

        if (PositionIndex == 0)
            return;

        // This code is a repeat of the Peek() method's for loop but for one iteration

        _peekBuffer[0] = _backtrackTuple;
        _peekIndex++;
        _peekSize++;

        /*
        // This is duplicated inside the ReadCharacter() code.
        StreamReader.Read(_streamReaderCharBuffer);
        _streamPositionIndex++;
        _streamByteIndex += StreamReader.CurrentEncoding.GetByteCount(_streamReaderCharBuffer);
        */
    }
    
    private int _index;

    /// <summary>
    /// Use '-1' for each int value to indicate 'null' for the entirety of the _deferredParsingTuple;
    /// </summary>
    private (int openTokenIndex, int closeTokenIndex, int tokenIndexToRestore) _deferredParsingTuple = (-1, -1, -1);

    /// <summary>
    /// '-1' should not appear for any of the int values in the stack.
    /// _deferredParsingTuple is the cached Peek() result.
    ///
    /// If this stack is empty, them the cached Peek() result should be '(-1, -1, -1)'.
    /// </summary>
    private Stack<(int openTokenIndex, int closeTokenIndex, int tokenIndexToRestore)>? _deferredParsingTupleStack = new();

    public int ConsumeCounter { get; private set; }

    public IReadOnlyList<SyntaxToken> TokenList { get; private set; }
    public int Index => _index;

    /// <summary>If there are any tokens, then assume the final token is the end of file token. Otherwise, fabricate an end of file token.</summary>
    private SyntaxToken EOF => TokenList.Count > 0
        ? TokenList[TokenList.Count - 1]
        : new SyntaxToken(SyntaxKind.EndOfFileToken, new(0, 0, 0));

    public SyntaxToken Backtrack()
    {
        if (_index > 0)
        {
            _index--;
            ConsumeCounter--;
        }

        return Peek(_index);
    }

    public void BacktrackNoReturnValue()
    {
        if (_index > 0)
        {
            _index--;
            ConsumeCounter--;
        }
    }

    /// <summary>If the syntaxKind passed in does not match the current token, then a syntax token with that syntax kind will be fabricated and then returned instead.</summary>
    public SyntaxToken Match(SyntaxKind expectedSyntaxKind)
    {
        if (Current.SyntaxKind == expectedSyntaxKind)
            return Consume();
        else
            return FabricateToken(expectedSyntaxKind);
    }

    /// <summary>
    /// IF "deferred parsing" continues being used, you need to track the byte position in the file, not the token index.
    /// ... you might be able to parse and then re-evaluate bad references at a later point?
    /// </summary>
    public void DeferredParsing(
        int openTokenIndex,
        int closeTokenIndex,
        int tokenIndexToRestore)
    {
        _index = openTokenIndex;
        _deferredParsingTuple = (openTokenIndex, closeTokenIndex, tokenIndexToRestore);
        _deferredParsingTupleStack.Push((openTokenIndex, closeTokenIndex, tokenIndexToRestore));
        ConsumeCounter++;
    }

    public void SetNullDeferredParsingTuple()
    {
        _deferredParsingTuple = (-1, -1, -1);
    }

    public void ConsumeCounterReset()
    {
        ConsumeCounter = 0;
    }

    public void Reinitialize(List<SyntaxToken> tokenList)
    {
        // TODO: Don't duplicate the constructor here...
#if DEBUG
        if (tokenList.Count > 0 &&
            tokenList[tokenList.Count - 1].SyntaxKind != SyntaxKind.EndOfFileToken)
        {
            throw new Clair.TextEditor.RazorLib.Exceptions.ClairTextEditorException($"The last token must be 'SyntaxKind.EndOfFileToken'.");
        }
#endif
        TokenList = tokenList;

        _index = 0;
        ConsumeCounter = 0;
        _deferredParsingTuple = (-1, -1, -1);
        _deferredParsingTupleStack.Clear();
    }

    private SyntaxToken GetBadToken() => new SyntaxToken(SyntaxKind.BadToken, new(0, 0, 0));
    
    public static SyntaxToken FabricateToken(SyntaxKind syntaxKind)
    {
        // !!! get the currentTextSpan variable
        throw new NotImplementedException("var currentTextSpan = tokenWalker.Peek(0).TextSpan;");
        TextEditorTextSpan currentTextSpan = default;

        switch (syntaxKind)
        {
            case SyntaxKind.CommentMultiLineToken:
                return new SyntaxToken(SyntaxKind.CommentMultiLineToken, currentTextSpan) { IsFabricated = true, };
            case SyntaxKind.CommentSingleLineToken:
                return new SyntaxToken(SyntaxKind.CommentSingleLineToken, currentTextSpan) { IsFabricated = true, };
            case SyntaxKind.IdentifierToken:
                return new SyntaxToken(SyntaxKind.IdentifierToken, currentTextSpan) { IsFabricated = true, };
            case SyntaxKind.NumericLiteralToken:
                return new SyntaxToken(SyntaxKind.NumericLiteralToken, currentTextSpan) { IsFabricated = true, };
            case SyntaxKind.StringLiteralToken:
                return new SyntaxToken(SyntaxKind.StringLiteralToken, currentTextSpan) { IsFabricated = true, };
            case SyntaxKind.TriviaToken:
                return new SyntaxToken(SyntaxKind.TriviaToken, currentTextSpan) { IsFabricated = true, };
            case SyntaxKind.PreprocessorDirectiveToken:
                return new SyntaxToken(SyntaxKind.PreprocessorDirectiveToken, currentTextSpan) { IsFabricated = true, };
            case SyntaxKind.LibraryReferenceToken:
                return new SyntaxToken(SyntaxKind.LibraryReferenceToken, currentTextSpan) { IsFabricated = true, };
            case SyntaxKind.PlusToken:
                return new SyntaxToken(SyntaxKind.PlusToken, currentTextSpan) { IsFabricated = true, };
            case SyntaxKind.PlusPlusToken:
                return new SyntaxToken(SyntaxKind.PlusPlusToken, currentTextSpan) { IsFabricated = true, };
            case SyntaxKind.MinusToken:
                return new SyntaxToken(SyntaxKind.MinusToken, currentTextSpan) { IsFabricated = true, };
            case SyntaxKind.MinusMinusToken:
                return new SyntaxToken(SyntaxKind.MinusMinusToken, currentTextSpan) { IsFabricated = true, };
            case SyntaxKind.EqualsToken:
                return new SyntaxToken(SyntaxKind.EqualsToken, currentTextSpan) { IsFabricated = true, };
            case SyntaxKind.EqualsEqualsToken:
                return new SyntaxToken(SyntaxKind.EqualsEqualsToken, currentTextSpan) { IsFabricated = true, };
            case SyntaxKind.QuestionMarkToken:
                return new SyntaxToken(SyntaxKind.QuestionMarkToken, currentTextSpan) { IsFabricated = true, };
            case SyntaxKind.QuestionMarkQuestionMarkToken:
                return new SyntaxToken(SyntaxKind.QuestionMarkQuestionMarkToken, currentTextSpan) { IsFabricated = true, };
            case SyntaxKind.BangToken:
                return new SyntaxToken(SyntaxKind.BangToken, currentTextSpan) { IsFabricated = true, };
            case SyntaxKind.StatementDelimiterToken:
                return new SyntaxToken(SyntaxKind.StatementDelimiterToken, currentTextSpan) { IsFabricated = true, };
            case SyntaxKind.OpenParenthesisToken:
                return new SyntaxToken(SyntaxKind.OpenParenthesisToken, currentTextSpan) { IsFabricated = true, };
            case SyntaxKind.CloseParenthesisToken:
                return new SyntaxToken(SyntaxKind.CloseParenthesisToken, currentTextSpan) { IsFabricated = true, };
            case SyntaxKind.OpenBraceToken:
                return new SyntaxToken(SyntaxKind.OpenBraceToken, currentTextSpan) { IsFabricated = true, };
            case SyntaxKind.CloseBraceToken:
                return new SyntaxToken(SyntaxKind.CloseBraceToken, currentTextSpan) { IsFabricated = true, };
            case SyntaxKind.OpenAngleBracketToken:
                return new SyntaxToken(SyntaxKind.OpenAngleBracketToken, currentTextSpan) { IsFabricated = true, };
            case SyntaxKind.CloseAngleBracketToken:
                return new SyntaxToken(SyntaxKind.CloseAngleBracketToken, currentTextSpan) { IsFabricated = true, };
            case SyntaxKind.OpenSquareBracketToken:
                return new SyntaxToken(SyntaxKind.OpenSquareBracketToken, currentTextSpan) { IsFabricated = true, };
            case SyntaxKind.CloseSquareBracketToken:
                return new SyntaxToken(SyntaxKind.CloseSquareBracketToken, currentTextSpan) { IsFabricated = true, };
            case SyntaxKind.DollarSignToken:
                return new SyntaxToken(SyntaxKind.DollarSignToken, currentTextSpan) { IsFabricated = true, };
            case SyntaxKind.ColonToken:
                return new SyntaxToken(SyntaxKind.ColonToken, currentTextSpan) { IsFabricated = true, };
            case SyntaxKind.MemberAccessToken:
                return new SyntaxToken(SyntaxKind.MemberAccessToken, currentTextSpan) { IsFabricated = true, };
            case SyntaxKind.CommaToken:
                return new SyntaxToken(SyntaxKind.CommaToken, currentTextSpan) { IsFabricated = true, };
            case SyntaxKind.BadToken:
                return new SyntaxToken(SyntaxKind.BadToken, currentTextSpan) { IsFabricated = true, };
            case SyntaxKind.EndOfFileToken:
                return new SyntaxToken(SyntaxKind.EndOfFileToken, currentTextSpan) { IsFabricated = true, };
            case SyntaxKind.AssociatedNameToken:
                return new SyntaxToken(SyntaxKind.AssociatedNameToken, currentTextSpan) { IsFabricated = true, };
            case SyntaxKind.AssociatedValueToken:
                return new SyntaxToken(SyntaxKind.AssociatedValueToken, currentTextSpan) { IsFabricated = true, };
            case SyntaxKind.OpenAssociatedGroupToken:
                return new SyntaxToken(SyntaxKind.OpenAssociatedGroupToken, currentTextSpan) { IsFabricated = true, };
            case SyntaxKind.CloseAssociatedGroupToken:
                return new SyntaxToken(SyntaxKind.CloseAssociatedGroupToken, currentTextSpan) { IsFabricated = true, };
            default:
                if (syntaxKind.ToString().EndsWith("ContextualKeyword"))
                {
                    return new SyntaxToken(syntaxKind, currentTextSpan) { IsFabricated = true, };
                }
                else if (syntaxKind.ToString().EndsWith("Keyword"))
                {
                    return new SyntaxToken(syntaxKind, currentTextSpan) { IsFabricated = true, };
                }
                else
                {
                    throw new NotImplementedException($"The {nameof(SyntaxKind)}: '{syntaxKind}' was unrecognized.");
                }
        }
    }
    
    public void DeferParsingOfChildScope(ref CSharpParserState parserModel)
    {
        
    }
}

