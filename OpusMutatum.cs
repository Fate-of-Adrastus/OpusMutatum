using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Collections.Generic;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text.RegularExpressions;

namespace OpusMutatum {

	public class OpusMutatum {

		// For intermediary or devExe
		static string PathToLightning = "./Lightning.exe";
        static string PathToIntermediaryLightning = "./IntermediaryLightning.exe";
		static string PathToModdedLightning = "./ModdedLightning.exe";

        // for merge
        static string PathToMonoMod = "./MonoMod.exe";

		// for strings
		static string StringDeobfName = null;
		static string StringDeobfIntermediaryName = "method_131";

		static List<string> IntermediaryToNamedMappingPaths = new List<string>();
		static List<string> ObfToIntermediaryMappingPaths = new List<string>();
		static List<string> StringsPaths = new List<string>();

		static AssemblyDefinition LightningAssembly, IntermediaryLightningAssembly, ModdedLightningAssembly;

		static Mappings ObfToIntermediaryMappings;
		static Dictionary<string, string> IntermediaryToNamedMappings = new Dictionary<string, string>();
		static Dictionary<int, string> Strings = new Dictionary<int, string>();

		static bool AutoExit = false;
		// OS enum, since Linux and Mac are different
		public enum OS {
			Windows,
			Linux,
			Mac
		};
		
		public static OS OpSystem = OS.Windows;

		static void Main(string[] args) {
			ArgumentParsingMode current = ArgumentParsingMode.Argument;
			RunAction action = RunAction.Setup;

			switch(Environment.OSVersion.Platform)
			{
				case PlatformID.Win32NT:
				case PlatformID.Win32S:
				case PlatformID.Win32Windows:
				case PlatformID.WinCE:
					OpSystem = OS.Windows;
					break;
				case PlatformID.MacOSX:
					OpSystem = OS.Mac;
					break;
				default:
					OpSystem = OS.Linux;
					break;
			};

			foreach(var arg in args) {
				switch(current) {
					case ArgumentParsingMode.Argument:
                        // check if its "run", "strings", "intermediary", merge", "setup", "devExe", "quintDevExe"
                        // or "--mappings", "--intermediary", "--strings", "--lightning", "--monomod", "--intermediaryPath", "--linux", "--mac", --"win"
                        if (arg.Equals("run"))
							action = RunAction.Run;
						else if(arg.Equals("strings"))
							action = RunAction.Strings;
						else if(arg.Equals("intermediary"))
							action = RunAction.Intermediary;
						else if(arg.Equals("merge"))
							action = RunAction.Merge;
						else if(arg.Equals("setup"))
							action = RunAction.Setup;
						else if(arg.Equals("devExe"))
							action = RunAction.DevExe;
                        else if (arg.Equals("quintDevExe"))
                            action = RunAction.QuintDevExe;
                        else if(arg.Equals("--mappings"))
							current = ArgumentParsingMode.IntermediaryToNamedMappingPath;
						else if(arg.Equals("--intermediary"))
							current = ArgumentParsingMode.ObfToIntermediaryMappingPath;
						else if(arg.Equals("--strings"))
							current = ArgumentParsingMode.StringsPath;
						else if(arg.Equals("--lightning"))
							current = ArgumentParsingMode.LightningPath;
						else if(arg.Equals("--monomod"))
							current = ArgumentParsingMode.MonoModPath;
						else if(arg.Equals("--stringdeobfname"))
							current = ArgumentParsingMode.StringDeobfName;
						else if(arg.Equals("--stringdeobfintname"))
							current = ArgumentParsingMode.StringDeobfIntermediaryName;
						else if(arg.Equals("--linux"))
							OpSystem = OS.Linux;
						else if(arg.Equals("--mac"))
							OpSystem = OS.Mac;
						else if(arg.Equals("--win"))
							OpSystem = OS.Windows;
                        else if (arg.Equals("--autoExit"))
                            AutoExit = true;
                        break;
					case ArgumentParsingMode.IntermediaryToNamedMappingPath:
						IntermediaryToNamedMappingPaths.Add(arg);
						current = ArgumentParsingMode.Argument;
						break;
					case ArgumentParsingMode.ObfToIntermediaryMappingPath:
						ObfToIntermediaryMappingPaths.Add(arg);
						current = ArgumentParsingMode.Argument;
						break;
					case ArgumentParsingMode.StringsPath:
						StringsPaths.Add(arg);
						current = ArgumentParsingMode.Argument;
						break;
					case ArgumentParsingMode.LightningPath:
						PathToLightning = arg;
						current = ArgumentParsingMode.Argument;
						break;
					case ArgumentParsingMode.MonoModPath:
						PathToMonoMod = arg;
						current = ArgumentParsingMode.Argument;
						break;
					case ArgumentParsingMode.StringDeobfName:
						StringDeobfName = arg;
						current = ArgumentParsingMode.Argument;
						break;
					case ArgumentParsingMode.StringDeobfIntermediaryName:
						StringDeobfIntermediaryName = arg;
						current = ArgumentParsingMode.Argument;
						break;
					default:
						Console.WriteLine("Invalid argument \"" + arg + "\"!");
						break;
				}
			}

			if(StringsPaths.Count == 0) {
				StringsPaths.Add("./StringDumping/out.csv");
				StringsPaths.Add("./out.csv");
			}

			if(ObfToIntermediaryMappingPaths.Count == 0) {
				foreach(var path in Directory.GetFiles("mappings")) {
					ObfToIntermediaryMappingPaths.Add(path);
				}
			}
            if (IntermediaryToNamedMappingPaths.Count == 0) {
                foreach (var path in Directory.GetFiles("mappings")) {
                    IntermediaryToNamedMappingPaths.Add(path);
                }
            }

            try {
				switch(action) {
					case RunAction.Strings:
						HandleStrings();
						break;
					case RunAction.Intermediary:
						HandleIntermediary();
						break;
					case RunAction.Merge:
						HandleMerge();
						break;
					case RunAction.Setup:
						HandleStrings();
						HandleIntermediary();
						HandleMerge();
						break;
					case RunAction.QuintDevExe:
                        HandleQuintDevExe();
                        break;
                    case RunAction.DevExe:
						HandleDevExe();
						break;
					case RunAction.Run:
					default:
						HandleRun();
						break;
				}
			} catch(Exception e) {
				Console.WriteLine("Error executing task:");
				Console.WriteLine(e.ToString());
			}
			Console.WriteLine("Done.");
			// keep command line open
			if (!AutoExit) Console.ReadKey();
		}

		static void HandleRun() {
			// just run MONOMODDED_IntermediaryLightning.exe
		}

		static void FindStringDeobfMethod(MethodDefinition mainMethod) {
			if (StringDeobfName == null) {
				if (mainMethod.Body != null && mainMethod.Body.Instructions != null) {
					var candidateMethods = new HashSet<string>();
					foreach (var instr in mainMethod.Body.Instructions) {
						if (instr.Operand is MethodReference methodRef && methodRef.Resolve() != null) {
							var method = methodRef.Resolve();
							// deobf method should be a static method of signature (int) => string
							if (method.IsStatic && method.Parameters.Count == 1 && method.Parameters[0].ParameterType.FullName == "System.Int32" && method.ReturnType.FullName == "System.String") {
								candidateMethods.Add($"{method.DeclaringType.Name}.{method.Name}");
							}
						}
					}
					// fail unless we found exactly one match
					if (candidateMethods.Count == 1){
						StringDeobfName = candidateMethods.Single();
						return;
					}
				}
				throw new Exception("Failed to find string deobf method");
			}
		}

		static void HandleStrings() {
			Console.WriteLine("Dumping strings...");
			LoadLightning();
			var module = LightningAssembly.MainModule;
			var mainMethod = module.EntryPoint;
			FindStringDeobfMethod(mainMethod);
			var ssplit = StringDeobfName.Split('.');
			var parse = module.FindMethod(ssplit[0], ssplit[1]);

			Console.WriteLine("Finding keys...");
			// get all the keys this way
			var refs = new List<Instruction>();
			foreach(var type in CollectNestedTypes(LightningAssembly.MainModule.Types))
				if(type != null)
				foreach(var method in type.Methods)
					if(method != null)
					if(method.HasBody && method.Body != null && method.Body.Instructions != null)
						foreach(var instr in method.Body.Instructions)
							if(instr != null && instr.OpCode != null)
							if(instr.OpCode.Code == Code.Call && instr.Operand is MethodReference operand && operand.Resolve() != null && operand.Resolve().Equals(parse))
								refs.Add(instr);

			var keys = new HashSet<int>();

			foreach(var instr in refs)
				keys.Add((int)instr.Previous.Operand);

			Console.WriteLine($"Found {keys.Count()} string keys");

			var proc = mainMethod.Body.GetILProcessor();
			var first = proc.Body.Instructions.First();

			var stringt = module.TypeSystem.String;

			// we want String.Concat(String?, String?, String?)
			Console.WriteLine("Resolving Concat method...");
			var concat = module.ImportReference(stringt.Resolve().Methods.First(f => f.Parameters.Count == 3 && f.Parameters.All(p => p.ParameterType.FullName.Equals(stringt.FullName))));
			Console.WriteLine("Getting StreamWriter class...");
			var streamWriter = module.ImportReference(typeof(StreamWriter)).Resolve();
			Console.WriteLine("Getting StreamWriter constructor...");
			var streamWriterConstructor = module.ImportReference(streamWriter.Methods.First(m => m.Name.Equals(".ctor") && m.Parameters.Count() == 1 && m.Parameters.All(param => param.ParameterType.FullName.Equals(stringt.FullName))));
			Console.WriteLine("Getting WriteLine method...");
			var writeLine = module.ImportReference(streamWriter.BaseType.Resolve().Methods.First(m => m.Name.Equals("WriteLine") && m.Parameters.Count == 1 && m.Parameters.All(p => p.ParameterType.FullName.Equals(stringt.FullName))));
			Console.WriteLine("Getting Dispose method...");
			var dispose = module.ImportReference(streamWriter.BaseType.Resolve().FindMethod("Close"));

			Console.WriteLine("Creating string dumper...");
			proc.InsertBefore(first, proc.Create(OpCodes.Ldstr, "./out.csv"));
			proc.InsertBefore(first, proc.Create(OpCodes.Newobj, streamWriterConstructor));
			foreach(var key in keys) {
				proc.InsertBefore(first, proc.Create(OpCodes.Dup));
				proc.InsertBefore(first, proc.Create(OpCodes.Ldstr, key.ToString()));
				proc.InsertBefore(first, proc.Create(OpCodes.Ldstr, "~,~"));
				proc.InsertBefore(first, proc.Create(OpCodes.Ldc_I4, key));
				proc.InsertBefore(first, proc.Create(OpCodes.Call, parse));
				proc.InsertBefore(first, proc.Create(OpCodes.Call, concat));
				proc.InsertBefore(first, proc.Create(OpCodes.Callvirt, writeLine));
			}
			//proc.InsertBefore(first, proc.Create(OpCodes.Ldc_I4_1));
			proc.InsertBefore(first, proc.Create(OpCodes.Callvirt, dispose));
			proc.InsertBefore(first, proc.Create(OpCodes.Ret));

			Directory.CreateDirectory("./StringDumping");
			module.Write("./StringDumping/Lightning.exe");

			// Yells at you if System and Steamworks aren't in the StringDumping directory
			if(OpSystem != OS.Windows && !File.Exists("./StringDumping/System.dll") && !File.Exists("./StringDumping/Steamworks.NET.dll")) {
				File.Copy("./System.dll", "./StringDumping/System.dll");
				File.Copy("./Steamworks.NET.dll", "./StringDumping/Steamworks.NET.dll");
			}
			Console.WriteLine("Running string dumper...");
			// run the string dumper automatically
			RunAndWait(Path.Combine(Directory.GetCurrentDirectory(), "StringDumping", "Lightning.exe"), "");
			Console.WriteLine();
		}

		static void HandleIntermediary() {
			// TODO: MonoMod relinking?
			Console.WriteLine("Generating intermediary EXE...");
			LoadLightning();
			LoadStrings();
			// take Lightning.exe, remap to Intermediary
			LoadObfToIntermediaryMappings();
			List<(Instruction, int)> stringsToBeInlined = new List<(Instruction, int)>();
			DoRemap(new IntermediaryRemapper(), CollectNestedTypes(LightningAssembly.MainModule.Types),
				(mref, newName, instr) => {
					if(newName.Equals(StringDeobfIntermediaryName) && mref.Parameters.Count == 1)
						if(instr.Previous.OpCode == OpCodes.Ldc_I4)
							stringsToBeInlined.Add((instr, (int)instr.Previous.Operand));
				},
				type => {
					
					if(type.IsNested)
						type.IsNestedPublic = true;
					else
						type.IsPublic = true;

				});
			if(stringsToBeInlined.Count > 0)
				foreach(var stringFunc in stringsToBeInlined)
					if(Strings.ContainsKey(stringFunc.Item2)) {
						stringFunc.Item1.Previous.Set(OpCodes.Nop, null);
						stringFunc.Item1.Set(OpCodes.Ldstr, Strings[stringFunc.Item2]);
					} else
						Console.WriteLine($"Missing string for {stringFunc.Item2}");

			LightningAssembly.Write("IntermediaryLightning.exe");
			Console.WriteLine();
		}

		static void LoadLightning() {
			Console.WriteLine("Reading Lightning.exe...");
			LightningAssembly = AssemblyDefinition.ReadAssembly(PathToLightning);
			Console.WriteLine(LightningAssembly == null ? "Failed to load Lightning.exe" : "Found Lightning executable: " + LightningAssembly.FullName);
		}

		static void LoadModdedLightning() {
			Console.WriteLine("Reading modded Lightning.exe...");
			ModdedLightningAssembly = AssemblyDefinition.ReadAssembly(PathToModdedLightning);
			Console.WriteLine(ModdedLightningAssembly == null ? $"Failed to load modded Lightning.exe at \"{PathToModdedLightning}\"" : "Found modded Lightning executable: " + ModdedLightningAssembly.FullName);
		}
		
        static void LoadIntermediaryLightning() {
            Console.WriteLine("Reading intermediary Lightning.exe...");
            IntermediaryLightningAssembly = AssemblyDefinition.ReadAssembly(PathToIntermediaryLightning);
            Console.WriteLine(IntermediaryLightningAssembly == null ? $"Failed to load intermediary Lightning.exe at \"{PathToIntermediaryLightning}\"" : "Found modded Lightning executable: " + IntermediaryLightningAssembly.FullName);
        }

        static void LoadStrings() {
			if(StringsPaths.Count > 0) {
				foreach(var path in StringsPaths) {
					if(!File.Exists(path))
						continue;
					string[] lines = File.ReadAllLines(path);
					int lastIndex = 0;
					foreach(string line in lines) {
						string[] split = line.Split(new string[] { "~,~" }, StringSplitOptions.None);
						if(split.Length > 1) {
							// if we *can* split on this line, then we're definitely at the first line of a string
							try {
								lastIndex = int.Parse(split[0]);
								Strings[lastIndex] = split[1];
							} catch(FormatException) { }
						} else {
							// if this line isn't blank (or even if it is), then we're continuing a previous multi-line string, so append
							Strings[lastIndex] = Strings[lastIndex] + "\n" + line;
						}
					}
				}
				Console.WriteLine("Loaded " + Strings.Count + " strings.");
			}
		}

		public static void DoRemap(Remapper remapper, Collection<TypeDefinition> types, Action<MethodReference, string, Instruction> onMethodReference, Action<TypeDefinition> onTypeDefinition) {
			// Renames are deferred so that everything compares against old names, rather than a mixture of old and new names
			var deferredRenames = new Dictionary<MemberReference, string>();
			var deferredParamRenames = new Dictionary<ParameterReference, string>();
			foreach(var type in types) {
				if (type.IsGenericInstance || type.IsGenericParameter || type.IsArray || type.IsByReference || type.IsPointer)
					throw new Exception($"Expected to only be remapping main types, not generic instances/generic parameters/arrays/references/pointers, but got {type.FullName}");
				deferredRenames[type] = remapper.RemapType(type);
				onTypeDefinition(type);
                foreach (var method in type.Methods) {
					// rtspecialname is applied to constructors and operators
					if(!method.IsRuntimeSpecialName)
						deferredRenames[method] = remapper.RemapMethod(method);
					foreach(var generic in method.GenericParameters)
						deferredRenames[generic] = remapper.RemapGeneric(generic);
					foreach(var param in method.Parameters)
						deferredParamRenames[param] = remapper.RemapMethodParam(param, method);
					// references to members in classes with generic parameters don't get remapped automatically
					// so here we update those references ourself
					if(method.Body != null && method.Body.Instructions != null) {
						foreach(var instr in method.Body.Instructions) {
							if(instr != null && instr.Operand is MethodReference mref && !mref.IsWindowsRuntimeProjection) {
								if(mref.IsGenericInstance)
									mref = ((GenericInstanceMethod)mref).GetElementMethod();
								deferredRenames[mref] = remapper.RemapMethod(mref);
								// also take the oppurtunity to replace references to string decoder with the actual string
								onMethodReference(mref, deferredRenames[mref], instr);
							}

							if(instr != null && instr.Operand is FieldReference fref)
								deferredRenames[fref] = remapper.RemapField(fref);
						}
					}
					foreach(var attr in method.CustomAttributes)
						if(attr.HasConstructorArguments)
							foreach(var arg in attr.ConstructorArguments)
								if(arg.Type.Name.Equals("Type"))
									deferredRenames[arg.Value as TypeReference] = remapper.RemapType(arg.Value as TypeReference);
					// TODO: map locals
				}
				foreach(var field in type.Fields)
					deferredRenames[field] = remapper.RemapField(field);
				foreach(var generic in type.GenericParameters)
					deferredRenames[generic] = remapper.RemapGeneric(generic);
			}
			foreach (var pair in deferredRenames) {
				pair.Key.Name = pair.Value;
			}
			foreach (var pair in deferredParamRenames) {
				pair.Key.Name = pair.Value;
			}
		}

		static Collection<TypeDefinition> CollectNestedTypes(Collection<TypeDefinition> topLevel) {
			var types = new Collection<TypeDefinition>();
			foreach(var type in topLevel)
				VisitTypes(type, t => types.Add(t));
			return types;
		}

		static void VisitTypes(TypeDefinition top, Action<TypeDefinition> act) {
			act(top);
			foreach(var type in top.NestedTypes)
				VisitTypes(type, act);
		}

		static void HandleMerge() {
			// run "./MonoMod.exe IntermediaryLightning.exe Quintessential.dll ModdedLightning.exe"
			// then "./MonoMod.RuntimeDetour.HookGen.exe ModdedLightning.exe"
			if(File.Exists("./MonoMod.exe")) {
				if(File.Exists("./Quintessential.dll")) {
					// TODO: check if there's already quintessential with this version
					Console.WriteLine("Modding Lightning...");
					RunAndWait(Path.Combine(Directory.GetCurrentDirectory(), "MonoMod.exe"), "IntermediaryLightning.exe Quintessential.dll ModdedLightning.exe");
					if(!File.Exists("./ModdedLightning.exe")) {
						Console.WriteLine("Failed to mod!");
						return;
					}
					if(File.Exists("./MonoMod.RuntimeDetour.HookGen.exe")) {
						Console.WriteLine("Generating hooks...");
						RunAndWait(Path.Combine(Directory.GetCurrentDirectory(), "MonoMod.RuntimeDetour.HookGen.exe"), "ModdedLightning.exe");
						if(OpSystem != OS.Windows) {
							// Fixes the SDL2.dll not found error
							File.Copy("Lightning.exe.config", "ModdedLightning.exe.config", true);
							// These are the files you run to make the thing do the thing. yes
							File.Copy("Lightning.bin.x86", "ModdedLightning.bin.x86", true);
							File.Copy("Lightning.bin.x86_64", "ModdedLightning.bin.x86_64", true);
						}
					}
				} else {
					Console.WriteLine("Quintessential not found, skipping merging.");
				}
			} else {
				Console.WriteLine("MonoMod not found, skipping merging.");
			}
			Console.WriteLine();
		}

		static void HandleDevExe() {
			// take ModdedLightning.exe, remap to named
			Console.WriteLine("Generating dev EXE...");
			LoadModdedLightning();
			LoadIntermediaryToNamedMappings();
			DoRemap(new NamedRemapper(), CollectNestedTypes(ModdedLightningAssembly.MainModule.Types), (mref, newName, instr) => { }, typeDef => { });
			ModdedLightningAssembly.Write("DevLightning.exe");
			Console.WriteLine();
		}
        static void HandleQuintDevExe() {
            // take LoadIntermediaryLightning.exe, remap to named (no merged quintessential)
            Console.WriteLine("Generating dev quint EXE...");
            LoadIntermediaryLightning();
            LoadIntermediaryToNamedMappings();
            DoRemap(new NamedRemapper(), CollectNestedTypes(IntermediaryLightningAssembly.MainModule.Types), (mref, newName, instr) => { }, typeDef => { });
            IntermediaryLightningAssembly.Write("QuintDevLightning.exe");
            Console.WriteLine();
        }
        static void RunAndWait(string file, string param){
			Console.WriteLine("Running " + file);
			if(!File.Exists(file)) {
				Console.WriteLine("Failed to run " + file + ", file not found.");
				return;
			}
			Process process = new Process();
			// I'm unsure if Windows needs the file to have quotes
			// Just in case I'm leaving that in
			if(OpSystem == OS.Windows) {
				file = "\"" + file + "\"";
			}
			process.StartInfo.FileName = file;
			process.StartInfo.Arguments = param;
			process.StartInfo.RedirectStandardOutput = true;
			process.StartInfo.UseShellExecute = false;
			process.Start();
			process.WaitForExit();
			Console.WriteLine();
			Console.WriteLine("Process output:");
			Console.WriteLine(process.StandardOutput.ReadToEnd());
			Console.WriteLine();
		}

		static string GetNamedForIntermediary(string intermediary, TypeReference owner) {
			if(!IntermediaryToNamedMappings.ContainsKey(intermediary))
				return intermediary;
			string name = IntermediaryToNamedMappings[intermediary];
			if(name.Contains(".")) {
				string[] split = name.Split('.');
				name = split[split.Length - 1];
			}

			return name;
		}

		class NamedRemapper : Remapper
		{
			public string RemapField(FieldReference field)
			{
				return GetNamedForIntermediary(field.Name, field.DeclaringType);
			}

			public string RemapGeneric(GenericParameter generic)
			{
				return GetNamedForIntermediary(generic.Name, generic.Type == GenericParameterType.Method ? generic.DeclaringMethod.DeclaringType : generic.DeclaringType);
			}

			public string RemapMethod(MethodReference method)
			{
				return GetNamedForIntermediary(method.Name, method.DeclaringType);
			}

			public string RemapMethodParam(ParameterReference param, MethodReference method)
			{
				return GetNamedForIntermediary(param.Name, method.DeclaringType);
			}

			public string RemapType(TypeReference type)
			{
				return GetNamedForIntermediary(type.Name, type.DeclaringType);
			}
		}

		static void LoadIntermediaryToNamedMappings() {
			foreach(var path in IntermediaryToNamedMappingPaths) {
				if(!File.Exists(path))
					continue;
				string[] lines = File.ReadAllLines(path);
				if (lines.Length <= 1 || !lines[0].StartsWith("Mapping version: ")) continue;
                Console.WriteLine("Found valid named mappings: " + Path.GetFileName(path));

                for (int i = 1; i < lines.Length; i++) {
					var line = lines[i];
					
					if(string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
						continue;
					if(!line.Contains(","))
						Console.WriteLine($"Invalid line in {path}: \"{line}\", missing comma.");
					string[] parts = line.Split(',');
					IntermediaryToNamedMappings[parts[0]] = parts[1];
				}
			}
		}

		class IntermediaryRemapper : Remapper
		{
			public string RemapField(FieldReference field)
			{
				return FindType(field.DeclaringType)?.Fields.Where(f => f.FieldNameA == field.Name).SingleOrNull()?.FieldNameB ?? field.Name;
			}

			public string RemapGeneric(GenericParameter generic)
			{
				if (generic.Type == GenericParameterType.Method) {
					return FindMethod(generic.DeclaringMethod)?.GenericParameters.Where(g => g.GenericNameA == generic.Name).SingleOrNull()?.GenericNameB ?? generic.Name;
				} else {
					return FindType(generic.DeclaringType)?.GenericParameters.Where(g => g.GenericNameA == generic.Name).SingleOrNull()?.GenericNameB ?? generic.Name;
				}
			}

			public string RemapMethod(MethodReference method)
			{
				return FindMethod(method)?.MethodNameB ?? method.Name;
			}

			public string RemapMethodParam(ParameterReference param, MethodReference method)
			{
				return FindMethod(method)?.Parameters.Where(p => p.ParameterNameA == param.Name).SingleOrNull()?.ParameterNameB ?? param.Name;
			}

			public string RemapType(TypeReference type)
			{
				return FindType(type)?.ClassNameB ?? type.Name;
			}

			private TypeReference GetMainType(TypeReference type) {
				if (type.IsGenericParameter) throw new Exception($"Attempted to get main type of a generic parameter {type.FullName}");
				if (type.IsGenericInstance || type.IsArray || type.IsByReference || type.IsPointer) return GetMainType(type.GetElementType());
				return type;
			}

			private ClassMapping FindType(TypeReference type) {
				type = GetMainType(type); // Ignore generics, array types, reference types, etc
				return ObfToIntermediaryMappings.Classes.Where(cls => cls.ClassFullNameA == type.FullName).SingleOrNull();
			}

			private MethodMapping FindMethod(MethodReference method) {
				return FindType(method.DeclaringType)?.Methods.Where(m => {
					return m.MethodNameA == method.Name
							&& m.ReturnTypeFullNameA == method.ReturnType.FullName
							&& m.ArgumentTypeFullNamesA.Count == method.Parameters.Count
							&& m.ArgumentTypeFullNamesA.Zip(method.Parameters, (a,b)=>(a,b)).All(pair => pair.a == pair.b.ParameterType.FullName);
				}).SingleOrNull();
			}
		}

		static void LoadObfToIntermediaryMappings() {
			// TODO choose file based on Lightning.exe MVID
			foreach (var path in ObfToIntermediaryMappingPaths) {
				if(!File.Exists(path))
					continue;
				using (StreamReader file = File.OpenText(path)) {
					try { 
						ObfToIntermediaryMappings = new JsonSerializer().Deserialize<Mappings>(new JsonTextReader(file));
                    } catch {
						continue;
                    }
                    Console.WriteLine("Found valid intermediary mappings: " + Path.GetFileName(path));
                    return;
                }
			}
		}

		enum ArgumentParsingMode{
			Argument, IntermediaryToNamedMappingPath, ObfToIntermediaryMappingPath, StringsPath, LightningPath, MonoModPath,
			StringDeobfName, StringDeobfIntermediaryName
		}

		enum RunAction{
			Run, Strings, Intermediary, Merge, Setup, DevExe, QuintDevExe
        }
	}
}
