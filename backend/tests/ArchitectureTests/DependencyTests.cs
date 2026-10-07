using System.Reflection;
using System.Xml.Linq;

namespace HawalekNow.ArchitectureTests;

public sealed class DependencyTests
{
    private static readonly Dictionary<string, string[]> Allowed = new()
    {
        ["HawalekNow.Domain"] = [],
        ["HawalekNow.Application"] = ["HawalekNow.Domain"],
        ["HawalekNow.Infrastructure"] = ["HawalekNow.Application", "HawalekNow.Domain"],
        ["HawalekNow.Api"] = ["HawalekNow.Application", "HawalekNow.Infrastructure"]
    };

    private static string Root
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "global.json")))
                directory = directory.Parent;
            return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
        }
    }

    [Theory]
    [InlineData("HawalekNow.Domain")]
    [InlineData("HawalekNow.Application")]
    [InlineData("HawalekNow.Infrastructure")]
    [InlineData("HawalekNow.Api")]
    public void Project_references_match_the_approved_graph(string project)
    {
        var path = Path.Combine(Root, "backend", "src", project, project + ".csproj");
        Assert.Empty(ProjectViolations(project, path));
    }

    internal static IEnumerable<string> ProjectViolations(string project, string path)
    {
        var document = XDocument.Load(path);
        var references = document.Descendants().Where(e => e.Name.LocalName == "ProjectReference").ToArray();
        var names = references.Select(e => Path.GetFileNameWithoutExtension(
            (e.Attribute("Include")?.Value ?? "").Replace('\\', '/'))).ToArray();
        if (!names.Order().SequenceEqual(Allowed[project].Order()))
            yield return $"{project}: unexpected project references: {string.Join(", ", names)}";
        foreach (var reference in references)
        {
            var include = (reference.Attribute("Include")?.Value ?? "").Replace('\\', Path.DirectorySeparatorChar);
            var resolved = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, include));
            var name = Path.GetFileNameWithoutExtension(resolved);
            var expected = Path.GetFullPath(Path.Combine(Root, "backend", "src", name, name + ".csproj"));
            if (!File.Exists(resolved) || resolved != expected || reference.Attribute("Condition") is not null)
                yield return $"Invalid or conditional project reference: {include}";
        }
        if (project == "HawalekNow.Domain")
        {
            if ((string?)document.Root?.Attribute("Sdk") != "Microsoft.NET.Sdk")
                yield return "Domain must use the plain .NET SDK.";
            foreach (var element in document.Descendants().Where(e => e.Name.LocalName is "PackageReference" or "FrameworkReference" or "Reference"))
            {
                var name = (string?)element.Attribute("Include") ?? (string?)element.Attribute("Update") ?? "";
                if (Forbidden(name)) yield return $"Domain contains forbidden dependency: {name}";
            }
        }
    }

    private static bool Forbidden(string name) =>
        name.StartsWith("Microsoft.AspNetCore", StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith("EntityFramework", StringComparison.OrdinalIgnoreCase);

    [Theory]
    [InlineData("HawalekNow.Domain")]
    [InlineData("HawalekNow.Application")]
    [InlineData("HawalekNow.Infrastructure")]
    [InlineData("HawalekNow.Api")]
    public void Assembly_dependencies_do_not_point_outwards(string project)
    {
        var assembly = Assembly.Load(project);
        var references = assembly.GetReferencedAssemblies().Select(r => r.Name!).ToArray();
        Assert.All(references.Where(r => r.StartsWith("HawalekNow.")), r => Assert.Contains(r, Allowed[project]));
        if (project == "HawalekNow.Domain") Assert.DoesNotContain(references, Forbidden);
    }

    [Theory]
    [InlineData("ProjectReference", "../HawalekNow.Infrastructure/HawalekNow.Infrastructure.csproj")]
    [InlineData("PackageReference", "Microsoft.EntityFrameworkCore")]
    [InlineData("FrameworkReference", "Microsoft.AspNetCore.App")]
    public void Isolated_mutation_is_rejected_without_editing_the_real_project(string kind, string include)
    {
        var original = Path.Combine(Root, "backend", "src", "HawalekNow.Domain", "HawalekNow.Domain.csproj");
        var bytes = File.ReadAllBytes(original);
        var document = XDocument.Load(original);
        document.Root!.Add(new XElement("ItemGroup", new XElement(kind, new XAttribute("Include", include))));
        var temporary = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".csproj");
        try
        {
            document.Save(temporary);
            var violations = ProjectViolations("HawalekNow.Domain", temporary).ToArray();
            if (kind == "ProjectReference")
                Assert.Contains(violations, v => v.Contains("unexpected project references"));
            else
                Assert.Contains(violations, v => v.Contains("Domain contains forbidden dependency"));
            Assert.Equal(bytes, File.ReadAllBytes(original));
        }
        finally { File.Delete(temporary); }
    }
}
