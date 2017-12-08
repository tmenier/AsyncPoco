using System;

namespace AsyncPoco.Tests
{

	enum State
	{
		Yes,
		No,
		Maybe,
	}

	// Non-decorated true poco
	class poco
	{
		public long id { get; set; }
		public string title { get; set; }
		public bool draft { get; set; }
		public DateTime date_created { get; set; }
		public DateTime? date_edited { get; set; }
		public string content { get; set; }
		public State state { get; set; }
		public State? state2 { get; set; }
		[Column("col w space")]
		public int col_w_space { get; set; }
		public float? nullreal { get; set; }
	}

	class enum_poco
	{
		public int id { get; set; }
		public Fruits? fruit_type { get; set; }

		/// <summary>Determines whether the specified object is equal to the current object.</summary>
		/// <returns>true if the specified object  is equal to the current object; otherwise, false.</returns>
		/// <param name="obj">The object to compare with the current object. </param>
		public override bool Equals(object obj)
		{
			var o = obj as enum_poco;
			if (o == null) return false;
			return o.id.Equals(id) && o.fruit_type.Equals(fruit_type);
		}
	}



	// Attributed not-so-true poco
	[TableName("petapoco")]
	[PrimaryKey("id", sequenceName = "article_id_seq")]
	[ExplicitColumns]
	class deco
	{
		[Column] public long id { get; set; }
		[Column] public string title { get; set; }
		[Column] public bool draft { get; set; }
		[Column(ForceToUtc = true)] public DateTime date_created { get; set; }
		[Column(ForceToUtc = true)] public DateTime? date_edited { get; set; }
		[Column] public string content { get; set; }
		[Column] public State state { get; set; }
		[Column] public State? state2 { get; set; }
		[Column("col w space")]
		public int col_w_space { get; set; }
		[Column] public float? nullreal { get; set; }
	}
	// Attributed not-so-true poco
	[TableName("petapoco")]
	[PrimaryKey("id")]
	[ExplicitColumns]
	class deco_explicit
	{
		[Column] public long id { get; set; }
		[Column] public string title { get; set; }
		[Column] public bool draft { get; set; }
		[Column] public DateTime date_created { get; set; }
		[Column] public State state { get; set; }
		[Column] public State? state2 { get; set; }
		public string content { get; set; }
		[Column("col w space")] public int col_w_space { get; set; }
		[Column] public float? nullreal { get; set; }
	}

	// Attributed not-so-true poco
	[TableName("petapoco")]
	[PrimaryKey("id")]
	class deco_non_explicit
	{
		public long id { get; set; }
		public string title { get; set; }
		public bool draft { get; set; }
		public DateTime date_created { get; set; }
		public State state { get; set; }
		[Ignore] public string content { get; set; }
		[Column("col w space")] public int col_w_space { get; set; }
		public float? nullreal { get; set; }
	}

	[TableName("petapoco2")]
	[PrimaryKey("email", autoIncrement = false)]
	class petapoco2
	{
		public string email { get; set; }
		public string name { get; set; }
	}

	[TableName("composite_pk")]
	[PrimaryKey("id1, id2")]
	class composite_pk
	{
		public int id1 { get; set; }
		public int id2 { get; set; }
		public string value { get; set; }
	}
}
