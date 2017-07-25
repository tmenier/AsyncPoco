using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace AsyncPoco.Tests
{
	class Utils
	{
		public static string LoadTextResource(string name)
		{
			string result = "";
			var assembly = Assembly.GetExecutingAssembly();
			if (assembly.GetManifestResourceNames().Contains(name))
			{
				using (var stream = assembly.GetManifestResourceStream(name))
				{
					using (var reader = new StreamReader(stream, Encoding.Default))
					{
						result = reader.ReadToEnd();
					}
				}
			}
			return result;
		}
	}
}
