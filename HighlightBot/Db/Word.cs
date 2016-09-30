using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HighlightBot.Db
{
	public class WordData
	{
		public int User { get; set; }

		public string Word { get; set; }

		internal WordData(int user, string word)
		{
			User = user;
			Word = word;
		}
	}
}
