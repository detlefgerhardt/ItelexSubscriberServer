namespace ItelexCommon
{
	public class SqlIdAttribute : Attribute
	{
	}

	public class SqlStringAttribute : Attribute
	{
		public int Length { get; set; }
	}

	public class SqlUInt64StrAttribute : Attribute
	{
	}

	/// <summary>
	/// 32 bit integer
	/// </summary>
	public class SqlIntAttribute : Attribute
	{
	}

	/// <summary>
	/// 16 bit integer
	/// </summary>
	public class SqlSmallIntAttribute : Attribute
	{
	}

	/// <summary>
	/// 8 bit integer
	/// </summary>
	public class SqlTinyIntAttribute : Attribute
	{
	}

	public class SqlBoolAttribute : Attribute
	{
	}

	public class SqlDateAttribute : Attribute
	{
	}

	public class SqlDateStrAttribute : Attribute
	{
	}

	public class SqlMemoAttribute : Attribute
	{
	}
}
