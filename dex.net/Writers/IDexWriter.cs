using System;
using System.Collections.Generic;
using System.IO;

namespace dex.net
{
	public interface IDexWriter
	{
		Dex dex { get; set; }

		void WriteOutMethod (Class dexClass, Method method, TextWriter output, Indentation indent, bool renderOpcodes=false);
		void WriteOutClass (Class dexClass, ClassDisplayOptions options, TextWriter output);
		string GetName ();
		string GetExtension ();
	}
}