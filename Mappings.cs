using System.Collections.Generic;
using Mono.Cecil;

namespace OpusMutatum {
	public class Mappings {
		public string NamespaceA;
		public string NamespaceB;
		public int nextClassIndex;
		public int nextEnumIndex;
		public int nextInterfaceIndex;
		public int nextStructIndex;
		public int nextDelegateIndex;
		public int nextMethodIndex;
		public int nextFieldIndex;
		public int nextGenericIndex;
		public int nextParamIndex;
		public List<ClassMapping> Classes;
	}

	public class ClassMapping {
		public string ClassFullNameA; // Includes containing types for nested types
		public string ClassNameB;
		public List<FieldMapping> Fields;
		public List<MethodMapping> Methods;
		public List<GenericParameterMapping> GenericParameters;
	}

	public class FieldMapping {
		public string FieldNameA;
		public string FieldNameB;
	}

	public class MethodMapping {
		public string MethodNameA;
		public string ReturnTypeFullNameA;
		public List<string> ArgumentTypeFullNamesA;
		public string MethodNameB;
		public List<MethodParameterMapping> Parameters;
		public List<GenericParameterMapping> GenericParameters;
	}

	public class MethodParameterMapping {
		public string ParameterNameA;
		public string ParameterNameB;
	}

	// TODO method locals

	public class GenericParameterMapping {
		public string GenericNameA;
		public string GenericNameB;
	}

	public interface Remapper {
		// these methods should return the current name if there is no remapping to be done
		string RemapType(TypeReference type);
		string RemapField(FieldReference field);
		string RemapMethod(MethodReference method, bool useCurrentMehod = false);
		string RemapMethodParam(ParameterReference param, MethodReference method, bool useCurrentMehod = false);
		string RemapGeneric(GenericParameter generic, bool useCurrentMehod = false);

		// Discard the current method reference if there is any.
		void CompleteCurrentMethod();
    }
}
