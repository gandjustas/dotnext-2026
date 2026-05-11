using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;


[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class ModuleAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor ModuleShouldNotExposePublicMembers = new(
        id: "MA0001",
        title: "Нарушены обязательные условия сборки",
        messageFormat: "Сборка не является запускаемой, помечена атрибутом '{0}' и содержит публичный класс '{1}', который не участвует в реализации IEntityTypeConfiguration и не наследует ControllerBase",
        category: "Design",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true); 
    private static readonly DiagnosticDescriptor ModuleShouldBeDerivedFromBase = new(
        id: "MA0002",
        title: "Нарушены обязательные условия сборки",
        messageFormat: "Сборка помечена атрибутом '{0}', с указанием типа модуля '{1}', который не наследует '{2}'",
        category: "Design",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true, customTags: WellKnownDiagnosticTags.CompilationEnd);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [ModuleShouldNotExposePublicMembers, ModuleShouldBeDerivedFromBase];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(CompilationAnalyzer);

    }

    private static void CompilationAnalyzer(CompilationStartAnalysisContext compilationStartContext)
    {
        var compilation = compilationStartContext.Compilation;

        var hostingStartupAttribute = compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Hosting.HostingStartupAttribute");
        var moduleAttribute = compilation.Assembly.GetAttributes()
            .FirstOrDefault(attr => SymbolEqualityComparer.Default.Equals(attr.AttributeClass, hostingStartupAttribute));
        if (moduleAttribute is null) return;

        var moduleType = moduleAttribute.ConstructorArguments.Single();
        var moduleClass = (ITypeSymbol)moduleType.Value;
        var moduleBaseClass = compilation.GetTypeByMetadataName("ModuleBase");

        compilationStartContext.RegisterCompilationEndAction(compilationContext => {
            if (moduleBaseClass == null || !InheritsFrom(moduleClass, moduleBaseClass))
            {
                var diag = Diagnostic.Create(ModuleShouldBeDerivedFromBase,
                    moduleClass.Locations.FirstOrDefault() ?? Location.None,
                    hostingStartupAttribute,                    
                    moduleClass.Name,
                    moduleBaseClass.Name);
                compilationContext.ReportDiagnostic(diag);
            }
        });

        if (compilation.GetEntryPoint(compilationStartContext.CancellationToken) is not null) return;

        var entityTypes = GetEntityTypeConfigurationEntities(compilation).ToArray();
        var controllerBaseType = compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Mvc.ControllerBase");

        // Регистрируем проверку каждого публичного класса
        compilationStartContext.RegisterSymbolAction(symbolContext =>
        {
            var classSymbol = (INamedTypeSymbol)symbolContext.Symbol;

            // Интересуют только публичные классы верхнего уровня в этой сборке
            if (classSymbol.TypeKind != TypeKind.Class ||
                classSymbol.DeclaredAccessibility != Accessibility.Public ||
                classSymbol.ContainingType != null ||
                !SymbolEqualityComparer.Default.Equals(classSymbol.ContainingAssembly, compilation.Assembly))
                return;

            // Проверка наследования от ControllerBase
            bool inheritsControllerBase = controllerBaseType != null && InheritsFrom(classSymbol, controllerBaseType);

            // Проверка что есть в списке сущностей
            bool isEntityType = entityTypes.Any(t => SymbolEqualityComparer.Default.Equals(classSymbol, t));

            // Если класс не подходит ни под одно из двух условий – выдаём ошибку
            if (!isEntityType && !inheritsControllerBase)
            {
                var diag = Diagnostic.Create(ModuleShouldNotExposePublicMembers,
                    classSymbol.Locations.FirstOrDefault() ?? Location.None,
                    hostingStartupAttribute,
                    classSymbol.Name);
                symbolContext.ReportDiagnostic(diag);
            }
        }, SymbolKind.NamedType);
    }

    private static IEnumerable<ITypeSymbol> GetEntityTypeConfigurationEntities(Compilation compilation)
    {
        var IEntityTypeConfiguration = compilation.GetTypeByMetadataName("Microsoft.EntityFrameworkCore.IEntityTypeConfiguration`1")?.ConstructUnboundGenericType();
        if (IEntityTypeConfiguration is null) yield break;

        var stack = new Stack<INamespaceSymbol>();
        stack.Push(compilation.Assembly.GlobalNamespace);

        while (stack.Count > 0)
        {
            var ns = stack.Pop();
            foreach (var member in ns.GetMembers())
            {
                if (member is INamespaceSymbol nestedNs) stack.Push(nestedNs);
                else if (member is INamedTypeSymbol {TypeKind: TypeKind.Class } type)
                {
                    foreach (var inteface in type.AllInterfaces)
                    {
                        if(inteface.IsGenericType &&
                            SymbolEqualityComparer.Default.Equals(inteface.ConstructUnboundGenericType(), IEntityTypeConfiguration))
                        {
                            yield return inteface.TypeArguments.Single();
                        }
                    }
                }
            }
        }
    }

    private static bool InheritsFrom(ITypeSymbol symbol, ITypeSymbol baseType)
    {
        var current = symbol.BaseType;
        while (current != null)
        {
            if (SymbolEqualityComparer.Default.Equals(current, baseType))
                return true;
            current = current.BaseType;
        }
        return false;
    }
}