using Clair.CompilerServices.CSharp.BinderCase;
using Clair.CompilerServices.CSharp.CompilerServiceCase;
using Clair.CompilerServices.CSharp.LexerCase;
using Clair.CompilerServices.CSharp.ParserCase;
using Clair.TextEditor.RazorLib;
using Clair.TextEditor.RazorLib.CompilerServices;
using Clair.TextEditor.RazorLib.Lexers.Models;

namespace Clair.Tests.csproj;

public class UnitTest1
{
    // Ref struct to make `CSharpLexerOutput` permissible as a property.
    public ref struct Test
    {
        public Test(byte[] contentArray, string contentString)
        {
            TextEditorService = new TextEditorService(
                jsRuntime: null,
                commonService: null);
    
            CompilerService = new CSharpCompilerService(TextEditorService);
    
            Binder = new CSharpBinder(
                TextEditorService,
                CompilerService);
            
            ResourceUri = new ResourceUri("/luthetusUnitTest1.cs");
            
            Content = "int;";

            // CompilationUnit = new CSharpCompilationUnit(CompilationUnitKind.IndividualFile_AllData);

            var compilationUnit = new CSharpCompilationUnit(CompilationUnitKind.IndividualFile_AllData);

            CompilerService._currentFileBeingParsedTuple = (ResourceUri.Value, contentString);
            TextEditorService.EditContext_GetText_Clear();
            
            using MemoryStream ms = new MemoryStream(contentArray);
            using StreamReader sr = new StreamReader(ms);
            var streamReaderWrap = new StreamReaderWrap(sr);
            var lexerOutput = CSharpLexer.Lex(Binder, ResourceUri, streamReaderWrap, shouldUseSharedStringWalker: true);

            Binder.StartCompilationUnit(ResourceUri);
            CSharpParser.Parse(ResourceUri, ref compilationUnit, Binder, ref lexerOutput);
        }
        
        public TextEditorService TextEditorService { get; set; }
        public CSharpCompilerService CompilerService { get; set; }
        public CSharpBinder Binder { get; set; }
        public ResourceUri ResourceUri { get; set; }
        public string Content { get; set; }
        public CSharpCompilationUnit CompilationUnit { get; set; }
    }

    [Fact]
    public void Namespace()
    {
        var test = new Test("namespace Clair;"u8.ToArray(), "namespace Clair;");
    }
}
