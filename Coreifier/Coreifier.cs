using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using CustomAttributeNamedArgument = Mono.Cecil.CustomAttributeNamedArgument;

namespace Coreifier;

public static class Coreifier {
    // called via reflection from OpusMutatum
    public static void Coreify(string inputAsm, string outputAsm = null) {
        ModuleDefinition module = null;
        try {
            // read the module
            ReaderParameters readerParams = new(ReadingMode.Immediate) { ReadSymbols = true };
            try {
                module = ModuleDefinition.ReadModule(inputAsm, readerParams);
            } catch (SymbolsNotFoundException) {
                readerParams.ReadSymbols = false;
                module = ModuleDefinition.ReadModule(inputAsm, readerParams);
            } catch (SymbolsNotMatchingException) {
                readerParams.ReadSymbols = false;
                module = ModuleDefinition.ReadModule(inputAsm, readerParams);
            }

            // convert the module
            Coreify(module);

            // write the converted module
            module.Write(outputAsm ?? inputAsm, new WriterParameters { WriteSymbols = readerParams.ReadSymbols });
        } finally {
            module?.Dispose();
        }
    }

    public static void Coreify(ModuleDefinition module, bool preventInlining = true) {
        module.RuntimeVersion = Assembly.GetExecutingAssembly().ImageRuntimeVersion;

        // clear 32-bit flags
        module.Attributes &= ~(ModuleAttributes.Required32Bit | ModuleAttributes.Preferred32Bit);

        // patch target framework attribute
        bool isFrameworkModule = false;
        TargetFrameworkAttribute attr = Assembly.GetExecutingAssembly().GetCustomAttribute<TargetFrameworkAttribute>();
        CustomAttribute moduleAttr = module.Assembly.CustomAttributes.FirstOrDefault(a => a.AttributeType.FullName == typeof(TargetFrameworkAttribute).FullName);
        if (moduleAttr is not null) {
            if (((string) moduleAttr.ConstructorArguments[0].Value).StartsWith(".NETFramework"))
                isFrameworkModule = true;

            if (attr is not null) {
                moduleAttr.ConstructorArguments[0] = new CustomAttributeArgument(module.ImportReference(typeof(string)), attr.FrameworkName);
                moduleAttr.Properties.Clear();
                moduleAttr.Properties.Add(new CustomAttributeNamedArgument(nameof(attr.FrameworkDisplayName), new CustomAttributeArgument(module.ImportReference(typeof(string)), attr.FrameworkDisplayName)));
            }
        } else
            // fall back to assembly references
            isFrameworkModule = module.AssemblyReferences.Any(asmRef => asmRef.Name == "mscorlib") && module.AssemblyReferences.All(asmRef => asmRef.Name != "System.Runtime");
        // skip the module if it's already using core
        if (!isFrameworkModule)
            return;

        // patch debuggable attribute
        // we can't get the attribute from our own assembly (because it's a temporary MonoMod one), so get it from the entry assembly (which is OpusMutatum)
        if (Assembly.GetEntryAssembly() is not { } mutatumAssembly)
            throw new InvalidOperationException("Coreifier must always be called from OpusMutatum!");
        DebuggableAttribute mutatumDebuggableAttr = mutatumAssembly.GetCustomAttribute<DebuggableAttribute>();
        CustomAttribute lightningDebuggableAttr = module.Assembly.CustomAttributes.FirstOrDefault(a => a.AttributeType.FullName == typeof(DebuggableAttribute).FullName);
        if (lightningDebuggableAttr is not null && mutatumDebuggableAttr is not null)
            lightningDebuggableAttr.ConstructorArguments[0] = new CustomAttributeArgument(module.ImportReference(typeof(DebuggableAttribute.DebuggingModes)), mutatumDebuggableAttr.DebuggingFlags);

        // relink framework code
        using (FrameworkModder modder = new() {
                    Module = module,
                    MissingDependencyThrow = false,
                    PreventInlining = preventInlining,
                }) {
            modder.MapDependencies();
            modder.AutoPatch();
        }
    }
}
