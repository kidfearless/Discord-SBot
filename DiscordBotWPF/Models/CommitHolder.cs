using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBotWPF.Models
{
	struct CommitHolder
	{
		public int Total;
		public int Take;
		public int Skip;
		public Commit[] Results;
	}
}
