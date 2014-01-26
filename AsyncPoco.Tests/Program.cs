using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AsyncPoco.Tests
{
	public class Program
	{
		public static void Main(string[] args)
		{
			PetaTest.Runner.RunMainAsync(args).Wait();
		}
	}
}
