using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;


[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class HostAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor HostShouldNotReferenceModules = new(
        id: "MOD0003",
        title: "Нарушены обязательные условия сборки",
        messageFormat: "Сборка является запускаемой, и имеет ссылку на сборку '{1}', которая помечена атрибутом '{0}",
        category: "Design",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true, customTags: WellKnownDiagnosticTags.CompilationEnd);

    private static readonly DiagnosticDescriptor ApplicationPartsShouldNotReferenceModules = new(
        id: "MOD0004",
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

        // Тест-проект тоже запускаемый, но он обязан ссылаться на хост: WebApplicationFactory<Program>
        // без этого не собрать. Проверка адресована настоящему хосту, а не тестам.
        if (IsTestProject(compilationContext.Options)) return;

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

        var usedReferenceErrors = hostShouldNotReferenceModulesErrors.ToArray();

        // GetUsedAssemblyReferences в сломанной компиляции возвращает ВСЕ ссылки как использованные,
        // поэтому на фоне ошибок компилятора правило обязано молчать: иначе оно уводит от настоящей
        // причины. Проверяем только когда уже собрались что-то сообщить — это дорогой вызов.
        if (usedReferenceErrors.Length > 0 && !HasCompilationErrors(compilation, compilationContext.CancellationToken))
        {
            foreach (var diag in usedReferenceErrors)
            {
                compilationContext.ReportDiagnostic(diag);
            }
        }

        var applicationPartAttribute = compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Mvc.ApplicationParts.ApplicationPartAttribute");
        if (applicationPartAttribute is null) return;

        var applicationParts = compilation
            .Assembly
            .GetAttributes()
            .Where(attr => SymbolEqualityComparer.Default.Equals(attr.AttributeClass, applicationPartAttribute))
            .Select(attr => (string)attr.ConstructorArguments.Single().Value)
            .ToImmutableHashSet();

        var applicationPartsShouldNotReferenceModulesErrors =
            from r in compilation.References
            let assembly = (IAssemblySymbol)compilation.GetAssemblyOrModuleSymbol(r)
            where applicationParts.Contains(assembly.Name)
            where assembly.GetAttributes().Any(attr => SymbolEqualityComparer.Default.Equals(attr.AttributeClass, hostingStartupAttribute))
            select Diagnostic.Create(ApplicationPartsShouldNotReferenceModules,
                            Location.None,
                            hostingStartupAttribute,
                            applicationPartAttribute,
                            assembly.Name);

        foreach (var diag in applicationPartsShouldNotReferenceModulesErrors)
        {
            compilationContext.ReportDiagnostic(diag);
        }
    }

    private static bool IsTestProject(AnalyzerOptions options) =>
        options.AnalyzerConfigOptionsProvider.GlobalOptions.TryGetValue("build_property.IsTestProject", out var value) &&
        string.Equals(value, "true", System.StringComparison.OrdinalIgnoreCase);

    private static bool HasCompilationErrors(Compilation compilation, System.Threading.CancellationToken cancellationToken) =>
        compilation.GetDiagnostics(cancellationToken).Any(d => d.Severity == DiagnosticSeverity.Error);
}
