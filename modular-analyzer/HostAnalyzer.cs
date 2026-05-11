using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;


[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class HostAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor HostShouldNotReferenceModules = new(
        id: "MA0003",
        title: "Нарушены обязательные условия сборки",
        messageFormat: "Сборка является запускаемой, и имеет ссылку на сборку '{1}', которая помечена атрибутом '{0}",
        category: "Design",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true, customTags: WellKnownDiagnosticTags.CompilationEnd);

    private static readonly DiagnosticDescriptor ApplicationPartsShouldNotReferenceModules = new(
        id: "MA0004",
        title: "Нарушены обязательные условия сборки",
        messageFormat: "Сборка является запускаемой, и имеет атрибут '{1}' с параметром '{2}', который ссылается на сборку, которая помечена атрибутом '{0}",
        category: "Design",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true, customTags: WellKnownDiagnosticTags.CompilationEnd);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [HostShouldNotReferenceModules, ApplicationPartsShouldNotReferenceModules];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationAction(CompilationAnalyzer);
    }

    private static void CompilationAnalyzer(CompilationAnalysisContext compilationContext)
    {
        var compilation = compilationContext.Compilation;

        if (compilation.GetEntryPoint(compilationContext.CancellationToken) is null) return;

        var hostingStartupAttribute = compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Hosting.HostingStartupAttribute");
        var hostShouldNotReferenceModulesErrors =
            from r in compilation.GetUsedAssemblyReferences(compilationContext.CancellationToken)
            let assembly = (IAssemblySymbol)compilation.GetAssemblyOrModuleSymbol(r)
            where !assembly.Name.StartsWith("Microsoft.AspNetCore.")
            where assembly.GetAttributes().Any(attr => SymbolEqualityComparer.Default.Equals(attr.AttributeClass, hostingStartupAttribute))
            select Diagnostic.Create(HostShouldNotReferenceModules,
                            Location.None,
                            hostingStartupAttribute,
                            assembly.Name);

        foreach (var diag in hostShouldNotReferenceModulesErrors)
        {
            compilationContext.ReportDiagnostic(diag);
        }

        var applicationPartAttribute = compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Mvc.ApplicationParts.ApplicationPartAttribute");
        if (applicationPartAttribute is null) return;
        
        var applicationParts = compilation
            .Assembly
            .GetAttributes()
            .Where(attr => SymbolEqualityComparer.Default.Equals(attr.AttributeClass, applicationPartAttribute))
            .Select(attr => (string)attr.ConstructorArguments.Single().Value)
            .ToImmutableHashSet();

        var ApplicationPartsShouldNotReferenceModulesErrors =
            from r in compilation.References
            let assembly = (IAssemblySymbol)compilation.GetAssemblyOrModuleSymbol(r)
            where applicationParts.Contains(assembly.Name)
            where assembly.GetAttributes().Any(attr => SymbolEqualityComparer.Default.Equals(attr.AttributeClass, hostingStartupAttribute))
            select Diagnostic.Create(HostShouldNotReferenceModules,
                            Location.None,
                            hostingStartupAttribute,
                            assembly.Name);

        foreach (var diag in ApplicationPartsShouldNotReferenceModulesErrors)
        {
            compilationContext.ReportDiagnostic(diag);
        }
    }
}