using Clair.TextEditor.RazorLib.Exceptions;
using Clair.TextEditor.RazorLib.Lexers.Models;
using Clair.TextEditor.RazorLib.Decorations.Models;
using Clair.Extensions.CompilerServices.Syntax;
using Clair.Extensions.CompilerServices.Syntax.Utility;
using Clair.CompilerServices.CSharp.LexerCase;
using Clair.CompilerServices.CSharp.ParserCase;
using Clair.CompilerServices.CSharp.BinderCase;

namespace Clair.CompilerServices.CSharp.CompilerServiceCase;

/// <summary>
/// ReInitialize must be invoked at the start of every "new usage" of the pooled instance.
/// This invocation having occurred is NOT asserted, so neglecting to invoke it is undefined behavior.
/// </summary>
public class TokenWalkerBuffer
{
    private TextEditorTextSpan _previousEscapeCharacterTextSpan;
    private int _interpolatedExpressionUnmatchedBraceCount;
    
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
    
    /// <summary>Trust that ReInitialize(/*...*/) will be invoked is presumed here.</summary>
    private CSharpBinder _binder = null!;

    public List<TextEditorTextSpan> MiscTextSpanList { get; private set; }
    
    public int ConsumeCounter { get; private set; }
    
    public StreamReaderPooledBufferWrap StreamReaderWrap { get; set; }

    // With respect to the StreamReaderPooledBufferWrap, this relates to reading 1 character at a time.
    // In terms of connecting each part of the StreamReaderPooledBuffer to its respective wording in TokenWalkerPooledBuffer.
    // Thus this reads in 1 SyntaxToken at a time. I will continue with that mindset for now. I am aware that it isn't "great".
    private SyntaxToken[] _syntaxTokenBuffer = new SyntaxToken[1];

    /// <summary>
    /// I'm pretty sure the PositionIndex is '_positionIndex - PeekSize;'
    /// But I'm adding it here cause I'm tired and don't want to end up in a possible rabbit hole over this right now.
    /// </summary>
    private (SyntaxToken SyntaxToken, int PositionIndex)[] _peekBuffer = new (SyntaxToken SyntaxToken, int PositionIndex)[3]; // largest Peek is 2
    private int _peekIndex = -1;
    private int _peekSize = 0;

    private (SyntaxToken SyntaxToken, int PositionIndex) _backtrackTuple;

    private int _index;
    public int Index
    {
        get
        {
            if (_peekIndex == -1)
            {
                return _index;
            }
            else
            {
                return _peekBuffer[_peekIndex].PositionIndex;
            }
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
                return _peekBuffer[_peekIndex].SyntaxToken;
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
                    return _peekBuffer[_peekIndex + 1].SyntaxToken;
                }
                else
                {
                    return _syntaxTokenBuffer[0];
                }
            }
        }
    }

    /// <summary>
    /// ReInitialize must be invoked at the start of every "new usage" of the pooled instance.
    /// This invocation having occurred is NOT asserted, so neglecting to invoke it is undefined behavior.
    /// </summary>
    public void ReInitialize(
        CSharpBinder binder,
        ResourceUri resourceUri,
        List<TextEditorTextSpan> miscTextSpanList,
        TokenWalkerBuffer tokenWalkerBuffer,
        StreamReaderPooledBufferWrap streamReaderWrap,
        bool shouldUseSharedStringWalker)
    {
        _binder = binder;
    
        _index = -1;
        ConsumeCounter = 0;
        _deferredParsingTuple = (-1, -1, -1);
        _deferredParsingTupleStack.Clear();
    
        MiscTextSpanList = miscTextSpanList;
        
        _previousEscapeCharacterTextSpan = new TextEditorTextSpan(
            0,
            0,
            (byte)GenericDecorationKind.None);
            
        _interpolatedExpressionUnmatchedBraceCount = -1;
        
        StreamReaderWrap = streamReaderWrap;
        
        // You probably don't have to set these to default because they just get overwritten when the time comes.
        // But I'm unsure, and there is far more valuable changes to be made so I'm just gonna set them to default.
        _peekBuffer[0] = default;
        _peekBuffer[1] = default;
        _peekBuffer[2] = default;

        _peekIndex = -1;
        _peekSize = 0;

        _backtrackTuple = default;
        
        // Incase the implementation details of 'Consume' ever change just explicitly invoke it so the changes reflect here too.
        _ = Consume();
        ConsumeCounterReset();
        
        Console.WriteLine("\n====");
        Console.WriteLine($"current:{Current.SyntaxKind}");
        Console.WriteLine($"Next:{Next.SyntaxKind}");
        Console.WriteLine($"current:{Current.SyntaxKind}");
        Console.WriteLine("====");
    }

    public SyntaxToken Consume()
    {
        ++ConsumeCounter;

        var consumedToken = Current;
    
        if (_peekIndex != -1)
        {
            _backtrackTuple = _peekBuffer[_peekIndex++];

            if (_peekIndex >= _peekSize)
            {
                _peekIndex = -1;
                _peekSize = 0;
            }
        }
        else
        {
            if (StreamReaderWrap.IsEof)
            {
                var endOfFileTextSpan = new TextEditorTextSpan(
                    StreamReaderWrap.PositionIndex,
                    StreamReaderWrap.PositionIndex,
                    (byte)GenericDecorationKind.None,
                    StreamReaderWrap.ByteIndex);
                _syntaxTokenBuffer[0] = new SyntaxToken(SyntaxKind.EndOfFileToken, endOfFileTextSpan);
                return consumedToken;
            }
            // This is duplicated more than once inside the Peek(int) code.

            _backtrackTuple = (_syntaxTokenBuffer[0], Index);

            ++_index;
            _syntaxTokenBuffer[0] = CSharpLexer.Lex_Frame(
                _binder,
                MiscTextSpanList,
                StreamReaderWrap,
                ref _previousEscapeCharacterTextSpan,
                ref _interpolatedExpressionUnmatchedBraceCount);
            MiscTextSpanList.Add(_syntaxTokenBuffer[0].TextSpan);
        }

        return consumedToken;
    }

    public SyntaxToken Peek(int offset)
    {
        // Peek(1)
        // -------
        //
        // 
        // The '=' represents the StreamReader
        // The '+' represents the "peek buffer position".
        //
        //
        // Before state
        // ------------
        // Abcd
        //  =
        //
        //
        // After state
        // -----------
        // Abcd
        //  +=
        //
        // 
        // The _peekCount = 1
        // The _peekIndex is _peekCount - 1
        //
        // 

        if (offset <= -1)
            throw new ClairTextEditorException($"{nameof(offset)} must be > -1");
        if (offset == 0)
            return _syntaxTokenBuffer[0];

        if (_peekIndex != -1)
        {
            // This means a Peek() was performed,
            // then before the PeekBuffer was fully traversed
            // another peek occurred.
            //
            // I'm hoping that this case just doesn't occur in the Lexer at the moment
            // because I'm quite tired.

            // Followup: this did happen
            // so I'm splitting by cases
            //
            // - Second Peek(int) is within PeekSize
            // - Second Peek(int) is currentCharacter
            // - ...

            if (_peekIndex + offset < _peekSize)
            {
                throw new NotImplementedException();
            }
            // This 'else if' is probably wrong.
            else if (_peekIndex + offset == _peekSize)
            {
                throw new NotImplementedException();
                //return _streamReaderCharBuffer[0];
            }
            else
            {
                throw new NotImplementedException();
                
                /*if (_peekIndex == 0)
                {
                    // Note: this says '<' NOT '<=' (which is probably what people would expect)...
                    // ...I'm tired and worried so I'm just moving extremely one by one.
                    // I know the less than works, maybe the less than equal to works.
                    // I mean it probably should work but I have a somewhat empty fuel tank right now.
                    if (_peekSize < _peekBuffer.Length)
                    {
                        if (_peekSize == 1 && offset == 2)
                        {
                            _peekBuffer[_peekSize] = (_streamReaderCharBuffer[0], PositionIndex, ByteIndex);
                            _peekSize++;

                            // This is duplicated inside the ReadCharacter() code.

                            _streamPositionIndex++;
                            _streamByteIndex += StreamReader.CurrentEncoding.GetByteCount(_streamReaderCharBuffer);
                            StreamReader.Read(_streamReaderCharBuffer);
                            return _streamReaderCharBuffer[0];
                        }
                    }
                }

                throw new NotImplementedException();*/
            }
        }

        for (int i = 0; i < offset; i++)
        {
            // TODO: Peek() before any Read()

            _peekBuffer[i] = (_syntaxTokenBuffer[0], Index);
            _peekIndex++;
            _peekSize++;

            // This is duplicated inside the ReadCharacter() code.

            ++_index;
            _syntaxTokenBuffer[0] = CSharpLexer.Lex_Frame(
                _binder,
                MiscTextSpanList,
                StreamReaderWrap,
                ref _previousEscapeCharacterTextSpan,
                ref _interpolatedExpressionUnmatchedBraceCount);
            MiscTextSpanList.Add(_syntaxTokenBuffer[0].TextSpan);
        }

        // TODO: Peek EOF
        // TODO: Peek overlap EOF
        return _syntaxTokenBuffer[0];
    }

    /// <summary>
    /// Backtrack is somewhat a sub-case of Peek(int)
    /// </summary>
    public void BacktrackNoReturnValue()
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

        if (Index == 0)
            return;

        // This code is a repeat of the Peek() method's for loop but for one iteration

        _peekBuffer[0] = _backtrackTuple;
        _peekIndex++;
        _peekSize++;
        --ConsumeCounter;
    }

    /// <summary>If the syntaxKind passed in does not match the current token, then a syntax token with that syntax kind will be fabricated and then returned instead.</summary>
    public SyntaxToken Match(SyntaxKind expectedSyntaxKind)
    {
        //throw new NotImplementedException();
        
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
        throw new NotImplementedException();
        _index = openTokenIndex;
        _deferredParsingTuple = (openTokenIndex, closeTokenIndex, tokenIndexToRestore);
        _deferredParsingTupleStack.Push((openTokenIndex, closeTokenIndex, tokenIndexToRestore));
        ConsumeCounter++;
    }

    public void SetNullDeferredParsingTuple()
    {
        throw new NotImplementedException();
        _deferredParsingTuple = (-1, -1, -1);
    }

    public void ConsumeCounterReset()
    {
        ConsumeCounter = 0;
    }
    
    public void DeferParsingOfChildScope(ref CSharpParserState parserModel)
    {
        throw new NotImplementedException();
    }

    private SyntaxToken GetBadToken() => new SyntaxToken(SyntaxKind.BadToken, new(0, 0, 0));
    
    public SyntaxToken FabricateToken(SyntaxKind syntaxKind)
    {
        var currentTextSpan = Peek(0).TextSpan;

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
}

