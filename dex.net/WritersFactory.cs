using System;
using System.Linq;
using System.Collections.Generic;

namespace dex.net
{
	public class WritersFactory
	{
		private Dictionary<string,IDexWriter> _writers;

		public WritersFactory ()
		{
			var writers = AppDomain.CurrentDomain.GetAssemblies()
						.SelectMany(s => s.GetTypes())
					.Where(p => typeof(IDexWriter).IsAssignableFrom(p) && p.IsClass);

			_writers = new Dictionary<string,IDexWriter> ();
			foreach(var writer in writers) {
				var writerInstace = (IDexWriter)Activator.CreateInstance (writer);
				_writers.Add (writerInstace.GetName(), writerInstace);
			}
		}

		public string[] GetWriters()
		{
			var names = new string[_writers.Keys.Count];
			_writers.Keys.CopyTo (names, 0);

			return names;
		}

		public IDexWriter GetWriter(string name)
		{
			return _writers [name];
		}
	}
}