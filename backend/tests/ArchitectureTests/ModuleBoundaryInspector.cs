using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace HawalekNow.ArchitectureTests;

internal sealed record ProductionSource(string Layer, string Path, string Text);

internal static class ModuleBoundaryInspector
{
    internal static readonly string[] Layers = ["Domain", "Application", "Infrastructure", "Api"];
    internal static readonly string[] Modules =
        ["Identity", "Merchants", "Catalog", "Locations", "Search", "Favorites", "Reports", "Notifications", "Analytics"];

    private static readonly Dictionary<string, string[]> AllowedLayers = new()
    {
        ["Domain"] = ["Domain"],
        ["Application"] = ["Application", "Domain"],
        ["Infrastructure"] = ["Infrastructure", "Application", "Domain"],
        ["Api"] = ["Api", "Application", "Infrastructure"]
    };

    private static readonly Lazy<PortableExecutableReference[]> PlatformReferences = new(() =>
    {
        var paths = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))?.Split(Path.PathSeparator)
            ?? throw new InvalidOperationException("Platform assemblies unavailable.");
        return paths.Where(path => !Path.GetFileNameWithoutExtension(path).StartsWith("HawalekNow.", StringComparison.Ordinal))
            .Distinct(StringComparer.OrdinalIgnoreCase).Select(path => MetadataReference.CreateFromFile(path)).ToArray();
    });

    internal static string Root
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "global.json")))
                directory = directory.Parent;
            return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
        }
    }

    internal static ProductionSource[] ReadSources(string root) => Layers.SelectMany(layer =>
    {
        var project = Path.Combine(root, "backend", "src", "HawalekNow." + layer);
        return Directory.EnumerateFiles(project, "*.cs", SearchOption.AllDirectories)
            .Where(path => !Path.GetRelativePath(project, path).Split(Path.DirectorySeparatorChar)
                .Any(part => part is "bin" or "obj"))
            .Select(path => new ProductionSource(layer, Path.GetRelativePath(project, path).Replace('\\', '/'), File.ReadAllText(path)));
    }).ToArray();

    internal static IEnumerable<string> LayoutViolations(string root)
    {
        foreach (var layer in Layers)
        {
            var path = Path.Combine(root, "backend", "src", "HawalekNow." + layer, "Modules");
            if (!Directory.Exists(path)) { yield return $"Missing Modules directory: {layer}"; continue; }
            var actual = Directory.EnumerateDirectories(path).Select(Path.GetFileName).Order().ToArray();
            if (!actual.SequenceEqual(Modules.Order())) yield return $"Unexpected module directories: {layer}";
            foreach (var module in Modules)
            {
                var readme = Path.Combine(path, module, "README.md");
                if (!File.Exists(readme)) { yield return $"Missing module ownership file: {layer}/{module}"; continue; }
                var lines = File.ReadAllLines(readme);
                if (!lines.Contains($"Namespace: HawalekNow.{layer}.Modules.{module}") || !lines.Contains($"Owner: {module}"))
                    yield return $"Module ownership/namespace mismatch: {layer}/{module}";
            }
        }
    }

    // The SDK compiler is a test dependency only. In-memory compilations never write production output.
    internal static string[] Inspect(IReadOnlyList<ProductionSource> sources)
    {
        var violations = new HashSet<string>(StringComparer.Ordinal);
        var platform = PlatformReferences.Value;
        var compiled = new List<MetadataReference>();
        foreach (var layer in Layers)
        {
            var layerSources = sources.Where(source => source.Layer == layer).ToArray();
            var trees = layerSources.Select(source => CSharpSyntaxTree.ParseText(source.Text, path: source.Path)).ToArray();
            var project = Path.Combine(Root, "backend", "src", "HawalekNow." + layer);
            var generated = Directory.EnumerateFiles(Path.Combine(project, "obj"), "HawalekNow." + layer + ".GlobalUsings.g.cs", SearchOption.AllDirectories)
                .Where(path => path.Contains(Path.DirectorySeparatorChar + "net10.0" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                .Order(StringComparer.Ordinal).FirstOrDefault()
                ?? throw new InvalidOperationException($"Build {layer} before architecture tests; generated global usings missing.");
            var globals = CSharpSyntaxTree.ParseText(File.ReadAllText(generated), path: "<generated-global-usings>");
            var compilation = CSharpCompilation.Create("HawalekNow." + layer, trees.Append(globals),
                platform.Concat(compiled), new CSharpCompilationOptions(
                    layer == "Api" ? OutputKind.ConsoleApplication : OutputKind.DynamicallyLinkedLibrary,
                    nullableContextOptions: NullableContextOptions.Enable));
            var errors = compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToArray();
            foreach (var error in errors) violations.Add($"Compilation: {layer}: {error}");
            for (var index = 0; index < trees.Length; index++)
            {
                var source = layerSources[index];
                var tree = trees[index];
                var model = compilation.GetSemanticModel(tree);
                var identity = SourceIdentity(source);
                if (identity is null) { violations.Add($"Ownership path: {layer}/{source.Path}"); continue; }
                var (module, expectedNamespace, composition) = identity.Value;
                var root = tree.GetRoot();
                if (!composition)
                {
                    var declarations = root.DescendantNodes().OfType<BaseNamespaceDeclarationSyntax>()
                        .Where(node => !node.DescendantNodes().OfType<BaseNamespaceDeclarationSyntax>().Any()).ToArray();
                    if (declarations.Length == 0) violations.Add($"Namespace: missing in {layer}/{source.Path}");
                    foreach (var declaration in declarations)
                    {
                        var name = model.GetDeclaredSymbol(declaration)?.ToDisplayString();
                        if (name != expectedNamespace) violations.Add($"Namespace: {layer}/{source.Path}: expected {expectedNamespace}, found {name}");
                    }
                    foreach (var declaration in root.DescendantNodes().Where(node => node is BaseTypeDeclarationSyntax or DelegateDeclarationSyntax))
                    {
                        if (model.GetDeclaredSymbol(declaration) is INamedTypeSymbol type && type.ContainingNamespace.ToDisplayString() != expectedNamespace)
                            violations.Add($"Namespace: misplaced type in {layer}/{source.Path}");
                    }
                }

                foreach (var node in root.DescendantNodes())
                {
                    // GetSymbolInfo resolves aliases, fully qualified names, constructors, attributes and members.
                    var symbol = model.GetSymbolInfo(node).Symbol;
                    if (symbol is IAliasSymbol alias) symbol = alias.Target;
                    if (node is UsingDirectiveSyntax use && use.Name is not null)
                    {
                        var imported = model.GetSymbolInfo(use.Name).Symbol;
                        if (imported is INamespaceSymbol ns) CheckNamespace(ns.ToDisplayString(), false);
                    }
                    var types = new List<ITypeSymbol?> { model.GetTypeInfo(node).Type };
                    switch (symbol)
                    {
                        case ITypeSymbol type: types.Add(type); break;
                        case IMethodSymbol method:
                            types.Add(method.ContainingType); types.Add(method.ReturnType);
                            types.AddRange(method.Parameters.Select(parameter => parameter.Type));
                            types.AddRange(method.TypeArguments); break;
                        case IPropertySymbol property: types.Add(property.ContainingType); types.Add(property.Type); break;
                        case IFieldSymbol field: types.Add(field.ContainingType); types.Add(field.Type); break;
                        case IEventSymbol evt: types.Add(evt.ContainingType); types.Add(evt.Type); break;
                        case ILocalSymbol local: types.Add(local.Type); break;
                        case IParameterSymbol parameter: types.Add(parameter.Type); break;
                    }
                    foreach (var type in types) CheckType(type);
                }

                void CheckType(ITypeSymbol? type)
                {
                    switch (type)
                    {
                        case INamedTypeSymbol named:
                            // Synthesized tuple/anonymous symbols can have no namespace; still inspect all members' types.
                            var targetNamespace = named.ContainingNamespace?.ToDisplayString() ?? "";
                            CheckNamespace(targetNamespace, true);
                            var targetParts = targetNamespace.Split('.');
                            if (!composition && targetParts.Length >= 5 && targetParts[0] == "HawalekNow" &&
                                targetParts[1] == "Application" && targetParts[2] == "Modules" &&
                                targetParts[3] != module && targetParts[4] == "Contracts" &&
                                named.DeclaredAccessibility != Accessibility.Public)
                                violations.Add($"Nonpublic module contract: {layer}/{source.Path} -> {named.ToDisplayString()}");
                            foreach (var argument in named.TypeArguments) CheckType(argument);
                            if (named.ContainingType is not null) CheckType(named.ContainingType);
                            break;
                        case IArrayTypeSymbol array: CheckType(array.ElementType); break;
                        case IPointerTypeSymbol pointer: CheckType(pointer.PointedAtType); break;
                    }
                }

                void CheckNamespace(string targetNamespace, bool typeReference)
                {
                    if (!targetNamespace.StartsWith("HawalekNow.", StringComparison.Ordinal)) return;
                    var parts = targetNamespace.Split('.');
                    if (parts.Length < 2 || !Layers.Contains(parts[1]))
                    { violations.Add($"Unclassified production namespace: {layer}/{source.Path} -> {targetNamespace}"); return; }
                    var targetLayer = parts[1];
                    if (!AllowedLayers[layer].Contains(targetLayer))
                    { violations.Add($"Layer dependency: {layer}/{source.Path} -> {targetNamespace}"); return; }
                    if (composition) return;
                    var targetShared = parts.Length >= 3 && parts[2] == "Shared";
                    var targetModule = parts.Length >= 4 && parts[2] == "Modules" ? parts[3] : null;
                    var contract = targetLayer == "Application" && parts.Length >= 5 && parts[4] == "Contracts";
                    var contractPrefix = $"HawalekNow.{layer}.Modules.{module}.Contracts";
                    var sourceContract = expectedNamespace == contractPrefix || expectedNamespace.StartsWith(contractPrefix + ".", StringComparison.Ordinal);
                    if (module == "Shared" && targetModule is not null)
                    { violations.Add($"Shared dependency: {layer}/{source.Path} -> {targetNamespace}"); return; }
                    if (sourceContract && typeReference && !contract && !targetShared)
                    { violations.Add($"Contract exposes implementation: {layer}/{source.Path} -> {targetNamespace}"); return; }
                    if (layer == "Api" && targetLayer == "Infrastructure")
                    { violations.Add($"Api implementation dependency: {source.Path} -> {targetNamespace}"); return; }
                    if (targetShared) return;
                    if (targetModule is null || !Modules.Contains(targetModule))
                    { violations.Add($"Unclassified production namespace: {layer}/{source.Path} -> {targetNamespace}"); return; }
                    if (targetModule != module && !contract)
                        violations.Add($"Module dependency: {layer}/{source.Path} -> {targetNamespace}");
                }
            }
            using var stream = new MemoryStream();
            var emitted = compilation.Emit(stream);
            if (!emitted.Success)
            {
                foreach (var error in emitted.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error))
                    violations.Add($"Compilation: {layer}: {error}");
                break;
            }
            compiled.Add(MetadataReference.CreateFromImage(stream.ToArray()));
        }
        return violations.Order(StringComparer.Ordinal).ToArray();
    }

    private static (string Module, string Namespace, bool Composition)? SourceIdentity(ProductionSource source)
    {
        if (source.Layer == "Api" && source.Path == "Program.cs") return ("", "", true);
        var parts = source.Path.Split('/');
        if (parts.Length < 2) return null;
        var module = parts[0] == "Shared" ? "Shared" : parts.Length >= 3 && parts[0] == "Modules" ? parts[1] : null;
        if (module is null || (module != "Shared" && !Modules.Contains(module))) return null;
        if (parts.Skip(2).SkipLast(1).Contains("Contracts") && (source.Layer != "Application" || parts[2] != "Contracts")) return null;
        return (module, "HawalekNow." + source.Layer + "." + string.Join('.', parts.SkipLast(1)), false);
    }
}
