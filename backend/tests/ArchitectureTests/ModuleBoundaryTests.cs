namespace HawalekNow.ArchitectureTests;

public sealed class ModuleBoundaryTests
{
    private static readonly Lazy<ProductionSource[]> RealSources = new(() => ModuleBoundaryInspector.ReadSources(ModuleBoundaryInspector.Root));
    private static readonly Lazy<string[]> RealViolations = new(() => ModuleBoundaryInspector.Inspect(RealSources.Value));
    private static readonly ProductionSource[] Fixture =
    [
        Source("Domain", "Catalog", "Product.cs", "public sealed class Product { }"),
        Source("Domain", "Merchants", "MerchantAccount.cs", "public sealed class MerchantAccount { }"),
        Source("Domain", "Merchants", "MerchantBase.cs", "public class MerchantBase { }"),
        Source("Domain", "Merchants", "IMerchantData.cs", "public interface IMerchantData { }"),
        Source("Domain", "Merchants", "MerchantMarkerAttribute.cs", "public sealed class MerchantMarkerAttribute : System.Attribute { }"),
        Source("Application", "Catalog", "ProductHandler.cs", "public sealed class ProductHandler { }"),
        Source("Application", "Merchants", "MerchantHandler.cs", "public sealed class MerchantHandler { }"),
        Source("Application", "Catalog", "Contracts/ProductInfo.cs", "public sealed record ProductInfo(string Name);"),
        Source("Application", "Merchants", "Contracts/MerchantStatus.cs", "public sealed record MerchantStatus(bool Approved);"),
        Source("Application", "Merchants", "Contracts/InternalStatus.cs", "internal sealed record InternalStatus(bool Approved);"),
        Source("Infrastructure", "Merchants", "MerchantStore.cs", "public sealed class MerchantStore { }"),
        new("Api", "Program.cs", "System.Console.WriteLine(\"isolated architecture fixture\");")
    ];
    private static readonly Lazy<string[]> FixtureViolations = new(() => ModuleBoundaryInspector.Inspect(Fixture));

    [Fact]
    public void All_four_layers_have_exactly_the_nine_owned_module_directories_and_namespaces() =>
        Assert.Empty(ModuleBoundaryInspector.LayoutViolations(ModuleBoundaryInspector.Root));

    [Fact]
    public void All_real_production_sources_follow_ownership_namespaces_layers_and_module_rules() =>
        Assert.Empty(RealViolations.Value);

    public static TheoryData<string, string, string, string, string> ForbiddenSources => new()
    {
        { "Domain", "Catalog", "Probe.cs", "public sealed class Probe { public HawalekNow.Domain.Modules.Merchants.MerchantAccount? Account { get; set; } }", "Module dependency:" },
        { "Domain", "Catalog", "Probe.cs", "using Account = HawalekNow.Domain.Modules.Merchants.MerchantAccount; public sealed class Probe { public Account? Value { get; set; } }", "Module dependency:" },
        { "Domain", "Catalog", "Probe.cs", "using HawalekNow.Domain.Modules.Merchants; public sealed class Probe { }", "Module dependency:" },
        { "Domain", "Catalog", "Probe.cs", "public sealed class Probe : HawalekNow.Domain.Modules.Merchants.MerchantBase { }", "Module dependency:" },
        { "Domain", "Catalog", "Probe.cs", "[HawalekNow.Domain.Modules.Merchants.MerchantMarker] public sealed class Probe { }", "Module dependency:" },
        { "Domain", "Catalog", "Probe.cs", "public sealed class Probe { public void Use(HawalekNow.Domain.Modules.Merchants.MerchantAccount value) { } }", "Module dependency:" },
        { "Domain", "Catalog", "Probe.cs", "public sealed class Probe<T> where T : HawalekNow.Domain.Modules.Merchants.IMerchantData { }", "Module dependency:" },
        { "Application", "Catalog", "Probe.cs", "public sealed class Probe { public HawalekNow.Application.Modules.Merchants.MerchantHandler? Handler { get; set; } }", "Module dependency:" },
        { "Application", "Catalog", "Probe.cs", "internal sealed record Probe(HawalekNow.Application.Modules.Merchants.Contracts.InternalStatus Status);", "Nonpublic module contract:" },
        { "Application", "Catalog", "Probe.cs", "public sealed class Probe { public HawalekNow.Domain.Modules.Merchants.MerchantAccount? Account { get; set; } }", "Module dependency:" },
        { "Application", "Catalog", "Probe.cs", "public sealed class Probe { public System.Collections.Generic.List<HawalekNow.Domain.Modules.Merchants.MerchantAccount> Items { get; } = []; }", "Module dependency:" },
        { "Infrastructure", "Catalog", "Probe.cs", "public sealed class Probe { public HawalekNow.Infrastructure.Modules.Merchants.MerchantStore? Store { get; set; } }", "Module dependency:" },
        { "Api", "Catalog", "Probe.cs", "public sealed class Probe { public HawalekNow.Infrastructure.Modules.Merchants.MerchantStore? Store { get; set; } }", "Api implementation dependency:" },
        { "Api", "Catalog", "Probe.cs", "public sealed class Probe { public HawalekNow.Domain.Modules.Catalog.Product? Value { get; set; } }", "Layer dependency:" },
        { "Application", "Catalog", "Contracts/Leak.cs", "public sealed record Leak(HawalekNow.Domain.Modules.Catalog.Product Value);", "Contract exposes implementation:" },
        { "Application", "Catalog", "Contracts/Leak.cs", "public sealed record Leak(HawalekNow.Application.Modules.Catalog.ProductHandler Value);", "Contract exposes implementation:" },
        { "Domain", "Catalog", "Contracts/Probe.cs", "public sealed class Probe { }", "Ownership path:" }
    };

    [Theory]
    [MemberData(nameof(ForbiddenSources))]
    public void Semantically_valid_isolated_dependency_mutations_are_rejected(
        string layer, string module, string path, string body, string expected)
    {
        Assert.Empty(RealViolations.Value);
        Assert.Empty(FixtureViolations.Value);
        var before = ModuleBoundaryInspector.ReadSources(ModuleBoundaryInspector.Root);
        var mutated = Fixture.Append(Source(layer, module, path, body)).ToArray();
        var violations = ModuleBoundaryInspector.Inspect(mutated);
        Assert.DoesNotContain(violations, violation => violation.StartsWith("Compilation:"));
        Assert.Contains(violations, violation => violation.StartsWith(expected));
        Assert.Equal(before, ModuleBoundaryInspector.ReadSources(ModuleBoundaryInspector.Root));
    }

    [Theory]
    [InlineData("Domain", "Catalog", "Probe.cs", "public sealed class Probe { public Product? Value { get; set; } }")]
    [InlineData("Application", "Catalog", "Probe.cs", "public sealed class Probe { public HawalekNow.Domain.Modules.Catalog.Product? Value { get; set; } }")]
    [InlineData("Application", "Catalog", "Probe.cs", "using Status = HawalekNow.Application.Modules.Merchants.Contracts.MerchantStatus; public sealed record Probe(Status Status);")]
    [InlineData("Infrastructure", "Catalog", "Probe.cs", "public sealed class Probe { public HawalekNow.Domain.Modules.Catalog.Product? Value { get; set; } public HawalekNow.Application.Modules.Catalog.ProductHandler? Handler { get; set; } }")]
    [InlineData("Api", "Catalog", "Probe.cs", "public sealed record Probe(HawalekNow.Application.Modules.Catalog.Contracts.ProductInfo Product, HawalekNow.Application.Modules.Merchants.Contracts.MerchantStatus Merchant);")]
    [InlineData("Application", "Catalog", "Contracts/Probe.cs", "public sealed record Probe(string Name);")]
    public void Isolated_approved_dependencies_and_contracts_are_accepted(string layer, string module, string path, string body)
    {
        Assert.Empty(FixtureViolations.Value);
        Assert.Empty(ModuleBoundaryInspector.Inspect(Fixture.Append(Source(layer, module, path, body)).ToArray()));
    }

    [Theory]
    [InlineData("namespace HawalekNow.Domain.Modules.Merchants; public sealed class Probe { }", "Namespace:")]
    [InlineData("public sealed class Probe { }", "Namespace:")]
    [InlineData("namespace HawalekNow.Domain.Modules.Catalog; public sealed class Probe { public System.Collections.Generic.List< { }", "Compilation:")]
    public void Wrong_or_missing_namespace_and_uncompilable_source_cannot_pass(string text, string expected)
    {
        Assert.Empty(FixtureViolations.Value);
        var source = new ProductionSource("Domain", "Modules/Catalog/Probe.cs", text);
        Assert.Contains(ModuleBoundaryInspector.Inspect(Fixture.Append(source).ToArray()), violation => violation.StartsWith(expected));
    }

    [Fact]
    public void Module_type_cannot_hide_at_project_root()
    {
        Assert.Empty(FixtureViolations.Value);
        var source = new ProductionSource("Domain", "Probe.cs", "namespace HawalekNow.Domain.Modules.Catalog; public sealed class Probe { }");
        Assert.Contains(ModuleBoundaryInspector.Inspect(Fixture.Append(source).ToArray()), violation => violation.StartsWith("Ownership path:"));
    }

    [Theory]
    [InlineData("Domain")]
    [InlineData("Application")]
    public void Shared_cannot_depend_on_module_types(string layer)
    {
        Assert.Empty(FixtureViolations.Value);
        var source = new ProductionSource(layer, "Shared/Probe.cs",
            $"namespace HawalekNow.{layer}.Shared; public sealed class Probe {{ public HawalekNow.Domain.Modules.Catalog.Product? Value {{ get; set; }} }}");
        var violations = ModuleBoundaryInspector.Inspect(Fixture.Append(source).ToArray());
        Assert.DoesNotContain(violations, violation => violation.StartsWith("Compilation:"));
        Assert.Contains(violations, violation => violation.StartsWith("Shared dependency:"));
    }

    [Fact]
    public void Approved_shared_direction_and_composition_root_are_accepted_in_isolation()
    {
        Assert.Empty(FixtureViolations.Value);
        var sources = Fixture.Where(source => source.Layer != "Api").Concat(
        [
            new ProductionSource("Domain", "Shared/TechnicalValue.cs", "namespace HawalekNow.Domain.Shared; public sealed class TechnicalValue { }"),
            Source("Domain", "Catalog", "Probe.cs", "public sealed record Probe(HawalekNow.Domain.Shared.TechnicalValue Value);"),
            new ProductionSource("Api", "Program.cs", "System.Console.WriteLine(typeof(HawalekNow.Infrastructure.Modules.Merchants.MerchantStore));")
        ]).ToArray();
        Assert.Empty(ModuleBoundaryInspector.Inspect(sources));
    }

    [Fact]
    public void Composition_root_cannot_directly_use_domain_even_via_transitive_reference()
    {
        Assert.Empty(FixtureViolations.Value);
        var sources = Fixture.Where(source => source.Layer != "Api").Append(
            new ProductionSource("Api", "Program.cs", "System.Console.WriteLine(typeof(HawalekNow.Domain.Modules.Catalog.Product));")).ToArray();
        var violations = ModuleBoundaryInspector.Inspect(sources);
        Assert.DoesNotContain(violations, violation => violation.StartsWith("Compilation:"));
        Assert.Contains(violations, violation => violation.StartsWith("Layer dependency:"));
    }

    [Fact]
    public void Global_alias_in_one_file_cannot_smuggle_a_foreign_module_type_into_another()
    {
        Assert.Empty(FixtureViolations.Value);
        var sources = Fixture.Concat(
        [
            new ProductionSource("Domain", "Modules/Catalog/Alias.cs", "global using Foreign = HawalekNow.Domain.Modules.Merchants.MerchantAccount; namespace HawalekNow.Domain.Modules.Catalog;"),
            Source("Domain", "Catalog", "Probe.cs", "public sealed record Probe(Foreign Value);")
        ]).ToArray();
        var violations = ModuleBoundaryInspector.Inspect(sources);
        Assert.DoesNotContain(violations, violation => violation.StartsWith("Compilation:"));
        Assert.Contains(violations, violation => violation.StartsWith("Module dependency:") && violation.Contains("Probe.cs"));
    }

    private static ProductionSource Source(string layer, string module, string path, string body)
    {
        var suffix = path.Contains('/') ? "." + path[..path.LastIndexOf('/')].Replace('/', '.') : "";
        // Usings must precede a file-scoped namespace; a block namespace supports alias fixtures.
        return new ProductionSource(layer, $"Modules/{module}/{path}",
            $"namespace HawalekNow.{layer}.Modules.{module}{suffix} {{ {body} }}");
    }
}
