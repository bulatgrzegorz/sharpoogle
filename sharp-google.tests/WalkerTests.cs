using FluentAssertions;
using Microsoft.CodeAnalysis.CSharp;

namespace sharp_google.tests;

public class WalkerTests
{
    [Theory]
    [InlineData("void(int, bool?)")]
    [InlineData("void(int,bool?)")]
    [InlineData("   void   (  int  ,   bool?   )")]
    public void InputQueryWalker_SimpleQuery_WillBeParsedWell(string query)
    {
        var inputWalker = new InputQueryWalker();
        inputWalker.Visit(CSharpSyntaxTree.ParseText(query).GetRoot());

        Assert.NotNull(inputWalker.QueryDefinition);
        Assert.Equal("void", inputWalker.QueryDefinition.Value.ReturnType);
        
        var expectedArguments = new []{"int", "bool?"};
        Assert.Equal(expectedArguments, inputWalker.QueryDefinition.Value.Arguments);
    }
    
    [Theory]
    [InlineData("void()")]
    [InlineData("void (  )")]
    [InlineData("void")]
    public void InputQueryWalker_EmptyArguments_WillBeParsedWell(string query)
    {
        var inputWalker = new InputQueryWalker();
        inputWalker.Visit(CSharpSyntaxTree.ParseText(query).GetRoot());

        Assert.NotNull(inputWalker.QueryDefinition);
        Assert.Equal("void", inputWalker.QueryDefinition.Value.ReturnType);
        
        Assert.Empty(inputWalker.QueryDefinition.Value.Arguments);
    }
    
    [Theory]
    [InlineData("Task<int>(ISomeInterface<IOtherOne<bool>>)")]
    public void InputQueryWalker_GenericTypes_WillBeParsedWell(string query)
    {
        var inputWalker = new InputQueryWalker();
        inputWalker.Visit(CSharpSyntaxTree.ParseText(query).GetRoot());

        Assert.NotNull(inputWalker.QueryDefinition);
        Assert.Equal("Task<int>", inputWalker.QueryDefinition.Value.ReturnType);
        var expectedArguments = new []{"ISomeInterface<IOtherOne<bool>>"};
        Assert.Equal(expectedArguments, inputWalker.QueryDefinition.Value.Arguments);
    }
    
    [Theory]
    [InlineData("SomeClass(SomeOtherClass, x)")]
    public void InputQueryWalker_CustomTypes_WillBeParsedWell(string query)
    {
        var inputWalker = new InputQueryWalker();
        inputWalker.Visit(CSharpSyntaxTree.ParseText(query).GetRoot());

        Assert.NotNull(inputWalker.QueryDefinition);
        Assert.Equal("SomeClass", inputWalker.QueryDefinition.Value.ReturnType);
        var expectedArguments = new []{"SomeOtherClass", "x"};
        Assert.Equal(expectedArguments, inputWalker.QueryDefinition.Value.Arguments);
    }

    [Fact]
    public void MethodWalker_WillCorrectlyParseGivenCsharpScript()
    {
        var path = "C:/SomePath/file.cs";
        
        var methodWalker = new MethodWalker();
        methodWalker.Visit(CSharpSyntaxTree.ParseText(SomeCsharp, path: path).GetRoot());

        Assert.NotEmpty(methodWalker.Methods);
        var expectedArguments = new[]
        {
            new MethodDefinition("Debug", "void", Array.Empty<string>(), (path, 18, 8)),
            new MethodDefinition("Handle", "Task", new []{"Params", "RequestContext<Result>"}, (path, 13, 8)),
            new MethodDefinition("InitializeService", "void", new []{"ServiceHost"}, (path, 11, 8)),
        };

        expectedArguments.Should().BeEquivalentTo(methodWalker.Methods.ToArray());
    }
    
    private const string SomeCsharp = """
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Microsoft.SqlServer.Dac.Compare;
using Microsoft.SqlTools.Hosting.Protocol;
using Microsoft.SqlTools.ServiceLayer.Connection;

namespace Some.Namespace
{
    class SomeClass
    {
        public void InitializeService(ServiceHost serviceHost){ }

        public Task Handle(Params parameters, RequestContext<Result> requestContext)
        {
            return Task.CompletedTask;
        }

        private void Debug(){}
    }
}
""";
}