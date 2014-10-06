using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AsyncPoco.Internal
{
	internal static class EnumMapper
	{
		public static object EnumFromString(Type enumType, string value)
		{
			if (!enumType.IsEnum) {
				enumType = Nullable.GetUnderlyingType(enumType);
			}

			Dictionary<string, object> map = _types.Get(enumType, () =>
			{
				var values = Enum.GetValues(enumType);

				var newmap = new Dictionary<string, object>(values.Length, StringComparer.InvariantCultureIgnoreCase);

				foreach (var v in values)
				{
					newmap.Add(v.ToString(), v);
				}

				return newmap;
			});


			return (value == null) ? null : map[value];
		}

		public static object EnumFromNullableInt(Type dstType, int? src)
		{
			return src.HasValue ? Enum.ToObject(Nullable.GetUnderlyingType(dstType), src) : null;
		}

		static Cache<Type, Dictionary<string, object>> _types = new Cache<Type,Dictionary<string,object>>();
	}
}
