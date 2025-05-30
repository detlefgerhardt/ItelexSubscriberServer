using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TlnServer.Data
{
	internal class AnswerbackItem
	{
		internal enum AnswerbackStatus { Ok = 1, Miss = 2, Diff = 3, Forward = 4 }

		internal int Number { get; set; }

		internal string Answerback { get; set; }

		internal AnswerbackStatus Status { get; set; }

		public override string ToString()
		{
			return $"{Number} '{Answerback}' {Status}";
		}
	}
}
