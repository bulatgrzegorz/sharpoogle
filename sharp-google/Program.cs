using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using sharp_google;
using Spectre.Console;
using Spectre.Console.Cli;
using static System.Console;

var app = new CommandApp<RunSearchCommand>();
await app.RunAsync(args);

internal readonly record struct QueryDefinition(string ReturnType, string[] Arguments)
{
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append($"{nameof(ReturnType)} = {ReturnType}, {nameof(Arguments)} = ({string.Join(", ", Arguments ?? Array.Empty<string>())})");

        return true;
    }

    public string NormalizeSignature() => $"{ReturnType} ({string.Join(", ", Arguments)})";
}
internal readonly record struct MethodDefinition(string Name, string ReturnType, string[] Arguments, (string SourceFile, int Row, int Column) Location)
{
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append($"{ReturnType} {Name}({string.Join(", ", Arguments)})");

        return true;
    }

    public string NormalizeSignature() => $"{ReturnType} ({string.Join(", ", Arguments)})";
}

internal class InputQueryWalker : CSharpSyntaxWalker
{
    public QueryDefinition? QueryDefinition { get; private set; }
    
    public override void VisitLocalDeclarationStatement(LocalDeclarationStatementSyntax node)
    {
        QueryDefinition = new QueryDefinition(node.ToString(), Array.Empty<string>());
        
        base.VisitLocalDeclarationStatement(node);
    }

    public override void VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        var childNodes = node.ChildNodes().ToList();
        
        //First node should contain identifier of return type (it could be PredefinedType/GenericName/IdentifierName/...)
        var returnType = childNodes[0].ToString();
        var arguments = Array.Empty<string>();

        foreach (var childNode in childNodes)
        {
            Console.WriteLine($"chile note: {childNode.Kind()}, i: {childNode}");
            
            if (childNode.IsKind(SyntaxKind.ArgumentList))
            {
                arguments = ((ArgumentListSyntax)childNode).Arguments.Select(x => x.ToString().Trim()).ToArray();
            }
        }

        QueryDefinition = new QueryDefinition(returnType, arguments);
        
        base.VisitInvocationExpression(node);
    }
}

internal class MethodWalker : CSharpSyntaxWalker
{
    public ConcurrentBag<MethodDefinition> Methods { get; } = new();
    
    public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        var location = node.GetLocation().GetLineSpan();
        
        Methods.Add(new MethodDefinition()
        {
            Name = node.Identifier.ToString(),
            ReturnType = node.ReturnType.ToString(),
            Arguments = node.ParameterList.Parameters.Select(x => x.Type?.ToString().Trim()).Where(x => x != null).ToArray()!,
            Location = (location.Path, location.StartLinePosition.Line, location.StartLinePosition.Character)
        });
        
        base.VisitMethodDeclaration(node);
    }

    public override void VisitLocalFunctionStatement(LocalFunctionStatementSyntax node)
    {
        var location = node.GetLocation().GetLineSpan();
        
        Methods.Add(new MethodDefinition()
        {
            Name = node.Identifier.ToString(),
            ReturnType = node.ReturnType.ToString(),
            Arguments = node.ParameterList.Parameters.Select(x => x.Type?.ToString().Trim()).Where(x => x != null).ToArray()!,
            Location = (location.Path, location.StartLinePosition.Line, location.StartLinePosition.Character)
        });
        
        base.VisitLocalFunctionStatement(node);
    }
}

internal sealed class RunSearchCommand : AsyncCommand<RunSearchCommand.Settings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var inputQueryWalker = new InputQueryWalker();
        inputQueryWalker.Visit(await CSharpSyntaxTree.ParseText(settings.Query).GetRootAsync());

        if (inputQueryWalker.QueryDefinition == null)
        {
            throw new ArgumentException($"Could not parse given query. Please use given format.");
        }

        var normalizedQuerySignature = inputQueryWalker.QueryDefinition.Value.NormalizeSignature();
        
        var searchPattern = settings.SearchPattern ?? "*.*";
        var searchPath = settings.SearchPath ?? Directory.GetCurrentDirectory();

        var files = File.Exists(searchPath) ? new[] { new FileInfo(searchPath) } : new DirectoryInfo(searchPath).GetFiles(searchPattern, SearchOption.AllDirectories);
        
        var progress = AnsiConsole.Progress();
        var methodWalker = new MethodWalker();
        await progress.StartAsync(async ctx =>
        {
            var progressTask = ctx.AddTask("Parsing files", new ProgressTaskSettings() { AutoStart = true, MaxValue = files.Length });
            await Task.WhenAll(files.Select(async file =>
            {
                await ParseFileMethods(file, methodWalker);
                
                progressTask.Increment(1);
            }));
        });

        var methodsSignatureFit = new List<(int distance, MethodDefinition methodDefinition)>();
        foreach (var methodDefinition in methodWalker.Methods)
        {
            var signatureFit = LevenshteinDistance.GetDistance(normalizedQuerySignature, methodDefinition.NormalizeSignature());
            methodsSignatureFit.Add((signatureFit, methodDefinition));
        }

        foreach (var t in methodsSignatureFit.OrderBy(x => x.distance).Take(20))
        {
            WriteLine($"file://{t.methodDefinition.Location.SourceFile.Replace("\\", "/")}:{t.methodDefinition.Location.Row}:{t.methodDefinition.Location.Column} {t.methodDefinition}");
        }

        return 0;
    }
    
    public sealed class Settings : CommandSettings
    {
        [Description("Path to search (file or directory). Defaults to current directory.")]
        [CommandArgument(0, "[searchPath]")]
        public string? SearchPath { get; init; }

        [Description("Pattern to search files in. Defaults to *.cs")]
        [CommandOption("-p|--pattern")]
        [DefaultValue("*.cs")]
        public string? SearchPattern { get; init; }

        [Description("Signature to search for. It should be in format of: \"returnType (arg1, arg2)\".")]
        [CommandOption("-q|--query")]
        public string Query { get; init; } = null!;
    }
    
    private async Task ParseFileMethods(FileInfo fileInfo, MethodWalker methodWalker)
    {
        await using var fileStream = fileInfo.Open(FileMode.Open, FileAccess.Read);
        using var streamReader = new StreamReader(fileStream);
        var fileContent = await streamReader.ReadToEndAsync();
        
        methodWalker.Visit(await CSharpSyntaxTree.ParseText(fileContent, path: fileInfo.FullName).GetRootAsync());
    }

    public override ValidationResult Validate([NotNull]CommandContext context, [NotNull]Settings settings)
    {
        return string.IsNullOrWhiteSpace(settings.Query) ? ValidationResult.Error("Query has to be provided.") : base.Validate(context, settings);
    }
}