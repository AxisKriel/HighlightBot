using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HighlightBot.Db
{
	public class User
	{
		public int Id { get; set; }

		public long ClientId { get; set; }

		public bool Enabled { get; set; }

		public static User New(ulong clientId)
		{
			return new User
			{
				ClientId = (long)clientId,
				Enabled = true
			};
		}

		public override bool Equals(object obj) => (obj as User)?.Id == Id;

		public override int GetHashCode() => Id;
	}
}
